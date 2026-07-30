//! Wire protocol between the `uuav` client dylib (inside Unity) and the
//! `uuav-helper` process. Messages are postcard-serialized enums, one
//! message per zmq frame.

use serde::{Deserialize, Serialize};

pub type PlayerId = u64;
pub type Corr = u32;

/// Mirror of the C ABI `AudioOptionsRaw`; sanitized by the core on arrival.
#[derive(Serialize, Deserialize, Clone, Copy, Debug, PartialEq, Eq)]
pub struct AudioOptionsWire {
    pub sample_rate: i32,
    pub channels: i32,
}

/// Mirror of the C ABI `UUAVState` discriminants.
#[derive(Serialize, Deserialize, Clone, Copy, Debug, PartialEq, Eq)]
pub enum PlayerStateWire {
    Closed,
    Opening,
    Ready,
    Playing,
    Paused,
    Ended,
    Error,
    Unknown,
}

/// Mirror of the C ABI `ControlsState`.
#[allow(clippy::struct_excessive_bools)] // wire mirror of a frozen C struct
#[derive(Serialize, Deserialize, Clone, Copy, Debug, Default)]
pub struct ControlsWire {
    pub rate: f64,
    pub play: bool,
    pub play_pending: bool,
    pub looping: bool,
    pub looping_pending: bool,
    pub rate_pending: bool,
}

/// Mirror of the C ABI `MediaInfo` with owned strings instead of the
/// fixed NUL-terminated buffers.
#[derive(Serialize, Deserialize, Clone, Debug, Default)]
pub struct MediaInfoWire {
    pub duration: f64,
    pub framerate: f64,
    pub video_bitrate: i64,
    pub audio_bitrate: i64,
    pub width: u32,
    pub height: u32,
    pub sample_rate: i32,
    pub channels: i32,
    pub video_codec: String,
    pub pixel_format: String,
    pub audio_codec: String,
    pub sample_format: String,
    pub has_video: bool,
    pub has_audio: bool,
}

/// Per-player snapshot pushed by the helper's state pump (~50 Hz per live
/// player).
///
/// The client caches the latest one and serves every per-frame getter from
/// it; `media_time` is extrapolated on the client from the snapshot's
/// arrival instant while `state == Playing`.
#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct StateUpdateWire {
    pub id: PlayerId,
    pub state: PlayerStateWire,
    /// `None` until the core reports a current time (mirrors the getter error).
    pub media_time: Option<f64>,
    /// `None` for realtime streams / not yet known (mirrors the getter error).
    pub duration: Option<f64>,
    pub controls: ControlsWire,
    /// `None` until the first decoded frame (mirrors the getter error).
    pub video_size: Option<(u32, u32)>,
    pub looping: bool,
    pub rate: f64,
}

/// FFmpeg log severity bucket, matching the three Unity sinks.
#[derive(Serialize, Deserialize, Clone, Copy, Debug)]
pub enum LogSink {
    Error,
    Warning,
    Log,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub enum ReplyBody {
    Unit,
    PlayerId(PlayerId),
}

/// Client (Unity) -> server (helper).
#[derive(Serialize, Deserialize, Clone, Debug)]
pub enum ToServer {
    /// Completes the handshake after the client validated `Hello`. The helper
    /// creates its GPU device (`adapter` = D3D11 LUID on Windows / Metal
    /// registryID on macOS, 0 = system default) and runs the core init.
    Configure {
        corr: Corr,
        audio: AudioOptionsWire,
        protocol_whitelist: String,
        log_level: i32,
        adapter: u64,
    },
    SetLogLevel {
        level: i32,
    },
    UpdateAudioOut {
        corr: Corr,
        audio: AudioOptionsWire,
    },
    PlayerNew {
        corr: Corr,
    },
    PlayerFree {
        id: PlayerId,
    },
    OpenMedia {
        id: PlayerId,
        url: String,
    },
    CloseMedia {
        id: PlayerId,
    },
    Play {
        id: PlayerId,
    },
    Pause {
        id: PlayerId,
    },
    Seek {
        id: PlayerId,
        time: f64,
    },
    SetLooping {
        id: PlayerId,
        looping: bool,
    },
    SetRate {
        id: PlayerId,
        rate: f64,
    },
    AssignMasterClock {
        id: PlayerId,
        time: f64,
    },
    /// M2: the client opened (or failed to open) a texture-set generation.
    TextureSetAck {
        id: PlayerId,
        generation: u32,
    },
    Shutdown,
}

/// Server (helper) -> client (Unity).
#[derive(Serialize, Deserialize, Clone, Debug)]
pub enum ToClient {
    /// First message on the wire; `token` must equal the uuid the client
    /// passed in argv, `abi` must equal the client's own version string.
    Hello {
        token: String,
        abi: String,
        pid: u32,
    },
    Reply {
        corr: Corr,
        result: Result<ReplyBody, String>,
    },
    State(StateUpdateWire),
    /// Sent once per media open when the core first reports media info.
    MediaInfo {
        id: PlayerId,
        info: MediaInfoWire,
    },
    /// A new shared-texture generation for the player: 3 slots x 2 planes.
    /// On macOS the actual IOSurface mach ports travel out-of-band on the
    /// mach channel, tagged (id, generation, slot, plane); the client
    /// assembles the set and answers with `TextureSetAck`.
    TextureSet {
        id: PlayerId,
        generation: u32,
        width: u32,
        height: u32,
    },
    /// The helper finished writing a frame into `slot` (GPU work complete);
    /// the client's next render event may consume it.
    FramePublished {
        id: PlayerId,
        generation: u32,
        slot: u8,
    },
    /// Player-level failure surfaced through the core's error callback.
    PlayerError {
        id: Option<PlayerId>,
        message: String,
    },
    /// One pre-formatted FFmpeg diagnostic line for the matching Unity sink.
    Log {
        sink: LogSink,
        line: String,
    },
}

/// Compile-time protocol/ABI stamp; both sides embed their crate version.
pub const ABI_VERSION: &str = env!("CARGO_PKG_VERSION");
