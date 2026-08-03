
use crate::protocol::{self, Fault};

pub const MESSAGE_MAGIC: u32 = 0x5557_4952;

pub const MESSAGE_BYTES: usize = 32;

pub const FLAG_HAS_HANDLE: u32 = 1 << 0;

pub const FLAG_MASK: u32 = FLAG_HAS_HANDLE;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Message {
    pub kind: u32,
    pub index: u32,
    pub flags: u32,
    pub payload: u64,
    pub handle: u64,
}

impl Message {
    pub const fn scalar(kind: u32, index: u32, payload: u64) -> Self {
        Self {
            kind,
            index,
            flags: 0,
            payload,
            handle: 0,
        }
    }

    pub const fn with_handle(kind: u32, index: u32, payload: u64, handle: u64) -> Self {
        Self {
            kind,
            index,
            flags: FLAG_HAS_HANDLE,
            payload,
            handle,
        }
    }

    pub const fn carries_handle(&self) -> bool {
        self.flags & FLAG_HAS_HANDLE != 0
    }

    pub fn encode(&self) -> [u8; MESSAGE_BYTES] {
        let mut out = [0u8; MESSAGE_BYTES];
        put32(&mut out, 0, MESSAGE_MAGIC);
        put32(&mut out, 4, self.kind);
        put32(&mut out, 8, self.index);
        put32(&mut out, 12, self.flags);
        put64(&mut out, 16, self.payload);
        put64(&mut out, 24, self.handle);
        out
    }
}

fn put32(out: &mut [u8; MESSAGE_BYTES], at: usize, value: u32) {
    if let Some(slot) = out.get_mut(at..at.saturating_add(4)) {
        slot.copy_from_slice(&value.to_le_bytes());
    }
}

fn put64(out: &mut [u8; MESSAGE_BYTES], at: usize, value: u64) {
    if let Some(slot) = out.get_mut(at..at.saturating_add(8)) {
        slot.copy_from_slice(&value.to_le_bytes());
    }
}

fn get32(bytes: &[u8], at: usize) -> Option<u32> {
    let slice = bytes.get(at..at.checked_add(4)?)?;
    Some(u32::from_le_bytes([
        *slice.first()?,
        *slice.get(1)?,
        *slice.get(2)?,
        *slice.get(3)?,
    ]))
}

fn get64(bytes: &[u8], at: usize) -> Option<u64> {
    let low = u64::from(get32(bytes, at)?);
    let high = u64::from(get32(bytes, at.checked_add(4)?)?);
    Some(low | (high << 32))
}

pub fn decode(bytes: &[u8]) -> Result<Message, Fault> {
    if bytes.len() != MESSAGE_BYTES {
        return Err(Fault::Message {
            kind: 0,
            what: "control message is not exactly one wire message",
        });
    }
    let (Some(magic), Some(kind), Some(index), Some(flags)) = (
        get32(bytes, 0),
        get32(bytes, 4),
        get32(bytes, 8),
        get32(bytes, 12),
    ) else {
        return Err(Fault::Message {
            kind: 0,
            what: "control message is truncated",
        });
    };
    let (Some(payload), Some(handle)) = (get64(bytes, 16), get64(bytes, 24)) else {
        return Err(Fault::Message {
            kind: 0,
            what: "control message is truncated",
        });
    };

    if magic != MESSAGE_MAGIC {
        return Err(Fault::Message {
            kind,
            what: "control message magic does not match",
        });
    }
    if flags & !FLAG_MASK != 0 {
        return Err(Fault::Message {
            kind,
            what: "control message sets an unknown flag",
        });
    }
    if flags & FLAG_HAS_HANDLE == 0 && handle != 0 {
        return Err(Fault::Message {
            kind,
            what: "control message carries a handle with no handle flag",
        });
    }
    if flags & FLAG_HAS_HANDLE != 0 {
        validate_handle_value(kind, handle)?;
    }

    Ok(Message {
        kind,
        index,
        flags,
        payload,
        handle,
    })
}

pub fn decode_from_helper(bytes: &[u8]) -> Result<Message, Fault> {
    let message = decode(bytes)?;
    protocol::check_incoming(message.kind, message.carries_handle())?;
    Ok(message)
}

pub fn decode_from_host(bytes: &[u8]) -> Result<Message, Fault> {
    let message = decode(bytes)?;
    if !protocol::is_host_to_helper(message.kind) {
        return Err(Fault::Message {
            kind: message.kind,
            what: "not a host-to-helper kind",
        });
    }
    if message.carries_handle() {
        return Err(Fault::Message {
            kind: message.kind,
            what: "host-to-helper messages carry no handle",
        });
    }
    Ok(message)
}

const INVALID_HANDLE_64: u64 = u64::MAX;
const INVALID_HANDLE_32: u64 = 0xFFFF_FFFF;

pub fn validate_handle_value(kind: u32, handle: u64) -> Result<(), Fault> {
    if handle == 0 {
        return Err(Fault::Message {
            kind,
            what: "handle flag set but the handle value is null",
        });
    }
    if handle == INVALID_HANDLE_64 || handle == INVALID_HANDLE_32 {
        return Err(Fault::Message {
            kind,
            what: "handle value is a pseudo-handle",
        });
    }
    if handle > u64::from(u32::MAX) {
        return Err(Fault::Message {
            kind,
            what: "handle value does not fit the documented 32 significant bits",
        });
    }
    if handle & 0b11 != 0 {
        return Err(Fault::Message {
            kind,
            what: "handle value is not a four-byte-aligned kernel handle",
        });
    }
    Ok(())
}

pub const PIPE_NAME_PREFIX: &str = r"\\.\pipe\com.decentraland.uuav.helper.";

pub fn pipe_name(host_pid: u32, nonce_high: u64, nonce_low: u64) -> String {
    format!("{PIPE_NAME_PREFIX}{host_pid:08x}.{nonce_high:016x}{nonce_low:016x}")
}


const fn proc_thread_attribute(number: u32, thread: bool, input: bool, additive: bool) -> usize {
    let mut value = number & 0x0000_FFFF;
    if thread {
        value |= 0x0001_0000;
    }
    if input {
        value |= 0x0002_0000;
    }
    if additive {
        value |= 0x0004_0000;
    }
    value as usize
}

pub const PROC_THREAD_ATTRIBUTE_HANDLE_LIST: usize = proc_thread_attribute(2, false, true, false);
pub const PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY: usize =
    proc_thread_attribute(7, false, true, false);
pub const PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES: usize =
    proc_thread_attribute(9, false, true, false);
pub const PROC_THREAD_ATTRIBUTE_JOB_LIST: usize = proc_thread_attribute(13, false, true, false);
pub const PROC_THREAD_ATTRIBUTE_CHILD_PROCESS_POLICY: usize =
    proc_thread_attribute(14, false, true, false);

pub const PROCESS_CREATION_CHILD_PROCESS_RESTRICTED: u32 = 0x01;

const fn mitigation_on(shift: u32) -> u64 {
    1u64 << shift
}

pub const MITIGATION_DEP_ENABLE: u64 = 0x0000_0001;
pub const MITIGATION_DEP_ATL_THUNK_ENABLE: u64 = 0x0000_0002;
pub const MITIGATION_SEHOP_ENABLE: u64 = 0x0000_0004;
pub const MITIGATION_FORCE_RELOCATE_IMAGES_ALWAYS_ON: u64 = mitigation_on(8);
pub const MITIGATION_HEAP_TERMINATE_ALWAYS_ON: u64 = mitigation_on(12);
pub const MITIGATION_BOTTOM_UP_ASLR_ALWAYS_ON: u64 = mitigation_on(16);
pub const MITIGATION_HIGH_ENTROPY_ASLR_ALWAYS_ON: u64 = mitigation_on(20);
pub const MITIGATION_STRICT_HANDLE_CHECKS_ALWAYS_ON: u64 = mitigation_on(24);
pub const MITIGATION_WIN32K_SYSTEM_CALL_DISABLE_ALWAYS_ON: u64 = mitigation_on(28);
pub const MITIGATION_EXTENSION_POINT_DISABLE_ALWAYS_ON: u64 = mitigation_on(32);
pub const MITIGATION_PROHIBIT_DYNAMIC_CODE_ALWAYS_ON: u64 = mitigation_on(36);
pub const MITIGATION_CONTROL_FLOW_GUARD_ALWAYS_ON: u64 = mitigation_on(40);
pub const MITIGATION_BLOCK_NON_MICROSOFT_BINARIES_ALWAYS_ON: u64 = mitigation_on(44);
pub const MITIGATION_IMAGE_LOAD_NO_REMOTE_ALWAYS_ON: u64 = mitigation_on(52);
pub const MITIGATION_IMAGE_LOAD_NO_LOW_LABEL_ALWAYS_ON: u64 = mitigation_on(56);
pub const MITIGATION_IMAGE_LOAD_PREFER_SYSTEM32_ALWAYS_ON: u64 = mitigation_on(60);

pub const JOB_OBJECT_LIMIT_ACTIVE_PROCESS: u32 = 0x0000_0008;
pub const JOB_OBJECT_LIMIT_PROCESS_MEMORY: u32 = 0x0000_0100;
pub const JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION: u32 = 0x0000_0400;
pub const JOB_OBJECT_LIMIT_BREAKAWAY_OK: u32 = 0x0000_0800;
pub const JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK: u32 = 0x0000_1000;
pub const JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE: u32 = 0x0000_2000;

pub const JOB_OBJECT_UILIMIT_HANDLES: u32 = 0x0000_0001;
pub const JOB_OBJECT_UILIMIT_READCLIPBOARD: u32 = 0x0000_0002;
pub const JOB_OBJECT_UILIMIT_WRITECLIPBOARD: u32 = 0x0000_0004;
pub const JOB_OBJECT_UILIMIT_SYSTEMPARAMETERS: u32 = 0x0000_0008;
pub const JOB_OBJECT_UILIMIT_DISPLAYSETTINGS: u32 = 0x0000_0010;
pub const JOB_OBJECT_UILIMIT_GLOBALATOMS: u32 = 0x0000_0020;
pub const JOB_OBJECT_UILIMIT_DESKTOP: u32 = 0x0000_0040;
pub const JOB_OBJECT_UILIMIT_EXITWINDOWS: u32 = 0x0000_0080;

pub const DISABLE_MAX_PRIVILEGE: u32 = 0x1;
pub const SANDBOX_INERT: u32 = 0x2;

pub const SECURITY_MANDATORY_LOW_RID: u32 = 0x1000;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Mode {
    Gpu,
    Software,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Token {
    Restricted,
    AppContainer,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Confinement {
    pub mode: Mode,
    pub token: Token,
    pub process_memory_bytes: u64,
}

pub const DEFAULT_PROCESS_MEMORY_BYTES: u64 = 1 << 30;

impl Confinement {
    pub const fn gpu() -> Self {
        Self {
            mode: Mode::Gpu,
            token: Token::Restricted,
            process_memory_bytes: DEFAULT_PROCESS_MEMORY_BYTES,
        }
    }

    pub const fn software() -> Self {
        Self {
            mode: Mode::Software,
            token: Token::Restricted,
            process_memory_bytes: DEFAULT_PROCESS_MEMORY_BYTES,
        }
    }

    pub const fn mitigation_policy(&self) -> u64 {
        let common = MITIGATION_DEP_ENABLE
            | MITIGATION_DEP_ATL_THUNK_ENABLE
            | MITIGATION_SEHOP_ENABLE
            | MITIGATION_FORCE_RELOCATE_IMAGES_ALWAYS_ON
            | MITIGATION_HEAP_TERMINATE_ALWAYS_ON
            | MITIGATION_BOTTOM_UP_ASLR_ALWAYS_ON
            | MITIGATION_HIGH_ENTROPY_ASLR_ALWAYS_ON
            | MITIGATION_STRICT_HANDLE_CHECKS_ALWAYS_ON
            | MITIGATION_EXTENSION_POINT_DISABLE_ALWAYS_ON
            | MITIGATION_CONTROL_FLOW_GUARD_ALWAYS_ON
            | MITIGATION_IMAGE_LOAD_NO_REMOTE_ALWAYS_ON
            | MITIGATION_IMAGE_LOAD_NO_LOW_LABEL_ALWAYS_ON
            | MITIGATION_IMAGE_LOAD_PREFER_SYSTEM32_ALWAYS_ON;
        match self.mode {
            Mode::Gpu => common,
            Mode::Software => {
                common
                    | MITIGATION_PROHIBIT_DYNAMIC_CODE_ALWAYS_ON
                    | MITIGATION_WIN32K_SYSTEM_CALL_DISABLE_ALWAYS_ON
            }
        }
    }

    pub const fn job_limit_flags(&self) -> u32 {
        JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            | JOB_OBJECT_LIMIT_ACTIVE_PROCESS
            | JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION
            | JOB_OBJECT_LIMIT_PROCESS_MEMORY
    }

    pub const fn job_ui_restrictions(&self) -> u32 {
        let common = JOB_OBJECT_UILIMIT_READCLIPBOARD
            | JOB_OBJECT_UILIMIT_WRITECLIPBOARD
            | JOB_OBJECT_UILIMIT_SYSTEMPARAMETERS
            | JOB_OBJECT_UILIMIT_DISPLAYSETTINGS
            | JOB_OBJECT_UILIMIT_GLOBALATOMS
            | JOB_OBJECT_UILIMIT_DESKTOP
            | JOB_OBJECT_UILIMIT_EXITWINDOWS;
        match self.mode {
            Mode::Gpu => common,
            Mode::Software => common | JOB_OBJECT_UILIMIT_HANDLES,
        }
    }
}

pub fn command_line(
    exe: &str,
    pipe_handle: u64,
    segment_handle: u64,
    cookie: u64,
    mode: Mode,
    adapter_luid: Option<u64>,
) -> String {
    let mut parts = vec![
        quote_argument(exe),
        "--pipe".to_owned(),
        format!("{pipe_handle:x}"),
        "--segment".to_owned(),
        format!("{segment_handle:x}"),
        "--cookie".to_owned(),
        format!("{cookie:016x}"),
        "--mode".to_owned(),
        match mode {
            Mode::Gpu => "gpu".to_owned(),
            Mode::Software => "software".to_owned(),
        },
    ];
    if let Some(luid) = adapter_luid {
        parts.push("--adapter".to_owned());
        parts.push(format!("{luid:016x}"));
    }
    parts.join(" ")
}

pub fn quote_argument(argument: &str) -> String {
    if !argument.is_empty() && !argument.contains([' ', '\t', '"']) {
        return argument.to_owned();
    }
    let mut out = String::with_capacity(argument.len().saturating_add(2));
    out.push('"');
    let mut backslashes = 0usize;
    for character in argument.chars() {
        match character {
            '\\' => {
                backslashes = backslashes.saturating_add(1);
                out.push('\\');
            }
            '"' => {
                for _ in 0..backslashes.saturating_add(1) {
                    out.push('\\');
                }
                backslashes = 0;
                out.push('"');
            }
            other => {
                backslashes = 0;
                out.push(other);
            }
        }
    }
    for _ in 0..backslashes {
        out.push('\\');
    }
    out.push('"');
    out
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;
    use crate::protocol::kind;

    fn frame(message: &Message) -> Vec<u8> {
        message.encode().to_vec()
    }

    #[test]
    fn a_scalar_message_round_trips() {
        let sent = Message::scalar(kind::OPENED, 7, 0xdead_beef_feed_face);
        let got = decode(&frame(&sent)).unwrap();
        assert_eq!(got, sent);
        assert!(!got.carries_handle());
    }

    #[test]
    fn a_handle_message_round_trips() {
        let sent = Message::with_handle(kind::SURFACE, 3, 9, 0x1234);
        let got = decode(&frame(&sent)).unwrap();
        assert_eq!(got, sent);
        assert!(got.carries_handle());
        assert_eq!(got.handle, 0x1234);
    }

    #[test]
    fn the_encoding_is_the_documented_little_endian_layout() {
        let bytes = Message::with_handle(0x0201, 0x0403, 0x0807_0605, 0x0c0b_0a08).encode();
        assert_eq!(&bytes[0..4], &MESSAGE_MAGIC.to_le_bytes());
        assert_eq!(&bytes[4..8], &0x0201u32.to_le_bytes());
        assert_eq!(&bytes[8..12], &0x0403u32.to_le_bytes());
        assert_eq!(&bytes[12..16], &FLAG_HAS_HANDLE.to_le_bytes());
        assert_eq!(&bytes[16..24], &0x0807_0605u64.to_le_bytes());
        assert_eq!(&bytes[24..32], &0x0c0b_0a08u64.to_le_bytes());
    }

    #[test]
    fn a_short_or_long_read_is_a_fault_not_a_partial_message() {
        let bytes = frame(&Message::scalar(kind::ENDED, 0, 0));
        assert!(decode(&bytes[..MESSAGE_BYTES - 1]).is_err());
        assert!(decode(&[]).is_err());
        let mut long = bytes;
        long.push(0);
        assert!(decode(&long).is_err());
    }

    #[test]
    fn a_wrong_magic_is_refused_before_the_kind_is_believed() {
        let mut bytes = frame(&Message::scalar(kind::OPENED, 0, 0));
        bytes[0] ^= 0xff;
        assert!(matches!(decode(&bytes), Err(Fault::Message { .. })));
    }

    #[test]
    fn an_unknown_flag_bit_is_a_fault() {
        let mut bytes = frame(&Message::scalar(kind::OPENED, 0, 0));
        bytes[12..16].copy_from_slice(&0x8000_0000u32.to_le_bytes());
        assert!(decode(&bytes).is_err());
    }

    #[test]
    fn a_handle_without_its_flag_is_a_fault_rather_than_ignored() {
        let mut bytes = frame(&Message::scalar(kind::OPENED, 0, 0));
        bytes[24..32].copy_from_slice(&0x1000u64.to_le_bytes());
        assert!(decode(&bytes).is_err());
    }

    #[test]
    fn hostile_handle_values_are_all_refused() {
        for hostile in [
            0u64,
            u64::MAX,
            0xFFFF_FFFF,
            0xFFFF_FFFE,
            0x1_0000_0000,
            0xFFFF_FFFF_FFFF_FFFC,
            0x1001,
            0x1002,
            0x1003,
        ] {
            assert!(
                validate_handle_value(kind::SURFACE, hostile).is_err(),
                "handle {hostile:#x} must be refused"
            );
        }
        for legal in [0x4u64, 0x1234, 0xFFFF_FFF8] {
            assert!(
                validate_handle_value(kind::SURFACE, legal).is_ok(),
                "handle {legal:#x} must be accepted"
            );
        }
    }

    #[test]
    fn the_helper_may_not_send_a_host_to_helper_kind() {
        for forbidden in [
            kind::OPEN,
            kind::PLAY,
            kind::PAUSE,
            kind::CLOSE,
            kind::SHUTDOWN,
            kind::SET_LOG_LEVEL,
        ] {
            let bytes = frame(&Message::scalar(forbidden, 0, 0));
            assert!(decode_from_helper(&bytes).is_err(), "kind {forbidden:#x}");
        }
    }

    #[test]
    fn the_helper_may_not_attach_a_handle_to_a_kind_that_carries_none() {
        for forbidden in [kind::OPENED, kind::FAILED, kind::ENDED, kind::GOODBYE, kind::FETCH] {
            let bytes = frame(&Message::with_handle(forbidden, 0, 0, 0x20));
            assert!(decode_from_helper(&bytes).is_err(), "kind {forbidden:#x}");
        }
    }

    #[test]
    fn a_fetch_doorbell_is_a_helper_scalar_carrying_handle_and_generation() {
        let doorbell = Message::scalar(kind::FETCH, 7, 42);
        let got = decode_from_helper(&frame(&doorbell)).unwrap();
        assert_eq!(got, doorbell);
        assert!(!got.carries_handle());
        assert!(decode_from_host(&frame(&Message::scalar(kind::FETCH, 0, 0))).is_err());
    }

    #[test]
    fn the_kinds_that_must_carry_a_handle_are_refused_without_one() {
        for required in [kind::HELLO, kind::SURFACE] {
            let bytes = frame(&Message::scalar(required, 0, 0));
            assert!(decode_from_helper(&bytes).is_err(), "kind {required:#x}");
            let ok = frame(&Message::with_handle(required, 0, 0, 0x40));
            assert!(decode_from_helper(&ok).is_ok(), "kind {required:#x}");
        }
    }

    #[test]
    fn the_host_may_not_send_helper_to_host_kinds_or_handles() {
        for forbidden in [kind::HELLO, kind::SURFACE, kind::OPENED, kind::ENDED] {
            assert!(decode_from_host(&frame(&Message::scalar(forbidden, 0, 0))).is_err());
        }
        assert!(decode_from_host(&frame(&Message::scalar(kind::PLAY, 0, 0))).is_ok());
        assert!(decode_from_host(&frame(&Message::with_handle(kind::PLAY, 0, 0, 0x40))).is_err());
    }

    #[test]
    fn decode_never_panics_on_arbitrary_bytes() {
        let mut state = 0x2545_F491_4F6C_DD1Du64;
        for _ in 0..200_000 {
            let mut bytes = [0u8; MESSAGE_BYTES];
            for slot in &mut bytes {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                *slot = state as u8;
            }
            if state & 1 == 0 {
                bytes[0..4].copy_from_slice(&MESSAGE_MAGIC.to_le_bytes());
            }
            let _ignored = decode(&bytes);
            let _ignored = decode_from_helper(&bytes);
            let _ignored = decode_from_host(&bytes);
        }
    }

    #[test]
    fn attribute_values_match_the_sdk() {
        assert_eq!(PROC_THREAD_ATTRIBUTE_HANDLE_LIST, 0x0002_0002);
        assert_eq!(PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY, 0x0002_0007);
        assert_eq!(PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES, 0x0002_0009);
        assert_eq!(PROC_THREAD_ATTRIBUTE_JOB_LIST, 0x0002_000D);
        assert_eq!(PROC_THREAD_ATTRIBUTE_CHILD_PROCESS_POLICY, 0x0002_000E);
    }

    #[test]
    fn mitigation_bits_are_the_two_bit_fields_the_header_defines() {
        assert_eq!(MITIGATION_FORCE_RELOCATE_IMAGES_ALWAYS_ON, 1 << 8);
        assert_eq!(MITIGATION_HEAP_TERMINATE_ALWAYS_ON, 1 << 12);
        assert_eq!(MITIGATION_BOTTOM_UP_ASLR_ALWAYS_ON, 1 << 16);
        assert_eq!(MITIGATION_HIGH_ENTROPY_ASLR_ALWAYS_ON, 1 << 20);
        assert_eq!(MITIGATION_STRICT_HANDLE_CHECKS_ALWAYS_ON, 1 << 24);
        assert_eq!(MITIGATION_WIN32K_SYSTEM_CALL_DISABLE_ALWAYS_ON, 1 << 28);
        assert_eq!(MITIGATION_EXTENSION_POINT_DISABLE_ALWAYS_ON, 1 << 32);
        assert_eq!(MITIGATION_PROHIBIT_DYNAMIC_CODE_ALWAYS_ON, 1 << 36);
        assert_eq!(MITIGATION_CONTROL_FLOW_GUARD_ALWAYS_ON, 1 << 40);
        assert_eq!(MITIGATION_BLOCK_NON_MICROSOFT_BINARIES_ALWAYS_ON, 1 << 44);
        assert_eq!(MITIGATION_IMAGE_LOAD_NO_REMOTE_ALWAYS_ON, 1 << 52);
        assert_eq!(MITIGATION_IMAGE_LOAD_NO_LOW_LABEL_ALWAYS_ON, 1 << 56);
        assert_eq!(MITIGATION_IMAGE_LOAD_PREFER_SYSTEM32_ALWAYS_ON, 1 << 60);
        for shift in [8u32, 12, 16, 20, 24, 28, 32, 36, 40, 44, 52, 56, 60] {
            assert_eq!(mitigation_on(shift) & (2 << shift), 0);
        }
    }

    #[test]
    fn the_gpu_plan_drops_exactly_the_two_policies_d3d_cannot_take() {
        let gpu = Confinement::gpu().mitigation_policy();
        let software = Confinement::software().mitigation_policy();

        assert_eq!(gpu & MITIGATION_PROHIBIT_DYNAMIC_CODE_ALWAYS_ON, 0);
        assert_eq!(gpu & MITIGATION_WIN32K_SYSTEM_CALL_DISABLE_ALWAYS_ON, 0);
        assert_ne!(software & MITIGATION_PROHIBIT_DYNAMIC_CODE_ALWAYS_ON, 0);
        assert_ne!(software & MITIGATION_WIN32K_SYSTEM_CALL_DISABLE_ALWAYS_ON, 0);

        for plan in [gpu, software] {
            assert_eq!(plan & MITIGATION_BLOCK_NON_MICROSOFT_BINARIES_ALWAYS_ON, 0);
            assert_ne!(plan & MITIGATION_DEP_ENABLE, 0);
            assert_ne!(plan & MITIGATION_HIGH_ENTROPY_ASLR_ALWAYS_ON, 0);
            assert_ne!(plan & MITIGATION_STRICT_HANDLE_CHECKS_ALWAYS_ON, 0);
            assert_ne!(plan & MITIGATION_EXTENSION_POINT_DISABLE_ALWAYS_ON, 0);
            assert_ne!(plan & MITIGATION_IMAGE_LOAD_PREFER_SYSTEM32_ALWAYS_ON, 0);
        }
        assert_eq!(software & gpu, gpu);
    }

    #[test]
    fn the_job_never_permits_breakaway_and_always_kills_on_close() {
        for plan in [Confinement::gpu(), Confinement::software()] {
            let flags = plan.job_limit_flags();
            assert_ne!(flags & JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE, 0);
            assert_ne!(flags & JOB_OBJECT_LIMIT_ACTIVE_PROCESS, 0);
            assert_ne!(flags & JOB_OBJECT_LIMIT_PROCESS_MEMORY, 0);
            assert_eq!(flags & JOB_OBJECT_LIMIT_BREAKAWAY_OK, 0);
            assert_eq!(flags & JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK, 0);
        }
    }

    #[test]
    fn only_the_software_plan_takes_away_user_handles() {
        assert_eq!(
            Confinement::gpu().job_ui_restrictions() & JOB_OBJECT_UILIMIT_HANDLES,
            0
        );
        assert_ne!(
            Confinement::software().job_ui_restrictions() & JOB_OBJECT_UILIMIT_HANDLES,
            0
        );
        for plan in [Confinement::gpu(), Confinement::software()] {
            assert_ne!(plan.job_ui_restrictions() & JOB_OBJECT_UILIMIT_DESKTOP, 0);
            assert_ne!(plan.job_ui_restrictions() & JOB_OBJECT_UILIMIT_EXITWINDOWS, 0);
        }
    }

    #[test]
    fn the_pipe_name_is_unguessable_and_well_formed() {
        let name = pipe_name(4242, 0x0123_4567_89ab_cdef, 0xfedc_ba98_7654_3210);
        assert!(name.starts_with(PIPE_NAME_PREFIX));
        assert!(name.ends_with("0123456789abcdeffedcba9876543210"));
        assert_ne!(
            name,
            pipe_name(4242, 0x0123_4567_89ab_cdef, 0xfedc_ba98_7654_3211)
        );
        assert!(name.len() < 256, "{} bytes", name.len());
    }

    #[test]
    fn the_command_line_is_the_frozen_shape() {
        let line = command_line(
            r"C:\Program Files\Game\uuav-ipc-helper.exe",
            0x1c,
            0x20,
            0xcafe_f00d,
            Mode::Gpu,
            Some(0x0000_0001_0000_9c40),
        );
        assert_eq!(
            line,
            "\"C:\\Program Files\\Game\\uuav-ipc-helper.exe\" \
             --pipe 1c --segment 20 --cookie 00000000cafef00d --mode gpu \
             --adapter 0000000100009c40"
        );
        let software = command_line(r"C:\g\h.exe", 4, 8, 1, Mode::Software, None);
        assert_eq!(
            software,
            r"C:\g\h.exe --pipe 4 --segment 8 --cookie 0000000000000001 --mode software"
        );
    }

    #[test]
    fn argument_quoting_survives_quotes_and_trailing_backslashes() {
        assert_eq!(quote_argument("plain"), "plain");
        assert_eq!(quote_argument("with space"), "\"with space\"");
        assert_eq!(quote_argument(r"C:\dir\"), r"C:\dir\");
        assert_eq!(quote_argument(r"C:\my dir\"), "\"C:\\my dir\\\\\"");
        assert_eq!(quote_argument(r#"a"b"#), r#""a\"b""#);
        assert_eq!(quote_argument(r#"a\"b"#), r#""a\\\"b""#);
        assert_eq!(quote_argument(""), "\"\"");
    }
}
