//! Out-of-band mach channel for IOSurface port transfer (the socketpair
//! control channel cannot carry
//! mach port rights).
//!
//! The client (Unity side) owns the receive right and registers it in the
//! shared bootstrap namespace under a per-session name before spawning the
//! helper; the helper looks the name up and sends one complex mach message
//! per surface: a `MOVE_SEND` port descriptor (from `IOSurfaceCreateMachPort`)
//! plus an inline tag identifying (player, generation, slot, plane).
//!
//! `bootstrap_register` is deprecated but functional and battle-proven for
//! parent-spawned children sharing the bootstrap namespace (Syphon has
//! shipped on it for over a decade); the accepted fallback, if Apple ever
//! removes it, is repackaging the helper as an XPC service.
//!
//! The service lives in the shared bootstrap namespace and its name leaks
//! through the helper's argv, so the receiver authenticates every message
//! instead of trusting the name: the kernel-written audit trailer must
//! carry the pid of the helper this session spawned
//! ([`Receiver::expect_sender`]), and anything else is destroyed unread.

// mach FFI: out-params as raw pointers and fixed-width message sizes are
// the kernel ABI's shape
#![allow(clippy::borrow_as_ptr, clippy::cast_possible_truncation)]

use anyhow::{Result, bail, ensure};
use mach2::kern_return::KERN_SUCCESS;
use mach2::mach_port::{mach_port_allocate, mach_port_deallocate, mach_port_insert_right, mach_port_mod_refs};
use mach2::message::{
    MACH_MSG_SUCCESS, MACH_MSG_TIMEOUT_NONE, MACH_MSG_TYPE_COPY_SEND, MACH_MSG_TYPE_MAKE_SEND,
    MACH_MSG_TYPE_MOVE_SEND, MACH_MSGH_BITS_COMPLEX, MACH_RCV_MSG, MACH_RCV_TIMED_OUT,
    MACH_RCV_TIMEOUT, MACH_RCV_TOO_LARGE, MACH_RCV_TRAILER_AUDIT, MACH_SEND_MSG, audit_token_t,
    mach_msg, mach_msg_audit_trailer_t, mach_msg_body_t, mach_msg_header_t, mach_msg_id_t,
    mach_msg_option_t, mach_msg_port_descriptor_t,
};
use mach2::port::{MACH_PORT_NULL, MACH_PORT_RIGHT_RECEIVE, mach_port_t};
use mach2::traps::mach_task_self;
use std::ffi::CString;
use std::os::raw::{c_char, c_int};

// bootstrap.h: not exposed by libc/mach2; provided by libSystem
unsafe extern "C" {
    static bootstrap_port: mach_port_t;
    fn bootstrap_register(
        bp: mach_port_t,
        service_name: *const c_char,
        sp: mach_port_t,
    ) -> c_int;
    fn bootstrap_look_up(
        bp: mach_port_t,
        service_name: *const c_char,
        sp: *mut mach_port_t,
    ) -> c_int;
}

// mach/message.h: not exposed by mach2; provided by libSystem
unsafe extern "C" {
    /// Releases every right and out-of-line region a received message
    /// carries.
    fn mach_msg_destroy(msg: *mut mach_msg_header_t);
}

/// `MACH_RCV_TRAILER_TYPE(MACH_MSG_TRAILER_FORMAT_0) |
/// MACH_RCV_TRAILER_ELEMENTS(MACH_RCV_TRAILER_AUDIT)` from mach/message.h;
/// mach2 ships the constants but not the composition macros.
#[allow(clippy::cast_possible_wrap)] // 0x0300_0000 fits
const RCV_TRAILER_AUDIT: mach_msg_option_t = (MACH_RCV_TRAILER_AUDIT << 24) as mach_msg_option_t;

/// 'UUAV'. Shape marker on surface messages; attacker-controlled on the
/// receive side, so it is validation, never authentication.
const SURFACE_MSG_ID: mach_msg_id_t = 0x5555_4156;

/// Pid field of a kernel audit token: `val[5]`, a fixed Darwin ABI; the
/// same extraction `audit_token_to_pid` performs (libbsm, not linked here).
const fn audit_token_pid(token: &audit_token_t) -> u32 {
    token.val[5]
}

/// Identifies which shared-texture cell a transferred surface belongs to;
/// pairs with the control-channel `TextureSet` announcement.
#[derive(Clone, Copy, Debug)]
pub struct SurfaceTag {
    pub player: u64,
    pub generation: u32,
    pub slot: u8,
    pub plane: u8,
}

#[repr(C)]
struct SurfaceMessage {
    header: mach_msg_header_t,
    body: mach_msg_body_t,
    port: mach_msg_port_descriptor_t,
    player: u64,
    generation: u32,
    slot: u8,
    plane: u8,
    _pad: [u8; 2],
}

#[repr(C)]
struct SurfaceMessageRecv {
    message: SurfaceMessage,
    trailer: mach_msg_audit_trailer_t,
}

/// The per-session bootstrap service name both sides derive from the token.
pub fn service_name(token: &str) -> String {
    format!("uuav.{token}")
}

/// Client side: owns the receive right, registered in the bootstrap
/// namespace. Dropping it destroys the receive right, which wakes and ends
/// any blocked [`Self::recv`].
pub struct Receiver {
    port: mach_port_t,
    /// The only pid [`Self::recv`] accepts messages from, per the kernel
    /// audit trailer. `None` (before [`Self::expect_sender`] arms it)
    /// accepts nothing: the right registers before the helper spawn, so
    /// the pid arrives later, and unarmed reception fails closed.
    sender_pid: Option<u32>,
}

// mach ports are task-global names; the kernel serializes operations
unsafe impl Send for Receiver {}
unsafe impl Sync for Receiver {}

impl Receiver {
    pub fn register(name: &str) -> Result<Self> {
        let name = CString::new(name)?;
        let mut port: mach_port_t = MACH_PORT_NULL;
        unsafe {
            ensure!(
                mach_port_allocate(mach_task_self(), MACH_PORT_RIGHT_RECEIVE, &mut port)
                    == KERN_SUCCESS,
                "mach_port_allocate failed"
            );
            // the registration needs a send right to hand to lookers-up
            ensure!(
                mach_port_insert_right(mach_task_self(), port, port, MACH_MSG_TYPE_MAKE_SEND)
                    == KERN_SUCCESS,
                "mach_port_insert_right failed"
            );
            ensure!(
                bootstrap_register(bootstrap_port, name.as_ptr(), port) == KERN_SUCCESS,
                "bootstrap_register failed"
            );
        }
        Ok(Self {
            port,
            sender_pid: None,
        })
    }

    /// Arms sender authentication with the pid of the helper this session
    /// spawned (the kernel-truth `Child` pid, not anything self-reported).
    pub const fn expect_sender(&mut self, pid: u32) {
        self.sender_pid = Some(pid);
    }

    /// One transferred surface: the tag plus a send right the caller now
    /// owns (deallocate after `IOSurfaceLookupFromMachPort`). `Ok(None)` on
    /// timeout or on a rejected message (wrong sender or shape: destroyed,
    /// never surfaced); an error means the channel is gone (receive right
    /// died).
    pub fn recv(&self, timeout_ms: u32) -> Result<Option<(SurfaceTag, mach_port_t)>> {
        let mut buffer: SurfaceMessageRecv = unsafe { std::mem::zeroed() };
        let result = unsafe {
            mach_msg(
                &mut buffer.message.header,
                MACH_RCV_MSG | MACH_RCV_TIMEOUT | RCV_TRAILER_AUDIT,
                0,
                std::mem::size_of::<SurfaceMessageRecv>() as u32,
                self.port,
                timeout_ms,
                MACH_PORT_NULL,
            )
        };
        if result == MACH_RCV_TIMED_OUT {
            return Ok(None);
        }
        // oversized means hostile (the helper's frames are fixed-size); the
        // kernel already destroyed the message and the receive right is
        // intact, so reception continues
        if result == MACH_RCV_TOO_LARGE {
            return Ok(None);
        }
        if result != MACH_MSG_SUCCESS {
            bail!("mach_msg receive failed: {result:#x}");
        }

        let message = &buffer.message;
        // The service name is discoverable (shared bootstrap namespace,
        // helper argv), so the sender is authenticated rather than assumed:
        // the audit trailer, written by the kernel and unforgeable, must
        // name the armed helper pid. The trailer sits at its struct offset
        // only for an exactly-sized message, hence the size check comes
        // first. msgh_id and the descriptor shape are sender-controlled:
        // validation only.
        let audit_pid = audit_token_pid(&buffer.trailer.msgh_audit);
        let accepted = message.header.msgh_size as usize == std::mem::size_of::<SurfaceMessage>()
            && buffer.trailer.msgh_trailer_size as usize
                >= std::mem::size_of::<mach_msg_audit_trailer_t>()
            && self.sender_pid == Some(audit_pid)
            && message.header.msgh_id == SURFACE_MSG_ID
            && message.header.msgh_bits & MACH_MSGH_BITS_COMPLEX != 0
            && message.body.msgh_descriptor_count == 1;
        if !accepted {
            // releases whatever rights the rejected message carried
            unsafe { mach_msg_destroy(&mut buffer.message.header) };
            return Ok(None);
        }
        Ok(Some((
            SurfaceTag {
                player: message.player,
                generation: message.generation,
                slot: message.slot,
                plane: message.plane,
            },
            message.port.name,
        )))
    }
}

impl Drop for Receiver {
    fn drop(&mut self) {
        unsafe {
            mach_port_mod_refs(mach_task_self(), self.port, MACH_PORT_RIGHT_RECEIVE, -1);
        }
    }
}

/// Helper side: a send right to the client's registered service.
pub struct Sender {
    port: mach_port_t,
}

unsafe impl Send for Sender {}

impl Sender {
    pub fn look_up(name: &str) -> Result<Self> {
        let name = CString::new(name)?;
        let mut port: mach_port_t = MACH_PORT_NULL;
        unsafe {
            ensure!(
                bootstrap_look_up(bootstrap_port, name.as_ptr(), &mut port) == KERN_SUCCESS,
                "bootstrap_look_up failed (client mach service not registered?)"
            );
        }
        Ok(Self { port })
    }

    /// Transfers ownership of `surface_port` (a send right from
    /// `IOSurfaceCreateMachPort`) to the client.
    pub fn send(&self, tag: SurfaceTag, surface_port: mach_port_t) -> Result<()> {
        let mut message = SurfaceMessage {
            header: mach_msg_header_t {
                // remote-port disposition sits in the low byte of msgh_bits;
                // mach2 has the type constants but not the composition macro
                msgh_bits: MACH_MSGH_BITS_COMPLEX | MACH_MSG_TYPE_COPY_SEND,
                msgh_size: std::mem::size_of::<SurfaceMessage>() as u32,
                msgh_remote_port: self.port,
                msgh_local_port: MACH_PORT_NULL,
                msgh_voucher_port: MACH_PORT_NULL,
                msgh_id: SURFACE_MSG_ID,
            },
            body: mach_msg_body_t {
                msgh_descriptor_count: 1,
            },
            port: mach_msg_port_descriptor_t::new(surface_port, MACH_MSG_TYPE_MOVE_SEND),
            player: tag.player,
            generation: tag.generation,
            slot: tag.slot,
            plane: tag.plane,
            _pad: [0; 2],
        };

        let result = unsafe {
            mach_msg(
                &mut message.header,
                MACH_SEND_MSG,
                message.header.msgh_size,
                0,
                MACH_PORT_NULL,
                MACH_MSG_TIMEOUT_NONE,
                MACH_PORT_NULL,
            )
        };
        ensure!(result == MACH_MSG_SUCCESS, "mach_msg send failed: {result:#x}");
        Ok(())
    }
}

impl Drop for Sender {
    fn drop(&mut self) {
        unsafe {
            mach_port_deallocate(mach_task_self(), self.port);
        }
    }
}

#[cfg(test)]
#[allow(clippy::unwrap_used, clippy::expect_used, clippy::panic)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicU32, Ordering};

    fn unique_service() -> String {
        static NEXT: AtomicU32 = AtomicU32::new(0);
        service_name(&format!(
            "test-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ))
    }

    /// A fresh send right to transfer; stands in for an IOSurface port.
    fn transferable_port() -> mach_port_t {
        let mut port: mach_port_t = MACH_PORT_NULL;
        unsafe {
            assert_eq!(
                mach_port_allocate(mach_task_self(), MACH_PORT_RIGHT_RECEIVE, &mut port),
                KERN_SUCCESS
            );
            assert_eq!(
                mach_port_insert_right(mach_task_self(), port, port, MACH_MSG_TYPE_MAKE_SEND),
                KERN_SUCCESS
            );
        }
        port
    }

    const TAG: SurfaceTag = SurfaceTag {
        player: 7,
        generation: 3,
        slot: 1,
        plane: 0,
    };

    #[test]
    fn armed_receiver_accepts_the_expected_sender() {
        let name = unique_service();
        let mut receiver = Receiver::register(&name).unwrap();
        receiver.expect_sender(std::process::id());
        let sender = Sender::look_up(&name).unwrap();

        sender.send(TAG, transferable_port()).unwrap();

        let (tag, port) = receiver
            .recv(2000)
            .unwrap()
            .expect("authentic message was rejected");
        assert_eq!(tag.player, TAG.player);
        assert_eq!(tag.generation, TAG.generation);
        assert_eq!(tag.slot, TAG.slot);
        assert_eq!(tag.plane, TAG.plane);
        unsafe {
            mach_port_deallocate(mach_task_self(), port);
        }
    }

    #[test]
    fn unarmed_receiver_rejects_everything() {
        let name = unique_service();
        let receiver = Receiver::register(&name).unwrap();
        let sender = Sender::look_up(&name).unwrap();

        sender.send(TAG, transferable_port()).unwrap();

        assert!(receiver.recv(500).unwrap().is_none());
    }

    #[test]
    fn wrong_sender_pid_is_rejected_and_the_channel_survives() {
        let name = unique_service();
        let mut receiver = Receiver::register(&name).unwrap();
        receiver.expect_sender(u32::MAX); // no such process
        let sender = Sender::look_up(&name).unwrap();

        sender.send(TAG, transferable_port()).unwrap();
        assert!(receiver.recv(500).unwrap().is_none());

        // rejection destroyed the message without harming the channel
        receiver.expect_sender(std::process::id());
        sender.send(TAG, transferable_port()).unwrap();
        let (_, port) = receiver
            .recv(2000)
            .unwrap()
            .expect("authentic message after a rejected one was dropped");
        unsafe {
            mach_port_deallocate(mach_task_self(), port);
        }
    }
}
