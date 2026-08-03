//! `uuav-helper`: hosts the unchanged uuav core in its own process and
//! adapts the IPC protocol onto the core's C API. Spawned by the `uuav`
//! client dylib with `--channel <inherited handle/fd> --token <uuid>
//! --parent-pid <pid>` (plus, on Windows, `--parent-handle <inherited
//! wait-only handle>`; the process itself runs sandboxed — restricted
//! low-integrity token, single-process job, mitigation policy — see the
//! client's `sandbox` module).

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
#[cfg(target_os = "windows")]
#[path = "video_windows.rs"]
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
use uuav_ipc::protocol;

struct Args {
    /// The inherited channel end: handle value (Windows) / fd (macOS).
    channel: String,
    token: String,
    parent_pid: u32,
    /// Inherited wait-only handle to the parent, for the orphan watch —
    /// the sandboxed helper cannot `OpenProcess` its higher-integrity
    /// parent. Absent when the helper is run by hand.
    #[cfg(target_os = "windows")]
    parent_handle: Option<u64>,
    /// The client's registered mach service for IOSurface port transfer.
    #[cfg(target_os = "macos")]
    service: String,
}

fn parse_args() -> anyhow::Result<Args> {
    let mut channel = None;
    let mut token = None;
    let mut parent_pid = None;
    #[cfg(target_os = "windows")]
    let mut parent_handle = None;
    #[cfg(target_os = "macos")]
    let mut service = None;

    let mut args = std::env::args().skip(1);
    while let Some(flag) = args.next() {
        let mut value = || {
            args.next()
                .with_context(|| format!("missing value for {flag}"))
        };
        match flag.as_str() {
            "--channel" => channel = Some(value()?),
            "--token" => token = Some(value()?),
            "--parent-pid" => parent_pid = Some(value()?.parse::<u32>()?),
            #[cfg(target_os = "windows")]
            "--parent-handle" => parent_handle = Some(value()?.parse::<u64>()?),
            #[cfg(target_os = "macos")]
            "--service" => service = Some(value()?),
            other => bail!("unknown argument: {other}"),
        }
    }

    Ok(Args {
        channel: channel.context("--channel is required")?,
        token: token.context("--token is required")?,
        parent_pid: parent_pid.context("--parent-pid is required")?,
        #[cfg(target_os = "windows")]
        parent_handle,
        #[cfg(target_os = "macos")]
        service: service.context("--service is required")?,
    })
}

fn main() -> anyhow::Result<()> {
    let args = parse_args()?;

    // If the parent dies for any reason (crash, force-quit), never outlive
    // it. Defense-in-depth on Windows: the client's kill-on-close job
    // object already terminates this process with the parent.
    #[cfg(target_os = "windows")]
    watch::exit_when_parent_dies(args.parent_pid, args.parent_handle)?;
    #[cfg(target_os = "macos")]
    watch::exit_when_parent_dies(args.parent_pid)?;

    let mut channel =
        uuav_ipc::channel::Channel::from_arg(&args.channel).context("adopt inherited channel")?;

    channel.send(&protocol::ToClient::Hello {
        token: args.token,
        abi: protocol::ABI_VERSION.to_owned(),
        pid: std::process::id(),
    })?;

    adapter::run(
        &mut channel,
        #[cfg(target_os = "macos")]
        &args.service,
    )
}
