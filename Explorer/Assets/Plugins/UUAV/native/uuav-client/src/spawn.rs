//! Locates and spawns the `uuav-helper` executable that ships next to this
//! dylib (same directory: that is also how the FFmpeg libraries resolve for
//! the helper, via `@loader_path` rpath on macOS / DLL search on Windows).

use anyhow::{Context as _, Result};
use std::path::PathBuf;
use std::process::{Child, Command};

#[cfg(target_os = "macos")]
const HELPER_FILE: &str = "uuav-helper";
#[cfg(target_os = "windows")]
const HELPER_FILE: &str = "uuav-helper.exe";

pub fn spawn_helper(endpoint: &str, token: &str) -> Result<Child> {
    let path = helper_path()?;
    let mut command = Command::new(&path);
    command
        .arg("--endpoint")
        .arg(endpoint)
        .arg("--token")
        .arg(token)
        .arg("--parent-pid")
        .arg(std::process::id().to_string());
    // the client-registered mach service the helper sends IOSurface ports to
    #[cfg(target_os = "macos")]
    command
        .arg("--service")
        .arg(uuav_ipc::mach_channel::service_name(token));
    command
        .spawn()
        .with_context(|| format!("failed to spawn {}", path.display()))
}

fn helper_path() -> Result<PathBuf> {
    let mut dir = own_dylib_path()?;
    dir.pop();
    Ok(dir.join(HELPER_FILE))
}

/// Path of this loaded dylib, resolved from one of its own symbols.
#[cfg(target_os = "macos")]
fn own_dylib_path() -> Result<PathBuf> {
    use std::ffi::CStr;
    use std::os::raw::c_void;

    let mut info: libc::Dl_info = unsafe { std::mem::zeroed() };
    let symbol = own_dylib_path as *const c_void;
    anyhow::ensure!(
        unsafe { libc::dladdr(symbol, &mut info) } != 0 && !info.dli_fname.is_null(),
        "dladdr failed to resolve the uuav dylib path"
    );
    let path = unsafe { CStr::from_ptr(info.dli_fname) };
    Ok(PathBuf::from(path.to_string_lossy().into_owned()))
}

#[cfg(target_os = "windows")]
fn own_dylib_path() -> Result<PathBuf> {
    use windows::Win32::Foundation::HMODULE;
    use windows::Win32::System::LibraryLoader::{
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        GetModuleFileNameW, GetModuleHandleExW,
    };
    use windows::core::PCWSTR;

    let mut module = HMODULE::default();
    unsafe {
        GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            PCWSTR(own_dylib_path as *const u16),
            &mut module,
        )
    }
    .context("GetModuleHandleExW failed for the uuav dylib")?;

    let mut buffer = [0u16; 1024];
    let len = unsafe { GetModuleFileNameW(Some(module), &mut buffer) } as usize;
    anyhow::ensure!(len > 0 && len < buffer.len(), "GetModuleFileNameW failed");
    let path = String::from_utf16_lossy(buffer.get(..len).unwrap_or_default());
    Ok(PathBuf::from(path))
}
