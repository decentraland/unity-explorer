//! Orphan prevention: exit as soon as the parent (Unity) process is gone.

use anyhow::{Context as _, ensure};
use std::process;
use std::thread;

/// Watches the parent with a pidfd from a dedicated thread and exits the
/// whole helper when it becomes readable (process exit). Deliberately
/// not `PR_SET_PDEATHSIG`: that fires when the spawning *thread* of the
/// parent exits, and the client spawns helpers from short-lived init and
/// recovery-worker threads — a latent self-kill. The `getppid` re-check
/// closes the parent-died-before-`pidfd_open` race (pid reuse would
/// otherwise hand us an unrelated process); the helper is a direct
/// child, so a dead parent reparents us and `getppid` stops matching.
pub fn exit_when_parent_dies(parent_pid: u32) -> anyhow::Result<()> {
    let pidfd = unsafe {
        libc::syscall(
            libc::SYS_pidfd_open,
            libc::pid_t::try_from(parent_pid).context("parent pid")?,
            0,
        )
    };
    ensure!(pidfd >= 0, "parent process is not observable (already exited?)");
    let pidfd = libc::c_int::try_from(pidfd).context("pidfd")?;

    let ppid = unsafe { libc::getppid() };
    ensure!(
        u32::try_from(ppid).is_ok_and(|ppid| ppid == parent_pid),
        "parent already exited (reparented to {ppid})"
    );

    thread::Builder::new()
        .name("uuav-parent-watch".into())
        .spawn(move || {
            let mut pollfd = libc::pollfd {
                fd: pidfd,
                events: libc::POLLIN,
                revents: 0,
            };
            // blocks until the parent exits (POLLIN on a pidfd); EINTR
            // just re-polls
            loop {
                let ready = unsafe { libc::poll(&mut pollfd, 1, -1) };
                if ready > 0 {
                    process::exit(0);
                }
            }
        })
        .context("spawn parent watch thread")?;

    Ok(())
}
