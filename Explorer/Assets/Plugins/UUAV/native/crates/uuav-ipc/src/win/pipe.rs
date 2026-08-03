
use std::io;
use std::os::windows::io::{AsRawHandle as _, FromRawHandle as _, OwnedHandle};
use std::ptr;

use anyhow::{Context as _, Result, anyhow, bail};
use windows_sys::Win32::Foundation::{
    CloseHandle, DUPLICATE_SAME_ACCESS, DuplicateHandle, ERROR_BROKEN_PIPE, ERROR_IO_PENDING,
    ERROR_MORE_DATA, ERROR_PIPE_BUSY, ERROR_PIPE_CONNECTED, GENERIC_READ, GENERIC_WRITE, HANDLE,
    INVALID_HANDLE_VALUE, LocalFree, WAIT_OBJECT_0, WAIT_TIMEOUT,
};
use windows_sys::Win32::Security::Authorization::{
    ConvertSidToStringSidW, ConvertStringSecurityDescriptorToSecurityDescriptorW, SDDL_REVISION_1,
};
use windows_sys::Win32::Security::{
    GetTokenInformation, PSECURITY_DESCRIPTOR, SECURITY_ATTRIBUTES, TOKEN_QUERY, TOKEN_USER,
    TokenUser,
};
use windows_sys::Win32::Storage::FileSystem::{
    CreateFileW, FILE_FLAG_FIRST_PIPE_INSTANCE, FILE_FLAG_OVERLAPPED, OPEN_EXISTING,
    PIPE_ACCESS_DUPLEX, ReadFile, WriteFile,
};
use windows_sys::Win32::System::Pipes::{
    ConnectNamedPipe, CreateNamedPipeW, GetNamedPipeClientProcessId, PIPE_READMODE_MESSAGE,
    PIPE_REJECT_REMOTE_CLIENTS, PIPE_TYPE_MESSAGE, PIPE_WAIT, SetNamedPipeHandleState,
};
use windows_sys::Win32::System::Threading::{
    CreateEventW, GetCurrentProcess, GetCurrentProcessId, OpenProcessToken, ResetEvent,
    WaitForSingleObject,
};
use windows_sys::Win32::System::IO::{CancelIoEx, GetOverlappedResult, OVERLAPPED};

use crate::protocol::{self, Fault};
use crate::win::wire::{self, MESSAGE_BYTES, Message};

const PIPE_BUFFER_BYTES: u32 = 4096;

fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(std::iter::once(0)).collect()
}

fn last_error() -> io::Error {
    io::Error::last_os_error()
}

fn raw_code(error: &io::Error) -> u32 {
    error.raw_os_error().unwrap_or(0) as u32
}

struct SecurityDescriptor(PSECURITY_DESCRIPTOR);

impl Drop for SecurityDescriptor {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe { LocalFree(self.0.cast()) };
        }
    }
}

fn current_user_only() -> Result<SecurityDescriptor> {
    let mut token: HANDLE = ptr::null_mut();
    if unsafe { OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw mut token) } == 0 {
        return Err(last_error()).context("OpenProcessToken(TOKEN_QUERY)");
    }
    let token = unsafe { OwnedHandle::from_raw_handle(token.cast()) };

    let mut needed = 0u32;
    unsafe {
        GetTokenInformation(
            token.as_raw_handle().cast(),
            TokenUser,
            ptr::null_mut(),
            0,
            &raw mut needed,
        )
    };
    if needed == 0 {
        return Err(last_error()).context("GetTokenInformation(TokenUser) size probe");
    }
    let mut buffer = vec![0u8; needed as usize];
    if unsafe {
        GetTokenInformation(
            token.as_raw_handle().cast(),
            TokenUser,
            buffer.as_mut_ptr().cast(),
            needed,
            &raw mut needed,
        )
    } == 0
    {
        return Err(last_error()).context("GetTokenInformation(TokenUser)");
    }

    let sid = unsafe { ptr::read_unaligned(buffer.as_ptr().cast::<TOKEN_USER>()) }
        .User
        .Sid;
    let mut sid_text: *mut u16 = ptr::null_mut();
    if unsafe { ConvertSidToStringSidW(sid, &raw mut sid_text) } == 0 {
        return Err(last_error()).context("ConvertSidToStringSidW");
    }
    let sid_string = unsafe { widestring_to_string(sid_text) };
    unsafe { LocalFree(sid_text.cast()) };

    let sddl = wide(&format!("D:P(A;;GA;;;{sid_string})"));
    let mut descriptor: PSECURITY_DESCRIPTOR = ptr::null_mut();
    if unsafe {
        ConvertStringSecurityDescriptorToSecurityDescriptorW(
            sddl.as_ptr(),
            SDDL_REVISION_1,
            &raw mut descriptor,
            ptr::null_mut(),
        )
    } == 0
    {
        return Err(last_error()).context("ConvertStringSecurityDescriptorToSecurityDescriptorW");
    }
    Ok(SecurityDescriptor(descriptor))
}

unsafe fn widestring_to_string(text: *const u16) -> String {
    let mut length = 0usize;
    while unsafe { *text.add(length) } != 0 {
        length = length.saturating_add(1);
    }
    String::from_utf16_lossy(unsafe { std::slice::from_raw_parts(text, length) })
}

fn new_event() -> Result<OwnedHandle> {
    let raw = unsafe { CreateEventW(ptr::null(), 1, 0, ptr::null()) };
    if raw.is_null() {
        return Err(last_error()).context("CreateEventW");
    }
    Ok(unsafe { OwnedHandle::from_raw_handle(raw.cast()) })
}

struct Transfer {
    buffer: Box<[u8; MESSAGE_BYTES]>,
    overlapped: Box<OVERLAPPED>,
    event: OwnedHandle,
    in_flight: bool,
}

impl Transfer {
    fn new() -> Result<Self> {
        let event = new_event()?;
        let mut overlapped: Box<OVERLAPPED> = Box::new(unsafe { std::mem::zeroed() });
        overlapped.hEvent = event.as_raw_handle().cast();
        Ok(Self {
            buffer: Box::new([0u8; MESSAGE_BYTES]),
            overlapped,
            event,
            in_flight: false,
        })
    }

    fn rearm(&mut self) {
        let stored_event = self.overlapped.hEvent;
        *self.overlapped = unsafe { std::mem::zeroed() };
        self.overlapped.hEvent = stored_event;
        unsafe { ResetEvent(self.event.as_raw_handle().cast()) };
    }

    fn wait(&mut self, pipe: HANDLE, timeout_ms: u32) -> Result<Option<u32>> {
        let waited = unsafe { WaitForSingleObject(self.event.as_raw_handle().cast(), timeout_ms) };
        if waited == WAIT_TIMEOUT {
            return Ok(None);
        }
        if waited != WAIT_OBJECT_0 {
            bail!("WaitForSingleObject on the pipe event returned {waited:#010x}");
        }
        let mut transferred = 0u32;
        let ok = unsafe {
            GetOverlappedResult(
                pipe,
                &raw const *self.overlapped,
                &raw mut transferred,
                0,
            )
        };
        self.in_flight = false;
        if ok == 0 {
            return Err(last_error()).context("GetOverlappedResult on the control pipe");
        }
        Ok(Some(transferred))
    }

    fn cancel(&mut self, pipe: HANDLE) {
        if !self.in_flight {
            return;
        }
        unsafe { CancelIoEx(pipe, &raw const *self.overlapped) };
        let mut transferred = 0u32;
        unsafe {
            GetOverlappedResult(
                pipe,
                &raw const *self.overlapped,
                &raw mut transferred,
                1,
            )
        };
        self.in_flight = false;
    }
}

pub struct Channel {
    pipe: OwnedHandle,
    read: Transfer,
    write: Transfer,
}

#[allow(
    clippy::non_send_fields_in_send_ty,
    reason = "the raw pointers inside OVERLAPPED are the kernel's view of buffers \
              this value owns; they are meaningful in any thread of this process"
)]
unsafe impl Send for Channel {}

impl Channel {
    pub fn create_pair(host_pid: u32) -> Result<(Self, OwnedHandle)> {
        let name = wire::pipe_name(host_pid, protocol::nonce(), protocol::nonce());
        let wide_name = wide(&name);
        let descriptor = current_user_only()?;

        let mut attributes = SECURITY_ATTRIBUTES {
            nLength: size_of::<SECURITY_ATTRIBUTES>() as u32,
            lpSecurityDescriptor: descriptor.0,
            bInheritHandle: 0,
        };

        let server = unsafe {
            CreateNamedPipeW(
                wide_name.as_ptr(),
                PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                1,
                PIPE_BUFFER_BYTES,
                PIPE_BUFFER_BYTES,
                0,
                &raw mut attributes,
            )
        };
        if server == INVALID_HANDLE_VALUE {
            return Err(last_error()).with_context(|| format!("CreateNamedPipeW({name})"));
        }
        let server = unsafe { OwnedHandle::from_raw_handle(server.cast()) };

        let mut client_attributes = SECURITY_ATTRIBUTES {
            nLength: size_of::<SECURITY_ATTRIBUTES>() as u32,
            lpSecurityDescriptor: descriptor.0,
            bInheritHandle: 1,
        };
        let client = unsafe {
            CreateFileW(
                wide_name.as_ptr(),
                GENERIC_READ | GENERIC_WRITE,
                0,
                &raw mut client_attributes,
                OPEN_EXISTING,
                FILE_FLAG_OVERLAPPED,
                ptr::null_mut(),
            )
        };
        if client == INVALID_HANDLE_VALUE {
            let error = last_error();
            if raw_code(&error) == ERROR_PIPE_BUSY {
                bail!("{name} was connected by another process before we could open it");
            }
            return Err(error).with_context(|| format!("CreateFileW({name})"));
        }
        let client = unsafe { OwnedHandle::from_raw_handle(client.cast()) };

        if unsafe { ConnectNamedPipe(server.as_raw_handle().cast(), ptr::null_mut()) } == 0 {
            let error = last_error();
            if raw_code(&error) != ERROR_PIPE_CONNECTED {
                return Err(error).context("ConnectNamedPipe");
            }
        }

        let mut client_pid = 0u32;
        if unsafe {
            GetNamedPipeClientProcessId(server.as_raw_handle().cast(), &raw mut client_pid)
        } == 0
        {
            return Err(last_error()).context("GetNamedPipeClientProcessId");
        }
        if client_pid != unsafe { GetCurrentProcessId() } {
            bail!("{name} was connected by pid {client_pid}, not by this process");
        }

        set_message_read_mode(&client)?;

        Ok((Self::wrap(server)?, client))
    }

    pub fn from_inherited(raw_value: u64) -> Result<Self> {
        wire::validate_handle_value(0, raw_value)
            .map_err(|fault| anyhow!("inherited pipe handle is not a handle: {fault}"))?;
        let pipe = unsafe { OwnedHandle::from_raw_handle(raw_value as usize as *mut _) };
        set_message_read_mode(&pipe)?;
        Self::wrap(pipe)
    }

    fn wrap(pipe: OwnedHandle) -> Result<Self> {
        Ok(Self {
            pipe,
            read: Transfer::new()?,
            write: Transfer::new()?,
        })
    }

    fn raw(&self) -> HANDLE {
        self.pipe.as_raw_handle().cast()
    }

    pub fn receive(&mut self, timeout_ms: u32) -> Result<Option<Message>> {
        if !self.read.in_flight {
            self.read.rearm();
            let ok = unsafe {
                ReadFile(
                    self.raw(),
                    self.read.buffer.as_mut_ptr(),
                    MESSAGE_BYTES as u32,
                    ptr::null_mut(),
                    &raw mut *self.read.overlapped,
                )
            };
            if ok == 0 {
                let error = last_error();
                match raw_code(&error) {
                    ERROR_IO_PENDING => self.read.in_flight = true,
                    ERROR_BROKEN_PIPE => return Err(error).context("the helper closed the pipe"),
                    ERROR_MORE_DATA => {
                        return Err(Fault::Message {
                            kind: 0,
                            what: "control message is larger than one wire message",
                        }
                        .into());
                    }
                    _ => return Err(error).context("ReadFile on the control pipe"),
                }
            } else {
                self.read.in_flight = true;
            }
        }

        let Some(transferred) = self.read.wait(self.raw(), timeout_ms)? else {
            return Ok(None);
        };
        if transferred as usize != MESSAGE_BYTES {
            return Err(Fault::Message {
                kind: 0,
                what: "control message is not exactly one wire message",
            }
            .into());
        }
        Ok(Some(wire::decode(self.read.buffer.as_slice())?))
    }

    pub fn send(&mut self, message: &Message, timeout_ms: u32) -> Result<()> {
        if self.write.in_flight {
            self.write.cancel(self.raw());
        }
        self.write.rearm();
        self.write.buffer.copy_from_slice(&message.encode());

        let ok = unsafe {
            WriteFile(
                self.raw(),
                self.write.buffer.as_ptr(),
                MESSAGE_BYTES as u32,
                ptr::null_mut(),
                &raw mut *self.write.overlapped,
            )
        };
        if ok == 0 {
            let error = last_error();
            if raw_code(&error) != ERROR_IO_PENDING {
                return Err(error).context("WriteFile on the control pipe");
            }
        }
        self.write.in_flight = true;

        match self.write.wait(self.raw(), timeout_ms)? {
            Some(transferred) if transferred as usize == MESSAGE_BYTES => Ok(()),
            Some(transferred) => bail!("control write moved {transferred} of {MESSAGE_BYTES} bytes"),
            None => {
                self.write.cancel(self.raw());
                bail!("the peer did not accept a control message within {timeout_ms} ms")
            }
        }
    }

    pub fn peer_is_gone(error: &anyhow::Error) -> bool {
        error
            .downcast_ref::<io::Error>()
            .is_some_and(|io| raw_code(io) == ERROR_BROKEN_PIPE)
    }
}

impl Drop for Channel {
    fn drop(&mut self) {
        let pipe = self.raw();
        self.read.cancel(pipe);
        self.write.cancel(pipe);
    }
}

fn set_message_read_mode(pipe: &OwnedHandle) -> Result<()> {
    let mut mode = PIPE_READMODE_MESSAGE;
    if unsafe {
        SetNamedPipeHandleState(
            pipe.as_raw_handle().cast(),
            &raw mut mode,
            ptr::null_mut(),
            ptr::null_mut(),
        )
    } == 0
    {
        return Err(last_error()).context("SetNamedPipeHandleState(PIPE_READMODE_MESSAGE)");
    }
    Ok(())
}

pub unsafe fn duplicate_to_child(
    handle: &OwnedHandle,
    child: HANDLE,
    inheritable: bool,
) -> Result<u64> {
    let mut out: HANDLE = ptr::null_mut();
    let ok = unsafe {
        DuplicateHandle(
            GetCurrentProcess(),
            handle.as_raw_handle().cast(),
            child,
            &raw mut out,
            0,
            i32::from(inheritable),
            DUPLICATE_SAME_ACCESS,
        )
    };
    if ok == 0 {
        return Err(last_error()).context("DuplicateHandle into the helper");
    }
    Ok(out as usize as u64)
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Access {
    Mask(u32),
    Same,
}

pub unsafe fn duplicate_from_child(
    child: HANDLE,
    kind: u32,
    value: u64,
    access: Access,
) -> Result<OwnedHandle> {
    wire::validate_handle_value(kind, value)?;
    let (desired, options) = match access {
        Access::Mask(mask) => (mask, 0),
        Access::Same => (0, DUPLICATE_SAME_ACCESS),
    };
    let mut out: HANDLE = ptr::null_mut();
    let ok = unsafe {
        DuplicateHandle(
            child,
            value as usize as *mut _,
            GetCurrentProcess(),
            &raw mut out,
            desired,
            0,
            options,
        )
    };
    if ok == 0 {
        return Err(last_error())
            .with_context(|| format!("DuplicateHandle({value:#x}) out of the helper"));
    }
    Ok(unsafe { OwnedHandle::from_raw_handle(out.cast()) })
}

pub unsafe fn close(handle: HANDLE) {
    unsafe { CloseHandle(handle) };
}
