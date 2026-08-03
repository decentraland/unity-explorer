
use std::collections::VecDeque;
use std::ffi::{CString, OsString, c_void};
use std::os::windows::ffi::OsStringExt as _;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering};
use std::sync::{Arc, Mutex, MutexGuard, PoisonError};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use dashmap::DashMap;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_BOX, D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, ID3D11Device,
    ID3D11DeviceContext, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_NV12, DXGI_SAMPLE_DESC};
use windows::Win32::Graphics::Dxgi::IDXGIKeyedMutex;
use windows::core::Interface as _;

use crate::audio::{self as jitter, JitterRing, RingState};
use crate::controls::HostControls;
use crate::fetch::{FetchResponder, SegmentSource};
use crate::protocol::{
    LogLevel, MediaFactsValue, PlaybackState, SharedSegment, TransportRead, uptime_nanos,
};
use crate::win::bridge::Drain;
use crate::win::gpu;
use crate::win::present::{Presenter, Selection};
use crate::win::session::{Phase, Session, SlotContent, uuav_state};
use crate::win::shm::Mapping;
use crate::win::spawn::Integrity;
use crate::win::wire::Mode;
use crate::{ControlsState, FrameInfo, PlayerId, RawLogCallback};

pub const ADAPTER_PATH_ENV: &str = "UUAV_ADAPTER_PATH";

pub const ADAPTER_FILE_NAME: &str = "uuav-adapter.exe";

pub const HELLO_TIMEOUT_ENV: &str = "UUAV_IPC_HELLO_TIMEOUT_MS";

pub const INTEGRITY_ENV: &str = "UUAV_IPC_INTEGRITY";

const ADAPTER_LOST: &str = "the media process for this player stopped; playback cannot continue";

const DRIVER_IDLE: Duration = Duration::from_millis(1);

const PRESENT_REPORT_EVERY: u64 = 30;

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
    pub device: ID3D11Device,
    pub log_level: i32,
}

struct SendDevice(ID3D11Device);

unsafe impl Send for SendDevice {}

struct Presentation {
    device: ID3D11Device,
    context: ID3D11DeviceContext,
    target: Option<SizedTarget>,
}

struct SizedTarget {
    texture: ID3D11Texture2D,
    width: u32,
    height: u32,
}

impl Presentation {
    fn new(device: ID3D11Device) -> Result<Self, String> {
        let context = unsafe { device.GetImmediateContext() }
            .map_err(|error| format!("the engine device has no immediate context: {error}"))?;
        Ok(Self {
            device,
            context,
            target: None,
        })
    }

    fn present(&mut self, pending: &PendingFrame) -> Option<FrameInfo> {
        let mut info = pending.info;
        let (Some(&luma_width), Some(&luma_height)) =
            (info.plane_width.first(), info.plane_height.first())
        else {
            return None;
        };
        let width = luma_width & !1;
        let height = luma_height & !1;
        if width == 0 || height == 0 {
            return None;
        }
        let target = self.ensure_target(width, height)?;
        let region = D3D11_BOX {
            left: 0,
            top: 0,
            front: 0,
            right: width,
            bottom: height,
            back: 1,
        };
        let copied = gpu::with_key(&pending.mutex, gpu::KEY, gpu::KEY, 0, || {
            unsafe {
                self.context.CopySubresourceRegion(
                    &target,
                    0,
                    0,
                    0,
                    0,
                    &pending.texture,
                    0,
                    Some(&raw const region),
                );
            }
            Ok(())
        });
        match copied {
            Ok(Some(())) => {}
            Ok(None) | Err(_) => return None,
        }
        unsafe { self.context.Flush() };
        let raw = target.as_raw() as usize;
        info.planes = [raw, raw];
        Some(info)
    }

    fn ensure_target(&mut self, width: u32, height: u32) -> Option<ID3D11Texture2D> {
        let target = match self.target.take() {
            Some(existing) if existing.width == width && existing.height == height => existing,
            _ => {
                let description = D3D11_TEXTURE2D_DESC {
                    Width: width,
                    Height: height,
                    MipLevels: 1,
                    ArraySize: 1,
                    Format: DXGI_FORMAT_NV12,
                    SampleDesc: DXGI_SAMPLE_DESC {
                        Count: 1,
                        Quality: 0,
                    },
                    Usage: D3D11_USAGE_DEFAULT,
                    BindFlags: D3D11_BIND_SHADER_RESOURCE.0 as u32,
                    CPUAccessFlags: 0,
                    MiscFlags: 0,
                };
                let mut created: Option<ID3D11Texture2D> = None;
                unsafe {
                    self.device
                        .CreateTexture2D(&description, None, Some(&raw mut created))
                }
                .ok()?;
                SizedTarget {
                    texture: created?,
                    width,
                    height,
                }
            }
        };
        Some(self.target.insert(target).texture.clone())
    }
}

struct PendingFrame {
    info: FrameInfo,
    texture: ID3D11Texture2D,
    mutex: IDXGIKeyedMutex,
}

#[derive(Default)]
struct FrameState {
    pending: Option<PendingFrame>,
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

    fn set_pending(&self, pending: PendingFrame) {
        lock(&self.frame).pending = Some(pending);
    }

    fn take_pending(&self) -> Option<PendingFrame> {
        lock(&self.frame).pending.take()
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

impl SegmentSource for Mapping {
    fn segment(&self) -> &SharedSegment {
        Mapping::segment(self)
    }
}

pub struct Player {
    id: PlayerId,
    mapping: Arc<Mapping>,
    shared: Arc<DriverShared>,
    controls: HostControls,
    presentation: Mutex<Presentation>,
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

    pub fn controls_state(&self) -> ControlsState {
        self.controls
            .controls_state(self.segment(), self.transport_state())
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

    pub fn media_facts(&self) -> Option<MediaFactsValue> {
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
        if let Some(pending) = self.shared.take_pending()
            && let Some(info) = lock(&self.presentation).present(&pending)
        {
            self.shared.set_latest(info);
        }
        let _ignored = self.shared.acknowledge();
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
            let _ignored = handle.join();
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
            device,
            log_level,
        } = config;
        let shared = Arc::new(DriverShared::new());
        shared.push_command(Command::SetLogLevel(log_level));
        let (ready_tx, ready_rx) = std::sync::mpsc::channel();
        let exe = adapter_exe.to_owned();
        let shared_for_driver = Arc::clone(&shared);
        let integrity = integrity();
        let presentation = Presentation::new(device.clone())?;
        let device = SendDevice(device);
        let ring = jitter::acquire(id);

        let handle = thread::Builder::new()
            .name(format!("uuav-driver-{id}"))
            .spawn(move || {
                let device = device;
                match Session::start(&exe, Some(&device.0), &whitelist, integrity) {
                    Ok(session) => {
                        let mapping = Arc::clone(session.mapping());
                        mapping
                            .segment()
                            .audio_options
                            .publish(engine_sample_rate, engine_channels);
                        if ready_tx.send(Ok(mapping)).is_ok() {
                            drive(id, session, &shared_for_driver, sinks, ring);
                        }
                    }
                    Err(error) => {
                        let _ignored = ready_tx.send(Err(format!("{error:#}")));
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
                        let _ignored = handle.join();
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
                    presentation: Mutex::new(presentation),
                    driver: Mutex::new(Some(handle)),
                    _fetch: fetch,
                };
                self.players.insert(id, player);
                Ok(())
            }
            Ok(Err(error)) => {
                let _ignored = handle.join();
                jitter::release(id);
                Err(error)
            }
            Err(_) => {
                shared.shutdown.store(true, Ordering::Release);
                let _ignored = handle.join();
                jitter::release(id);
                Err("the adapter did not complete the handshake in time".to_owned())
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
    id: PlayerId,
    mut session: Session,
    shared: &DriverShared,
    sinks: LogSinks,
    ring: Option<&'static JitterRing>,
) {
    let mut presenter = Presenter::new();
    let mut drain = Drain::new();
    let mut announced = false;
    let mut presenter_down = false;
    let mut present_reported: u64 = 0;
    let mut offset_last = f64::NAN;
    let mut offset_min = f64::INFINITY;
    let mut offset_max = f64::NEG_INFINITY;
    let mut drops_logged: u64 = 0;
    let mut poll_calls: u64 = 0;
    let mut host_window = Instant::now();
    let mut last_presented: u64 = 0;
    let mut last_released: u64 = 0;

    loop {
        for command in shared.drain_commands() {
            match command {
                Command::Open(url) => {
                    if let Some(ring) = ring {
                        ring.restart();
                    }
                    presenter.begin_prime();
                    let _ignored = session.open(&url);
                }
                Command::Play => {
                    let _ignored = session.play();
                }
                Command::Pause => {
                    let _ignored = session.pause();
                }
                Command::Close => {
                    if let Some(ring) = ring {
                        ring.restart();
                    }
                    let _ignored = session.close();
                }
                Command::SetLogLevel(level) => {
                    let _ignored = session.set_log_level(level);
                }
            }
        }

        if let Some(ring) = ring {
            publish_audio_state(&session, ring);
            let _moved = drain.pump(session.segment(), ring);
        }

        if shared.shutdown.load(Ordering::Acquire) {
            break;
        }

        if session.phase() != Phase::Failed {
            if let Ok(pumped) = session.pump() {
                for entry in &pumped.logs {
                    sinks.dispatch(entry);
                }
                if let Some(code) = pumped.failed {
                    shared
                        .fault_code
                        .store(code.wrapping_add(1), Ordering::Release);
                    announced = true;
                }
            }

            if session.mode() == Mode::Gpu && !presenter_down {
                let selection = presenter.poll(&session, uptime_nanos(), ring);
                poll_calls = poll_calls.wrapping_add(1);
                for (dropped_pts, drop_now) in presenter.take_drops() {
                    if drops_logged < 400 {
                        drops_logged = drops_logged.wrapping_add(1);
                        LogSinks::emit(
                            sinks.log,
                            &format!(
                                "uuav-present: dropped_late pts={dropped_pts:.3} \
                                 now={drop_now:.3} late_ms={:.1}",
                                (drop_now - dropped_pts) * 1e3
                            ),
                        );
                    }
                }
                match selection {
                    Ok(Selection::Ready(presented)) => {
                        if let Some(pts) = presented.pts {
                            let now = presenter.last_now();
                            if now.is_finite() {
                                let offset_ms = (pts - now) * 1e3;
                                offset_last = offset_ms;
                                offset_min = offset_min.min(offset_ms);
                                offset_max = offset_max.max(offset_ms);
                            }
                        }
                        let stats = presenter.stats();
                        if stats.presented >= present_reported.wrapping_add(PRESENT_REPORT_EVERY) {
                            present_reported = stats.presented;
                            LogSinks::emit(
                                sinks.log,
                                &format!(
                                    "uuav-present: player={id} presented={} dropped_late={} \
                                     released={} held={} offset_ms={:.1} offset_min_ms={:.1} \
                                     offset_max_ms={:.1}",
                                    stats.presented,
                                    stats.dropped_late,
                                    stats.released,
                                    presenter.held(),
                                    offset_last,
                                    offset_min,
                                    offset_max,
                                ),
                            );
                            offset_min = f64::INFINITY;
                            offset_max = f64::NEG_INFINITY;
                        }
                        if let Some(slot) = session.surface(presented.slot)
                            && let SlotContent::Shared { texture, mutex, .. } = &slot.content
                        {
                            shared.set_pending(PendingFrame {
                                info: presented.info,
                                texture: texture.clone(),
                                mutex: mutex.clone(),
                            });
                        } else {
                            presenter_down = true;
                            LogSinks::emit(
                                sinks.error,
                                &format!(
                                    "presented slot {} has no shared surface; frame delivery \
                                     stops",
                                    presented.slot
                                ),
                            );
                        }
                    }
                    Ok(Selection::Idle | Selection::NotImported { .. }) => {}
                    Ok(Selection::Faulted(fault)) => {
                        presenter_down = true;
                        LogSinks::emit(
                            sinks.error,
                            &format!("the adapter broke the frame protocol: {fault:?}"),
                        );
                    }
                    Err(error) => {
                        presenter_down = true;
                        LogSinks::emit(sinks.error, &format!("presenter bug: {error:#}"));
                    }
                }
            }
        }

        if session.phase() == Phase::Failed && !announced {
            announced = true;
            LogSinks::emit(sinks.error, ADAPTER_LOST);
        }

        shared
            .phase
            .store(phase_code(session.phase()), Ordering::Release);

        if host_window.elapsed().as_secs_f64() >= 1.0 {
            let elapsed = host_window.elapsed().as_secs_f64();
            let stats = presenter.stats();
            let poll_rate = poll_calls as f64 / elapsed;
            let presented_rate = stats.presented.saturating_sub(last_presented) as f64 / elapsed;
            let released_rate = stats.released.saturating_sub(last_released) as f64 / elapsed;
            LogSinks::emit(
                sinks.log,
                &format!(
                    "uuav-host: poll_rate={poll_rate:.0}/s presented={presented_rate:.1}/s \
                     released={released_rate:.1}/s held={} audio_latency_ms={:.1}",
                    presenter.held(),
                    presenter.audio_latency() * 1e3,
                ),
            );
            last_presented = stats.presented;
            last_released = stats.released;
            poll_calls = 0;
            host_window = Instant::now();
        }

        thread::sleep(DRIVER_IDLE);
    }
}

fn publish_audio_state(session: &Session, ring: &JitterRing) {
    let segment = session.segment();
    let (clock, playing) = match segment.transport.read() {
        TransportRead::Fresh(snapshot) => {
            (snapshot.clock, snapshot.state == PlaybackState::Playing)
        }
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
        .unwrap_or(crate::win::session::HELLO_TIMEOUT_MS)
}

fn integrity() -> Integrity {
    match std::env::var(INTEGRITY_ENV) {
        Ok(value) if value.eq_ignore_ascii_case("medium") => Integrity::Medium,
        _ => Integrity::Low,
    }
}

pub fn resolve_adapter() -> Result<PathBuf, String> {
    if let Some(path) = std::env::var_os(ADAPTER_PATH_ENV) {
        return Ok(PathBuf::from(path));
    }
    adapter_beside_self().ok_or_else(|| {
        format!(
            "cannot locate the adapter binary; set {ADAPTER_PATH_ENV} or ship \
             {ADAPTER_FILE_NAME} beside the library"
        )
    })
}

fn adapter_beside_self() -> Option<PathBuf> {
    use windows_sys::Win32::Foundation::{HMODULE, MAX_PATH};
    use windows_sys::Win32::System::LibraryLoader::{
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        GetModuleFileNameW, GetModuleHandleExW,
    };

    let mut module: HMODULE = std::ptr::null_mut();
    let found = unsafe {
        GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            adapter_beside_self as *const u16,
            &raw mut module,
        )
    };
    if found == 0 {
        return None;
    }

    let mut buffer = [0u16; MAX_PATH as usize];
    let written = unsafe { GetModuleFileNameW(module, buffer.as_mut_ptr(), buffer.len() as u32) };
    if written == 0 || written as usize >= buffer.len() {
        return None;
    }
    let path = PathBuf::from(OsString::from_wide(buffer.get(..written as usize)?));
    Some(path.parent()?.join(ADAPTER_FILE_NAME))
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::panic,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;

    #[test]
    fn an_empty_registry_answers_nothing() {
        let registry = Registry::new();
        assert!(registry.is_empty());
        assert_eq!(registry.len(), 0);
        assert!(registry.with(1, |_ignored| ()).is_none());
    }

    #[test]
    fn the_phase_code_round_trips_every_phase() {
        for phase in [
            Phase::Closed,
            Phase::Opening,
            Phase::Open,
            Phase::Ended,
            Phase::Failed,
        ] {
            assert_eq!(phase_of_code(phase_code(phase)), phase);
        }
    }

    #[test]
    fn the_integrity_override_defaults_to_low() {
        unsafe { std::env::remove_var(INTEGRITY_ENV) };
        assert_eq!(integrity(), Integrity::Low);
        for value in ["", "high", "lo w", "mediumish", "0"] {
            unsafe { std::env::set_var(INTEGRITY_ENV, value) };
            assert_eq!(integrity(), Integrity::Low, "{value:?}");
        }
        unsafe { std::env::set_var(INTEGRITY_ENV, "Medium") };
        assert_eq!(integrity(), Integrity::Medium);
        unsafe { std::env::remove_var(INTEGRITY_ENV) };
    }

    #[test]
    fn the_adapter_path_override_wins() {
        unsafe { std::env::set_var(ADAPTER_PATH_ENV, r"C:\explicit\uuav-adapter.exe") };
        let resolved = resolve_adapter().unwrap();
        unsafe { std::env::remove_var(ADAPTER_PATH_ENV) };
        assert_eq!(resolved, PathBuf::from(r"C:\explicit\uuav-adapter.exe"));
    }
}
