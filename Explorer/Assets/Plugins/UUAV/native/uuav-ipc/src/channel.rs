//! Duplex client<->helper control channel over native OS primitives.
//!
//! Windows: a message-mode named pipe (overlapped I/O, the helper's
//! end explicitly inherited at spawn). macOS: an `AF_UNIX` socketpair
//! (the helper's fd inherited across exec). One postcard-encoded
//! message per frame; the Windows pipe preserves message boundaries by
//! itself, the macOS stream carries a u32-LE length prefix.
//!
//! The client creates both ends with [`Channel::pair`] before spawning
//! the helper, hands the [`ChildHandoff`] to the spawn path, and keeps
//! its own end; the helper adopts the inherited end via
//! [`Channel::from_arg`]. A channel is owned by exactly one thread
//! (the client's io thread / the helper's serve loop): `Send`, not
//! `Sync`.

use anyhow::Context as _;
use serde::{Serialize, de::DeserializeOwned};
use std::time::{Duration, Instant};

#[cfg(target_os = "macos")]
#[path = "channel_macos.rs"]
mod imp;
#[cfg(target_os = "windows")]
#[path = "channel_windows.rs"]
mod imp;

/// Hard sanity cap on a single message: anything larger means a corrupt
/// length prefix or a runaway sender, and surfaces as a channel error
/// (feeding the same recovery path as a dead peer).
pub(crate) const MAX_FRAME: usize = 64 * 1024 * 1024;

/// One end of the control channel. See the module docs for ownership.
pub struct Channel {
    imp: imp::Channel,
}

/// The helper's pre-created channel end.
///
/// Alive in the parent process only between [`Channel::pair`] and the
/// spawn; dropping it closes the parent's copy (on every path, success
/// or failure), which is what makes helper death observable as
/// EOF/broken pipe.
pub struct ChildHandoff {
    imp: imp::ChildHandoff,
}

impl Channel {
    /// Client side: creates both ends. `token` names the Windows pipe
    /// (`\\.\pipe\uuav-<token>`); unused on macOS.
    pub fn pair(token: &str) -> anyhow::Result<(Self, ChildHandoff)> {
        let (channel, handoff) = imp::Channel::pair(token)?;
        Ok((Self { imp: channel }, ChildHandoff { imp: handoff }))
    }

    /// Helper side: adopts the inherited handle/fd passed as the
    /// `--channel` argv value (produced by [`ChildHandoff::arg`]).
    pub fn from_arg(value: &str) -> anyhow::Result<Self> {
        Ok(Self {
            imp: imp::Channel::from_arg(value)?,
        })
    }

    /// Blocking send of one typed message. Backpressure is the kernel
    /// buffer: a stalled peer turns into a stalled send, never
    /// unbounded queueing.
    pub fn send<T: Serialize>(&mut self, message: &T) -> anyhow::Result<()> {
        let bytes = postcard::to_allocvec(message).context("serialize message")?;
        anyhow::ensure!(bytes.len() <= MAX_FRAME, "message exceeds frame cap");
        self.imp.send_frame(&bytes)
    }

    /// Blocking receive of one typed message with a deadline (handshake
    /// only). A deadline loop rather than one poll: on macOS "readable"
    /// does not imply a complete frame is buffered yet.
    pub fn recv_timeout<T: DeserializeOwned>(&mut self, timeout: Duration) -> anyhow::Result<T> {
        let deadline = Instant::now()
            .checked_add(timeout)
            .context("receive deadline overflow")?;
        loop {
            if let Some(message) = self.try_recv()? {
                return Ok(message);
            }
            let remaining = deadline.saturating_duration_since(Instant::now());
            anyhow::ensure!(!remaining.is_zero(), "timed out waiting for message");
            let millis = u32::try_from(remaining.as_millis()).unwrap_or(u32::MAX).max(1);
            self.imp.poll_readable(millis)?;
        }
    }

    /// True when at least one complete inbound message is ready within
    /// `timeout_ms`.
    pub fn poll_readable(&mut self, timeout_ms: u32) -> anyhow::Result<bool> {
        self.imp.poll_readable(timeout_ms)
    }

    /// Non-blocking receive: `Ok(None)` when no complete message is
    /// pending. `Err` means the channel is dead (peer closed / broken
    /// pipe) — callers treat it exactly like any other I/O failure.
    pub fn try_recv<T: DeserializeOwned>(&mut self) -> anyhow::Result<Option<T>> {
        match self.imp.try_recv_frame()? {
            Some(bytes) => Ok(Some(
                postcard::from_bytes(bytes).context("deserialize message")?,
            )),
            None => Ok(None),
        }
    }
}

impl ChildHandoff {
    /// The `--channel` argv value: the decimal handle value (Windows) /
    /// fd number (macOS), both of which survive into the child
    /// unchanged (inherited handles keep their numeric value; fds pass
    /// through exec untouched).
    pub fn arg(&self) -> String {
        self.imp.arg()
    }

    /// The raw handle for the spawn attribute list (explicit
    /// inheritance via `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`).
    #[cfg(target_os = "windows")]
    pub const fn raw_handle(&self) -> windows::Win32::Foundation::HANDLE {
        self.imp.raw_handle()
    }
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::similar_names
)]
mod tests {
    use super::*;
    use serde::Deserialize;
    use std::sync::atomic::{AtomicU32, Ordering};

    #[derive(Serialize, Deserialize, PartialEq, Eq, Debug)]
    struct Msg {
        id: u64,
        data: Vec<u8>,
    }

    /// Both ends in one process: `from_arg` adopts the child end the
    /// same way the helper does, minus the spawn.
    fn in_process_pair() -> (Channel, Channel) {
        static NEXT: AtomicU32 = AtomicU32::new(0);
        let token = format!(
            "test-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        );
        let (parent, handoff) = Channel::pair(&token).unwrap();
        let child = Channel::from_arg(&handoff.arg()).unwrap();
        // the child channel now owns the handle/fd; the handoff's Drop
        // must not close it a second time
        std::mem::forget(handoff);
        (parent, child)
    }

    #[test]
    fn round_trip_both_directions() {
        let (mut parent, mut child) = in_process_pair();
        let ping = Msg {
            id: 1,
            data: vec![0xAB; 32],
        };
        parent.send(&ping).unwrap();
        assert_eq!(child.recv_timeout::<Msg>(Duration::from_secs(5)).unwrap(), ping);

        let pong = Msg {
            id: 2,
            data: vec![0xCD; 32],
        };
        child.send(&pong).unwrap();
        assert_eq!(parent.recv_timeout::<Msg>(Duration::from_secs(5)).unwrap(), pong);
    }

    #[test]
    fn large_message_reassembles() {
        // > the 64 KiB read chunk: exercises the ERROR_MORE_DATA growth
        // loop on Windows / partial-frame reassembly on macOS, while
        // staying inside the 1 MiB kernel buffer so a single-threaded
        // send cannot stall
        let (mut parent, mut child) = in_process_pair();
        let big = Msg {
            id: 3,
            data: (0..200_000u32).map(|i| (i % 251) as u8).collect(),
        };
        parent.send(&big).unwrap();
        assert_eq!(child.recv_timeout::<Msg>(Duration::from_secs(5)).unwrap(), big);
    }

    #[test]
    fn messages_keep_order() {
        let (mut parent, mut child) = in_process_pair();
        for id in 0..100u64 {
            parent.send(&Msg { id, data: vec![] }).unwrap();
        }
        for id in 0..100u64 {
            let got = child.recv_timeout::<Msg>(Duration::from_secs(5)).unwrap();
            assert_eq!(got.id, id);
        }
    }

    #[test]
    fn idle_try_recv_is_none() {
        let (mut parent, _child) = in_process_pair();
        assert!(parent.try_recv::<Msg>().unwrap().is_none());
        assert!(!parent.poll_readable(10).unwrap());
    }

    #[test]
    fn peer_drop_is_an_error() {
        let (mut parent, child) = in_process_pair();
        drop(child);
        // the pending/next read observes EOF or a broken pipe; a poll
        // in between must not mask it
        let err = loop {
            match parent.try_recv::<Msg>() {
                Ok(None) => {
                    parent.poll_readable(100).unwrap();
                }
                Ok(Some(_)) => panic!("no message was ever sent"),
                Err(e) => break e,
            }
        };
        let _ = err;
    }

    #[test]
    fn recv_timeout_expires() {
        let (mut parent, _child) = in_process_pair();
        let start = Instant::now();
        let result = parent.recv_timeout::<Msg>(Duration::from_millis(50));
        assert!(result.is_err());
        assert!(start.elapsed() >= Duration::from_millis(50));
    }
}
