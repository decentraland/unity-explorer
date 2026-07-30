//! `uuav-helper`: hosts the unchanged uuav core in its own process and
//! adapts the IPC protocol onto the core's C API. Spawned by the `uuav`
//! client dylib with `--endpoint <zmq> --token <uuid> --parent-pid <pid>`.

#![warn(clippy::all, clippy::pedantic, clippy::nursery)]
#![deny(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::indexing_slicing,
    clippy::todo,
    clippy::dbg_macro
)]
// borrow_as_ptr / cast truncation / doc_markdown: C out-params, fixed-width
// FFI ids, and "IOSurface" in prose are pervasive at the core's boundary,
// same allowances as the core crate itself
#![allow(
    clippy::missing_errors_doc,
    clippy::uninlined_format_args,
    clippy::borrow_as_ptr,
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::doc_markdown
)]

mod adapter;
mod state;

#[cfg(target_os = "macos")]
#[path = "device_macos.rs"]
mod device;
#[cfg(target_os = "windows")]
#[path = "device_windows.rs"]
mod device;

#[cfg(target_os = "macos")]
#[path = "video_macos.rs"]
mod video;

#[cfg(target_os = "macos")]
#[path = "watch_macos.rs"]
mod watch;
#[cfg(target_os = "windows")]
#[path = "watch_windows.rs"]
mod watch;

#[cfg(not(any(target_os = "windows", target_os = "macos")))]
compile_error!("uuav-helper supports Windows (D3D11) and macOS (Metal) only");

use anyhow::{Context as _, bail};
use uuav_ipc::{protocol, socket, zmq};

struct Args {
    endpoint: String,
    token: String,
    parent_pid: u32,
    /// The client's registered mach service for IOSurface port transfer.
    #[cfg(target_os = "macos")]
    service: String,
}

fn parse_args() -> anyhow::Result<Args> {
    let mut endpoint = None;
    let mut token = None;
    let mut parent_pid = None;
    #[cfg(target_os = "macos")]
    let mut service = None;

    let mut args = std::env::args().skip(1);
    while let Some(flag) = args.next() {
        let mut value = || {
            args.next()
                .with_context(|| format!("missing value for {flag}"))
        };
        match flag.as_str() {
            "--endpoint" => endpoint = Some(value()?),
            "--token" => token = Some(value()?),
            "--parent-pid" => parent_pid = Some(value()?.parse::<u32>()?),
            #[cfg(target_os = "macos")]
            "--service" => service = Some(value()?),
            other => bail!("unknown argument: {other}"),
        }
    }

    Ok(Args {
        endpoint: endpoint.context("--endpoint is required")?,
        token: token.context("--token is required")?,
        parent_pid: parent_pid.context("--parent-pid is required")?,
        #[cfg(target_os = "macos")]
        service: service.context("--service is required")?,
    })
}

fn main() -> anyhow::Result<()> {
    let args = parse_args()?;

    // If the parent dies for any reason (crash, force-quit), never outlive it.
    watch::exit_when_parent_dies(args.parent_pid)?;

    let context = zmq::Context::new();
    let socket = socket::dealer(&context)?;
    socket.connect(&args.endpoint).context("connect endpoint")?;

    socket::send(
        &socket,
        &protocol::ToClient::Hello {
            token: args.token,
            abi: protocol::ABI_VERSION.to_owned(),
            pid: std::process::id(),
        },
    )?;

    adapter::run(
        &socket,
        #[cfg(target_os = "macos")]
        &args.service,
    )
}
