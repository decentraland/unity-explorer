
use anyhow::{Result, anyhow};
use mach2::bootstrap::{bootstrap_check_in, bootstrap_look_up, bootstrap_port, bootstrap_strerror};
use mach2::kern_return::{KERN_SUCCESS, kern_return_t};
use mach2::mach_port::{mach_port_allocate, mach_port_deallocate, mach_port_mod_refs};
use mach2::message::{
    MACH_MSG_PORT_DESCRIPTOR, MACH_MSG_TYPE_COPY_SEND, MACH_MSG_TYPE_MAKE_SEND,
    MACH_MSG_TYPE_MAKE_SEND_ONCE, MACH_MSG_TYPE_MOVE_SEND, MACH_MSG_TYPE_MOVE_SEND_ONCE,
    MACH_MSGH_BITS, MACH_MSGH_BITS_COMPLEX, MACH_RCV_MSG, MACH_RCV_TIMEOUT, MACH_SEND_MSG,
    MACH_SEND_TIMEOUT, mach_msg, mach_msg_body_t, mach_msg_destroy, mach_msg_header_t,
    mach_msg_port_descriptor_t, mach_msg_return_t, mach_msg_size_t,
};
use mach2::port::{MACH_PORT_NULL, MACH_PORT_RIGHT_RECEIVE, mach_port_t};
use mach2::traps::mach_task_self;
use std::ffi::{CStr, CString};
use std::mem;

use crate::protocol::{AUDIO_PACKET_SAMPLES, Fault};

const MAX_TRAILER: usize = 68;

#[derive(Debug)]
pub struct SendRight(mach_port_t);

impl SendRight {
    pub const unsafe fn from_raw(name: mach_port_t) -> Self {
        Self(name)
    }

    pub const fn as_raw(&self) -> mach_port_t {
        self.0
    }

    #[allow(clippy::missing_const_for_fn, reason = "mem::forget is not const")]
    pub fn into_raw(self) -> mach_port_t {
        let name = self.0;
        mem::forget(self);
        name
    }
}

impl Drop for SendRight {
    fn drop(&mut self) {
        if self.0 != MACH_PORT_NULL {
            let _ = unsafe { mach_port_deallocate(mach_task_self(), self.0) };
        }
    }
}

#[derive(Debug)]
pub struct ReceiveRight(mach_port_t);

impl ReceiveRight {
    pub fn allocate() -> Result<Self> {
        let mut name = MACH_PORT_NULL;
        let kr = unsafe { mach_port_allocate(mach_task_self(), MACH_PORT_RIGHT_RECEIVE, &mut name) };
        kern_check(kr, "mach_port_allocate")?;
        Ok(Self(name))
    }

    pub const fn as_raw(&self) -> mach_port_t {
        self.0
    }
}

impl Drop for ReceiveRight {
    fn drop(&mut self) {
        if self.0 != MACH_PORT_NULL {
            let _ =
                unsafe { mach_port_mod_refs(mach_task_self(), self.0, MACH_PORT_RIGHT_RECEIVE, -1) };
        }
    }
}

#[derive(Debug)]
pub struct ReplyTo(mach_port_t);

impl ReplyTo {
    #[allow(clippy::missing_const_for_fn, reason = "mem::forget is not const")]
    fn into_raw(self) -> mach_port_t {
        let name = self.0;
        mem::forget(self);
        name
    }
}

impl Drop for ReplyTo {
    fn drop(&mut self) {
        if self.0 != MACH_PORT_NULL {
            let _ = unsafe { mach_port_deallocate(mach_task_self(), self.0) };
        }
    }
}

pub fn check_in(name: &CStr) -> Result<ReceiveRight> {
    let mut port = MACH_PORT_NULL;
    let kr = unsafe { bootstrap_check_in(bootstrap_port, name.as_ptr(), &mut port) };
    if kr != KERN_SUCCESS {
        return Err(anyhow!(
            "bootstrap_check_in({name:?}) -> {kr} ({})",
            bootstrap_error(kr)
        ));
    }
    Ok(ReceiveRight(port))
}

pub fn look_up(name: &CStr) -> Result<SendRight> {
    let mut port = MACH_PORT_NULL;
    let kr = unsafe { bootstrap_look_up(bootstrap_port, name.as_ptr(), &mut port) };
    if kr != KERN_SUCCESS {
        return Err(anyhow!(
            "bootstrap_look_up({name:?}) -> {kr} ({})",
            bootstrap_error(kr)
        ));
    }
    Ok(SendRight(port))
}

fn bootstrap_error(kr: kern_return_t) -> String {
    let text = unsafe { bootstrap_strerror(kr) };
    if text.is_null() {
        return format!("kern_return {kr}");
    }
    unsafe { CStr::from_ptr(text) }
        .to_string_lossy()
        .into_owned()
}

fn kern_check(kr: kern_return_t, what: &str) -> Result<()> {
    if kr == KERN_SUCCESS {
        Ok(())
    } else {
        Err(anyhow!("{what} -> {kr:#x}"))
    }
}

#[repr(C)]
#[derive(Clone, Copy)]
struct Message {
    header: mach_msg_header_t,
    body: mach_msg_body_t,
    port: mach_msg_port_descriptor_t,
    kind: u32,
    index: u32,
    payload: u64,
}

#[repr(C)]
#[derive(Clone, Copy)]
struct AudioMessage {
    control: Message,
    samples: u32,
    _reserved: u32,
    data: [f32; AUDIO_PACKET_SAMPLES],
}

#[repr(C)]
struct Envelope {
    message: AudioMessage,
    trailer: [u8; MAX_TRAILER],
}

const MESSAGE_SIZE: mach_msg_size_t = size_of::<Message>() as mach_msg_size_t;
const ENVELOPE_SIZE: mach_msg_size_t = size_of::<Envelope>() as mach_msg_size_t;

const AUDIO_PREFIX_SIZE: usize = size_of::<Message>() + 2 * size_of::<u32>();

const _: () = assert!(AUDIO_PREFIX_SIZE.is_multiple_of(4));

impl Message {
    const fn zeroed() -> Self {
        unsafe { mem::zeroed() }
    }
}

impl AudioMessage {
    const fn zeroed() -> Self {
        unsafe { mem::zeroed() }
    }
}

impl Envelope {
    const fn zeroed() -> Self {
        unsafe { mem::zeroed() }
    }

    const fn header_ptr(&mut self) -> *mut mach_msg_header_t {
        &raw mut self.message.control.header
    }
}

pub struct Incoming {
    pub kind: u32,
    pub index: u32,
    pub payload: u64,
    pub right: Option<SendRight>,
    pub reply: Option<ReplyTo>,
    pub samples: u32,
}

pub fn receive(rx: &ReceiveRight, timeout_ms: u32) -> Result<Incoming> {
    receive_into(rx, timeout_ms, &mut [])
}

pub fn receive_into(rx: &ReceiveRight, timeout_ms: u32, out: &mut [f32]) -> Result<Incoming> {
    let mut envelope = Envelope::zeroed();
    let kr = unsafe {
        mach_msg(
            envelope.header_ptr(),
            MACH_RCV_MSG | MACH_RCV_TIMEOUT,
            0,
            ENVELOPE_SIZE,
            rx.as_raw(),
            timeout_ms,
            MACH_PORT_NULL,
        )
    };
    msg_check(kr, "mach_msg(RCV)")?;
    let delivered = envelope.message.control.header.msgh_size as usize;
    let mut incoming = parse(&mut envelope.message.control).map_err(anyhow::Error::new)?;
    if incoming.kind == crate::protocol::kind::AUDIO {
        incoming.samples = copy_samples(&envelope.message, delivered, out);
    }
    Ok(incoming)
}

fn copy_samples(message: &AudioMessage, delivered: usize, out: &mut [f32]) -> u32 {
    let claimed = message.samples as usize;
    if claimed > AUDIO_PACKET_SAMPLES {
        return 0;
    }
    let needed = AUDIO_PREFIX_SIZE.saturating_add(claimed.saturating_mul(size_of::<f32>()));
    if delivered < needed {
        return 0;
    }
    let take = claimed.min(out.len());
    let (Some(source), Some(destination)) = (message.data.get(..take), out.get_mut(..take)) else {
        return 0;
    };
    destination.copy_from_slice(source);
    take as u32
}

pub fn send_audio(
    destination: &SendRight,
    first_pts: f64,
    samples: &[f32],
    discontinuous: bool,
    timeout_ms: u32,
) -> Result<()> {
    let take = samples.len().min(AUDIO_PACKET_SAMPLES);
    let mut message = AudioMessage::zeroed();
    message.control.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0);
    message.control.header.msgh_remote_port = destination.as_raw();
    message.control.header.msgh_local_port = MACH_PORT_NULL;
    message.control.header.msgh_id = 0;
    message.control.kind = crate::protocol::kind::AUDIO;
    message.control.index = u32::from(discontinuous);
    message.control.payload = first_pts.to_bits();
    message.samples = take as u32;
    let (Some(source), Some(destination_slice)) = (samples.get(..take), message.data.get_mut(..take))
    else {
        return Ok(());
    };
    destination_slice.copy_from_slice(source);

    let size = AUDIO_PREFIX_SIZE.saturating_add(take.saturating_mul(size_of::<f32>()));
    message.control.header.msgh_size = size as mach_msg_size_t;

    let kr = unsafe {
        mach_msg(
            &raw mut message.control.header,
            MACH_SEND_MSG | MACH_SEND_TIMEOUT,
            message.control.header.msgh_size,
            0,
            MACH_PORT_NULL,
            timeout_ms,
            MACH_PORT_NULL,
        )
    };
    msg_check(kr, "mach_msg(SEND audio)")
}

pub fn send(
    destination: &SendRight,
    kind: u32,
    index: u32,
    payload: u64,
    right: Option<SendRight>,
    timeout_ms: u32,
) -> Result<()> {
    let mut message = Message::zeroed();
    message.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0);
    message.header.msgh_size = MESSAGE_SIZE;
    message.header.msgh_remote_port = destination.as_raw();
    message.header.msgh_local_port = MACH_PORT_NULL;
    message.header.msgh_id = 0;
    message.kind = kind;
    message.index = index;
    message.payload = payload;
    attach(&mut message, right);

    let kr = unsafe {
        mach_msg(
            &raw mut message.header,
            MACH_SEND_MSG | MACH_SEND_TIMEOUT,
            MESSAGE_SIZE,
            0,
            MACH_PORT_NULL,
            timeout_ms,
            MACH_PORT_NULL,
        )
    };
    msg_check(kr, "mach_msg(SEND)")
}

pub fn call(
    destination: &SendRight,
    reply: &ReceiveRight,
    kind: u32,
    index: u32,
    timeout_ms: u32,
) -> Result<Incoming> {
    let mut envelope = Envelope::zeroed();
    let request = &mut envelope.message.control;
    request.header.msgh_bits =
        MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, MACH_MSG_TYPE_MAKE_SEND_ONCE);
    request.header.msgh_size = MESSAGE_SIZE;
    request.header.msgh_remote_port = destination.as_raw();
    request.header.msgh_local_port = reply.as_raw();
    request.header.msgh_id = 0;
    request.kind = kind;
    request.index = index;

    let kr = unsafe {
        mach_msg(
            envelope.header_ptr(),
            MACH_SEND_MSG | MACH_RCV_MSG | MACH_SEND_TIMEOUT | MACH_RCV_TIMEOUT,
            MESSAGE_SIZE,
            ENVELOPE_SIZE,
            reply.as_raw(),
            timeout_ms,
            MACH_PORT_NULL,
        )
    };
    msg_check(kr, "mach_msg(SEND|RCV)")?;
    parse(&mut envelope.message.control).map_err(anyhow::Error::new)
}

pub fn reply(
    to: ReplyTo,
    kind: u32,
    index: u32,
    payload: u64,
    right: Option<SendRight>,
    timeout_ms: u32,
) -> Result<()> {
    let mut message = Message::zeroed();
    message.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_MOVE_SEND_ONCE, 0);
    message.header.msgh_size = MESSAGE_SIZE;
    message.header.msgh_remote_port = to.into_raw();
    message.header.msgh_local_port = MACH_PORT_NULL;
    message.header.msgh_id = 0;
    message.kind = kind;
    message.index = index;
    message.payload = payload;
    attach(&mut message, right);

    let kr = unsafe {
        mach_msg(
            &raw mut message.header,
            MACH_SEND_MSG | MACH_SEND_TIMEOUT,
            MESSAGE_SIZE,
            0,
            MACH_PORT_NULL,
            timeout_ms,
            MACH_PORT_NULL,
        )
    };
    msg_check(kr, "mach_msg(SEND reply)")
}

fn attach(message: &mut Message, right: Option<SendRight>) {
    let Some(right) = right else { return };
    message.header.msgh_bits |= MACH_MSGH_BITS_COMPLEX;
    message.body.msgh_descriptor_count = 1;
    message.port.name = right.into_raw();
    message.port.disposition = MACH_MSG_TYPE_MOVE_SEND as u8;
    message.port.type_ = MACH_MSG_PORT_DESCRIPTOR as u8;
}

pub fn send_handout(
    destination: &SendRight,
    kind: u32,
    index: u32,
    payload: u64,
    receive: &ReceiveRight,
    timeout_ms: u32,
) -> Result<()> {
    let mut message = Message::zeroed();
    message.header.msgh_bits =
        MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0) | MACH_MSGH_BITS_COMPLEX;
    message.header.msgh_size = MESSAGE_SIZE;
    message.header.msgh_remote_port = destination.as_raw();
    message.header.msgh_local_port = MACH_PORT_NULL;
    message.header.msgh_id = 0;
    message.kind = kind;
    message.index = index;
    message.payload = payload;
    message.body.msgh_descriptor_count = 1;
    message.port.name = receive.as_raw();
    message.port.disposition = MACH_MSG_TYPE_MAKE_SEND as u8;
    message.port.type_ = MACH_MSG_PORT_DESCRIPTOR as u8;

    let kr = unsafe {
        mach_msg(
            &raw mut message.header,
            MACH_SEND_MSG | MACH_SEND_TIMEOUT,
            MESSAGE_SIZE,
            0,
            MACH_PORT_NULL,
            timeout_ms,
            MACH_PORT_NULL,
        )
    };
    msg_check(kr, "mach_msg(SEND handout)")
}

fn parse(message: &mut Message) -> Result<Incoming, Fault> {
    let complex = message.header.msgh_bits & MACH_MSGH_BITS_COMPLEX != 0;
    if complex {
        let one_port_descriptor = message.body.msgh_descriptor_count == 1
            && message.port.type_ == MACH_MSG_PORT_DESCRIPTOR as u8;
        if !one_port_descriptor {
            unsafe { mach_msg_destroy(&raw mut message.header) };
            return Err(Fault::Message {
                kind: message.header.msgh_id as u32,
                what: "complex message is not a single port descriptor",
            });
        }
    }

    let one_port = complex && message.port.name != MACH_PORT_NULL;
    let right = one_port.then(|| unsafe { SendRight::from_raw(message.port.name) });
    let remote = message.header.msgh_remote_port;
    let reply = (remote != MACH_PORT_NULL).then_some(ReplyTo(remote));

    Ok(Incoming {
        kind: message.kind,
        index: message.index,
        payload: message.payload,
        right,
        reply,
        samples: 0,
    })
}

fn msg_check(kr: mach_msg_return_t, what: &str) -> Result<()> {
    if kr == KERN_SUCCESS {
        Ok(())
    } else {
        Err(anyhow!("{what} -> {kr:#010x}"))
    }
}

pub fn service_name(prefix: &str, pid: u32, nonce: u64) -> Result<CString> {
    CString::new(format!("{prefix}.{pid}.{nonce:016x}")).map_err(|error| anyhow!("{error}"))
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::float_cmp,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;
    use crate::protocol::kind;
    use mach2::kern_return::{KERN_INVALID_RIGHT, KERN_SUCCESS};
    use mach2::mach_port::mach_port_insert_right;
    use mach2::message::MACH_MSG_OOL_DESCRIPTOR;
    use mach2::port::MACH_PORT_RIGHT_SEND;

    #[test]
    fn parse_takes_the_one_port_descriptor_and_leaves_scalars_alone() {
        let mut plain = Message::zeroed();
        plain.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0);
        plain.kind = kind::OPENED;
        plain.index = 7;
        plain.payload = 0xdead_beef;
        let incoming = parse(&mut plain).unwrap();
        assert_eq!(incoming.kind, kind::OPENED);
        assert_eq!(incoming.index, 7);
        assert_eq!(incoming.payload, 0xdead_beef);
        assert!(incoming.right.is_none());

        let port = ReceiveRight::allocate().unwrap();
        let name = port.as_raw();
        let kr = unsafe {
            mach_port_insert_right(mach_task_self(), name, name, MACH_MSG_TYPE_MAKE_SEND)
        };
        assert_eq!(kr, KERN_SUCCESS);

        let mut complex = Message::zeroed();
        complex.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0) | MACH_MSGH_BITS_COMPLEX;
        complex.body.msgh_descriptor_count = 1;
        complex.port.name = name;
        complex.port.disposition = MACH_MSG_TYPE_MOVE_SEND as u8;
        complex.port.type_ = MACH_MSG_PORT_DESCRIPTOR as u8;
        complex.kind = kind::SURFACE;
        complex.index = 3;
        let incoming = parse(&mut complex).unwrap();
        assert_eq!(incoming.kind, kind::SURFACE);
        assert_eq!(incoming.index, 3);
        let right = incoming.right.expect("the send right must be taken");
        assert_eq!(right.as_raw(), name);
        drop(right);
        drop(port);
    }

    #[test]
    fn parse_rejects_a_shifted_complex_layout() {
        let mut zero = Message::zeroed();
        zero.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0) | MACH_MSGH_BITS_COMPLEX;
        zero.body.msgh_descriptor_count = 0;
        zero.kind = kind::OPENED;
        assert!(matches!(
            parse(&mut zero),
            Err(Fault::Message { .. })
        ));

        let mut ool = Message::zeroed();
        ool.header.msgh_bits = MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0) | MACH_MSGH_BITS_COMPLEX;
        ool.body.msgh_descriptor_count = 1;
        ool.port.type_ = MACH_MSG_OOL_DESCRIPTOR as u8;
        ool.port.name = MACH_PORT_NULL;
        assert!(matches!(parse(&mut ool), Err(Fault::Message { .. })));
    }

    #[test]
    fn parse_destroys_unexpected_descriptor() {
        let port = ReceiveRight::allocate().unwrap();
        let name = port.as_raw();
        let kr = unsafe {
            mach_port_insert_right(mach_task_self(), name, name, MACH_MSG_TYPE_MAKE_SEND)
        };
        assert_eq!(kr, KERN_SUCCESS);
        let before = unsafe { mach_port_mod_refs(mach_task_self(), name, MACH_PORT_RIGHT_SEND, 0) };
        assert_eq!(before, KERN_SUCCESS, "the send right should exist pre-parse");

        let mut message = Message::zeroed();
        message.header.msgh_bits =
            MACH_MSGH_BITS(MACH_MSG_TYPE_COPY_SEND, 0) | MACH_MSGH_BITS_COMPLEX;
        message.body.msgh_descriptor_count = 2;
        message.port.name = name;
        message.port.disposition = MACH_MSG_TYPE_MOVE_SEND as u8;
        message.port.type_ = MACH_MSG_PORT_DESCRIPTOR as u8;

        let result = parse(&mut message);
        assert!(
            matches!(result, Err(Fault::Message { .. })),
            "a two-descriptor message must be refused with a typed fault"
        );

        let after = unsafe { mach_port_mod_refs(mach_task_self(), name, MACH_PORT_RIGHT_SEND, 0) };
        assert_eq!(
            after, KERN_INVALID_RIGHT,
            "mach_msg_destroy must have released the descriptor's send right"
        );

        drop(port);
    }

    #[test]
    fn an_audio_packet_round_trips_inline() {
        let port = ReceiveRight::allocate().unwrap();
        let name = port.as_raw();
        let kr = unsafe {
            mach_port_insert_right(mach_task_self(), name, name, MACH_MSG_TYPE_MAKE_SEND)
        };
        assert_eq!(kr, KERN_SUCCESS);
        let destination = unsafe { SendRight::from_raw(name) };

        let samples: Vec<f32> = (0..960).map(|i| i as f32 * 0.5).collect();
        send_audio(&destination, 12.25, &samples, true, 100).unwrap();

        let mut out = [0.0f32; AUDIO_PACKET_SAMPLES];
        let incoming = receive_into(&port, 100, &mut out).unwrap();
        assert_eq!(incoming.kind, kind::AUDIO);
        assert_eq!(incoming.index, 1, "the discontinuity flag rides `index`");
        assert_eq!(f64::from_bits(incoming.payload), 12.25);
        assert_eq!(incoming.samples as usize, samples.len());
        assert_eq!(&out[..samples.len()], samples.as_slice());

        std::mem::forget(destination);
        drop(port);
    }

    #[test]
    fn an_over_claiming_audio_packet_yields_nothing() {
        let mut message = AudioMessage::zeroed();
        message.samples = 64;
        let mut out = [0.0f32; 64];
        assert_eq!(
            copy_samples(&message, AUDIO_PREFIX_SIZE + 4, &mut out),
            0,
            "64 samples claimed, one sample's worth delivered"
        );

        message.samples = (AUDIO_PACKET_SAMPLES + 1) as u32;
        assert_eq!(
            copy_samples(&message, usize::MAX, &mut out),
            0,
            "a count past the inline array is refused outright"
        );
    }
}
