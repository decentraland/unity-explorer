//! The channel to the helper: synchronous handshake on the init thread,
//! then a single IO thread owning the OS channel — inbound messages route
//! to the registry/callbacks, outbound messages arrive over an in-proc
//! channel (the channel is single-owner).

use crate::registry::{self, Registry};
use anyhow::{Context as _, Result, bail};
use crossbeam_channel::{Receiver, Sender, bounded, unbounded};
use dashmap::DashMap;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU8, AtomicU32, Ordering};
use std::time::Duration;
use uuav_ipc::channel::{Channel, ChildHandoff};
use uuav_ipc::protocol::{ABI_VERSION, Corr, LogSink, ReplyBody, ToClient, ToServer};

const HANDSHAKE_TIMEOUT: Duration = Duration::from_secs(5);
const REPLY_TIMEOUT: Duration = Duration::from_secs(5);
const IO_POLL_TIMEOUT_MS: u32 = 5;

/// The runtime's single lifecycle state, shared by the connection(s), the
/// IO threads, the callback sinks, and the recovery worker. States move
/// freely between `Running`/`Recovering`/`Failed` (a dead helper recovers,
/// a parked runtime re-arms); only `ShutDown` is terminal.
#[repr(u8)]
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Lifecycle {
    /// Helper alive: commands flow, callbacks reach Unity.
    Running = 0,
    /// Helper died and the recovery worker is resurrecting it: commands are
    /// absorbed into desired state, players read as OPENING.
    Recovering = 1,
    /// Respawn attempts capped out: commands degrade, players read as
    /// ERROR, until an open/play re-arms the worker.
    Failed = 2,
    /// `uuav_deinit` ran: nothing may call into Unity anymore; the IO
    /// thread flushes the Shutdown frame and exits.
    ShutDown = 3,
}

/// Shared [`Lifecycle`] holder. Any state may be set at any time except
/// past `ShutDown`, which absorbs every later transition — racing writers
/// (the recovery worker vs `uuav_deinit`) always resolve to shut down.
pub struct LifecycleCell(AtomicU8);

impl LifecycleCell {
    pub const fn new() -> Self {
        Self(AtomicU8::new(Lifecycle::Running as u8))
    }

    pub fn get(&self) -> Lifecycle {
        match self.0.load(Ordering::Acquire) {
            0 => Lifecycle::Running,
            1 => Lifecycle::Recovering,
            2 => Lifecycle::Failed,
            _ => Lifecycle::ShutDown,
        }
    }

    /// Moves to `to` unless already shut down.
    pub fn transition(&self, to: Lifecycle) {
        _ = self.0.fetch_update(Ordering::AcqRel, Ordering::Acquire, |current| {
            (current != Lifecycle::ShutDown as u8).then_some(to as u8)
        });
    }
}

impl Default for LifecycleCell {
    fn default() -> Self {
        Self::new()
    }
}

/// Sinks the IO thread routes inbound diagnostics into; they wrap the C#
/// delegates registered at init.
pub trait EventSinks: Send + Sync + 'static {
    fn on_log(&self, sink: LogSink, line: &str);
    fn on_player_error(&self, id: Option<u64>, message: &str);
}

pub struct Connection {
    outbound: Sender<ToServer>,
    pending: Arc<DashMap<Corr, Sender<Result<ReplyBody, String>>>>,
    next_corr: AtomicU32,
    lifecycle: Arc<LifecycleCell>,
    /// This connection generation's liveness: retired (and its IO thread
    /// exits) when the helper behind it dies; a resurrected helper gets a
    /// whole new `Connection`.
    alive: Arc<AtomicBool>,
}

impl Connection {
    /// Creates both channel ends, waits for the helper's `Hello`, verifies
    /// token + ABI, then hands the channel to the IO thread. `spawn` is
    /// called with the helper's end (the helper is spawned between pair
    /// and recv); a helper that dies before Hello fails the handshake
    /// immediately via EOF rather than burning the full timeout.
    pub fn establish(
        token: &str,
        spawn: impl FnOnce(ChildHandoff) -> Result<()>,
        lifecycle: Arc<LifecycleCell>,
        registry: Arc<Registry>,
        sinks: Arc<dyn EventSinks>,
    ) -> Result<Self> {
        let (mut channel, handoff) = Channel::pair(token).context("create uuav channel")?;

        spawn(handoff)?;

        let hello: ToClient = channel
            .recv_timeout(HANDSHAKE_TIMEOUT)
            .context("helper did not say Hello")?;
        let ToClient::Hello { token: got, abi, pid: _ } = hello else {
            bail!("unexpected first message from helper");
        };
        if got != token {
            bail!("helper presented a wrong session token");
        }
        if abi != ABI_VERSION {
            bail!("helper ABI {abi} does not match client {ABI_VERSION} (stale uuav-helper?)");
        }

        let (outbound, outbound_rx) = unbounded::<ToServer>();
        let pending: Arc<DashMap<Corr, Sender<Result<ReplyBody, String>>>> =
            Arc::new(DashMap::new());
        let alive = Arc::new(AtomicBool::new(true));

        io_thread(
            channel,
            outbound_rx,
            Arc::clone(&pending),
            Arc::clone(&lifecycle),
            Arc::clone(&alive),
            registry,
            sinks,
        )?;

        Ok(Self {
            outbound,
            pending,
            next_corr: AtomicU32::new(1),
            lifecycle,
            alive,
        })
    }

    /// Called when the helper behind this connection was reaped: the IO
    /// thread exits and every later send/request degrades. The lifecycle
    /// itself is the recovery worker's to manage.
    pub fn retire(&self) {
        self.alive.store(false, Ordering::Release);
    }

    /// Handle for platform channel threads (macOS mach receiver) tied to
    /// this connection generation.
    #[cfg(target_os = "macos")]
    pub fn alive_flag(&self) -> Arc<AtomicBool> {
        Arc::clone(&self.alive)
    }

    fn usable(&self) -> bool {
        self.alive.load(Ordering::Acquire) && self.lifecycle.get() != Lifecycle::ShutDown
    }

    /// Fire-and-forget command.
    pub fn send(&self, message: ToServer) -> Result<()> {
        if !self.usable() {
            bail!("uuav helper is not running");
        }
        self.outbound.send(message).context("uuav IO thread is gone")
    }

    /// Round-trip command; blocks up to [`REPLY_TIMEOUT`].
    pub fn request(
        &self,
        build: impl FnOnce(Corr) -> ToServer,
    ) -> Result<ReplyBody, String> {
        if !self.usable() {
            return Err("uuav helper is not running".to_owned());
        }
        let corr = self.next_corr.fetch_add(1, Ordering::Relaxed);
        let (tx, rx) = bounded(1);
        self.pending.insert(corr, tx);

        if let Err(e) = self.outbound.send(build(corr)) {
            self.pending.remove(&corr);
            return Err(format!("uuav IO thread is gone: {e}"));
        }

        rx.recv_timeout(REPLY_TIMEOUT).unwrap_or_else(|_| {
            self.pending.remove(&corr);
            Err("uuav helper did not reply in time".to_owned())
        })
    }

    /// Planned teardown: the IO thread flushes the Shutdown frame and exits,
    /// and the shared lifecycle silences the callback sinks.
    pub fn shutdown(&self) {
        _ = self.outbound.send(ToServer::Shutdown);
        self.lifecycle.transition(Lifecycle::ShutDown);
    }
}

fn io_thread(
    mut channel: Channel,
    outbound: Receiver<ToServer>,
    pending: Arc<DashMap<Corr, Sender<Result<ReplyBody, String>>>>,
    lifecycle: Arc<LifecycleCell>,
    alive: Arc<AtomicBool>,
    registry: Arc<Registry>,
    sinks: Arc<dyn EventSinks>,
) -> Result<()> {
    std::thread::Builder::new()
        .name("uuav-io".into())
        .spawn(move || {
            loop {
                // flush what was queued on planned shutdown (incl. the
                // Shutdown frame; completed sends sit in the kernel
                // buffer, which outlives our end of the channel)
                if lifecycle.get() == Lifecycle::ShutDown {
                    for message in outbound.try_iter() {
                        if channel.send(&message).is_err() {
                            break;
                        }
                    }
                    return;
                }
                // retired: the helper behind this channel is gone; nothing
                // left to flush to
                if !alive.load(Ordering::Acquire) {
                    return;
                }

                for message in outbound.try_iter() {
                    if channel.send(&message).is_err() {
                        return;
                    }
                }

                match channel.poll_readable(IO_POLL_TIMEOUT_MS) {
                    Ok(true) => {}
                    Ok(false) => continue,
                    Err(_) => return,
                }

                loop {
                    match channel.try_recv::<ToClient>() {
                        Ok(Some(message)) => {
                            route(message, &pending, &registry, sinks.as_ref());
                        }
                        Ok(None) => break,
                        Err(_) => return,
                    }
                }
            }
        })
        .context("spawn uuav IO thread")?;
    Ok(())
}

fn route(
    message: ToClient,
    pending: &DashMap<Corr, Sender<Result<ReplyBody, String>>>,
    registry: &Registry,
    sinks: &dyn EventSinks,
) {
    match message {
        ToClient::Reply { corr, result } => {
            if let Some((_, tx)) = pending.remove(&corr) {
                _ = tx.send(result);
            }
        }
        ToClient::State(update) => registry::apply_state(registry, update),
        ToClient::MediaInfo { id, info } => registry::apply_media_info(registry, id, info),
        ToClient::AudioPacket { id, samples } => registry::apply_audio(registry, id, &samples),
        #[cfg(target_os = "macos")]
        ToClient::TextureSet {
            id,
            generation,
            width,
            height,
            handles: _, // surfaces arrive as mach ports, not handles
        } => registry::apply_texture_set(registry, id, generation, width, height),
        #[cfg(target_os = "windows")]
        ToClient::TextureSet {
            id,
            generation,
            width,
            height,
            handles,
        } => registry::apply_texture_set(registry, id, generation, width, height, &handles),
        ToClient::FramePublished {
            id,
            generation,
            slot,
        } => registry::apply_frame_published(registry, id, generation, slot),
        ToClient::PlayerError { id, message } => {
            // the wire carries the helper-side id; report the public one C#
            // knows (a stale id from a previous helper resolves to None)
            let public = id.and_then(|helper| registry.public_of(helper));
            sinks.on_player_error(public, &message);
        }
        ToClient::Log { sink, line } => sinks.on_log(sink, &line),
        ToClient::ServeStats {
            max_iter_us,
            audio_pull_clamps,
        } => registry::apply_serve_stats(registry, max_iter_us, audio_pull_clamps),
        ToClient::Hello { .. } => { /* handshake is over; ignore */ }
    }
}
