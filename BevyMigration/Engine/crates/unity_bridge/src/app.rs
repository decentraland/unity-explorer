//! The Bevy `App` on the bridge thread: headless plugin set, output buffers, and one `update()` per
//! snapshot so that Bevy renders frame N while the host composites N-1 (plan § 8 "Topology").

use crate::{
    bridge::{Command, Status},
    logging,
    output::{self, OutputBuffers, OutputHandles},
};
use abi::{BmInitConfig, BmOutputKind, OUTPUT_BUFFER_COUNT};
use bevy::{
    app::PluginsState,
    asset::AssetPlugin,
    core_pipeline::CorePipelinePlugin,
    pbr::PbrPlugin,
    prelude::*,
    render::{
        RenderPlugin,
        camera::ManualTextureViewHandle,
        render_resource::{Maintain, PipelineCompilationMode},
        renderer::RenderDevice,
        settings::{Backends, PowerPreference, RenderCreation, WgpuSettings},
        texture::ImagePlugin,
    },
    window::{ExitCondition, WindowPlugin},
};
use render::{
    BevyMigrationRenderPlugin, PendingSnapshot,
    readback::{LatestPixels, ReadbackPlugin, ReadbackSource},
    resources::PendingResources,
};
use std::{
    panic::{AssertUnwindSafe, catch_unwind},
    sync::{
        Arc,
        atomic::Ordering,
        mpsc::{Receiver, Sender},
    },
};

pub struct ThreadArgs {
    pub config: BmInitConfig,
    pub commands: Receiver<Command>,
    pub status: Arc<Status>,
    pub ready: Sender<Result<Vec<OutputHandles>, String>>,
    pub pixels: LatestPixels,
}

pub fn run(args: ThreadArgs) {
    let ThreadArgs {
        config,
        commands,
        status,
        ready,
        pixels,
    } = args;

    let started = catch_unwind(AssertUnwindSafe(|| start(config, &pixels)));

    let (mut app, buffers) = match started {
        Ok(Ok(parts)) => parts,
        Ok(Err(message)) => {
            let _ = ready.send(Err(message));
            return;
        }
        Err(payload) => {
            let _ = ready.send(Err(format!(
                "panic while starting Bevy: {}",
                logging::panic_message(payload.as_ref())
            )));
            return;
        }
    };

    if ready.send(Ok(buffers.handles())).is_err() {
        return;
    }

    let device = app.world().resource::<RenderDevice>().clone();
    let count = u64::from(OUTPUT_BUFFER_COUNT);

    while let Ok(command) = commands.recv() {
        let snapshot = match command {
            Command::Snapshot(snapshot) => snapshot,
            Command::Resource(resource) => {
                app.world_mut()
                    .resource_mut::<PendingResources>()
                    .0
                    .push(resource);
                continue;
            }
            Command::Shutdown => break,
        };

        let frame = snapshot.frame;
        let buffer = usize::try_from(frame % count).unwrap_or(0);

        let rendered = catch_unwind(AssertUnwindSafe(|| {
            if config.output == BmOutputKind::Offscreen
                && let Some(output) = buffers.buffers.get(buffer)
            {
                app.world_mut().insert_resource(ReadbackSource {
                    texture: output.texture.clone(),
                    size: UVec2::new(config.width, config.height),
                    format: output::OUTPUT_FORMAT,
                });
            }

            app.world_mut().resource_mut::<PendingSnapshot>().0 = Some(*snapshot);
            app.update();
            // Bevy's own thread may block: the host never does (plan § 8 "Synchronisation").
            let _ = device.poll(Maintain::Wait);
            render::readback::collect_latest(app.world_mut());
        }));

        status.in_flight.fetch_sub(1, Ordering::AcqRel);

        match rendered {
            Ok(()) => {
                status
                    .completed_buffer
                    .store(u32::try_from(buffer).unwrap_or(0), Ordering::Release);
                status.completed_frame.store(frame, Ordering::Release);
            }
            Err(payload) => {
                logging::error(&format!(
                    "Bevy frame {frame} panicked: {}",
                    logging::panic_message(payload.as_ref())
                ));
                status.failed.store(true, Ordering::Release);
                break;
            }
        }
    }

    drop(buffers);
    drop(app);
}

fn start(config: BmInitConfig, pixels: &LatestPixels) -> Result<(App, OutputBuffers), String> {
    let mut app = App::new();

    app.add_plugins((
        bevy::app::TaskPoolPlugin::default(),
        bevy::diagnostic::FrameCountPlugin,
        bevy::time::TimePlugin,
        AssetPlugin::default(),
        WindowPlugin {
            primary_window: None,
            exit_condition: ExitCondition::DontExit,
            close_when_requested: false,
        },
        RenderPlugin {
            render_creation: RenderCreation::Automatic(WgpuSettings {
                backends: Some(backends()),
                power_preference: PowerPreference::HighPerformance,
                ..default()
            }),
            pipeline_compilation_mode: PipelineCompilationMode::Sync,
            ..default()
        },
        ImagePlugin::default(),
        CorePipelinePlugin,
        PbrPlugin::default(),
        BevyMigrationRenderPlugin,
        ReadbackPlugin,
    ));

    app.insert_resource(pixels.clone());

    while app.plugins_state() == PluginsState::Adding {
        bevy::tasks::tick_global_task_pools_on_main_thread();
        std::thread::sleep(std::time::Duration::from_millis(1));
    }

    app.finish();
    app.cleanup();

    let device = app
        .world()
        .get_resource::<RenderDevice>()
        .cloned()
        .ok_or_else(|| "unsupported: no render device (no compatible GPU adapter)".to_owned())?;

    let size = UVec2::new(config.width, config.height);
    let buffers = output::create(config.output, &device, size, OUTPUT_BUFFER_COUNT)?;

    let views = buffers
        .buffers
        .iter()
        .enumerate()
        .map(|(index, buffer)| {
            (
                ManualTextureViewHandle(u32::try_from(index).unwrap_or(0)),
                buffer.manual_view(size),
            )
        })
        .collect();

    render::install_output_views(app.world_mut(), views, size);
    Ok((app, buffers))
}

fn backends() -> Backends {
    if cfg!(target_os = "macos") {
        Backends::METAL
    } else {
        Backends::PRIMARY
    }
}
