
use std::ffi::{CStr, CString};
use std::io;
use std::os::unix::ffi::OsStrExt as _;
use std::path::Path;
use std::time::{Duration, Instant};

use anyhow::{Result, anyhow, bail};

const POSIX_SPAWN_CLOEXEC_DEFAULT: libc::c_short = 0x4000;

const STANDARD_DESCRIPTORS: [libc::c_int; 3] = [0, 1, 2];

const MAX_ENVIRONMENT_ENTRIES: usize = 4096;

const REAP_POLL: Duration = Duration::from_millis(1);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ExitStatus {
    Exited(i32),
    Signalled(i32),
    Unknown,
}

impl ExitStatus {
    const fn from_wait(raw: libc::c_int) -> Self {
        if libc::WIFEXITED(raw) {
            Self::Exited(libc::WEXITSTATUS(raw))
        } else if libc::WIFSIGNALED(raw) {
            Self::Signalled(libc::WTERMSIG(raw))
        } else {
            Self::Unknown
        }
    }
}

#[derive(Debug)]
pub struct Helper {
    pid: libc::pid_t,
    status: Option<ExitStatus>,
}

impl Helper {
    pub fn spawn(exe: &Path, service: &CStr, segment: &CStr, cookie: u64) -> Result<Self> {
        let argv = argv_for(exe, service, segment, cookie)?;
        let borrowed: Vec<&CStr> = argv.iter().map(CString::as_c_str).collect();
        Self::spawn_argv(exe, &borrowed, &[])
    }

    pub fn spawn_argv(exe: &Path, argv: &[&CStr], extra_env: &[&CStr]) -> Result<Self> {
        if argv.is_empty() {
            bail!("argv must contain at least argv[0]");
        }
        let path = path_to_c(exe)?;

        let attributes = SpawnAttributes::new()?;
        let actions = FileActions::new()?;

        let mut raw_argv: Vec<*mut libc::c_char> =
            argv.iter().map(|item| item.as_ptr().cast_mut()).collect();
        raw_argv.push(std::ptr::null_mut());
        let mut raw_env = environment_with(extra_env);

        let mut pid: libc::pid_t = 0;
        let code = unsafe {
            libc::posix_spawn(
                &raw mut pid,
                path.as_ptr(),
                actions.as_ptr(),
                attributes.as_ptr(),
                raw_argv.as_mut_ptr(),
                raw_env.as_mut_ptr(),
            )
        };
        if code != 0 {
            return Err(io::Error::from_raw_os_error(code))
                .map_err(|error| anyhow!("posix_spawn({}) -> {error}", exe.display()));
        }
        if pid <= 0 {
            bail!("posix_spawn({}) reported success but no pid", exe.display());
        }

        Ok(Self { pid, status: None })
    }

    pub const fn pid(&self) -> libc::pid_t {
        self.pid
    }

    pub fn is_alive(&mut self) -> bool {
        if self.status.is_some() {
            return false;
        }
        let mut raw: libc::c_int = 0;
        let reaped = unsafe { libc::waitpid(self.pid, &raw mut raw, libc::WNOHANG) };
        if reaped == self.pid {
            self.status = Some(ExitStatus::from_wait(raw));
            return false;
        }
        if reaped < 0 {
            self.status = Some(ExitStatus::Unknown);
            return false;
        }
        true
    }

    pub fn exit_status(&mut self) -> Option<ExitStatus> {
        let _ignored = self.is_alive();
        self.status
    }

    pub fn wait_for_exit(&mut self, timeout_ms: u32) -> Option<ExitStatus> {
        let deadline = Instant::now().checked_add(Duration::from_millis(u64::from(timeout_ms)));
        loop {
            if !self.is_alive() {
                return self.status;
            }
            match deadline {
                Some(deadline) if Instant::now() < deadline => std::thread::sleep(REAP_POLL),
                _ => return None,
            }
        }
    }

    pub fn kill(&mut self) {
        if self.status.is_some() {
            return;
        }
        let _ = unsafe { libc::kill(self.pid, libc::SIGKILL) };
        let mut raw: libc::c_int = 0;
        let reaped = unsafe { libc::waitpid(self.pid, &raw mut raw, 0) };
        self.status = Some(if reaped == self.pid {
            ExitStatus::from_wait(raw)
        } else {
            ExitStatus::Unknown
        });
    }
}

impl Drop for Helper {
    fn drop(&mut self) {
        self.kill();
    }
}

pub fn argv_for(exe: &Path, service: &CStr, segment: &CStr, cookie: u64) -> Result<Vec<CString>> {
    Ok(vec![
        path_to_c(exe)?,
        CString::new("--service")?,
        service.to_owned(),
        CString::new("--segment")?,
        segment.to_owned(),
        CString::new("--cookie")?,
        CString::new(format!("{cookie:016x}"))?,
    ])
}

fn path_to_c(path: &Path) -> Result<CString> {
    CString::new(path.as_os_str().as_bytes())
        .map_err(|error| anyhow!("{} is not a usable path: {error}", path.display()))
}

fn environment_with(extra: &[&CStr]) -> Vec<*mut libc::c_char> {
    let mut out = Vec::with_capacity(extra.len().saturating_add(1));
    let base = process_environment();
    if !base.is_null() {
        let mut cursor = base;
        for _ in 0..MAX_ENVIRONMENT_ENTRIES {
            let entry = unsafe { *cursor };
            if entry.is_null() {
                break;
            }
            out.push(entry);
            cursor = unsafe { cursor.add(1) };
        }
    }
    for item in extra {
        out.push(item.as_ptr().cast_mut());
    }
    out.push(std::ptr::null_mut());
    out
}

unsafe extern "C" {
    fn _NSGetEnviron() -> *mut *mut *mut libc::c_char;
}

fn process_environment() -> *mut *mut libc::c_char {
    let slot = unsafe { _NSGetEnviron() };
    if slot.is_null() {
        return std::ptr::null_mut();
    }
    unsafe { *slot }
}

struct SpawnAttributes(libc::posix_spawnattr_t);

impl SpawnAttributes {
    fn new() -> Result<Self> {
        let mut raw: libc::posix_spawnattr_t = unsafe { std::mem::zeroed() };
        let code = unsafe { libc::posix_spawnattr_init(&raw mut raw) };
        if code != 0 {
            return Err(io::Error::from_raw_os_error(code))
                .map_err(|error| anyhow!("posix_spawnattr_init -> {error}"));
        }
        let mut attributes = Self(raw);
        let code = unsafe {
            libc::posix_spawnattr_setflags(&raw mut attributes.0, POSIX_SPAWN_CLOEXEC_DEFAULT)
        };
        if code != 0 {
            return Err(io::Error::from_raw_os_error(code))
                .map_err(|error| anyhow!("posix_spawnattr_setflags -> {error}"));
        }
        Ok(attributes)
    }

    const fn as_ptr(&self) -> *const libc::posix_spawnattr_t {
        &raw const self.0
    }
}

impl Drop for SpawnAttributes {
    fn drop(&mut self) {
        let _ = unsafe { libc::posix_spawnattr_destroy(&raw mut self.0) };
    }
}

struct FileActions(libc::posix_spawn_file_actions_t);

impl FileActions {
    fn new() -> Result<Self> {
        let mut raw: libc::posix_spawn_file_actions_t = unsafe { std::mem::zeroed() };
        let code = unsafe { libc::posix_spawn_file_actions_init(&raw mut raw) };
        if code != 0 {
            return Err(io::Error::from_raw_os_error(code))
                .map_err(|error| anyhow!("posix_spawn_file_actions_init -> {error}"));
        }
        let mut actions = Self(raw);
        for descriptor in STANDARD_DESCRIPTORS {
            let code = unsafe {
                libc::posix_spawn_file_actions_adddup2(&raw mut actions.0, descriptor, descriptor)
            };
            if code != 0 {
                return Err(io::Error::from_raw_os_error(code))
                    .map_err(|error| anyhow!("posix_spawn_file_actions_adddup2 -> {error}"));
            }
        }
        Ok(actions)
    }

    const fn as_ptr(&self) -> *const libc::posix_spawn_file_actions_t {
        &raw const self.0
    }
}

impl Drop for FileActions {
    fn drop(&mut self) {
        let _ = unsafe { libc::posix_spawn_file_actions_destroy(&raw mut self.0) };
    }
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;

    fn c(text: &str) -> CString {
        CString::new(text).unwrap()
    }

    #[test]
    fn argv_is_the_frozen_shape() {
        let argv = argv_for(
            Path::new("/opt/uuav-ipc-helper"),
            &c("com.decentraland.uuav.helper.42.00000000cafef00d"),
            &c("/uuav.0000002a.cafef00d"),
            0xcafe_f00d,
        )
        .unwrap();
        let rendered: Vec<&str> = argv
            .iter()
            .map(|item| item.to_str().unwrap())
            .collect::<Vec<_>>();
        assert_eq!(
            rendered,
            vec![
                "/opt/uuav-ipc-helper",
                "--service",
                "com.decentraland.uuav.helper.42.00000000cafef00d",
                "--segment",
                "/uuav.0000002a.cafef00d",
                "--cookie",
                "00000000cafef00d",
            ]
        );
    }

    #[test]
    fn spawning_something_that_is_not_there_fails_rather_than_hangs() {
        let exe = Path::new("/nonexistent/uuav-ipc-helper");
        assert!(Helper::spawn_argv(exe, &[&c("uuav-ipc-helper")], &[]).is_err());
    }

    #[test]
    fn an_empty_argv_is_refused() {
        assert!(Helper::spawn_argv(Path::new("/bin/sh"), &[], &[]).is_err());
    }
}
