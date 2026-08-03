//! macOS transport: one `AF_UNIX` `SOCK_STREAM` socketpair.
//!
//! The client creates the pair; the helper's fd stays inheritable
//! (parent fd is `FD_CLOEXEC`) and its number rides argv into the
//! child unchanged. Darwin has no `AF_UNIX` `SOCK_SEQPACKET`, so the
//! stream carries a u32-LE length prefix per frame and `try_recv_frame`
//! reassembles partial reads across calls.
//!
//! The fd stays blocking: sends block on a full kernel buffer
//! (backpressure replacing zmq's high-water mark), while reads use
//! `MSG_DONTWAIT` per call. `SO_NOSIGPIPE` is set on both fds at
//! creation — a raw write to a dead helper must surface as `EPIPE`,
//! not deliver SIGPIPE to the Unity process.

use anyhow::Context as _;

use super::MAX_FRAME;

/// Kernel buffer per direction, matching the Windows pipe sizing (the
/// Darwin default is a few tens of KiB — too small to ride out a peer
/// stall at audio rates).
const BUFFER_SIZE: libc::c_int = 1024 * 1024;

const LEN_PREFIX: usize = 4;

pub(super) struct Channel {
    fd: libc::c_int,
    /// Raw inbound bytes; partial frames persist here across calls.
    rx: Vec<u8>,
    /// The last complete frame, moved out of `rx` (capacity reused).
    frame: Vec<u8>,
    scratch: Vec<u8>,
}

pub(super) struct ChildHandoff {
    fd: libc::c_int,
}

impl Channel {
    pub fn pair(_token: &str) -> anyhow::Result<(Self, ChildHandoff)> {
        let mut fds = [0 as libc::c_int; 2];
        if unsafe { libc::socketpair(libc::AF_UNIX, libc::SOCK_STREAM, 0, fds.as_mut_ptr()) } != 0
        {
            return Err(std::io::Error::last_os_error()).context("socketpair");
        }
        let [parent, child] = fds;
        let owned_parent = OwnedFd(parent);
        let owned_child = OwnedFd(child);

        for fd in [parent, child] {
            set_option(fd, libc::SO_NOSIGPIPE, 1).context("set SO_NOSIGPIPE")?;
            set_option(fd, libc::SO_SNDBUF, BUFFER_SIZE).context("set SO_SNDBUF")?;
            set_option(fd, libc::SO_RCVBUF, BUFFER_SIZE).context("set SO_RCVBUF")?;
        }

        // parent end must not leak into the helper (or any other child
        // spawned meanwhile); the child end stays inheritable — that
        // window between pair() and spawn is the accepted cost of fd
        // inheritance (liveness detection is try_wait-driven anyway)
        if unsafe { libc::fcntl(parent, libc::F_SETFD, libc::FD_CLOEXEC) } != 0 {
            return Err(std::io::Error::last_os_error()).context("set FD_CLOEXEC");
        }

        Ok((
            Self::wrap(owned_parent.into_raw()),
            ChildHandoff {
                fd: owned_child.into_raw(),
            },
        ))
    }

    pub fn from_arg(value: &str) -> anyhow::Result<Self> {
        let fd: libc::c_int = value.parse().context("--channel is not an fd number")?;
        anyhow::ensure!(fd >= 0, "--channel is a negative fd");
        // belt-and-braces: the option was set at pair() and inherited,
        // but it is load-bearing enough to re-assert
        set_option(fd, libc::SO_NOSIGPIPE, 1).context("set SO_NOSIGPIPE on inherited fd")?;
        Ok(Self::wrap(fd))
    }

    fn wrap(fd: libc::c_int) -> Self {
        Self {
            fd,
            rx: Vec::new(),
            frame: Vec::new(),
            scratch: vec![0; 64 * 1024],
        }
    }

    pub fn send_frame(&mut self, bytes: &[u8]) -> anyhow::Result<()> {
        let len = u32::try_from(bytes.len()).context("message exceeds frame cap")?;
        self.write_all(&len.to_le_bytes())?;
        self.write_all(bytes)
    }

    pub fn poll_readable(&mut self, timeout_ms: u32) -> anyhow::Result<bool> {
        // a frame may already be fully buffered from a previous read
        // burst; a kernel poll would sleep right past it
        if self.buffered_frame_len()?.is_some() {
            return Ok(true);
        }
        let mut pollfd = libc::pollfd {
            fd: self.fd,
            events: libc::POLLIN,
            revents: 0,
        };
        let timeout = libc::c_int::try_from(timeout_ms).unwrap_or(libc::c_int::MAX);
        match unsafe { libc::poll(&mut pollfd, 1, timeout) } {
            // POLLHUP/POLLERR count as readable: the EOF/error surfaces
            // through try_recv_frame's error path
            n if n > 0 => Ok(true),
            0 => Ok(false),
            _ => {
                let err = std::io::Error::last_os_error();
                if err.raw_os_error() == Some(libc::EINTR) {
                    Ok(false)
                } else {
                    Err(err).context("poll channel")
                }
            }
        }
    }

    pub fn try_recv_frame(&mut self) -> anyhow::Result<Option<&[u8]>> {
        loop {
            if let Some(len) = self.buffered_frame_len()? {
                let total = LEN_PREFIX.checked_add(len).context("frame length overflow")?;
                self.frame.clear();
                self.frame
                    .extend_from_slice(self.rx.get(LEN_PREFIX..total).context("frame bookkeeping")?);
                self.rx.drain(..total);
                return Ok(Some(&self.frame));
            }
            let received = unsafe {
                libc::recv(
                    self.fd,
                    self.scratch.as_mut_ptr().cast(),
                    self.scratch.len(),
                    libc::MSG_DONTWAIT,
                )
            };
            match received {
                0 => anyhow::bail!("peer closed the channel"),
                n if n > 0 => {
                    let landed = usize::try_from(n).context("recv length")?;
                    self.rx
                        .extend_from_slice(self.scratch.get(..landed).context("recv bookkeeping")?);
                }
                _ => {
                    let err = std::io::Error::last_os_error();
                    match err.raw_os_error() {
                        Some(libc::EAGAIN) => return Ok(None),
                        Some(libc::EINTR) => {}
                        _ => return Err(err).context("recv from channel"),
                    }
                }
            }
        }
    }

    /// Payload length of a complete frame at the head of `rx`, if one
    /// is fully buffered.
    fn buffered_frame_len(&self) -> anyhow::Result<Option<usize>> {
        let Some(prefix) = self.rx.get(..LEN_PREFIX) else {
            return Ok(None);
        };
        let prefix: [u8; LEN_PREFIX] = prefix.try_into().context("frame prefix")?;
        let len = usize::try_from(u32::from_le_bytes(prefix)).context("frame length")?;
        anyhow::ensure!(len <= MAX_FRAME, "message exceeds frame cap");
        let total = LEN_PREFIX.checked_add(len).context("frame length overflow")?;
        Ok((self.rx.len() >= total).then_some(len))
    }

    fn write_all(&mut self, bytes: &[u8]) -> anyhow::Result<()> {
        let mut offset = 0usize;
        while offset < bytes.len() {
            let rest = bytes.get(offset..).context("write bookkeeping")?;
            let written = unsafe { libc::write(self.fd, rest.as_ptr().cast(), rest.len()) };
            if written < 0 {
                let err = std::io::Error::last_os_error();
                match err.raw_os_error() {
                    Some(libc::EINTR) => continue,
                    Some(libc::EPIPE) => anyhow::bail!("peer closed the channel"),
                    _ => return Err(err).context("write to channel"),
                }
            }
            offset = offset
                .checked_add(usize::try_from(written).context("write length")?)
                .context("write offset overflow")?;
        }
        Ok(())
    }
}

impl Drop for Channel {
    fn drop(&mut self) {
        unsafe { libc::close(self.fd) };
    }
}

impl ChildHandoff {
    pub fn arg(&self) -> String {
        self.fd.to_string()
    }
}

impl Drop for ChildHandoff {
    fn drop(&mut self) {
        unsafe { libc::close(self.fd) };
    }
}

/// Close-on-drop guard for the construction path; ownership leaves via
/// `into_raw`.
struct OwnedFd(libc::c_int);

impl OwnedFd {
    const fn into_raw(self) -> libc::c_int {
        let fd = self.0;
        std::mem::forget(self);
        fd
    }
}

impl Drop for OwnedFd {
    fn drop(&mut self) {
        unsafe { libc::close(self.0) };
    }
}

fn set_option(fd: libc::c_int, option: libc::c_int, value: libc::c_int) -> anyhow::Result<()> {
    let result = unsafe {
        libc::setsockopt(
            fd,
            libc::SOL_SOCKET,
            option,
            std::ptr::from_ref(&value).cast(),
            u32::try_from(std::mem::size_of::<libc::c_int>()).context("option size")?,
        )
    };
    if result != 0 {
        return Err(std::io::Error::last_os_error()).context("setsockopt");
    }
    Ok(())
}
