//! Locates and spawns the `uuav-helper` executable that ships next to this
//! dylib (same directory: that is also how the FFmpeg libraries resolve for
//! the helper, via `@loader_path` rpath on macOS / DLL search on Windows).
//!
//! The helper's channel end travels by inheritance: on Windows a raw
//! `CreateProcessAsUserW` with a `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`
//! restricts inheritance to exactly the pipe handle plus a wait-only
//! parent handle; on macOS the socketpair fd is simply left non-cloexec.
//! Either way the parent's copy closes right after the spawn (the
//! [`ChildHandoff`] drop), so helper death is observable as EOF on the
//! channel.
//!
//! On Windows the helper is sandboxed at spawn — restricted low-integrity
//! token, single-process kill-on-close job object, process mitigation
//! policy — see [`crate::sandbox`]. On macOS the helper locks itself down
//! under a deny-by-default Seatbelt profile as the first act of its main
//! (the server's `sandbox` module); `allow_file_read` is the only
//! sandbox-relevant spawn input, opening broad file reads for the
//! Editor's `file:` protocol.

use anyhow::{Context as _, Result};
use std::path::PathBuf;
use uuav_ipc::channel::ChildHandoff;

#[cfg(target_os = "macos")]
const HELPER_FILE: &str = "uuav-helper";
#[cfg(target_os = "windows")]
const HELPER_FILE: &str = "uuav-helper.exe";

#[cfg(target_os = "macos")]
pub fn spawn_helper(
    handoff: ChildHandoff,
    token: &str,
    allow_file_read: bool,
) -> Result<HelperChild> {
    let path = helper_path()?;
    let mut command = std::process::Command::new(&path);
    command
        .arg("--channel")
        .arg(handoff.arg())
        .arg("--token")
        .arg(token)
        .arg("--parent-pid")
        .arg(std::process::id().to_string())
        // the client-registered mach service the helper sends IOSurface
        // ports to
        .arg("--service")
        .arg(uuav_ipc::mach_channel::service_name(token));
    if allow_file_read {
        command.arg("--allow-file-read");
    }
    let child = command
        .spawn()
        .with_context(|| format!("failed to spawn {}", path.display()))?;
    drop(handoff);
    Ok(HelperChild(child))
}

/// The spawned helper process. Same trio the recovery worker and deinit
/// used on `std::process::Child`; on Windows it wraps the raw process
/// handle because the spawn itself is a raw `CreateProcessW`.
#[cfg(target_os = "macos")]
pub struct HelperChild(std::process::Child);

#[cfg(target_os = "macos")]
impl HelperChild {
    /// Kernel-truth pid of the spawned helper; the mach surface receiver
    /// authenticates senders against it.
    pub fn id(&self) -> u32 {
        self.0.id()
    }

    pub fn try_wait(&mut self) -> Result<Option<HelperExitStatus>> {
        Ok(self
            .0
            .try_wait()
            .context("poll helper process")?
            .map(HelperExitStatus))
    }

    pub fn kill(&mut self) {
        _ = self.0.kill();
    }

    pub fn wait(&mut self) {
        _ = self.0.wait();
    }
}

#[cfg(target_os = "macos")]
pub struct HelperExitStatus(std::process::ExitStatus);

#[cfg(target_os = "macos")]
impl std::fmt::Display for HelperExitStatus {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.0.fmt(f)
    }
}

#[cfg(target_os = "windows")]
pub fn spawn_helper(handoff: ChildHandoff, token: &str) -> Result<HelperChild> {
    use crate::sandbox;
    use windows::Win32::System::Threading::{
        CREATE_NO_WINDOW, CREATE_SUSPENDED, CreateProcessAsUserW,
        DeleteProcThreadAttributeList, EXTENDED_STARTUPINFO_PRESENT,
        InitializeProcThreadAttributeList, LPPROC_THREAD_ATTRIBUTE_LIST,
        PROC_THREAD_ATTRIBUTE_HANDLE_LIST, PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY,
        PROCESS_INFORMATION, STARTUPINFOEXW, TerminateProcess, UpdateProcThreadAttribute,
    };
    use windows::core::{PCWSTR, PWSTR};

    let path = helper_path()?;
    let restricted_token = sandbox::restricted_low_integrity_token()?;
    let job = sandbox::helper_job()?;
    // wait-only handle for the helper's orphan watch; the inheritable
    // duplicate closes with this guard once the helper holds its copy
    let parent = sandbox::inheritable_parent_handle()?;

    let (application, mut command_line) = wide_command_line(&path, &handoff, token, &parent);

    // attribute list restricting inheritance to exactly the pipe + parent
    // handles (bInheritHandles must be TRUE for the list to apply, and the
    // list keeps every other inheritable handle out of the helper) and
    // carrying the mitigation policy
    let mut size = 0usize;
    _ = unsafe { InitializeProcThreadAttributeList(None, 2, None, &mut size) };
    anyhow::ensure!(size > 0, "proc-thread attribute list size query failed");
    let mut backing = vec![0u8; size];
    let list = LPPROC_THREAD_ATTRIBUTE_LIST(backing.as_mut_ptr().cast());
    unsafe { InitializeProcThreadAttributeList(Some(list), 2, None, &mut size) }
        .context("initialize proc-thread attribute list")?;

    let inherited = [handoff.raw_handle(), parent.raw()];
    let mitigation = [sandbox::MITIGATION_POLICY];
    let spawned = unsafe {
        UpdateProcThreadAttribute(
            list,
            0,
            PROC_THREAD_ATTRIBUTE_HANDLE_LIST as usize,
            Some(inherited.as_ptr().cast()),
            std::mem::size_of_val(&inherited),
            None,
            None,
        )
        .context("set inherited handle list")
        .and_then(|()| {
            UpdateProcThreadAttribute(
                list,
                0,
                PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY as usize,
                Some(mitigation.as_ptr().cast()),
                std::mem::size_of_val(&mitigation),
                None,
                None,
            )
            .context("set process mitigation policy")
        })
        .and_then(|()| {
            let startup = STARTUPINFOEXW {
                StartupInfo: windows::Win32::System::Threading::STARTUPINFOW {
                    cb: u32::try_from(std::mem::size_of::<STARTUPINFOEXW>())
                        .context("STARTUPINFOEXW size")?,
                    ..Default::default()
                },
                lpAttributeList: list,
            };
            let mut process_info = PROCESS_INFORMATION::default();
            // suspended: the job limits must bind before the first helper
            // instruction runs
            CreateProcessAsUserW(
                Some(restricted_token.raw()),
                PCWSTR(application.as_ptr()),
                Some(PWSTR(command_line.as_mut_ptr())),
                None,
                None,
                true,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW | CREATE_SUSPENDED,
                None,
                PCWSTR::null(),
                &startup.StartupInfo,
                &mut process_info,
            )
            .with_context(|| format!("failed to spawn {}", path.display()))?;
            Ok(process_info)
        })
    };
    unsafe { DeleteProcThreadAttributeList(list) };
    // parent's copy of the helper's pipe end closes here (also on the
    // error path): helper death is observable as a broken pipe
    drop(handoff);

    let process_info = spawned?;
    if let Err(e) = release_into_job(&job, &process_info) {
        unsafe {
            _ = TerminateProcess(process_info.hProcess, 1);
            _ = windows::Win32::Foundation::CloseHandle(process_info.hProcess);
        }
        return Err(e);
    }
    Ok(HelperChild {
        process: process_info.hProcess,
        _job: job,
    })
}

/// NUL-terminated UTF-16 exe path and command line. All argument values
/// are decimal digits / uuid hex, so only the exe path needs quoting.
#[cfg(target_os = "windows")]
fn wide_command_line(
    path: &std::path::Path,
    handoff: &ChildHandoff,
    token: &str,
    parent: &crate::sandbox::OwnedHandle,
) -> (Vec<u16>, Vec<u16>) {
    use std::os::windows::ffi::OsStrExt as _;

    let command_line = format!(
        "\"{}\" --channel {} --token {token} --parent-pid {} --parent-handle {}",
        path.display(),
        handoff.arg(),
        std::process::id(),
        parent.raw().0 as usize,
    );
    let application = path
        .as_os_str()
        .encode_wide()
        .chain(std::iter::once(0))
        .collect();
    let command_line = command_line
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();
    (application, command_line)
}

/// Binds the suspended helper into its job and releases its main thread —
/// the job limits are in force before the helper's first instruction. The
/// thread handle closes on both paths; a failure leaves the process for
/// the caller to terminate.
#[cfg(target_os = "windows")]
fn release_into_job(
    job: &crate::sandbox::OwnedHandle,
    process_info: &windows::Win32::System::Threading::PROCESS_INFORMATION,
) -> Result<()> {
    use windows::Win32::System::Threading::ResumeThread;

    let resumed = crate::sandbox::assign_to_job(job, process_info.hProcess).and_then(|()| {
        // ResumeThread reports the previous suspend count; u32::MAX is the
        // documented failure value
        anyhow::ensure!(
            unsafe { ResumeThread(process_info.hThread) } != u32::MAX,
            "resume helper main thread"
        );
        Ok(())
    });
    unsafe { _ = windows::Win32::Foundation::CloseHandle(process_info.hThread) };
    resumed
}

#[cfg(target_os = "windows")]
pub struct HelperChild {
    process: windows::Win32::Foundation::HANDLE,
    /// The only handle to the helper's kill-on-close job: dropping it (or
    /// this process exiting, however abruptly) terminates the helper.
    _job: crate::sandbox::OwnedHandle,
}

// a process handle is a raw kernel handle; the child is single-owner
// (Client.child behind a mutex)
#[cfg(target_os = "windows")]
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for HelperChild {}

#[cfg(target_os = "windows")]
impl HelperChild {
    /// A `PROCESS_DUP_HANDLE` view of this helper, for pulling the shared
    /// texture handles it announces out of its process (the sandboxed
    /// helper cannot push them into ours).
    pub fn dup_source(&self) -> Result<crate::sandbox::OwnedHandle> {
        crate::sandbox::duplication_source(self.process)
    }

    pub fn try_wait(&mut self) -> Result<Option<HelperExitStatus>> {
        use windows::Win32::Foundation::{WAIT_OBJECT_0, WAIT_TIMEOUT};
        use windows::Win32::System::Threading::{GetExitCodeProcess, WaitForSingleObject};

        match unsafe { WaitForSingleObject(self.process, 0) } {
            WAIT_OBJECT_0 => {
                let mut code = 0u32;
                unsafe { GetExitCodeProcess(self.process, &mut code) }
                    .context("read helper exit code")?;
                Ok(Some(HelperExitStatus(code)))
            }
            WAIT_TIMEOUT => Ok(None),
            _ => Err(windows::core::Error::from_win32()).context("poll helper process"),
        }
    }

    pub fn kill(&mut self) {
        // already-dead processes report access errors; either way the
        // subsequent wait() observes termination
        unsafe { _ = windows::Win32::System::Threading::TerminateProcess(self.process, 1) };
    }

    pub fn wait(&mut self) {
        use windows::Win32::System::Threading::{INFINITE, WaitForSingleObject};
        unsafe { WaitForSingleObject(self.process, INFINITE) };
    }
}

#[cfg(target_os = "windows")]
impl Drop for HelperChild {
    fn drop(&mut self) {
        unsafe { _ = windows::Win32::Foundation::CloseHandle(self.process) };
    }
}

#[cfg(target_os = "windows")]
pub struct HelperExitStatus(u32);

#[cfg(target_os = "windows")]
impl std::fmt::Display for HelperExitStatus {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        // NTSTATUS-looking codes (crashes) read better in hex
        if self.0 > 0xFFFF {
            write!(f, "exit code {:#010x}", self.0)
        } else {
            write!(f, "exit code {}", self.0)
        }
    }
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
