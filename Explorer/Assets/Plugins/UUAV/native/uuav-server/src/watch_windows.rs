//! Orphan prevention: exit as soon as the parent (Unity) process is gone.

use anyhow::Context as _;
use std::process;
use std::thread;
use windows::Win32::Foundation::CloseHandle;
use windows::Win32::System::Threading::{
    INFINITE, OpenProcess, PROCESS_SYNCHRONIZE, WaitForSingleObject,
};

/// Blocks a dedicated thread on the parent process handle and exits the
/// whole helper when it is signaled. The client passes a wait-only handle
/// by inheritance (`--parent-handle`) because this sandboxed low-integrity
/// process may not `OpenProcess` its medium-integrity parent; the pid
/// fallback keeps hand-run helpers working. Open failure (parent already
/// gone) exits immediately.
pub fn exit_when_parent_dies(parent_pid: u32, inherited: Option<u64>) -> anyhow::Result<()> {
    let raw = if let Some(value) = inherited {
        anyhow::ensure!(value != 0, "--parent-handle is a null handle");
        value as usize
    } else {
        let handle = unsafe { OpenProcess(PROCESS_SYNCHRONIZE, false, parent_pid) }
            .context("parent process is not observable (already exited?)")?;
        handle.0 as usize
    };

    // HANDLE is a raw kernel handle; moving the numeric value into the
    // thread is sound, the thread is its only user until process exit.
    thread::Builder::new()
        .name("uuav-parent-watch".into())
        .spawn(move || {
            let handle = windows::Win32::Foundation::HANDLE(raw as *mut _);
            unsafe { WaitForSingleObject(handle, INFINITE) };
            unsafe { _ = CloseHandle(handle) };
            process::exit(0);
        })
        .context("spawn parent watch thread")?;

    Ok(())
}
