//! Windows transport: one duplex message-mode named pipe with
//! overlapped I/O.
//!
//! The client creates the pipe (`\\.\pipe\uuav-<token>`, single
//! instance, local-only) and immediately opens the helper's end itself
//! with an inheritable handle — there is no connect-by-name window for
//! another process to squat. The helper receives that handle through an
//! explicit `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` at spawn and adopts it
//! by numeric value from argv (inherited handles keep their value in
//! the child).
//!
//! Reads keep one overlapped `ReadFile` permanently pending:
//! `poll_readable` waits on its event, `try_recv_frame` harvests it
//! with a non-blocking `GetOverlappedResult`. `PIPE_READMODE_MESSAGE`
//! preserves message boundaries, so a frame is exactly one pipe
//! message; a message larger than the posted buffer surfaces as
//! `ERROR_MORE_DATA` continuations that accumulate into the same
//! buffer. Writes are overlapped-but-waited: a full kernel buffer turns
//! into send backpressure, replacing zmq's high-water mark.

use anyhow::Context as _;
use windows::Win32::Foundation::{
    CloseHandle, ERROR_BROKEN_PIPE, ERROR_IO_INCOMPLETE, ERROR_IO_PENDING, ERROR_MORE_DATA,
    ERROR_PIPE_CONNECTED, GENERIC_READ, GENERIC_WRITE, HANDLE, WAIT_OBJECT_0, WIN32_ERROR,
};
use windows::Win32::Security::SECURITY_ATTRIBUTES;
use windows::Win32::Storage::FileSystem::{
    CreateFileW, FILE_FLAG_FIRST_PIPE_INSTANCE, FILE_FLAG_OVERLAPPED, FILE_SHARE_MODE,
    OPEN_EXISTING, PIPE_ACCESS_DUPLEX, ReadFile, WriteFile,
};
use windows::Win32::System::IO::{CancelIoEx, GetOverlappedResult, OVERLAPPED};
use windows::Win32::System::Pipes::{
    ConnectNamedPipe, CreateNamedPipeW, PIPE_READMODE_MESSAGE, PIPE_REJECT_REMOTE_CLIENTS,
    PIPE_TYPE_MESSAGE, PIPE_WAIT, SetNamedPipeHandleState,
};
use windows::Win32::System::Threading::{CreateEventW, ResetEvent, WaitForSingleObject};
use windows::core::{HRESULT, PCWSTR};

use super::MAX_FRAME;

/// Kernel buffer per direction. Sized so that the steady-state traffic
/// (state at ~50 Hz, audio at ~4 KiB / 10 ms per player) rides out a
/// multi-hundred-millisecond peer stall before a send blocks.
const BUFFER_SIZE: u32 = 1024 * 1024;

/// Initial/incremental posted read size; messages larger than this
/// arrive via `ERROR_MORE_DATA` continuations.
const READ_CHUNK: usize = 64 * 1024;

pub(super) struct Channel {
    handle: HANDLE,
    read_event: HANDLE,
    write_event: HANDLE,
    /// Boxed for a stable address: the kernel holds this pointer while
    /// a read is pending.
    read_ov: Box<OVERLAPPED>,
    read_buf: Vec<u8>,
    /// Bytes of the current (partial) message already landed by
    /// previous `ERROR_MORE_DATA` continuations.
    read_done: usize,
    read_pending: bool,
}

// HANDLE is a raw kernel handle; the channel is single-owner and moves
// between threads only as a whole.
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for Channel {}

pub(super) struct ChildHandoff {
    handle: HANDLE,
}

#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for ChildHandoff {}

impl Channel {
    pub fn pair(token: &str) -> anyhow::Result<(Self, ChildHandoff)> {
        let name: Vec<u16> = format!(r"\\.\pipe\uuav-{token}")
            .encode_utf16()
            .chain(std::iter::once(0))
            .collect();

        // single instance + first-instance + reject-remote + a UUID
        // name: nothing else can pre-create or connect to this pipe
        let parent = unsafe {
            CreateNamedPipeW(
                PCWSTR(name.as_ptr()),
                PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                1,
                BUFFER_SIZE,
                BUFFER_SIZE,
                0,
                None,
            )
        };
        if parent.is_invalid() {
            return Err(windows::core::Error::from_win32()).context("create named pipe");
        }
        let parent = OwnedHandle(parent);

        // the helper's end, opened inheritable right away
        let inherit = SECURITY_ATTRIBUTES {
            nLength: u32::try_from(std::mem::size_of::<SECURITY_ATTRIBUTES>())
                .context("SECURITY_ATTRIBUTES size")?,
            lpSecurityDescriptor: std::ptr::null_mut(),
            bInheritHandle: true.into(),
        };
        let child = OwnedHandle(
            unsafe {
                CreateFileW(
                    PCWSTR(name.as_ptr()),
                    GENERIC_READ.0 | GENERIC_WRITE.0,
                    FILE_SHARE_MODE(0),
                    Some(&inherit),
                    OPEN_EXISTING,
                    FILE_FLAG_OVERLAPPED,
                    None,
                )
            }
            .context("open the helper end of the pipe")?,
        );

        // CreateFileW opens in byte read mode even on a message-type
        // pipe; the mode is a per-handle property and travels with the
        // inherited handle into the helper
        unsafe { SetNamedPipeHandleState(child.0, Some(&PIPE_READMODE_MESSAGE), None, None) }
            .context("set message read mode on the helper end")?;

        // the helper end is already open, so this reports "connected"
        // immediately rather than pending
        match unsafe { ConnectNamedPipe(parent.0, None) } {
            Ok(()) => {}
            Err(e) if is(&e, ERROR_PIPE_CONNECTED) => {}
            Err(e) => return Err(e).context("connect named pipe"),
        }

        let channel = Self::wrap(parent)?;
        let handoff = ChildHandoff {
            handle: child.into_raw(),
        };
        Ok((channel, handoff))
    }

    pub fn from_arg(value: &str) -> anyhow::Result<Self> {
        let raw: usize = value.parse().context("--channel is not a handle value")?;
        anyhow::ensure!(raw != 0, "--channel is a null handle");
        Self::wrap(OwnedHandle(HANDLE(raw as *mut std::ffi::c_void)))
    }

    fn wrap(handle: OwnedHandle) -> anyhow::Result<Self> {
        let read_event = manual_reset_event().context("create read event")?;
        let write_event = match manual_reset_event().context("create write event") {
            Ok(event) => event,
            Err(e) => {
                unsafe { _ = CloseHandle(read_event) };
                return Err(e);
            }
        };
        Ok(Self {
            handle: handle.into_raw(),
            read_event,
            write_event,
            read_ov: Box::new(OVERLAPPED::default()),
            read_buf: Vec::new(),
            read_done: 0,
            read_pending: false,
        })
    }

    pub fn send_frame(&mut self, bytes: &[u8]) -> anyhow::Result<()> {
        unsafe { ResetEvent(self.write_event) }.context("reset write event")?;
        // never returned while pending (GetOverlappedResult waits), so a
        // stack OVERLAPPED is sound here
        let mut overlapped = OVERLAPPED {
            hEvent: self.write_event,
            ..Default::default()
        };
        match unsafe { WriteFile(self.handle, Some(bytes), None, Some(&mut overlapped)) } {
            Ok(()) => {}
            Err(e) if is(&e, ERROR_IO_PENDING) => {}
            Err(e) => return Err(channel_error(e, "write to channel")),
        }
        let mut written = 0u32;
        unsafe { GetOverlappedResult(self.handle, &overlapped, &mut written, true) }
            .map_err(|e| channel_error(e, "complete channel write"))?;
        anyhow::ensure!(written as usize == bytes.len(), "short pipe write");
        Ok(())
    }

    pub fn poll_readable(&mut self, timeout_ms: u32) -> anyhow::Result<bool> {
        self.ensure_read_pending()?;
        Ok(unsafe { WaitForSingleObject(self.read_event, timeout_ms) } == WAIT_OBJECT_0)
    }

    pub fn try_recv_frame(&mut self) -> anyhow::Result<Option<&[u8]>> {
        loop {
            self.ensure_read_pending()?;
            let mut transferred = 0u32;
            let result = unsafe {
                GetOverlappedResult(self.handle, self.read_ov.as_ref(), &mut transferred, false)
            };
            match result {
                Ok(()) => {
                    let total = self
                        .read_done
                        .checked_add(transferred as usize)
                        .context("message length overflow")?;
                    self.read_done = 0;
                    self.read_pending = false;
                    return Ok(Some(self.read_buf.get(..total).context("read bookkeeping")?));
                }
                Err(e) if is(&e, ERROR_IO_INCOMPLETE) => return Ok(None),
                Err(e) if is(&e, ERROR_MORE_DATA) => {
                    // the posted chunk filled up mid-message; keep what
                    // landed and post a read for the tail (already in
                    // the pipe buffer, so it completes without waiting
                    // on the peer)
                    self.read_done = self
                        .read_done
                        .checked_add(transferred as usize)
                        .context("message length overflow")?;
                    anyhow::ensure!(self.read_done <= MAX_FRAME, "message exceeds frame cap");
                    self.read_pending = false;
                }
                Err(e) => return Err(channel_error(e, "read from channel")),
            }
        }
    }

    fn ensure_read_pending(&mut self) -> anyhow::Result<()> {
        if self.read_pending {
            return Ok(());
        }
        let needed = self
            .read_done
            .checked_add(READ_CHUNK)
            .context("read buffer overflow")?;
        if self.read_buf.len() < needed {
            self.read_buf.resize(needed, 0);
        }
        unsafe { ResetEvent(self.read_event) }.context("reset read event")?;
        *self.read_ov = OVERLAPPED {
            hEvent: self.read_event,
            ..Default::default()
        };
        let target = self
            .read_buf
            .get_mut(self.read_done..needed)
            .context("read bookkeeping")?;
        let issued = unsafe {
            ReadFile(
                self.handle,
                Some(target),
                None,
                Some(std::ptr::from_mut::<OVERLAPPED>(self.read_ov.as_mut())),
            )
        };
        match issued {
            // synchronous completion (incl. ERROR_MORE_DATA) still
            // signals the event and reports through GetOverlappedResult,
            // so both funnel into the uniform pending path
            Ok(()) => {}
            Err(e) if is(&e, ERROR_IO_PENDING) || is(&e, ERROR_MORE_DATA) => {}
            Err(e) => return Err(channel_error(e, "post channel read")),
        }
        self.read_pending = true;
        Ok(())
    }
}

impl Drop for Channel {
    fn drop(&mut self) {
        unsafe {
            if self.read_pending {
                // reap the pending read before the buffer and OVERLAPPED
                // are freed: the kernel writes through those pointers
                // until the I/O completes or the cancel lands
                _ = CancelIoEx(self.handle, Some(std::ptr::from_ref(self.read_ov.as_ref())));
                let mut transferred = 0u32;
                _ = GetOverlappedResult(self.handle, self.read_ov.as_ref(), &mut transferred, true);
            }
            _ = CloseHandle(self.handle);
            _ = CloseHandle(self.read_event);
            _ = CloseHandle(self.write_event);
        }
    }
}

impl ChildHandoff {
    pub fn arg(&self) -> String {
        (self.handle.0 as usize).to_string()
    }

    pub const fn raw_handle(&self) -> HANDLE {
        self.handle
    }
}

impl Drop for ChildHandoff {
    fn drop(&mut self) {
        unsafe { _ = CloseHandle(self.handle) };
    }
}

/// Close-on-drop guard for the construction paths; ownership leaves via
/// `into_raw`.
struct OwnedHandle(HANDLE);

impl OwnedHandle {
    const fn into_raw(self) -> HANDLE {
        let handle = self.0;
        std::mem::forget(self);
        handle
    }
}

impl Drop for OwnedHandle {
    fn drop(&mut self) {
        unsafe { _ = CloseHandle(self.0) };
    }
}

fn manual_reset_event() -> windows::core::Result<HANDLE> {
    unsafe { CreateEventW(None, true, false, PCWSTR::null()) }
}

fn is(error: &windows::core::Error, code: WIN32_ERROR) -> bool {
    error.code() == HRESULT::from_win32(code.0)
}

fn channel_error(error: windows::core::Error, action: &'static str) -> anyhow::Error {
    if is(&error, ERROR_BROKEN_PIPE) {
        anyhow::anyhow!("peer closed the channel")
    } else {
        anyhow::Error::new(error).context(action)
    }
}
