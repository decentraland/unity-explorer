//! The macOS Seatbelt sandbox the helper applies to itself. FFmpeg demuxes
//! attacker-controlled media, so before the IPC channel is adopted and any
//! untrusted byte arrives, the helper drops to a deny-by-default profile
//! (helper.sb, embedded at compile time) that leaves only: system library
//! reads, Metal/IOSurface/VideoToolbox user clients, outbound sockets plus
//! the DNS and trustd brokers, and mach-lookup of exactly the one
//! per-session bootstrap service the client registered. No process-exec,
//! no process-fork, no persistent file writes: a decoder exploit cannot
//! launch a payload or persist files on disk.
//!
//! Unlike Windows, where restrictions must bind from the parent before the
//! first helper instruction (see the client's `sandbox` module), Seatbelt
//! is applied from inside the process; everything that runs before it,
//! argument parsing, touches no untrusted data. Fail closed: on any error
//! the caller exits without touching the channel.
//!
//! `sandbox_init_with_parameters` is exported by libSystem but absent from
//! public headers (Chromium, Firefox and WebKit all ship on it), hence the
//! manual declaration, same as `bootstrap_register` in uuav-ipc. The
//! parameter array keeps per-session values out of the profile source, so
//! the token is never spliced into Scheme text.

use anyhow::{Context as _, Result, bail};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

const PROFILE: &str = include_str!("../helper.sb");

// sandbox.h private API surface (libSystem)
unsafe extern "C" {
    /// `flags` 0 interprets `profile` as SBPL source text.
    fn sandbox_init_with_parameters(
        profile: *const c_char,
        flags: u64,
        parameters: *const *const c_char,
        errorbuf: *mut *mut c_char,
    ) -> c_int;
    fn sandbox_free_error(errorbuf: *mut c_char);
}

/// Applies helper.sb to the current process. `allow_media_file_read` opens
/// broad file reads for the Editor's `file:` protocol; writes and exec
/// stay denied regardless.
pub fn apply(service: &str, allow_media_file_read: bool) -> Result<()> {
    // Seatbelt path filters match fully resolved paths
    let exe = std::env::current_exe()
        .and_then(|p| p.canonicalize())
        .context("resolve helper executable path")?;
    let plugin_dir = exe
        .parent()
        .context("helper executable has no parent directory")?
        .as_os_str()
        .to_str()
        .context("plugin directory path is not valid UTF-8")?
        .to_owned();
    let home = std::env::var("HOME").context("HOME is not set")?;

    let storage = [
        CString::new("SERVICE_NAME"),
        CString::new(service),
        CString::new("PLUGIN_DIR"),
        CString::new(plugin_dir),
        CString::new("HOME"),
        CString::new(home),
        CString::new("ALLOW_MEDIA_FILE_READ"),
        CString::new(if allow_media_file_read { "1" } else { "0" }),
    ];
    let mut params = Vec::with_capacity(storage.len() + 1);
    for entry in &storage {
        params.push(
            entry
                .as_ref()
                .map_err(|_| anyhow::anyhow!("sandbox parameter contains NUL"))?
                .as_ptr(),
        );
    }
    params.push(std::ptr::null());

    let profile = CString::new(PROFILE).context("profile contains NUL")?;
    let mut error: *mut c_char = std::ptr::null_mut();
    let rc = unsafe {
        sandbox_init_with_parameters(profile.as_ptr(), 0, params.as_ptr(), &mut error)
    };
    if rc != 0 {
        let detail = if error.is_null() {
            format!("code {rc}")
        } else {
            let message = unsafe { CStr::from_ptr(error) }
                .to_string_lossy()
                .into_owned();
            unsafe { sandbox_free_error(error) };
            message
        };
        bail!("sandbox_init_with_parameters failed: {detail}");
    }
    Ok(())
}
