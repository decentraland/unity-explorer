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

// mach FFI: out-params as raw pointers and fixed-width message sizes are
// the kernel ABI's shape
#![allow(clippy::borrow_as_ptr, clippy::cast_possible_truncation)]

use anyhow::{Result, bail, ensure};
use mach2::kern_return::KERN_SUCCESS;
use mach2::mach_port::{mach_port_allocate, mach_port_deallocate, mach_port_insert_right, mach_port_mod_refs};
use mach2::message::{
    MACH_MSG_SUCCESS, MACH_MSG_TIMEOUT_NONE, MACH_MSG_TYPE_COPY_SEND, MACH_MSG_TYPE_MAKE_SEND,
    MACH_MSG_TYPE_MOVE_SEND, MACH_MSGH_BITS_COMPLEX, MACH_RCV_MSG, MACH_RCV_TIMED_OUT,
    MACH_RCV_TIMEOUT, MACH_SEND_MSG, mach_msg, mach_msg_body_t, mach_msg_header_t,
    mach_msg_port_descriptor_t, mach_msg_trailer_t,
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
    trailer: mach_msg_trailer_t,
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
        Ok(Self { port })
    }

    /// One transferred surface: the tag plus a send right the caller now
    /// owns (deallocate after `IOSurfaceLookupFromMachPort`). `Ok(None)` on
    /// timeout; an error means the channel is gone (receive right died).
    pub fn recv(&self, timeout_ms: u32) -> Result<Option<(SurfaceTag, mach_port_t)>> {
        let mut buffer: SurfaceMessageRecv = unsafe { std::mem::zeroed() };
        let result = unsafe {
            mach_msg(
                &mut buffer.message.header,
                MACH_RCV_MSG | MACH_RCV_TIMEOUT,
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
        if result != MACH_MSG_SUCCESS {
            bail!("mach_msg receive failed: {result:#x}");
        }

        let message = &buffer.message;
        ensure!(
            message.header.msgh_bits & MACH_MSGH_BITS_COMPLEX != 0 && message.body.msgh_descriptor_count == 1,
            "unexpected mach message shape"
        );
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
                msgh_id: 0x5555_4156, // 'UUAV'
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
