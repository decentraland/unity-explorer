//! Host-facing state: the Bevy thread, the command channel into it, and the frame status it publishes.

use crate::{app, logging, output::OutputHandles};
use abi::{BmFrameStatus, BmInitConfig, BmOutputTexture, BmResult, OUTPUT_BUFFER_COUNT};
use render::{readback::Pixels, resources::ResourceCommand, snapshot::Snapshot};
use std::{
    sync::{
        Arc,
        atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering},
        mpsc::{self, Sender},
    },
    thread::JoinHandle,
    time::Duration,
};

/// How long `bm_init` waits for the render device and output buffers.
const READY_TIMEOUT: Duration = Duration::from_mins(1);

pub enum Command {
    Snapshot(Box<Snapshot>),
    Resource(ResourceCommand),
    Shutdown,
}

/// Written by the Bevy thread after each frame's GPU work completed, read by the host every frame.
#[derive(Debug, Default)]
pub struct Status {
    pub completed_frame: AtomicU64,
    pub completed_buffer: AtomicU32,
    pub in_flight: AtomicU32,
    pub failed: AtomicBool,
    pub render_events: AtomicU64,
}

pub struct Bridge {
    commands: Sender<Command>,
    thread: Option<JoinHandle<()>>,
    status: Arc<Status>,
    outputs: Vec<OutputHandles>,
    output_kind: abi::BmOutputKind,
    pixels: render::readback::LatestPixels,
}

impl Bridge {
    pub fn start(config: BmInitConfig) -> Result<Self, BmResult> {
        if config.width == 0 || config.height == 0 {
            return Err(BmResult::InvalidArgument);
        }

        let (commands, receiver) = mpsc::channel();
        let (ready_tx, ready_rx) = mpsc::channel();
        let status = Arc::new(Status::default());
        let pixels = render::readback::LatestPixels::default();

        let thread_args = app::ThreadArgs {
            config,
            commands: receiver,
            status: Arc::clone(&status),
            ready: ready_tx,
            pixels: pixels.clone(),
        };

        let thread = std::thread::Builder::new()
            .name("bevy-migration".to_owned())
            .spawn(move || app::run(thread_args))
            .map_err(|error| {
                logging::error(&format!("cannot spawn the Bevy thread: {error}"));
                BmResult::Internal
            })?;

        let outputs = match ready_rx.recv_timeout(READY_TIMEOUT) {
            Ok(Ok(outputs)) => outputs,
            Ok(Err(message)) => {
                logging::error(&format!("bridge start failed: {message}"));
                let _ = thread.join();
                return Err(if message.contains("unsupported") {
                    BmResult::Unsupported
                } else {
                    BmResult::Internal
                });
            }
            Err(_) => {
                logging::error("bridge start timed out waiting for the render device");
                return Err(BmResult::Internal);
            }
        };

        if cfg!(target_os = "macos")
            && config.output == abi::BmOutputKind::IoSurface
            && crate::unity_plugin::unity_renderer() != crate::unity_plugin::RENDERER_METAL
        {
            logging::emit(
                abi::BmLogLevel::Warning,
                &format!(
                    "Unity reports graphics API {} rather than Metal; shared output may not work",
                    crate::unity_plugin::unity_renderer()
                ),
            );
        }

        logging::emit(
            abi::BmLogLevel::Info,
            &format!(
                "bridge started: {}x{} with {} output buffers ({:?})",
                config.width,
                config.height,
                outputs.len(),
                config.output
            ),
        );

        Ok(Self {
            commands,
            thread: Some(thread),
            status,
            outputs,
            output_kind: config.output,
            pixels,
        })
    }

    pub fn submit(&self, snapshot: Snapshot) -> BmResult {
        if self.status.failed.load(Ordering::Acquire) {
            return BmResult::Internal;
        }

        self.status.in_flight.fetch_add(1, Ordering::AcqRel);

        if self
            .commands
            .send(Command::Snapshot(Box::new(snapshot)))
            .is_err()
        {
            self.status.in_flight.fetch_sub(1, Ordering::AcqRel);
            return BmResult::Internal;
        }

        BmResult::Ok
    }

    pub fn resource(&self, command: ResourceCommand) -> BmResult {
        if self.status.failed.load(Ordering::Acquire) {
            return BmResult::Internal;
        }

        if self.commands.send(Command::Resource(command)).is_err() {
            BmResult::Internal
        } else {
            BmResult::Ok
        }
    }

    pub fn frame_status(&self) -> BmFrameStatus {
        BmFrameStatus {
            completed_frame: self.status.completed_frame.load(Ordering::Acquire),
            completed_buffer: self.status.completed_buffer.load(Ordering::Acquire)
                % OUTPUT_BUFFER_COUNT,
            in_flight: self.status.in_flight.load(Ordering::Acquire),
        }
    }

    pub fn output(&self, index: u32) -> Option<BmOutputTexture> {
        self.outputs
            .get(usize::try_from(index).ok()?)
            .map(OutputHandles::describe)
    }

    pub fn latest_pixels(&self) -> Option<Pixels> {
        #[cfg(target_os = "macos")]
        if self.output_kind == abi::BmOutputKind::IoSurface {
            let status = self.frame_status();

            if status.completed_frame == 0 {
                return None;
            }

            let output = self.outputs.get(status.completed_buffer as usize)?;
            // SAFETY: the surface stays alive as long as the Bevy thread, which `self` owns.
            let data = unsafe {
                crate::output::iosurface::read_pixels(
                    output.io_surface,
                    output.width,
                    output.height,
                )
            }?;

            return Some(Pixels {
                size: bevy::math::UVec2::new(output.width, output.height),
                bytes_per_pixel: 4,
                data,
            });
        }

        self.pixels.0.lock().clone()
    }

    pub fn note_render_event(&self, _event_id: i32) {
        self.status.render_events.fetch_add(1, Ordering::AcqRel);
    }

    pub fn render_events(&self) -> u64 {
        self.status.render_events.load(Ordering::Acquire)
    }
}

impl Drop for Bridge {
    fn drop(&mut self) {
        let _ = self.commands.send(Command::Shutdown);

        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
        }
    }
}
