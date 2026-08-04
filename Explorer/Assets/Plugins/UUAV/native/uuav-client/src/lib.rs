//! The `uuav` plugin Unity loads. Same C ABI as the old in-process plugin
//! (`DllImport("uuav")` in C# is untouched); the decode pipeline now lives
//! in the spawned `uuav-helper` process and this dylib is the middleware:
//! it spawns/monitors the helper, forwards commands over the OS channel
//! (named pipe / socketpair), and serves
//! every per-frame getter from locally cached state snapshots.
//!
//! Helper crashes self-heal here, with no involvement from C# or the ECS
//! layer: every command keeps a per-player *desired state* current, and the
//! recovery worker respawns the helper (with backoff) and rebuilds every
//! player from its mirror — reopening the URL, seeking back to where the
//! clock was, resuming playback. While the helper is down, players read as
//! OPENING and commands are absorbed into desired state.

#![warn(clippy::all, clippy::pedantic, clippy::nursery)]
#![deny(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::todo,
    clippy::dbg_macro
)]
// borrow_as_ptr / cast_possible_wrap: C out-params and c_char buffer fills
// are pervasive at the FFI boundary, same allowances as the core crate
#![allow(
    clippy::missing_errors_doc,
    clippy::missing_safety_doc,
    clippy::must_use_candidate,
    clippy::uninlined_format_args,
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_possible_wrap,
    clippy::borrow_as_ptr,
    clippy::doc_markdown
)]

mod audio_ring;
mod connection;
mod registry;
mod spawn;

#[cfg(target_os = "windows")]
#[path = "sandbox_windows.rs"]
mod sandbox;

#[cfg(target_os = "macos")]
#[path = "platform_macos.rs"]
mod platform;
#[cfg(target_os = "windows")]
#[path = "platform_windows.rs"]
mod platform;

#[cfg(target_os = "macos")]
#[path = "present_macos.rs"]
mod present;
#[cfg(target_os = "windows")]
#[path = "present_windows.rs"]
mod present;

#[cfg(not(any(target_os = "windows", target_os = "macos")))]
compile_error!("uuav supports Windows (D3D11) and macOS (Metal) only");

use arc_swap::{ArcSwap, ArcSwapOption};
use connection::{Connection, EventSinks, Lifecycle, LifecycleCell};
use registry::{Desired, PlayerMirror, Registry};
use spawn::HelperChild;
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int, c_void};
use std::ptr;
use std::sync::atomic::{AtomicI32, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};
use uuav_ipc::protocol::{
    AudioOptionsWire, LogSink, MediaInfoWire, PlayerStateWire, ReplyBody, ToServer,
};

// error strings kept identical to the in-process plugin
const ERR_NO_RUNTIME: &str = "Runtime is not found";
const ERR_NO_PLAYER: &str = "player with specific id not found";
const ERR_HELPER_DEAD: &str = "uuav helper is not running";

const DEFAULT_PLAYBACK_RATE: f64 = 1.0;

/// `assign_master_clock` arrives every frame; forward at most this often.
const MASTER_CLOCK_INTERVAL: Duration = Duration::from_millis(16);

/// Child poll cadence of the recovery worker.
const MONITOR_INTERVAL: Duration = Duration::from_millis(200);

/// Backoff before each respawn attempt of one recovery round; after the
/// last attempt fails the runtime parks in `Failed` until re-armed.
const RESPAWN_DELAYS: [Duration; 3] = [
    Duration::ZERO,
    Duration::from_secs(1),
    Duration::from_secs(3),
];

/// How long the recovery worker waits for restored players to leave
/// OPENING before giving up on their resume seeks.
const RESTORE_SEEK_WINDOW: Duration = Duration::from_secs(10);

static CLIENT: ArcSwapOption<Client> = ArcSwapOption::const_empty();

pub type PlayerId = u64;
type RawCallback = extern "C" fn(*const c_char);

// ---- C ABI data types (frozen; mirrors the in-process plugin) ---------

#[allow(non_camel_case_types)]
#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub enum UUAVState {
    UUAV_CLOSED = 0,
    UUAV_OPENING = 1,
    UUAV_READY = 2,
    UUAV_PLAYING = 3,
    UUAV_PAUSED = 4,
    UUAV_ENDED = 5,
    UUAV_ERROR = 6,
    UUAV_UNKNOWN = 7,
}

#[repr(C)]
#[derive(Default)]
pub struct Status {
    pub players_count: u64,
    pub initialized: bool,
    pub audio_options: AudioOptionsRaw,
    pub device_remove_reason: *const c_char,
}

#[repr(C)]
#[derive(Default, Clone, Copy, PartialEq, Eq)]
pub struct AudioOptionsRaw {
    pub sample_rate: i32,
    pub channels: i32,
}

#[repr(C)]
#[derive(Clone)]
pub struct VideoSize {
    pub width: u32,
    pub height: u32,
}

pub const MEDIA_INFO_NAME_LEN: usize = 32;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct MediaInfo {
    pub duration: f64,
    pub framerate: f64,
    pub video_bitrate: i64,
    pub audio_bitrate: i64,
    pub width: u32,
    pub height: u32,
    pub sample_rate: i32,
    pub channels: i32,
    pub video_codec: [c_char; MEDIA_INFO_NAME_LEN],
    pub pixel_format: [c_char; MEDIA_INFO_NAME_LEN],
    pub audio_codec: [c_char; MEDIA_INFO_NAME_LEN],
    pub sample_format: [c_char; MEDIA_INFO_NAME_LEN],
    pub has_video: u8,
    pub has_audio: u8,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct ControlsState {
    pub rate: f64,
    pub play: u8,
    pub play_pending: u8,
    pub looping: u8,
    pub looping_pending: u8,
    pub rate_pending: u8,
}

#[repr(C)]
pub struct NewPlayerResult {
    pub player_id: PlayerId,
    pub error_message: *const c_char,
}

#[repr(C)]
pub struct ResultFFI {
    pub error_message: *const c_char,
}

impl ResultFFI {
    const fn ok() -> Self {
        Self {
            error_message: ptr::null(),
        }
    }

    fn error(message: impl AsRef<str>) -> Self {
        Self {
            error_message: string_to_c_bytes(message),
        }
    }
}

fn string_to_c_bytes(s: impl AsRef<str>) -> *const c_char {
    CString::new(s.as_ref()).unwrap_or_default().into_raw()
}

// ---- client state ------------------------------------------------------

/// The C# delegates registered at init. The shared lifecycle gates every
/// invocation: after `uuav_deinit` (`ShutDown`) nothing calls into Unity;
/// helper deaths and recovery progress still reach Unity before that.
struct CallbackSinks {
    error: RawCallback,
    warning: RawCallback,
    log: RawCallback,
    lifecycle: Arc<LifecycleCell>,
}

impl CallbackSinks {
    fn invoke(&self, callback: RawCallback, line: &str) {
        if self.lifecycle.get() == Lifecycle::ShutDown {
            return;
        }
        let c = CString::new(line).unwrap_or_default();
        callback(c.as_ptr());
    }
}

impl EventSinks for CallbackSinks {
    fn on_log(&self, sink: LogSink, line: &str) {
        let callback = match sink {
            LogSink::Error => self.error,
            LogSink::Warning => self.warning,
            LogSink::Log => self.log,
        };
        self.invoke(callback, line);
    }

    fn on_player_error(&self, id: Option<u64>, message: &str) {
        let line = id.map_or_else(
            || message.to_owned(),
            |id| format!("[player {id}] {message}"),
        );
        self.invoke(self.error, &line);
    }
}

struct Client {
    /// Current connection generation; swapped whole on helper resurrection.
    conn: ArcSwap<Connection>,
    registry: Arc<Registry>,
    audio_options: ArcSwap<AudioOptionsRaw>,
    sinks: Arc<CallbackSinks>,
    child: Arc<Mutex<HelperChild>>,
    lifecycle: Arc<LifecycleCell>,
    /// Init-time parameters replayed on every helper (re)configure.
    protocol_whitelist: String,
    log_level: AtomicI32,
    adapter: u64,
    /// Wakes the parked recovery worker after attempts capped out.
    rearm: crossbeam_channel::Sender<()>,
    /// Unity's GPU device + what presentation copies run on (Metal: a blit
    /// queue; D3D11: the immediate context, render thread only).
    unity: Arc<platform::UnityDevice>,
}

impl Client {
    fn conn(&self) -> Arc<Connection> {
        self.conn.load_full()
    }

    fn mirror(&self, public: PlayerId) -> Option<Arc<PlayerMirror>> {
        self.registry.get(public)
    }
}

fn validate_audio(options: AudioOptionsRaw) -> Result<AudioOptionsWire, String> {
    if options.sample_rate <= 0 {
        return Err(format!(
            "sample_rate must be positive, got {}",
            options.sample_rate
        ));
    }
    if options.channels <= 0 {
        return Err(format!("channels must be positive, got {}", options.channels));
    }
    Ok(AudioOptionsWire {
        sample_rate: options.sample_rate,
        channels: options.channels,
    })
}

const fn map_state(state: PlayerStateWire) -> UUAVState {
    match state {
        PlayerStateWire::Closed => UUAVState::UUAV_CLOSED,
        PlayerStateWire::Opening => UUAVState::UUAV_OPENING,
        PlayerStateWire::Ready => UUAVState::UUAV_READY,
        PlayerStateWire::Playing => UUAVState::UUAV_PLAYING,
        PlayerStateWire::Paused => UUAVState::UUAV_PAUSED,
        PlayerStateWire::Ended => UUAVState::UUAV_ENDED,
        PlayerStateWire::Error => UUAVState::UUAV_ERROR,
        PlayerStateWire::Unknown => UUAVState::UUAV_UNKNOWN,
    }
}

fn name_to_field(name: &str) -> [c_char; MEDIA_INFO_NAME_LEN] {
    let mut field = [0 as c_char; MEDIA_INFO_NAME_LEN];
    for (dst, src) in field
        .iter_mut()
        .zip(name.bytes().take(MEDIA_INFO_NAME_LEN.saturating_sub(1)))
    {
        *dst = src as c_char;
    }
    field
}

fn media_info_to_c(info: &MediaInfoWire) -> MediaInfo {
    MediaInfo {
        duration: info.duration,
        framerate: info.framerate,
        video_bitrate: info.video_bitrate,
        audio_bitrate: info.audio_bitrate,
        width: info.width,
        height: info.height,
        sample_rate: info.sample_rate,
        channels: info.channels,
        video_codec: name_to_field(&info.video_codec),
        pixel_format: name_to_field(&info.pixel_format),
        audio_codec: name_to_field(&info.audio_codec),
        sample_format: name_to_field(&info.sample_format),
        has_video: u8::from(info.has_video),
        has_audio: u8::from(info.has_audio),
    }
}

// ---- helper session (shared by init and the recovery worker) -----------

/// One helper generation: spawn, handshake, configure. On success the
/// helper is fully configured and the connection's IO thread is pumping.
fn start_session(
    lifecycle: &Arc<LifecycleCell>,
    registry: &Arc<Registry>,
    sinks: &Arc<CallbackSinks>,
    audio: AudioOptionsWire,
    protocol_whitelist: &str,
    log_level: i32,
    adapter: u64,
) -> Result<(Connection, HelperChild), String> {
    let token = uuid::Uuid::new_v4().simple().to_string();

    // the surface-port channel must exist before the helper looks it up
    #[cfg(target_os = "macos")]
    let mut mach_receiver = {
        let service = uuav_ipc::mach_channel::service_name(&token);
        uuav_ipc::mach_channel::Receiver::register(&service)
            .map_err(|e| format!("mach channel: {e}"))?
    };

    #[cfg(target_os = "macos")]
    let allow_file_read = protocol_whitelist
        .split(',')
        .any(|p| p.trim().eq_ignore_ascii_case("file"));

    let mut child: Option<HelperChild> = None;
    let conn = Connection::establish(
        &token,
        |handoff| {
            let spawned = spawn::spawn_helper(
                handoff,
                &token,
                #[cfg(target_os = "macos")]
                allow_file_read,
            )?;
            // the registry pulls announced texture handles out of the
            // helper's process; arm it before any announcement can arrive
            // (the helper decodes nothing until Configure)
            #[cfg(target_os = "windows")]
            registry.set_helper_process(spawned.dup_source()?);
            child = Some(spawned);
            Ok(())
        },
        Arc::clone(lifecycle),
        Arc::clone(registry),
        Arc::clone(sinks) as Arc<dyn EventSinks>,
    );

    let conn = match conn {
        Ok(conn) => conn,
        Err(e) => {
            if let Some(mut child) = child {
                child.kill();
                child.wait();
            }
            return Err(format!("failed to start uuav helper: {e}"));
        }
    };
    let Some(child) = child else {
        return Err("uuav helper was not spawned".to_owned());
    };

    // the helper creates its device and runs the core init on Configure
    let configured = conn.request(|corr| ToServer::Configure {
        corr,
        audio,
        protocol_whitelist: protocol_whitelist.to_owned(),
        log_level,
        adapter,
    });
    if let Err(e) = configured {
        conn.retire();
        let mut child = child;
        child.kill();
        child.wait();
        return Err(e);
    }

    #[cfg(target_os = "macos")]
    {
        // messages queued while the helper configured are still verified:
        // the receive thread starts only now, armed
        mach_receiver.expect_sender(child.id());
        spawn_mach_receiver(
            mach_receiver,
            Arc::clone(registry),
            Arc::clone(lifecycle),
            conn.alive_flag(),
        );
    }

    Ok((conn, child))
}

/// Owns the mach receive right on a dedicated thread: every transferred
/// surface lands in the matching player mirror. Exits (destroying the
/// right) when its connection generation retires or the runtime shuts down.
#[cfg(target_os = "macos")]
fn spawn_mach_receiver(
    receiver: uuav_ipc::mach_channel::Receiver,
    registry: Arc<Registry>,
    lifecycle: Arc<LifecycleCell>,
    alive: Arc<std::sync::atomic::AtomicBool>,
) {
    _ = std::thread::Builder::new()
        .name("uuav-mach".into())
        .spawn(move || {
            while alive.load(Ordering::Acquire) && lifecycle.get() != Lifecycle::ShutDown {
                match receiver.recv(200) {
                    Ok(Some((tag, port))) => {
                        let surface =
                            objc2_io_surface::IOSurfaceRef::lookup_from_mach_port(port);
                        // the lookup retained the surface; the transferred
                        // send right itself is no longer needed
                        unsafe {
                            mach2::mach_port::mach_port_deallocate(
                                mach2::traps::mach_task_self(),
                                port,
                            );
                        }
                        if let Some(surface) = surface {
                            registry::apply_surface(&registry, &tag, surface);
                        }
                    }
                    Ok(None) => {}
                    Err(_) => return,
                }
            }
        });
}

// ---- recovery ------------------------------------------------------------

/// Watches the helper process; on unexpected death, drives the automatic
/// resurrection: retire the connection, freeze the mirrors, respawn with
/// backoff, rebuild every player from its desired state. Parks in `Failed`
/// after the attempts cap; an open/play re-arms it through `Client::rearm`.
fn spawn_recovery_worker(client: &Arc<Client>, rearm: crossbeam_channel::Receiver<()>) {
    let weak = Arc::downgrade(client);
    _ = std::thread::Builder::new()
        .name("uuav-recovery".into())
        .spawn(move || {
            loop {
                std::thread::sleep(MONITOR_INTERVAL);
                let Some(client) = weak.upgrade() else {
                    return;
                };
                match client.lifecycle.get() {
                    Lifecycle::ShutDown => return,
                    // transient while this thread itself recovers; nothing
                    // to do on a stray observation
                    Lifecycle::Recovering => continue,
                    Lifecycle::Failed => {
                        // parked: wait for a re-arm (an open/play)
                        match rearm.recv_timeout(MONITOR_INTERVAL) {
                            Ok(()) => {
                                client.lifecycle.transition(Lifecycle::Recovering);
                                recover(&client);
                                while rearm.try_recv().is_ok() { /* drain stale re-arms */ }
                            }
                            Err(crossbeam_channel::RecvTimeoutError::Timeout) => {}
                            Err(crossbeam_channel::RecvTimeoutError::Disconnected) => return,
                        }
                        continue;
                    }
                    Lifecycle::Running => {}
                }

                let exited = client
                    .child
                    .lock()
                    .ok()
                    .and_then(|mut child| child.try_wait().ok().flatten());
                if let Some(status) = exited {
                    client.lifecycle.transition(Lifecycle::Recovering);
                    client.conn().retire();
                    // suppressed automatically after a planned shutdown:
                    // the sinks gate on the same lifecycle
                    client.sinks.on_player_error(
                        None,
                        &format!("uuav helper terminated ({status}); recovering"),
                    );
                    freeze_mirrors(&client);
                    while rearm.try_recv().is_ok() { /* drain stale re-arms */ }
                    recover(&client);
                }
            }
        });
}

/// Captures where every player was and severs the helper bindings; getters
/// serve OPENING + the frozen time until the new helper reports in.
fn freeze_mirrors(client: &Client) {
    let audio = **client.audio_options.load();
    for (_, mirror) in client.registry.snapshot() {
        let now = mirror.state.load().as_ref().and_then(|c| c.media_time_now());
        if let Some(time) = now
            && let Ok(mut desired) = mirror.desired.lock()
        {
            desired.resume_time = Some(time);
        }
        mirror.awaiting_snapshot.store(true, Ordering::Release);
        if let Ok(mut video) = mirror.video.lock() {
            video.reset_for_recovery();
        }
        mirror.audio.reset(audio.sample_rate, audio.channels);
    }
    client.registry.unbind_all();
}

/// One recovery round: respawn attempts with backoff, then either a running
/// restored session or the parked `Failed` state.
fn recover(client: &Arc<Client>) {
    for (attempt, delay) in RESPAWN_DELAYS.iter().enumerate() {
        if !sleep_unless_shutdown(client, *delay) {
            return;
        }
        let raw = **client.audio_options.load();
        let audio = AudioOptionsWire {
            sample_rate: raw.sample_rate,
            channels: raw.channels,
        };
        let session = start_session(
            &client.lifecycle,
            &client.registry,
            &client.sinks,
            audio,
            &client.protocol_whitelist,
            client.log_level.load(Ordering::Acquire),
            client.adapter,
        );
        match session {
            Ok((conn, child)) => {
                if client.lifecycle.get() == Lifecycle::ShutDown {
                    conn.retire();
                    let mut child = child;
                    child.kill();
                    child.wait();
                    return;
                }
                client.conn.store(Arc::new(conn));
                if let Ok(mut slot) = client.child.lock() {
                    *slot = child;
                }
                client.lifecycle.transition(Lifecycle::Running);
                restore_players(client);
                client
                    .sinks
                    .on_log(LogSink::Warning, "uuav helper restarted; players restored");
                return;
            }
            Err(e) => client.sinks.on_log(
                LogSink::Warning,
                &format!("uuav helper restart attempt {} failed: {e}", attempt.saturating_add(1)),
            ),
        }
    }
    client.lifecycle.transition(Lifecycle::Failed);
    client.sinks.on_player_error(
        None,
        "uuav helper could not be restarted; players halt until the next open or play",
    );
}

/// `false` when the runtime shut down mid-sleep.
fn sleep_unless_shutdown(client: &Client, delay: Duration) -> bool {
    let started = Instant::now();
    while started.elapsed() < delay {
        if client.lifecycle.get() == Lifecycle::ShutDown {
            return false;
        }
        std::thread::sleep(Duration::from_millis(50));
    }
    client.lifecycle.get() != Lifecycle::ShutDown
}

/// Rebuilds every mirrored player on the fresh helper from desired state:
/// bind, reopen, resume playback, then seek back once past OPENING.
fn restore_players(client: &Arc<Client>) {
    let conn = client.conn();
    let players = client.registry.snapshot();

    for (public, mirror) in &players {
        if client.lifecycle.get() != Lifecycle::Running {
            return;
        }
        if let Err(e) = bind_helper_player(client, &conn, *public, mirror) {
            client
                .sinks
                .on_player_error(Some(*public), &format!("restore failed: {e}"));
            continue;
        }
        let Some(helper) = mirror.helper_id() else {
            continue;
        };
        let desired = match mirror.desired.lock() {
            Ok(desired) => desired.clone(),
            Err(_) => continue,
        };
        if let Some(url) = desired.url {
            _ = conn.send(ToServer::OpenMedia { id: helper, url });
            // play/looping/rate queue as pending controls while the media
            // opens; only seek has no pending slot (handled below)
            if desired.want_playing {
                _ = conn.send(ToServer::Play { id: helper });
            }
        }
    }

    // resume seeks once each player is past OPENING
    let started = Instant::now();
    let mut waiting: Vec<&(PlayerId, Arc<PlayerMirror>)> = players
        .iter()
        .filter(|(_, mirror)| {
            mirror
                .desired
                .lock()
                .ok()
                .is_some_and(|desired| desired.resume_time.is_some() && desired.url.is_some())
        })
        .collect();
    while !waiting.is_empty()
        && started.elapsed() < RESTORE_SEEK_WINDOW
        && client.lifecycle.get() == Lifecycle::Running
    {
        std::thread::sleep(Duration::from_millis(100));
        waiting.retain(|(_, mirror)| {
            if mirror.awaiting_snapshot.load(Ordering::Acquire) {
                return true;
            }
            let Some(cached) = mirror.state.load_full() else {
                return true;
            };
            match cached.update.state {
                PlayerStateWire::Ready | PlayerStateWire::Playing | PlayerStateWire::Paused => {
                    let time = mirror
                        .desired
                        .lock()
                        .ok()
                        .and_then(|mut desired| desired.resume_time.take());
                    if let (Some(time), Some(helper)) = (time, mirror.helper_id()) {
                        _ = conn.send(ToServer::Seek { id: helper, time });
                    }
                    false
                }
                // the stream itself failed or ended; nothing to resume into
                PlayerStateWire::Error | PlayerStateWire::Closed | PlayerStateWire::Ended => false,
                PlayerStateWire::Opening | PlayerStateWire::Unknown => true,
            }
        });
    }
}

/// Creates the helper-side player behind a public mirror and primes it with
/// the desired looping/rate (both queue as pending controls even while a
/// later open is still in flight). Serialized per mirror via `binding`.
fn bind_helper_player(
    client: &Client,
    conn: &Connection,
    public: PlayerId,
    mirror: &PlayerMirror,
) -> Result<PlayerId, String> {
    let _guard = mirror
        .binding
        .lock()
        .map_err(|_| "player binding lock is poisoned".to_owned())?;
    if let Some(existing) = mirror.helper_id() {
        return Ok(existing);
    }

    let helper = match conn.request(|corr| ToServer::PlayerNew { corr })? {
        ReplyBody::PlayerId(id) => id,
        ReplyBody::Unit => return Err("helper returned no player id".to_owned()),
    };
    if !client.registry.contains(public) {
        // freed while the request was in flight; don't leak the helper player
        _ = conn.send(ToServer::PlayerFree { id: helper });
        return Err(ERR_NO_PLAYER.to_owned());
    }
    client.registry.bind_helper(public, mirror, helper);

    let (looping, rate) = mirror
        .desired
        .lock()
        .map_or((false, DEFAULT_PLAYBACK_RATE), |desired| {
            (desired.looping, desired.rate)
        });
    if looping {
        _ = conn.send(ToServer::SetLooping {
            id: helper,
            looping: true,
        });
    }
    if rate.is_finite() && rate > 0.0 {
        _ = conn.send(ToServer::SetRate { id: helper, rate });
    }
    Ok(helper)
}

// ---- runtime lifecycle -------------------------------------------------

/// releases an error message
/// must be called exactly once per non-null message
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_string_free(string: *mut c_char) {
    if string.is_null() {
        return;
    }
    drop(unsafe { CString::from_raw(string) });
}

#[unsafe(no_mangle)]
pub const extern "C" fn uuav_abi_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), '\0').as_ptr().cast()
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_init(
    texture: *const c_void,
    audio_options: AudioOptionsRaw,
    error_callback: Option<RawCallback>,
    warning_callback: Option<RawCallback>,
    log_callback: Option<RawCallback>,
    protocol_whitelist: *const c_char,
    log_level: c_int,
) -> ResultFFI {
    if CLIENT.load().is_some() {
        return ResultFFI::error("Already initialized");
    }

    let Some(error_callback) = error_callback else {
        return ResultFFI::error("Error callback is null");
    };
    let Some(warning_callback) = warning_callback else {
        return ResultFFI::error("Warning callback is null");
    };
    let Some(log_callback) = log_callback else {
        return ResultFFI::error("Log callback is null");
    };
    if texture.is_null() {
        return ResultFFI::error("Texture to capture the HwDevice from is not provided");
    }

    let unity = match unsafe { platform::capture_probe(texture) } {
        Ok(unity) => Arc::new(unity),
        Err(e) => return ResultFFI::error(e.to_string()),
    };
    #[cfg(target_os = "macos")]
    let adapter = unity.registry_id;
    #[cfg(target_os = "windows")]
    let adapter = unity.adapter_luid;

    let audio = match validate_audio(audio_options) {
        Ok(audio) => audio,
        Err(e) => return ResultFFI::error(e),
    };

    if protocol_whitelist.is_null() {
        return ResultFFI::error("protocol whitelist is null");
    }
    let whitelist = match unsafe { CStr::from_ptr(protocol_whitelist) }.to_str() {
        Ok(w) => w.to_owned(),
        Err(_) => return ResultFFI::error("protocol whitelist is not valid UTF-8"),
    };

    let lifecycle = Arc::new(LifecycleCell::new());
    let sinks = Arc::new(CallbackSinks {
        error: error_callback,
        warning: warning_callback,
        log: log_callback,
        lifecycle: Arc::clone(&lifecycle),
    });
    let registry: Arc<Registry> = Arc::new(Registry::new());

    let (conn, child) = match start_session(
        &lifecycle,
        &registry,
        &sinks,
        audio,
        &whitelist,
        log_level,
        adapter,
    ) {
        Ok(session) => session,
        Err(e) => return ResultFFI::error(e),
    };

    let (rearm_tx, rearm_rx) = crossbeam_channel::unbounded();
    let client = Arc::new(Client {
        conn: ArcSwap::from(Arc::new(conn)),
        registry,
        audio_options: ArcSwap::new(Arc::new(audio_options)),
        sinks,
        child: Arc::new(Mutex::new(child)),
        lifecycle,
        protocol_whitelist: whitelist,
        log_level: AtomicI32::new(log_level),
        adapter,
        rearm: rearm_tx,
        unity,
    });
    spawn_recovery_worker(&client, rearm_rx);
    CLIENT.store(Some(client));

    ResultFFI::ok()
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_deinit() {
    let Some(client) = CLIENT.swap(None) else {
        return;
    };

    // planned shutdown: the escalated lifecycle silences the sinks (no late
    // callbacks into Unity, no "terminated" report from the worker) and
    // tells the IO thread to flush the Shutdown frame and exit
    client.lifecycle.transition(Lifecycle::ShutDown);
    client.conn().shutdown();

    let waiting_since = Instant::now();
    loop {
        let exited = client
            .child
            .lock()
            .ok()
            .and_then(|mut child| child.try_wait().ok().flatten());
        if exited.is_some() {
            return;
        }
        if waiting_since.elapsed() >= Duration::from_secs(1) {
            if let Ok(mut child) = client.child.lock() {
                child.kill();
                child.wait();
            }
            return;
        }
        std::thread::sleep(Duration::from_millis(20));
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_set_log_level(level: c_int) {
    if let Some(client) = CLIENT.load().as_ref() {
        client.log_level.store(level, Ordering::Release);
        if client.lifecycle.get() == Lifecycle::Running {
            _ = client.conn().send(ToServer::SetLogLevel { level });
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_update_audio_out(options: AudioOptionsRaw) -> ResultFFI {
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return ResultFFI::error("Not initialized");
    };

    let audio = match validate_audio(options) {
        Ok(audio) => audio,
        Err(e) => return ResultFFI::error(e),
    };

    let apply_locally = |client: &Client| {
        client.audio_options.store(Arc::new(options));
        // queued samples are in the old format; drop and re-prime
        for (_, mirror) in client.registry.snapshot() {
            mirror.audio.reset(options.sample_rate, options.channels);
        }
    };

    match client.lifecycle.get() {
        Lifecycle::Running => {
            match client
                .conn()
                .request(|corr| ToServer::UpdateAudioOut { corr, audio })
            {
                Ok(_) => {
                    apply_locally(client);
                    ResultFFI::ok()
                }
                Err(_) if client.lifecycle.get() != Lifecycle::Running => {
                    // helper died mid-call; the re-Configure carries the
                    // stored options
                    apply_locally(client);
                    ResultFFI::ok()
                }
                Err(e) => ResultFFI::error(e),
            }
        }
        // absorbed: the recovery worker re-Configures with the stored options
        Lifecycle::Recovering => {
            apply_locally(client);
            ResultFFI::ok()
        }
        Lifecycle::Failed | Lifecycle::ShutDown => ResultFFI::error(ERR_HELPER_DEAD),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_status() -> Status {
    CLIENT.load().as_ref().map_or_else(Status::default, |client| Status {
        players_count: client.registry.len() as u64,
        initialized: true,
        audio_options: **client.audio_options.load(),
        // the helper's device health surfaces through player errors; Unity's
        // own device is not the decode device anymore
        device_remove_reason: ptr::null(),
    })
}

/// Diagnostics-only lifecycle probe: -1 when the runtime is not
/// initialized, else the client [`Lifecycle`] as its discriminant
/// Refer to Lifecycle definition
#[unsafe(no_mangle)]
pub extern "C" fn uuav_lifecycle() -> i32 {
    CLIENT
        .load()
        .as_ref()
        .map_or(-1, |client| client.lifecycle.get() as i32)
}

// ---- player lifecycle --------------------------------------------------

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_new() -> NewPlayerResult {
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return NewPlayerResult {
            player_id: 0,
            error_message: string_to_c_bytes(ERR_NO_RUNTIME),
        };
    };

    let audio = **client.audio_options.load();
    let (public, mirror) = client.registry.create(audio.sample_rate, audio.channels);
    let ok = NewPlayerResult {
        player_id: public,
        error_message: ptr::null(),
    };

    match client.lifecycle.get() {
        Lifecycle::Running => {
            let conn = client.conn();
            match bind_helper_player(client, &conn, public, &mirror) {
                Ok(_) => ok,
                // helper died mid-call: keep the mirror, recovery rebinds it
                Err(_) if client.lifecycle.get() != Lifecycle::Running => ok,
                Err(e) => {
                    client.registry.remove(public);
                    NewPlayerResult {
                        player_id: 0,
                        error_message: string_to_c_bytes(e),
                    }
                }
            }
        }
        // bound by the recovery worker (or lazily on the first command)
        Lifecycle::Recovering => ok,
        Lifecycle::Failed | Lifecycle::ShutDown => {
            client.registry.remove(public);
            NewPlayerResult {
                player_id: 0,
                error_message: string_to_c_bytes(ERR_HELPER_DEAD),
            }
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_free(player_id: PlayerId) {
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return;
    };
    if let Some(mirror) = client.registry.remove(player_id)
        && let Some(helper) = mirror.helper_id()
        && client.lifecycle.get() == Lifecycle::Running
    {
        _ = client.conn().send(ToServer::PlayerFree { id: helper });
    }
}

/// How a command behaves while the runtime is parked in `Failed`.
#[derive(Clone, Copy)]
enum DownPolicy {
    /// Absorb into desired state and report success (per-frame noise like
    /// the master clock; erroring every frame would spam the console).
    Absorb,
    /// Absorb, and wake the parked recovery worker — reserved for commands
    /// that express clear user intent to play something (open/play).
    Rearm,
    /// Report the helper-dead error.
    Reject,
}

/// Shared prologue of every per-player command. The desired state is
/// updated first — that is what makes the helper resurrectable — then the
/// message is forwarded while the helper runs, or absorbed while it is
/// being resurrected (the recovery worker replays desired state).
fn with_player(
    public: PlayerId,
    down_policy: DownPolicy,
    desire: impl FnOnce(&mut Desired),
    build: impl FnOnce(PlayerId) -> ToServer,
) -> Result<(), String> {
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return Err(ERR_NO_RUNTIME.to_owned());
    };
    let Some(mirror) = client.mirror(public) else {
        return Err(ERR_NO_PLAYER.to_owned());
    };

    if let Ok(mut desired) = mirror.desired.lock() {
        desire(&mut desired);
    }

    match client.lifecycle.get() {
        Lifecycle::Running => {
            let conn = client.conn();
            let outcome = bind_helper_player(client, &conn, public, &mirror)
                .and_then(|helper| conn.send(build(helper)).map_err(|e| e.to_string()));
            match outcome {
                Ok(()) => Ok(()),
                // helper died mid-call: the command is already absorbed in
                // desired state and the recovery worker replays it
                Err(_) if client.lifecycle.get() != Lifecycle::Running => Ok(()),
                Err(e) => Err(e),
            }
        }
        Lifecycle::Recovering => Ok(()),
        Lifecycle::Failed => match down_policy {
            DownPolicy::Absorb => Ok(()),
            DownPolicy::Rearm => {
                _ = client.rearm.send(());
                Ok(())
            }
            DownPolicy::Reject => Err(ERR_HELPER_DEAD.to_owned()),
        },
        Lifecycle::ShutDown => Err(ERR_HELPER_DEAD.to_owned()),
    }
}

fn command_result(outcome: Result<(), String>) -> ResultFFI {
    match outcome {
        Ok(()) => ResultFFI::ok(),
        Err(e) => ResultFFI::error(e),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_play(player_id: PlayerId) -> ResultFFI {
    command_result(with_player(
        player_id,
        DownPolicy::Rearm,
        |desired| desired.want_playing = true,
        |id| ToServer::Play { id },
    ))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_pause(player_id: PlayerId) -> ResultFFI {
    command_result(with_player(
        player_id,
        DownPolicy::Reject,
        |desired| desired.want_playing = false,
        |id| ToServer::Pause { id },
    ))
}

// async! returns immediately
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_open_media_async(
    player_id: PlayerId,
    url: *const c_char,
) -> ResultFFI {
    if url.is_null() {
        return ResultFFI::error("url is null");
    }
    let url = match unsafe { CStr::from_ptr(url) }.to_str() {
        Ok(url) => url.to_owned(),
        Err(_) => return ResultFFI::error("url is not valid UTF-8"),
    };

    // new media, new info: drop the cached one until the helper re-announces
    if let Some(client) = CLIENT.load().as_ref()
        && let Some(mirror) = client.mirror(player_id)
    {
        mirror.media_info.store(None);
    }

    let desired_url = url.clone();
    command_result(with_player(
        player_id,
        DownPolicy::Rearm,
        move |desired| {
            desired.url = Some(desired_url);
            desired.resume_time = None;
        },
        move |id| ToServer::OpenMedia { id, url },
    ))
}

// back to CLOSED, player reusable
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_close_media(player_id: PlayerId) -> ResultFFI {
    command_result(with_player(
        player_id,
        DownPolicy::Reject,
        |desired| {
            desired.url = None;
            desired.want_playing = false;
            desired.resume_time = None;
        },
        |id| ToServer::CloseMedia { id },
    ))
}

// ---- state getters (served from the cached snapshots) ------------------

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_state(player_id: PlayerId) -> UUAVState {
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return UUAVState::UUAV_UNKNOWN;
    };
    let Some(mirror) = client.mirror(player_id) else {
        return UUAVState::UUAV_UNKNOWN;
    };

    let has_media = mirror
        .desired
        .lock()
        .ok()
        .is_some_and(|desired| desired.url.is_some());

    match client.lifecycle.get() {
        // resurrection in flight: a buffering-like blip, per owner decision
        Lifecycle::Recovering => {
            if has_media {
                UUAVState::UUAV_OPENING
            } else {
                UUAVState::UUAV_CLOSED
            }
        }
        Lifecycle::Failed | Lifecycle::ShutDown => UUAVState::UUAV_ERROR,
        Lifecycle::Running => {
            if mirror.awaiting_snapshot.load(Ordering::Acquire)
                || (mirror.helper_id().is_none() && has_media)
            {
                // restored/created player the fresh helper has not
                // reported on yet
                UUAVState::UUAV_OPENING
            } else {
                // a fresh player has no snapshot yet; the core starts
                // players CLOSED
                mirror
                    .state
                    .load()
                    .as_ref()
                    .map_or(UUAVState::UUAV_CLOSED, |cached| map_state(cached.update.state))
            }
        }
    }
}

/// Shared prologue of the out-param getters.
fn with_mirror<T>(
    player_id: PlayerId,
    read: impl FnOnce(&Client, &PlayerMirror) -> Result<T, String>,
) -> Result<T, String> {
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return Err(ERR_NO_RUNTIME.to_owned());
    };
    let Some(mirror) = client.mirror(player_id) else {
        return Err(ERR_NO_PLAYER.to_owned());
    };
    read(client, &mirror)
}

fn write_out<T>(out: *mut T, outcome: Result<T, String>) -> ResultFFI {
    if out.is_null() {
        return ResultFFI::error("out pointer is null");
    }
    match outcome {
        Ok(value) => {
            unsafe { out.write(value) };
            ResultFFI::ok()
        }
        Err(e) => ResultFFI::error(e),
    }
}

// may be unavailable for realtime streams
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_duration(
    player_id: PlayerId,
    out_duration: *mut f64,
) -> ResultFFI {
    write_out(
        out_duration,
        with_mirror(player_id, |_, mirror| {
            mirror
                .state
                .load()
                .as_ref()
                .and_then(|cached| cached.update.duration)
                .ok_or_else(|| "duration is not available".to_owned())
        }),
    )
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_current_time(
    player_id: PlayerId,
    out_time: *mut f64,
) -> ResultFFI {
    write_out(
        out_time,
        with_mirror(player_id, |client, mirror| {
            // while the helper is down (or freshly restored) the clock
            // freezes at the resume point instead of extrapolating on
            if client.lifecycle.get() != Lifecycle::Running
                || mirror.awaiting_snapshot.load(Ordering::Acquire)
            {
                return mirror
                    .frozen_time()
                    .ok_or_else(|| "current time is not available".to_owned());
            }
            mirror
                .state
                .load()
                .as_ref()
                .and_then(|cached| cached.media_time_now())
                .ok_or_else(|| "current time is not available".to_owned())
        }),
    )
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_current_controls_state(
    player_id: PlayerId,
    out_state: *mut ControlsState,
) -> ResultFFI {
    write_out(
        out_state,
        with_mirror(player_id, |_, mirror| {
            Ok(mirror.state.load().as_ref().map_or(
                ControlsState {
                    rate: DEFAULT_PLAYBACK_RATE,
                    play: 0,
                    play_pending: 0,
                    looping: 0,
                    looping_pending: 0,
                    rate_pending: 0,
                },
                |cached| ControlsState {
                    rate: cached.update.controls.rate,
                    play: u8::from(cached.update.controls.play),
                    play_pending: u8::from(cached.update.controls.play_pending),
                    looping: u8::from(cached.update.controls.looping),
                    looping_pending: u8::from(cached.update.controls.looping_pending),
                    rate_pending: u8::from(cached.update.controls.rate_pending),
                },
            ))
        }),
    )
}

// the player slaves its playback to the externally provided master clock
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_assign_master_clock(
    player_id: PlayerId,
    current_time: f64,
) -> ResultFFI {
    // per-frame call: throttle the forwarding, the wire value stays fresh
    // enough for the core's drift tolerance
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return ResultFFI::error(ERR_NO_RUNTIME);
    };
    let Some(mirror) = client.mirror(player_id) else {
        return ResultFFI::error(ERR_NO_PLAYER);
    };

    if let Ok(mut last) = mirror.last_master_clock.lock() {
        if last.is_some_and(|at| at.elapsed() < MASTER_CLOCK_INTERVAL) {
            return ResultFFI::ok();
        }
        *last = Some(Instant::now());
    }

    command_result(with_player(
        player_id,
        DownPolicy::Absorb,
        |_| {},
        |id| ToServer::AssignMasterClock {
            id,
            time: current_time,
        },
    ))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_video_size(
    player_id: PlayerId,
    out_size: *mut VideoSize,
) -> ResultFFI {
    write_out(
        out_size,
        with_mirror(player_id, |_, mirror| {
            mirror
                .state
                .load()
                .as_ref()
                .and_then(|cached| cached.update.video_size)
                .map(|(width, height)| VideoSize { width, height })
                .ok_or_else(|| "video size is not available yet".to_owned())
        }),
    )
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_media_info(
    player_id: PlayerId,
    out_info: *mut MediaInfo,
) -> ResultFFI {
    write_out(
        out_info,
        with_mirror(player_id, |_, mirror| {
            mirror
                .media_info
                .load()
                .as_ref()
                .map(|info| media_info_to_c(info))
                .ok_or_else(|| "media info is not available yet".to_owned())
        }),
    )
}

// ---- transport (commands: forwarded, helper's core obeys) --------------

// async; coalesces repeated calls
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_seek_async(player_id: PlayerId, time: f64) -> ResultFFI {
    command_result(with_player(
        player_id,
        DownPolicy::Reject,
        |desired| desired.resume_time = Some(time),
        |id| ToServer::Seek { id, time },
    ))
}

// persists across url switches
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_set_looping(player_id: PlayerId, looping: u8) -> ResultFFI {
    command_result(with_player(
        player_id,
        DownPolicy::Reject,
        |desired| desired.looping = looping != 0,
        |id| ToServer::SetLooping {
            id,
            looping: looping != 0,
        },
    ))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_looping(player_id: PlayerId) -> u8 {
    with_mirror(player_id, |_, mirror| {
        Ok(mirror
            .state
            .load()
            .as_ref()
            .is_some_and(|cached| cached.update.looping))
    })
    .map_or(0, u8::from)
}

// Expect: realtime streams (no duration) keep playing at 1x
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_set_rate(player_id: PlayerId, rate: f64) -> ResultFFI {
    if !(rate.is_finite() && rate > 0.0) {
        return ResultFFI::error(format!(
            "playback rate must be finite and positive, got {rate}"
        ));
    }
    command_result(with_player(
        player_id,
        DownPolicy::Reject,
        |desired| desired.rate = rate,
        |id| ToServer::SetRate { id, rate },
    ))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_rate(player_id: PlayerId) -> f64 {
    with_mirror(player_id, |_, mirror| {
        Ok(mirror
            .state
            .load()
            .as_ref()
            .map_or(DEFAULT_PLAYBACK_RATE, |cached| cached.update.rate))
    })
    .unwrap_or(DEFAULT_PLAYBACK_RATE)
}

// ---- video -------------------------------------------------------------

// Unity's UnityRenderingEvent signature
pub type UUAVRenderEvent = extern "C" fn(event_id: i32);

// [render] entry point issued via GL.IssuePluginEvent; copies the helper's
// latest published shared slot into the presentation texture(s) (and
// completes any pending texture-set wrap, acking it to the helper)
extern "C" fn uuav_render_event(event_id: i32) {
    let Ok(player_id) = PlayerId::try_from(event_id) else {
        return;
    };
    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return;
    };
    let Some(mirror) = client.mirror(player_id) else {
        return;
    };

    let ack = {
        let Ok(mut video) = mirror.video.lock() else {
            return;
        };
        match video.present(&client.unity) {
            Ok(ack) => ack,
            Err(e) => {
                client
                    .sinks
                    .on_player_error(Some(player_id), &format!("present failed: {e}"));
                return;
            }
        }
    };

    if let (Some(generation), Some(helper)) = (ack, mirror.helper_id()) {
        _ = client.conn().send(ToServer::TextureSetAck {
            id: helper,
            generation,
        });
    }
}

// pass to GL.IssuePluginEvent
#[unsafe(no_mangle)]
pub const extern "C" fn uuav_get_render_callback() -> UUAVRenderEvent {
    uuav_render_event
}

// Valid from the first presented frame; the pointer is a client-owned
// presentation texture on Unity's device (Metal: one per plane; D3D11: one
// NV12 texture, `plane` unused as in-process), stable across frames and
// replaced (with a one-poll retire grace) on resolution change.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_video_texture(
    player_id: PlayerId,
    plane: i32,
    out_texture: *mut *const c_void,
) -> ResultFFI {
    write_out(
        out_texture,
        with_mirror(player_id, |_, mirror| {
            mirror
                .video
                .lock()
                .map_err(|_| "video state is poisoned".to_owned())?
                .texture_ptr(plane)
        }),
    )
}

// ---- audio -------------------------------------------------------------

// [audio] fills interleaved FLT from the jitter ring; pads silence on
// underrun, never blocks; returns frames actually copied
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_read_audio(
    player_id: PlayerId,
    dst: *mut f32,
    nb_frames: i32,
) -> i32 {
    if dst.is_null() || nb_frames <= 0 {
        return 0;
    }

    let state = CLIENT.load();
    let Some(client) = state.as_ref() else {
        return 0;
    };
    let Some(mirror) = client.mirror(player_id) else {
        return 0;
    };

    let channels = client.audio_options.load().channels.max(1) as usize;
    let samples = (nb_frames as usize).saturating_mul(channels);
    let dst = unsafe { std::slice::from_raw_parts_mut(dst, samples) };
    mirror.audio.read(dst).checked_div(channels).unwrap_or(0) as i32
}
