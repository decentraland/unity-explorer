//! Linux surface channel: a dedicated `AF_UNIX` `SOCK_SEQPACKET`
//! socketpair carrying one `(SurfaceTag, fd)` per datagram — the Linux
//! analog of the macOS mach channel.
//!
//! It exists so the control stream stays free of ancillary data and
//! every shared-surface fd arrives explicitly tagged, with datagram
//! boundaries doing the pairing (nothing positional to get wrong).
//!
//! Unlike the mach bootstrap namespace there is nothing to look up and
//! no sender to authenticate: the pair is anonymous, created by the
//! client and inherited only by the helper it spawns, so possession of
//! an end is the authentication. The receiver's end is `FD_CLOEXEC`;
//! the helper's end rides argv as a number, like the control channel's.
//!
//! Flow mirrors macOS: the helper sends every plane fd of a texture-set
//! generation here, then announces the generation over the control
//! channel; the client's dedicated receive thread files arriving fds by
//! tag until the announcement completes the set.

use anyhow::Context as _;
use serde::{Deserialize, Serialize};
use std::os::fd::{AsRawFd as _, FromRawFd as _, OwnedFd, RawFd};

/// Identifies which plane image an fd belongs to; same shape as the
/// mach channel's tag.
#[derive(Serialize, Deserialize, Clone, Copy, Debug, PartialEq, Eq)]
pub struct SurfaceTag {
    pub player: u64,
    pub generation: u32,
    pub slot: u8,
    pub plane: u8,
}

/// Sanity cap on a tag datagram; anything larger is not ours.
const MAX_TAG_BYTES: usize = 256;

/// Client-side end; owned by the dedicated surface receive thread.
pub struct Receiver {
    fd: OwnedFd,
}

/// Helper-side end, adopted from the inherited fd.
pub struct Sender {
    fd: OwnedFd,
}

/// The helper's pre-created end, alive in the client only between
/// [`pair`] and the spawn; dropping it closes the client's copy so
/// helper death surfaces as EOF on the receiver.
pub struct ChildHandoff {
    fd: OwnedFd,
}

impl ChildHandoff {
    /// The `--surface` argv value: the fd number, unchanged across exec.
    pub fn arg(&self) -> String {
        self.fd.as_raw_fd().to_string()
    }
}

/// Client side: creates both ends before the helper is spawned.
pub fn pair() -> anyhow::Result<(Receiver, ChildHandoff)> {
    let mut fds = [0 as libc::c_int; 2];
    if unsafe { libc::socketpair(libc::AF_UNIX, libc::SOCK_SEQPACKET, 0, fds.as_mut_ptr()) } != 0 {
        return Err(std::io::Error::last_os_error()).context("socketpair(SEQPACKET)");
    }
    let receiver = unsafe { OwnedFd::from_raw_fd(fds[0]) };
    let child = unsafe { OwnedFd::from_raw_fd(fds[1]) };

    // the receiver end must not leak into the helper; the child end
    // stays inheritable (same stance as the control channel)
    if unsafe { libc::fcntl(receiver.as_raw_fd(), libc::F_SETFD, libc::FD_CLOEXEC) } != 0 {
        return Err(std::io::Error::last_os_error()).context("set FD_CLOEXEC");
    }

    Ok((Receiver { fd: receiver }, ChildHandoff { fd: child }))
}

impl Sender {
    /// Helper side: adopts the inherited fd passed as the `--surface`
    /// argv value.
    pub fn from_arg(value: &str) -> anyhow::Result<Self> {
        let fd: libc::c_int = value.parse().context("--surface is not an fd number")?;
        anyhow::ensure!(fd >= 0, "--surface is a negative fd");
        Ok(Self {
            fd: unsafe { OwnedFd::from_raw_fd(fd) },
        })
    }

    /// Ships one surface fd. The fd stays owned by the caller (the
    /// kernel installs its own duplicate in the receiver).
    pub fn send(&mut self, tag: SurfaceTag, surface: RawFd) -> anyhow::Result<()> {
        let mut payload = postcard::to_allocvec(&tag).context("serialize surface tag")?;
        anyhow::ensure!(payload.len() <= MAX_TAG_BYTES, "surface tag too large");

        let fd_bytes = std::mem::size_of::<libc::c_int>();
        let control_len =
            usize::try_from(unsafe { libc::CMSG_SPACE(u32::try_from(fd_bytes).context("fd size")?) })
                .context("control length")?;
        // u64 backing keeps the control buffer aligned for cmsghdr
        let mut control = vec![0u64; control_len.div_ceil(8)];

        let mut iov = libc::iovec {
            iov_base: payload.as_mut_ptr().cast(),
            iov_len: payload.len(),
        };
        let mut msg: libc::msghdr = unsafe { std::mem::zeroed() };
        msg.msg_iov = &mut iov;
        msg.msg_iovlen = 1;
        msg.msg_control = control.as_mut_ptr().cast();
        #[allow(clippy::cast_possible_truncation)]
        {
            msg.msg_controllen = control_len as _;
        }
        unsafe {
            let cmsg = libc::CMSG_FIRSTHDR(&msg);
            anyhow::ensure!(!cmsg.is_null(), "control buffer too small for header");
            (*cmsg).cmsg_level = libc::SOL_SOCKET;
            (*cmsg).cmsg_type = libc::SCM_RIGHTS;
            #[allow(clippy::cast_possible_truncation)]
            {
                (*cmsg).cmsg_len =
                    libc::CMSG_LEN(u32::try_from(fd_bytes).context("fd size")?) as _;
            }
            std::ptr::copy_nonoverlapping(
                std::ptr::from_ref(&surface).cast::<u8>(),
                libc::CMSG_DATA(cmsg),
                fd_bytes,
            );
        }

        loop {
            let sent = unsafe { libc::sendmsg(self.fd.as_raw_fd(), &msg, libc::MSG_NOSIGNAL) };
            if sent >= 0 {
                return Ok(());
            }
            let err = std::io::Error::last_os_error();
            match err.raw_os_error() {
                Some(libc::EINTR) => {}
                Some(libc::EPIPE) => anyhow::bail!("peer closed the surface channel"),
                _ => return Err(err).context("sendmsg on surface channel"),
            }
        }
    }
}

impl Receiver {
    /// Blocks up to `timeout_ms` for one datagram. `Ok(None)` on
    /// timeout; `Err` when the peer is gone (the receive thread's exit
    /// signal, mirroring the mach receiver).
    pub fn recv(&mut self, timeout_ms: u32) -> anyhow::Result<Option<(SurfaceTag, OwnedFd)>> {
        let mut pollfd = libc::pollfd {
            fd: self.fd.as_raw_fd(),
            events: libc::POLLIN,
            revents: 0,
        };
        let timeout = libc::c_int::try_from(timeout_ms).unwrap_or(libc::c_int::MAX);
        match unsafe { libc::poll(&mut pollfd, 1, timeout) } {
            0 => return Ok(None),
            n if n > 0 => {}
            _ => {
                let err = std::io::Error::last_os_error();
                if err.raw_os_error() == Some(libc::EINTR) {
                    return Ok(None);
                }
                return Err(err).context("poll surface channel");
            }
        }

        let mut payload = [0u8; MAX_TAG_BYTES];
        // sized for one fd; more trips MSG_CTRUNC below
        let mut control = [0u64; 8];
        let mut iov = libc::iovec {
            iov_base: payload.as_mut_ptr().cast(),
            iov_len: payload.len(),
        };
        let mut msg: libc::msghdr = unsafe { std::mem::zeroed() };
        msg.msg_iov = &mut iov;
        msg.msg_iovlen = 1;
        msg.msg_control = control.as_mut_ptr().cast();
        #[allow(clippy::cast_possible_truncation)]
        {
            msg.msg_controllen = std::mem::size_of_val(&control) as _;
        }

        let received = loop {
            let received = unsafe {
                libc::recvmsg(
                    self.fd.as_raw_fd(),
                    &mut msg,
                    libc::MSG_DONTWAIT | libc::MSG_CMSG_CLOEXEC,
                )
            };
            if received >= 0 {
                break received;
            }
            let err = std::io::Error::last_os_error();
            match err.raw_os_error() {
                // poll raced another reader state change; report as idle
                Some(libc::EAGAIN) => return Ok(None),
                Some(libc::EINTR) => {}
                _ => return Err(err).context("recvmsg on surface channel"),
            }
        };
        // adopt any fd before validating so an early return cannot leak it
        let surface = Self::adopt_single_fd(&msg)?;
        anyhow::ensure!(received > 0, "peer closed the surface channel");
        anyhow::ensure!(
            msg.msg_flags & (libc::MSG_CTRUNC | libc::MSG_TRUNC) == 0,
            "surface datagram truncated"
        );

        let landed = usize::try_from(received).context("recv length")?;
        let tag: SurfaceTag =
            postcard::from_bytes(payload.get(..landed).context("recv bookkeeping")?)
                .context("deserialize surface tag")?;
        let surface = surface.context("surface datagram carried no fd")?;
        Ok(Some((tag, surface)))
    }

    /// Adopts the datagram's `SCM_RIGHTS` fd, if any; rejects more than
    /// one.
    fn adopt_single_fd(msg: &libc::msghdr) -> anyhow::Result<Option<OwnedFd>> {
        let mut adopted: Option<OwnedFd> = None;
        let mut cmsg = unsafe { libc::CMSG_FIRSTHDR(msg) };
        while !cmsg.is_null() {
            let header = unsafe { *cmsg };
            if header.cmsg_level == libc::SOL_SOCKET && header.cmsg_type == libc::SCM_RIGHTS {
                let header_len =
                    usize::try_from(unsafe { libc::CMSG_LEN(0) }).context("cmsg header length")?;
                // cmsg_len is u32 on Darwin, usize on Linux
                #[allow(clippy::useless_conversion)]
                let payload = usize::try_from(header.cmsg_len)
                    .ok()
                    .and_then(|len| len.checked_sub(header_len))
                    .context("cmsg length underflow")?;
                let count = payload
                    .checked_div(std::mem::size_of::<libc::c_int>())
                    .context("cmsg element size")?;
                let data = unsafe { libc::CMSG_DATA(cmsg) };
                for index in 0..count {
                    let offset = index.saturating_mul(std::mem::size_of::<libc::c_int>());
                    let fd = unsafe { data.add(offset).cast::<libc::c_int>().read_unaligned() };
                    let owned = unsafe { OwnedFd::from_raw_fd(fd) };
                    // adopt-then-validate: extras get closed by Drop
                    anyhow::ensure!(
                        adopted.replace(owned).is_none(),
                        "surface datagram carried more than one fd"
                    );
                }
            }
            cmsg = unsafe { libc::CMSG_NXTHDR(msg, cmsg) };
        }
        Ok(adopted)
    }
}

#[cfg(test)]
#[allow(clippy::unwrap_used, clippy::expect_used, clippy::panic)]
mod tests {
    use super::*;
    use std::io::{Read as _, Write as _};

    /// Both ends in one process, the way the helper adopts its end.
    fn in_process_pair() -> (Receiver, Sender) {
        let (receiver, handoff) = pair().unwrap();
        let sender = Sender::from_arg(&handoff.arg()).unwrap();
        // the sender now owns the fd; the handoff must not close it too
        std::mem::forget(handoff);
        (receiver, sender)
    }

    fn make_pipe() -> (std::fs::File, OwnedFd) {
        let mut fds = [0 as libc::c_int; 2];
        assert_eq!(unsafe { libc::pipe(fds.as_mut_ptr()) }, 0);
        (unsafe { std::fs::File::from_raw_fd(fds[0]) }, unsafe {
            OwnedFd::from_raw_fd(fds[1])
        })
    }

    #[test]
    fn tagged_fd_round_trips() {
        let (mut receiver, mut sender) = in_process_pair();
        let (mut read_end, write_end) = make_pipe();

        let tag = SurfaceTag {
            player: 3,
            generation: 9,
            slot: 2,
            plane: 1,
        };
        sender.send(tag, write_end.as_raw_fd()).unwrap();
        drop(write_end);

        let (got_tag, got_fd) = receiver.recv(5000).unwrap().expect("datagram");
        assert_eq!(got_tag, tag);

        let mut file: std::fs::File = got_fd.into();
        file.write_all(b"x").unwrap();
        drop(file);
        let mut buf = Vec::new();
        read_end.read_to_end(&mut buf).unwrap();
        assert_eq!(buf, b"x");
    }

    #[test]
    fn datagrams_keep_order() {
        let (mut receiver, mut sender) = in_process_pair();
        let ends: Vec<_> = (0..6u8)
            .map(|plane| {
                let (read, write) = make_pipe();
                let tag = SurfaceTag {
                    player: 1,
                    generation: 1,
                    slot: plane / 2,
                    plane: plane % 2,
                };
                sender.send(tag, write.as_raw_fd()).unwrap();
                (read, tag)
            })
            .collect();

        for (_read, expected) in &ends {
            let (tag, _fd) = receiver.recv(5000).unwrap().expect("datagram");
            assert_eq!(tag, *expected);
        }
    }

    #[test]
    fn idle_recv_times_out() {
        let (mut receiver, _sender) = in_process_pair();
        assert!(receiver.recv(50).unwrap().is_none());
    }

    #[test]
    fn peer_drop_is_an_error() {
        let (mut receiver, sender) = in_process_pair();
        drop(sender);
        assert!(receiver.recv(1000).is_err());
    }
}
