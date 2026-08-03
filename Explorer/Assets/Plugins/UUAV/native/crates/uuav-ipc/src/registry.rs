
use std::collections::VecDeque;
use std::ffi::{CStr, CString, OsStr, c_void};
use std::os::unix::ffi::OsStrExt as _;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering};
use std::sync::{Arc, Mutex, MutexGuard, PoisonError};
use std::thread::{self, JoinHandle};
use std::time::Duration;

use dashmap::DashMap;

use crate::audio::{self as jitter, JitterRing, RingState};
use crate::controls::HostControls;
use crate::fetch::{FetchResponder, SegmentSource};
use crate::host::present::{Presenter, Selection};
use crate::host::{FrameInfo, ImportedSurface, SurfaceTable, metal::MetalContext};
use crate::protocol::{
    LogLevel, PlaybackState, SURFACE_SLOT_COUNT, SharedSegment, TransportRead, uptime_nanos,
};
use crate::session::{Phase, Session};

pub use uuav_abi::{PlayerId, RawLogCallback};

#[derive(Clone, Copy)]
pub struct LogSinks {
    pub error: RawLogCallback,
    pub warning: RawLogCallback,
    pub log: RawLogCallback,
}

impl LogSinks {
    fn dispatch(self, entry: &crate::protocol::LogEntry) {
        let sink = match entry.level {
            LogLevel::Error => self.error,
            LogLevel::Warning => self.warning,
            LogLevel::Info => self.log,
        };
        Self::emit(sink, entry.text.as_str());
    }

    fn emit(sink: RawLogCallback, text: &str) {
        let line = CString::new(text).unwrap_or_default();
        sink(line.as_ptr());
    }
}

const ADAPTER_LOST: &str = "the media process for this player stopped; playback cannot continue";

const DRIVER_IDLE: Duration = Duration::from_millis(1);

pub const ADAPTER_PATH_ENV: &str = "UUAV_ADAPTER_PATH";

pub const ADAPTER_FILE_NAME: &str = "uuav-adapter";

pub const HELLO_TIMEOUT_ENV: &str = "UUAV_IPC_HELLO_TIMEOUT_MS";

impl SegmentSource for crate::shm::Mapping {
    fn segment(&self) -> &SharedSegment {
        crate::shm::Mapping::segment(self)
    }
}

fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(PoisonError::into_inner)
}

enum Command {
    Open(String),
    Play,
    Pause,
    Close,
    SetLogLevel(i32),
}

pub struct PlayerConfig {
    pub protocol_whitelist: String,
    pub engine_sample_rate: u32,
    pub engine_channels: u32,
    pub sinks: LogSinks,
    pub hello_timeout_ms: u32,
    pub log_level: i32,
}

#[derive(Default)]
struct FrameState {
    latest: Option<FrameInfo>,
    acked: Option<FrameInfo>,
}

struct DriverShared {
    phase: AtomicU32,
    fault_code: AtomicU64,
    commands: Mutex<VecDeque<Command>>,
    frame: Mutex<FrameState>,
    shutdown: AtomicBool,
}

impl DriverShared {
    fn new() -> Self {
        Self {
            phase: AtomicU32::new(phase_code(Phase::Closed)),
            fault_code: AtomicU64::new(0),
            commands: Mutex::new(VecDeque::new()),
            frame: Mutex::new(FrameState::default()),
            shutdown: AtomicBool::new(false),
        }
    }

    fn push_command(&self, command: Command) {
        lock(&self.commands).push_back(command);
    }

    fn drain_commands(&self) -> VecDeque<Command> {
        std::mem::take(&mut *lock(&self.commands))
    }

    fn set_latest(&self, info: FrameInfo) {
        lock(&self.frame).latest = Some(info);
    }

    fn acknowledge(&self) -> Option<FrameInfo> {
        let mut frame = lock(&self.frame);
        if let Some(latest) = frame.latest {
            frame.acked = Some(latest);
        }
        frame.acked
    }

    fn acked(&self) -> Option<FrameInfo> {
        lock(&self.frame).acked
    }

    fn phase(&self) -> Phase {
        phase_of_code(self.phase.load(Ordering::Acquire))
    }
}

pub struct Player {
    id: PlayerId,
    mapping: Arc<crate::shm::Mapping>,
    shared: Arc<DriverShared>,
    controls: HostControls,
    driver: Mutex<Option<JoinHandle<()>>>,
    _fetch: FetchResponder,
}

impl Player {
    pub fn helper_pid(&self) -> u32 {
        self.segment().helper_pid()
    }

    fn segment(&self) -> &SharedSegment {
        self.mapping.segment()
    }

    pub fn open_media(&self, url: String) {
        self.controls.note_play(false);
        self.shared.push_command(Command::Open(url));
    }

    pub fn play(&self) {
        self.controls.note_play(true);
        self.shared.push_command(Command::Play);
    }

    pub fn pause(&self) {
        self.controls.note_play(false);
        self.shared.push_command(Command::Pause);
    }

    pub fn close_media(&self) {
        self.controls.note_play(false);
        self.shared.push_command(Command::Close);
    }

    pub fn set_log_level(&self, level: i32) {
        self.shared.push_command(Command::SetLogLevel(level));
    }

    pub fn last_error(&self) -> u64 {
        self.shared
            .fault_code
            .load(Ordering::Acquire)
            .saturating_sub(1)
    }

    pub fn seek(&self, seconds: f64) {
        self.controls.seek(self.segment(), seconds);
    }

    pub fn set_looping(&self, looping: bool) {
        self.controls.set_looping(self.segment(), looping);
    }

    pub fn looping(&self) -> bool {
        self.controls.looping(self.segment())
    }

    pub fn set_rate(&self, rate: f64) {
        self.controls.set_rate(self.segment(), rate);
    }

    pub fn rate(&self) -> f64 {
        self.controls.rate(self.segment())
    }

    pub fn assign_master_clock(&self, seconds: f64) {
        self.controls.assign_master_clock(self.segment(), seconds);
    }

    pub fn uuav_state(&self) -> u32 {
        use crate::session::uuav_state;
        match self.shared.phase() {
            Phase::Failed => uuav_state::ERROR,
            Phase::Closed => uuav_state::CLOSED,
            Phase::Opening => uuav_state::OPENING,
            Phase::Ended => uuav_state::ENDED,
            Phase::Open => match self.segment().transport.read() {
                TransportRead::Fresh(snapshot) => snapshot.state.to_wire(),
                TransportRead::Contended => uuav_state::READY,
                TransportRead::Corrupt(_) => uuav_state::ERROR,
            },
        }
    }

    fn transport_state(&self) -> PlaybackState {
        match self.segment().transport.read() {
            TransportRead::Fresh(snapshot) => snapshot.state,
            TransportRead::Contended | TransportRead::Corrupt(_) => PlaybackState::Ready,
        }
    }

    pub fn controls_state(&self) -> crate::ControlsState {
        self.controls.controls_state(self.segment(), self.transport_state())
    }

    fn media_available(&self) -> bool {
        matches!(self.shared.phase(), Phase::Open | Phase::Ended)
    }

    pub fn duration(&self) -> Option<f64> {
        if !self.media_available() {
            return None;
        }
        let facts = self.segment().media.read().ok()??;
        (facts.duration > 0.0).then_some(facts.duration)
    }

    pub fn current_time(&self) -> Option<f64> {
        if !self.media_available() {
            return None;
        }
        match self.segment().transport.read() {
            TransportRead::Fresh(snapshot) => Some(snapshot.clock.now(uptime_nanos())),
            TransportRead::Contended | TransportRead::Corrupt(_) => None,
        }
    }

    pub fn video_size(&self) -> Option<(u32, u32)> {
        if !self.media_available() {
            return None;
        }
        let facts = self.segment().media.read().ok()??;
        (facts.has_video && facts.visible_width > 0 && facts.visible_height > 0)
            .then_some((facts.visible_width, facts.visible_height))
    }

    pub fn media_facts(&self) -> Option<crate::protocol::MediaFactsValue> {
        if !self.media_available() {
            return None;
        }
        self.segment().media.read().ok()?
    }

    pub fn frame_info(&self) -> Option<FrameInfo> {
        self.shared.acked()
    }

    pub fn video_texture(&self, plane: i32) -> Option<*const c_void> {
        let info = self.shared.acked()?;
        let index = usize::try_from(plane).ok()?;
        let pointer = info.planes.get(index).copied()?;
        (pointer != 0).then_some(pointer as *const c_void)
    }

    pub fn on_render_event(&self) {
        let _ = self.shared.acknowledge();
    }

    pub fn update_audio_out(&self, sample_rate: u32, channels: u32) {
        self.segment().audio_options.publish(sample_rate, channels);
    }
}

impl Drop for Player {
    fn drop(&mut self) {
        jitter::release(self.id);
        self.shared.shutdown.store(true, Ordering::Release);
        let handle = lock(&self.driver).take();
        if let Some(handle) = handle {
            let _ = handle.join();
        }
    }
}

pub struct Registry {
    players: DashMap<PlayerId, Player>,
}

impl Default for Registry {
    fn default() -> Self {
        Self::new()
    }
}

impl Registry {
    pub fn new() -> Self {
        Self {
            players: DashMap::new(),
        }
    }

    pub fn len(&self) -> usize {
        self.players.len()
    }

    pub fn is_empty(&self) -> bool {
        self.players.is_empty()
    }

    pub fn create(
        &self,
        id: PlayerId,
        adapter_exe: &Path,
        config: PlayerConfig,
    ) -> Result<(), String> {
        let PlayerConfig {
            protocol_whitelist: whitelist,
            engine_sample_rate,
            engine_channels,
            sinks,
            hello_timeout_ms: hello_timeout,
            log_level,
        } = config;
        let shared = Arc::new(DriverShared::new());
        shared.push_command(Command::SetLogLevel(log_level));
        let (ready_tx, ready_rx) = std::sync::mpsc::channel();
        let exe = adapter_exe.to_owned();
        let shared_for_driver = Arc::clone(&shared);
        let ring = jitter::acquire(id);

        let handle = thread::Builder::new()
            .name(format!("uuav-driver-{id}"))
            .spawn(move || {
                match Session::start_with(&exe, &whitelist, hello_timeout) {
                    Ok(mut session) => {
                        let mapping = Arc::clone(session.mapping());
                        mapping
                            .segment()
                            .audio_options
                            .publish(engine_sample_rate, engine_channels);
                        if let Some(ring) = ring {
                            session.set_audio_ring(ring);
                        }
                        if ready_tx.send(Ok(mapping)).is_ok() {
                            drive(session, &shared_for_driver, sinks, ring);
                        }
                    }
                    Err(error) => {
                        let _ = ready_tx.send(Err(error.to_string()));
                    }
                }
            })
            .map_err(|error| format!("could not spawn the driver thread: {error}"))?;

        let budget = Duration::from_millis(u64::from(hello_timeout).saturating_add(5_000));
        match ready_rx.recv_timeout(budget) {
            Ok(Ok(mapping)) => {
                let fetch = match FetchResponder::spawn(id, Arc::clone(&mapping)) {
                    Ok(fetch) => fetch,
                    Err(error) => {
                        shared.shutdown.store(true, Ordering::Release);
                        let _ = handle.join();
                        jitter::release(id);
                        return Err(format!(
                            "could not spawn the fetch responder thread: {error}"
                        ));
                    }
                };
                let player = Player {
                    id,
                    mapping,
                    shared,
                    controls: HostControls::new(),
                    driver: Mutex::new(Some(handle)),
                    _fetch: fetch,
                };
                self.players.insert(id, player);
                Ok(())
            }
            Ok(Err(error)) => {
                let _ = handle.join();
                jitter::release(id);
                Err(error)
            }
            Err(_) => {
                shared.shutdown.store(true, Ordering::Release);
                let _ = handle.join();
                jitter::release(id);
                Err("helper did not complete the handshake in time".to_owned())
            }
        }
    }

    pub fn with<R>(&self, id: PlayerId, f: impl FnOnce(&Player) -> R) -> Option<R> {
        self.players.get(&id).map(|player| f(player.value()))
    }

    pub fn remove(&self, id: PlayerId) {
        drop(self.players.remove(&id));
    }

    pub fn for_each(&self, mut f: impl FnMut(&Player)) {
        for entry in &self.players {
            f(entry.value());
        }
    }
}

fn drive(
    mut session: Session,
    shared: &DriverShared,
    sinks: LogSinks,
    ring: Option<&'static JitterRing>,
) {
    let metal = MetalContext::new().ok();
    let mut table = SurfaceTable::new();
    let mut presenter = Presenter::new();
    let mut announced = false;

    loop {
        for command in shared.drain_commands() {
            match command {
                Command::Open(url) => {
                    if let Some(ring) = ring {
                        ring.restart();
                    }
                    presenter.begin_prime();
                    let _ = session.open(&url);
                }
                Command::Play => {
                    let _ = session.play();
                }
                Command::Pause => {
                    let _ = session.pause();
                }
                Command::SetLogLevel(level) => {
                    let _ = session.set_log_level(level);
                }
                Command::Close => {
                    if let Some(ring) = ring {
                        ring.restart();
                    }
                    let _ = session.close();
                }
            }
        }

        if let Some(ring) = ring {
            publish_audio_state(&session, ring);
        }

        if shared.shutdown.load(Ordering::Acquire) {
            break;
        }

        if session.phase() != Phase::Failed {
            if let Ok(pumped) = session.pump() {
                if pumped.surfaces_imported > 0
                    && let Some(metal) = metal.as_ref()
                {
                    let _ = adopt_surfaces(metal, &mut table, &session);
                }
                for entry in &pumped.logs {
                    sinks.dispatch(entry);
                }
                if let Some(code) = pumped.failed {
                    shared.fault_code.store(code.wrapping_add(1), Ordering::Release);
                    announced = true;
                }
            }

            if metal.is_some()
                && let Ok(Selection::Ready(presented)) =
                    presenter.poll(session.segment(), &table, uptime_nanos(), ring)
            {
                shared.set_latest(presented.info);
            }
        }

        if session.phase() == Phase::Failed && !announced {
            announced = true;
            LogSinks::emit(sinks.error, ADAPTER_LOST);
        }

        shared
            .phase
            .store(phase_code(session.phase()), Ordering::Release);
        thread::sleep(DRIVER_IDLE);
    }
}

fn publish_audio_state(session: &Session, ring: &JitterRing) {
    let segment = session.segment();
    let (clock, playing) = match segment.transport.read() {
        TransportRead::Fresh(snapshot) => (snapshot.clock, snapshot.state == PlaybackState::Playing),
        TransportRead::Contended | TransportRead::Corrupt(_) => return,
    };
    let Some((sample_rate, channels, _generation)) = segment.audio_options.read() else {
        return;
    };
    ring.publish_state(RingState {
        clock,
        playing,
        sample_rate,
        channels,
    });
}

fn adopt_surfaces(
    metal: &MetalContext,
    table: &mut SurfaceTable,
    session: &Session,
) -> anyhow::Result<()> {
    for slot in 0..SURFACE_SLOT_COUNT {
        let Some(imported) = session.surface(slot) else {
            continue;
        };
        if table.get(slot).map(ImportedSurface::generation) == Some(imported.generation) {
            continue;
        }
        table.insert(metal, slot, imported.surface.clone(), imported.generation)?;
    }
    Ok(())
}

const fn phase_code(phase: Phase) -> u32 {
    match phase {
        Phase::Closed => 0,
        Phase::Opening => 1,
        Phase::Open => 2,
        Phase::Ended => 3,
        Phase::Failed => 4,
    }
}

const fn phase_of_code(code: u32) -> Phase {
    match code {
        1 => Phase::Opening,
        2 => Phase::Open,
        3 => Phase::Ended,
        4 => Phase::Failed,
        _ => Phase::Closed,
    }
}

pub fn hello_timeout_ms() -> u32 {
    std::env::var(HELLO_TIMEOUT_ENV)
        .ok()
        .and_then(|value| value.parse().ok())
        .unwrap_or(crate::session::HELLO_TIMEOUT_MS)
}

pub fn resolve_adapter() -> Result<PathBuf, String> {
    if let Some(path) = std::env::var_os(ADAPTER_PATH_ENV) {
        return Ok(PathBuf::from(path));
    }
    adapter_beside_self().ok_or_else(|| {
        format!(
            "cannot locate the adapter binary; set {ADAPTER_PATH_ENV} or ship \
             uuav-adapter beside the library"
        )
    })
}

fn adapter_beside_self() -> Option<PathBuf> {
    let mut info: libc::Dl_info = unsafe { std::mem::zeroed() };
    let anchor = adapter_beside_self as *const c_void;
    let found = unsafe { libc::dladdr(anchor, &raw mut info) };
    if found == 0 || info.dli_fname.is_null() {
        return None;
    }
    let name = unsafe { CStr::from_ptr(info.dli_fname) };
    let path = PathBuf::from(OsStr::from_bytes(name.to_bytes()));
    Some(path.parent()?.join(ADAPTER_FILE_NAME))
}
