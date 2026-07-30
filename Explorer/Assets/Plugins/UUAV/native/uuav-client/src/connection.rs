//! The channel to the helper: synchronous handshake on the init thread,
//! then a single IO thread owning the zmq socket — inbound messages route
//! to the registry/callbacks, outbound messages arrive over an in-proc
//! channel (zmq sockets must not be shared across threads).

use crate::registry::{self, Registry};
use anyhow::{Context as _, Result, bail};
use crossbeam_channel::{Receiver, Sender, bounded, unbounded};
use dashmap::DashMap;
use std::sync::Arc;
use std::sync::atomic::{AtomicU8, AtomicU32, Ordering};
use std::time::Duration;
use uuav_ipc::protocol::{ABI_VERSION, Corr, LogSink, ReplyBody, ToClient, ToServer};
use uuav_ipc::{socket, zmq};

const HANDSHAKE_TIMEOUT_MS: i32 = 5000;
const REPLY_TIMEOUT: Duration = Duration::from_secs(5);
const IO_POLL_TIMEOUT_MS: i64 = 5;

/// The connection's single lifecycle state. Ordered: transitions only
/// escalate (via [`LifecycleCell::escalate`]), so racing writers — the
/// child monitor reaping a crash vs `uuav_deinit` — always resolve to the
/// stricter state.
#[repr(u8)]
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub enum Lifecycle {
    /// Helper alive: commands flow, callbacks reach Unity.
    Running = 0,
    /// Helper process died unexpectedly: commands/getters degrade, the IO
    /// thread exits, but callbacks still reach Unity (this state is what
    /// delivers the "helper terminated" error).
    HelperDead = 1,
    /// `uuav_deinit` ran: nothing may call into Unity anymore; the IO
    /// thread flushes the Shutdown frame and exits.
    ShutDown = 2,
}

/// Shared, monotonically-escalating [`Lifecycle`] holder; one instance is
/// shared by the connection, the IO thread, and the callback sinks.
pub struct LifecycleCell(AtomicU8);

impl LifecycleCell {
    pub const fn new() -> Self {
        Self(AtomicU8::new(Lifecycle::Running as u8))
    }

    pub fn get(&self) -> Lifecycle {
        match self.0.load(Ordering::Acquire) {
            0 => Lifecycle::Running,
            1 => Lifecycle::HelperDead,
            _ => Lifecycle::ShutDown,
        }
    }

    /// Moves to `to` unless the current state is already stricter.
    pub fn escalate(&self, to: Lifecycle) {
        self.0.fetch_max(to as u8, Ordering::AcqRel);
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
}

impl Connection {
    /// Binds, waits for the helper's `Hello`, verifies token + ABI, then
    /// hands the socket to the IO thread. `spawn_after_bind` is called with
    /// the resolved endpoint (the helper is spawned between bind and recv).
    pub fn establish(
        token: &str,
        spawn_after_bind: impl FnOnce(&str) -> Result<()>,
        lifecycle: Arc<LifecycleCell>,
        registry: Arc<Registry>,
        sinks: Arc<dyn EventSinks>,
    ) -> Result<Self> {
        let context = zmq::Context::new();
        let sock = socket::dealer(&context)?;
        sock.bind(&socket::bind_endpoint(token))
            .context("bind uuav endpoint")?;
        let endpoint = socket::bound_endpoint(&sock)?;

        spawn_after_bind(&endpoint)?;

        sock.set_rcvtimeo(HANDSHAKE_TIMEOUT_MS)?;
        let hello: ToClient = socket::recv(&sock).context("helper did not say Hello")?;
        let ToClient::Hello { token: got, abi, pid: _ } = hello else {
            bail!("unexpected first message from helper");
        };
        if got != token {
            bail!("helper presented a wrong session token");
        }
        if abi != ABI_VERSION {
            bail!("helper ABI {abi} does not match client {ABI_VERSION} (stale uuav-helper?)");
        }
        sock.set_rcvtimeo(-1)?;

        let (outbound, outbound_rx) = unbounded::<ToServer>();
        let pending: Arc<DashMap<Corr, Sender<Result<ReplyBody, String>>>> =
            Arc::new(DashMap::new());

        io_thread(
            sock,
            outbound_rx,
            Arc::clone(&pending),
            Arc::clone(&lifecycle),
            registry,
            sinks,
        )?;

        Ok(Self {
            outbound,
            pending,
            next_corr: AtomicU32::new(1),
            lifecycle,
        })
    }

    pub fn lifecycle(&self) -> Lifecycle {
        self.lifecycle.get()
    }

    /// Called by the child monitor when the helper process was reaped.
    pub fn mark_helper_dead(&self) {
        self.lifecycle.escalate(Lifecycle::HelperDead);
    }

    /// Fire-and-forget command.
    pub fn send(&self, message: ToServer) -> Result<()> {
        if self.lifecycle() != Lifecycle::Running {
            bail!("uuav helper is not running");
        }
        self.outbound.send(message).context("uuav IO thread is gone")
    }

    /// Round-trip command; blocks up to [`REPLY_TIMEOUT`].
    pub fn request(
        &self,
        build: impl FnOnce(Corr) -> ToServer,
    ) -> Result<ReplyBody, String> {
        if self.lifecycle() != Lifecycle::Running {
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
        self.lifecycle.escalate(Lifecycle::ShutDown);
    }
}

fn io_thread(
    sock: zmq::Socket,
    outbound: Receiver<ToServer>,
    pending: Arc<DashMap<Corr, Sender<Result<ReplyBody, String>>>>,
    lifecycle: Arc<LifecycleCell>,
    registry: Arc<Registry>,
    sinks: Arc<dyn EventSinks>,
) -> Result<()> {
    std::thread::Builder::new()
        .name("uuav-io".into())
        .spawn(move || {
            loop {
                match lifecycle.get() {
                    Lifecycle::Running => {}
                    // peer is gone; nothing left to flush to
                    Lifecycle::HelperDead => return,
                    // flush what was queued (incl. the Shutdown frame; the
                    // socket's bounded linger covers the wire transmission)
                    Lifecycle::ShutDown => {
                        for message in outbound.try_iter() {
                            if socket::send(&sock, &message).is_err() {
                                break;
                            }
                        }
                        return;
                    }
                }

                for message in outbound.try_iter() {
                    if socket::send(&sock, &message).is_err() {
                        return;
                    }
                }

                match socket::poll_readable(&sock, IO_POLL_TIMEOUT_MS) {
                    Ok(true) => {}
                    Ok(false) => continue,
                    Err(_) => return,
                }

                loop {
                    match socket::try_recv::<ToClient>(&sock) {
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
        } => registry::apply_texture_set(registry, id, generation, width, height),
        #[cfg(target_os = "macos")]
        ToClient::FramePublished {
            id,
            generation,
            slot,
        } => registry::apply_frame_published(registry, id, generation, slot),
        #[cfg(target_os = "windows")]
        ToClient::TextureSet { .. } | ToClient::FramePublished { .. } => {
            // M4: Windows shared textures
        }
        ToClient::PlayerError { id, message } => sinks.on_player_error(id, &message),
        ToClient::Log { sink, line } => sinks.on_log(sink, &line),
        ToClient::Hello { .. } => { /* handshake is over; ignore */ }
    }
}
