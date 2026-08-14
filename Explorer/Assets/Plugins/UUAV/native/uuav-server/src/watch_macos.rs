//! Orphan prevention: exit as soon as the parent (Unity) process is gone.

use anyhow::{Context as _, ensure};
use std::process;
use std::thread;

/// Watches the parent pid with a kqueue `EVFILT_PROC/NOTE_EXIT` filter from a
/// dedicated thread and exits the whole helper when it fires. Registration
/// failure (parent already gone) exits immediately.
pub fn exit_when_parent_dies(parent_pid: u32) -> anyhow::Result<()> {
    let kq = unsafe { libc::kqueue() };
    ensure!(kq >= 0, "kqueue() failed");

    let change = libc::kevent {
        ident: parent_pid as usize,
        filter: libc::EVFILT_PROC,
        flags: libc::EV_ADD | libc::EV_ENABLE,
        fflags: libc::NOTE_EXIT,
        data: 0,
        udata: std::ptr::null_mut(),
    };
    let registered = unsafe {
        libc::kevent(
            kq,
            &change,
            1,
            std::ptr::null_mut(),
            0,
            std::ptr::null(),
        )
    };
    ensure!(registered >= 0, "parent process is not observable (already exited?)");

    thread::Builder::new()
        .name("uuav-parent-watch".into())
        .spawn(move || {
            let mut event = unsafe { std::mem::zeroed::<libc::kevent>() };
            // blocks until the parent exits (or the kqueue errors out)
            unsafe { libc::kevent(kq, std::ptr::null(), 0, &mut event, 1, std::ptr::null()) };
            process::exit(0);
        })
        .context("spawn parent watch thread")?;

    Ok(())
}
