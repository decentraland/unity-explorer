//! Orphan prevention: exit as soon as the parent (Unity) process is gone.

use anyhow::Context as _;
use std::process;
use std::thread;
use windows::Win32::Foundation::CloseHandle;
use windows::Win32::System::Threading::{
    INFINITE, OpenProcess, PROCESS_SYNCHRONIZE, WaitForSingleObject,
};

/// Blocks a dedicated thread on the parent process handle and exits the
/// whole helper when it is signaled. Open failure (parent already gone)
/// exits immediately.
pub fn exit_when_parent_dies(parent_pid: u32) -> anyhow::Result<()> {
    let handle = unsafe { OpenProcess(PROCESS_SYNCHRONIZE, false, parent_pid) }
        .context("parent process is not observable (already exited?)")?;

    // HANDLE is a raw kernel handle; moving the numeric value into the
    // thread is sound, the thread is its only user until process exit.
    let raw = handle.0 as isize;
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
