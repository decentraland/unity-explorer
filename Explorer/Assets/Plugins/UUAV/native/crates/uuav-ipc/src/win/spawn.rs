
use std::ffi::OsStr;
use std::io;
use std::os::windows::ffi::OsStrExt as _;
use std::os::windows::io::{AsRawHandle as _, FromRawHandle as _, OwnedHandle};
use std::path::Path;
use std::ptr;
use std::time::{Duration, Instant};

use anyhow::{Context as _, Result, anyhow, bail};
use windows_sys::Win32::Foundation::{
    ERROR_ACCESS_DENIED, HANDLE, HANDLE_FLAG_INHERIT, LocalFree, SetHandleInformation,
    WAIT_OBJECT_0, WAIT_TIMEOUT,
};
use windows_sys::Win32::Security::Authorization::ConvertStringSidToSidW;
use windows_sys::Win32::Security::{
    CreateRestrictedToken, GetTokenInformation, SID_AND_ATTRIBUTES, SetTokenInformation,
    TOKEN_ADJUST_DEFAULT, TOKEN_ASSIGN_PRIMARY, TOKEN_DUPLICATE, TOKEN_GROUPS,
    TOKEN_MANDATORY_LABEL, TOKEN_QUERY, TokenGroups, TokenIntegrityLevel,
};
use windows_sys::Win32::System::SystemServices::{
    SE_GROUP_INTEGRITY, SE_GROUP_LOGON_ID, SE_GROUP_USE_FOR_DENY_ONLY,
};
use windows_sys::Win32::System::JobObjects::{
    AssignProcessToJobObject, CreateJobObjectW, IsProcessInJob, JOBOBJECT_BASIC_UI_RESTRICTIONS,
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION, JobObjectBasicUIRestrictions,
    JobObjectExtendedLimitInformation, SetInformationJobObject, TerminateJobObject,
};
use windows_sys::Win32::System::Threading::{
    CREATE_SUSPENDED, CREATE_UNICODE_ENVIRONMENT, CreateProcessAsUserW, DETACHED_PROCESS,
    DeleteProcThreadAttributeList, EXTENDED_STARTUPINFO_PRESENT, GetCurrentProcess,
    GetExitCodeProcess, InitializeProcThreadAttributeList, LPPROC_THREAD_ATTRIBUTE_LIST,
    OpenProcessToken, PROCESS_INFORMATION, ResumeThread, STARTUPINFOEXW, STARTUPINFOW,
    TerminateProcess, UpdateProcThreadAttribute, WaitForSingleObject,
};

use crate::win::wire::{self, Confinement, Mode, Token};

const REAP_POLL: Duration = Duration::from_millis(1);

const KILLED_BY_JOB: u32 = 0x5555_4156;

const KILL_WAIT_MS: u32 = 5_000;

const HANDSHAKE_DEATH_GRACE_MS: u32 = 250;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ExitStatus {
    Exited(u32),
    Killed(u32),
}

fn last_error() -> io::Error {
    io::Error::last_os_error()
}

fn is_error(error: &io::Error, code: u32) -> bool {
    error.raw_os_error().is_some_and(|raw| raw as u32 == code)
}

fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(std::iter::once(0)).collect()
}

fn wide_path(path: &Path) -> Vec<u16> {
    OsStr::new(path)
        .encode_wide()
        .chain(std::iter::once(0))
        .collect()
}

struct Sid(*mut core::ffi::c_void);

impl Sid {
    fn parse(text: &str) -> Result<Self> {
        let wide_text = wide(text);
        let mut sid: *mut core::ffi::c_void = ptr::null_mut();
        if unsafe { ConvertStringSidToSidW(wide_text.as_ptr(), &raw mut sid) } == 0 {
            return Err(last_error()).with_context(|| format!("ConvertStringSidToSidW({text})"));
        }
        Ok(Self(sid))
    }

    const fn raw(&self) -> *mut core::ffi::c_void {
        self.0
    }
}

impl Drop for Sid {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe { LocalFree(self.0.cast()) };
        }
    }
}

struct Groups(Vec<u64>);

impl Groups {
    fn query(token: &OwnedHandle) -> Result<Self> {
        let mut needed = 0u32;
        unsafe {
            GetTokenInformation(
                token.as_raw_handle().cast(),
                TokenGroups,
                ptr::null_mut(),
                0,
                &raw mut needed,
            )
        };
        if needed == 0 {
            return Err(last_error()).context("GetTokenInformation(TokenGroups) size probe");
        }
        let mut words = vec![0u64; needed.div_ceil(8) as usize];
        if unsafe {
            GetTokenInformation(
                token.as_raw_handle().cast(),
                TokenGroups,
                words.as_mut_ptr().cast(),
                needed,
                &raw mut needed,
            )
        } == 0
        {
            return Err(last_error()).context("GetTokenInformation(TokenGroups)");
        }
        Ok(Self(words))
    }

    fn logon(&self) -> Option<*mut core::ffi::c_void> {
        let groups = self.0.as_ptr().cast::<TOKEN_GROUPS>();
        let count = unsafe { (*groups).GroupCount } as usize;
        let first = unsafe { (&raw const (*groups).Groups).cast::<SID_AND_ATTRIBUTES>() };
        let wanted = SE_GROUP_LOGON_ID as u32;
        (0..count).find_map(|index| {
            let entry = unsafe { *first.add(index) };
            (entry.Attributes & wanted == wanted).then_some(entry.Sid)
        })
    }
}

const SID_ADMINISTRATORS: &str = "S-1-5-32-544";
const SID_RESTRICTED: &str = "S-1-5-12";
const SID_USERS: &str = "S-1-5-32-545";
const SID_EVERYONE: &str = "S-1-1-0";

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Integrity {
    Low,
    Medium,
}

impl Integrity {
    const fn sid(self) -> &'static str {
        match self {
            Self::Low => "S-1-16-4096",
            Self::Medium => "S-1-16-8192",
        }
    }
}

fn restricted_token(integrity: Integrity) -> Result<OwnedHandle> {
    let mut source: HANDLE = ptr::null_mut();
    if unsafe {
        OpenProcessToken(
            GetCurrentProcess(),
            TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT,
            &raw mut source,
        )
    } == 0
    {
        return Err(last_error()).context("OpenProcessToken on the player");
    }
    let source = unsafe { OwnedHandle::from_raw_handle(source.cast()) };

    let administrators = Sid::parse(SID_ADMINISTRATORS)?;
    let mut deny = [SID_AND_ATTRIBUTES {
        Sid: administrators.raw(),
        Attributes: SE_GROUP_USE_FOR_DENY_ONLY as u32,
    }];

    let restricted = Sid::parse(SID_RESTRICTED)?;
    let users = Sid::parse(SID_USERS)?;
    let everyone = Sid::parse(SID_EVERYONE)?;
    let groups = Groups::query(&source)?;
    let mut restrict = vec![
        SID_AND_ATTRIBUTES {
            Sid: restricted.raw(),
            Attributes: 0,
        },
        SID_AND_ATTRIBUTES {
            Sid: users.raw(),
            Attributes: 0,
        },
        SID_AND_ATTRIBUTES {
            Sid: everyone.raw(),
            Attributes: 0,
        },
    ];
    if let Some(logon) = groups.logon() {
        restrict.push(SID_AND_ATTRIBUTES {
            Sid: logon,
            Attributes: 0,
        });
    }

    let mut token: HANDLE = ptr::null_mut();
    if unsafe {
        CreateRestrictedToken(
            source.as_raw_handle().cast(),
            wire::DISABLE_MAX_PRIVILEGE,
            deny.len() as u32,
            deny.as_mut_ptr(),
            0,
            ptr::null_mut(),
            restrict.len() as u32,
            restrict.as_mut_ptr(),
            &raw mut token,
        )
    } == 0
    {
        return Err(last_error()).context("CreateRestrictedToken");
    }
    let token = unsafe { OwnedHandle::from_raw_handle(token.cast()) };

    set_integrity(&token, integrity)?;
    Ok(token)
}

fn set_integrity(token: &OwnedHandle, integrity: Integrity) -> Result<()> {
    let level = Sid::parse(integrity.sid())?;
    let mut label = TOKEN_MANDATORY_LABEL {
        Label: SID_AND_ATTRIBUTES {
            Sid: level.raw(),
            Attributes: SE_GROUP_INTEGRITY as u32,
        },
    };
    if unsafe {
        SetTokenInformation(
            token.as_raw_handle().cast(),
            TokenIntegrityLevel,
            (&raw mut label).cast(),
            size_of::<TOKEN_MANDATORY_LABEL>() as u32,
        )
    } == 0
    {
        return Err(last_error())
            .with_context(|| format!("SetTokenInformation(integrity {:?})", integrity));
    }
    Ok(())
}

pub struct Job(OwnedHandle);

impl Job {
    fn new(plan: &Confinement) -> Result<Self> {
        let raw = unsafe { CreateJobObjectW(ptr::null(), ptr::null()) };
        if raw.is_null() {
            return Err(last_error()).context("CreateJobObjectW");
        }
        let job = Self(unsafe { OwnedHandle::from_raw_handle(raw.cast()) });

        let mut limits: JOBOBJECT_EXTENDED_LIMIT_INFORMATION = unsafe { std::mem::zeroed() };
        limits.BasicLimitInformation.LimitFlags = plan.job_limit_flags();
        limits.BasicLimitInformation.ActiveProcessLimit = 1;
        limits.ProcessMemoryLimit = plan.process_memory_bytes as usize;
        if unsafe {
            SetInformationJobObject(
                job.raw(),
                JobObjectExtendedLimitInformation,
                (&raw const limits).cast(),
                size_of::<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>() as u32,
            )
        } == 0
        {
            return Err(last_error()).context("SetInformationJobObject(extended limits)");
        }

        let restrictions = JOBOBJECT_BASIC_UI_RESTRICTIONS {
            UIRestrictionsClass: plan.job_ui_restrictions(),
        };
        if unsafe {
            SetInformationJobObject(
                job.raw(),
                JobObjectBasicUIRestrictions,
                (&raw const restrictions).cast(),
                size_of::<JOBOBJECT_BASIC_UI_RESTRICTIONS>() as u32,
            )
        } == 0
        {
            return Err(last_error()).context("SetInformationJobObject(UI restrictions)");
        }

        Ok(job)
    }

    fn raw(&self) -> HANDLE {
        self.0.as_raw_handle().cast()
    }

    fn terminate(&self) {
        unsafe { TerminateJobObject(self.raw(), KILLED_BY_JOB) };
    }
}

struct AttributeList {
    storage: Vec<u8>,
    initialised: bool,
}

impl AttributeList {
    fn new(count: u32) -> Result<Self> {
        let mut bytes = 0usize;
        unsafe { InitializeProcThreadAttributeList(ptr::null_mut(), count, 0, &raw mut bytes) };
        if bytes == 0 {
            return Err(last_error()).context("InitializeProcThreadAttributeList size probe");
        }
        let mut list = Self {
            storage: vec![0u8; bytes],
            initialised: false,
        };
        if unsafe {
            InitializeProcThreadAttributeList(list.as_ptr(), count, 0, &raw mut bytes)
        } == 0
        {
            return Err(last_error()).context("InitializeProcThreadAttributeList");
        }
        list.initialised = true;
        Ok(list)
    }

    const fn as_ptr(&mut self) -> LPPROC_THREAD_ATTRIBUTE_LIST {
        self.storage.as_mut_ptr().cast()
    }

    unsafe fn set(
        &mut self,
        attribute: usize,
        value: *const core::ffi::c_void,
        bytes: usize,
        what: &str,
    ) -> Result<()> {
        if unsafe {
            UpdateProcThreadAttribute(
                self.as_ptr(),
                0,
                attribute,
                value.cast_mut(),
                bytes,
                ptr::null_mut(),
                ptr::null_mut(),
            )
        } == 0
        {
            return Err(last_error()).with_context(|| format!("UpdateProcThreadAttribute({what})"));
        }
        Ok(())
    }
}

impl Drop for AttributeList {
    fn drop(&mut self) {
        if self.initialised {
            unsafe { DeleteProcThreadAttributeList(self.as_ptr()) };
        }
    }
}

pub struct Launch<'a> {
    pub exe: &'a Path,
    pub plan: Confinement,
    pub integrity: Integrity,
    pub pipe: &'a OwnedHandle,
    pub segment: &'a OwnedHandle,
    pub cookie: u64,
    pub adapter_luid: Option<u64>,
}

pub struct Helper {
    process: OwnedHandle,
    job: Job,
    pid: u32,
    status: Option<ExitStatus>,
}

impl Helper {
    pub fn spawn(launch: &Launch<'_>) -> Result<Self> {
        check_launch(launch)?;
        let job = Job::new(&launch.plan)?;
        let token = restricted_token(launch.integrity)?;

        make_inheritable(launch.pipe)?;
        make_inheritable(launch.segment)?;
        let inherited: [HANDLE; 2] = [
            launch.pipe.as_raw_handle().cast(),
            launch.segment.as_raw_handle().cast(),
        ];

        let exe_text = launch
            .exe
            .to_str()
            .ok_or_else(|| anyhow!("helper path is not valid UTF-16-able text"))?;
        let mut command = wide(&wire::command_line(
            exe_text,
            inherited[0] as usize as u64,
            inherited[1] as usize as u64,
            launch.cookie,
            launch.plan.mode,
            launch.adapter_luid,
        ));

        let mitigation = launch.plan.mitigation_policy();
        let child_policy = wire::PROCESS_CREATION_CHILD_PROCESS_RESTRICTED;
        let job_list: [HANDLE; 1] = [job.raw()];

        let mut attributes = AttributeList::new(4)?;
        unsafe {
            attributes.set(
                wire::PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                inherited.as_ptr().cast(),
                size_of_val(&inherited),
                "handle list",
            )?;
            attributes.set(
                wire::PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY,
                (&raw const mitigation).cast(),
                size_of::<u64>(),
                "mitigation policy",
            )?;
            attributes.set(
                wire::PROC_THREAD_ATTRIBUTE_CHILD_PROCESS_POLICY,
                (&raw const child_policy).cast(),
                size_of::<u32>(),
                "child process policy",
            )?;
            attributes.set(
                wire::PROC_THREAD_ATTRIBUTE_JOB_LIST,
                job_list.as_ptr().cast(),
                size_of_val(&job_list),
                "job list",
            )?;
        }

        let mut startup: STARTUPINFOEXW = unsafe { std::mem::zeroed() };
        startup.StartupInfo.cb = size_of::<STARTUPINFOEXW>() as u32;
        startup.lpAttributeList = attributes.as_ptr();

        let mut environment = minimal_environment();
        let directory = launch
            .exe
            .parent()
            .map_or_else(|| wide("."), wide_path);

        let mut information: PROCESS_INFORMATION = unsafe { std::mem::zeroed() };
        let created = unsafe {
            CreateProcessAsUserW(
                token.as_raw_handle().cast(),
                ptr::null(),
                command.as_mut_ptr(),
                ptr::null(),
                ptr::null(),
                1,
                EXTENDED_STARTUPINFO_PRESENT
                    | CREATE_SUSPENDED
                    | DETACHED_PROCESS
                    | CREATE_UNICODE_ENVIRONMENT,
                environment.as_mut_ptr().cast(),
                directory.as_ptr(),
                (&raw mut startup).cast::<STARTUPINFOW>(),
                &raw mut information,
            )
        };
        if created == 0 {
            return Err(last_error())
                .with_context(|| format!("CreateProcessAsUserW({})", launch.exe.display()));
        }

        let process = unsafe { OwnedHandle::from_raw_handle(information.hProcess.cast()) };
        let thread = unsafe { OwnedHandle::from_raw_handle(information.hThread.cast()) };

        let mut helper = Self {
            process,
            job,
            pid: information.dwProcessId,
            status: None,
        };

        let pipe_cleared = clear_inheritable(launch.pipe);
        let segment_cleared = clear_inheritable(launch.segment);
        if let Err(error) = pipe_cleared.and(segment_cleared) {
            helper.kill();
            return Err(error);
        }

        helper.confirm_job_then_resume(&thread)?;
        Ok(helper)
    }

    fn confirm_job_then_resume(&mut self, thread: &OwnedHandle) -> Result<()> {
        let assigned = unsafe { AssignProcessToJobObject(self.job.raw(), self.process_handle()) };
        if assigned == 0 {
            let error = last_error();
            if !is_error(&error, ERROR_ACCESS_DENIED) {
                self.kill();
                return Err(error).context("AssignProcessToJobObject");
            }
            let mut in_job = 0i32;
            let queried = unsafe {
                IsProcessInJob(self.process_handle(), self.job.raw(), &raw mut in_job)
            };
            if queried == 0 {
                let error = last_error();
                self.kill();
                return Err(error).context("IsProcessInJob");
            }
            if in_job == 0 {
                self.kill();
                bail!("the helper is not in its job object, so it would run uncontained");
            }
        }

        if unsafe { ResumeThread(thread.as_raw_handle().cast()) } == u32::MAX {
            let error = last_error();
            self.kill();
            return Err(error).context("ResumeThread on the helper");
        }
        Ok(())
    }

    pub const fn pid(&self) -> u32 {
        self.pid
    }

    pub fn process_handle(&self) -> HANDLE {
        self.process.as_raw_handle().cast()
    }

    pub fn is_alive(&mut self) -> bool {
        if self.status.is_some() {
            return false;
        }
        let waited = unsafe { WaitForSingleObject(self.process_handle(), 0) };
        if waited == WAIT_TIMEOUT {
            return true;
        }
        self.status = Some(self.read_exit_status());
        false
    }

    pub fn exit_status(&mut self) -> Option<ExitStatus> {
        let _ignored = self.is_alive();
        self.status
    }

    pub fn died_before_hello(&mut self, what: &str) -> anyhow::Error {
        let _ignored = self.wait_for_exit(HANDSHAKE_DEATH_GRACE_MS);
        match self.exit_status() {
            Some(ExitStatus::Exited(code)) => anyhow!(
                "the helper exited with {code:#010x} ({}) before the handshake completed: {what}",
                describe_exit(code)
            ),
            Some(ExitStatus::Killed(code)) => {
                anyhow!("the helper was terminated ({code:#010x}) before the handshake completed: {what}")
            }
            None => anyhow!("the helper is running but did not complete the handshake: {what}"),
        }
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
        self.job.terminate();
        let mut waited = unsafe { WaitForSingleObject(self.process_handle(), KILL_WAIT_MS) };
        if waited != WAIT_OBJECT_0 {
            unsafe { TerminateProcess(self.process_handle(), KILLED_BY_JOB) };
            waited = unsafe { WaitForSingleObject(self.process_handle(), KILL_WAIT_MS) };
        }
        self.status = Some(if waited == WAIT_OBJECT_0 {
            self.read_exit_status()
        } else {
            ExitStatus::Killed(KILLED_BY_JOB)
        });
    }

    fn read_exit_status(&self) -> ExitStatus {
        let mut code = 0u32;
        if unsafe { GetExitCodeProcess(self.process_handle(), &raw mut code) } == 0 {
            return ExitStatus::Killed(KILLED_BY_JOB);
        }
        if code == KILLED_BY_JOB {
            ExitStatus::Killed(code)
        } else {
            ExitStatus::Exited(code)
        }
    }
}

impl Drop for Helper {
    fn drop(&mut self) {
        self.kill();
    }
}

const fn describe_exit(code: u32) -> &'static str {
    match code {
        1 => "the helper's own error path - it printed the reason to a stderr nobody is reading",
        0xC000_0022 => "STATUS_ACCESS_DENIED - the restricted token cannot reach its own image, \
                        its DLLs or the graphics device; check that the plugin directory grants \
                        read+execute to Users",
        0xC000_0135 => "STATUS_DLL_NOT_FOUND - a DLL beside the executable did not resolve",
        0xC000_0139 => "STATUS_ENTRYPOINT_NOT_FOUND - a DLL beside the executable is the wrong build",
        0xC000_0142 => "STATUS_DLL_INIT_FAILED - a DLL's initialiser was refused, which is what a \
                        mitigation policy an imported module cannot take looks like",
        0xC000_00FD => "STATUS_STACK_OVERFLOW",
        _ => "see the NT status code",
    }
}

fn check_launch(launch: &Launch<'_>) -> Result<()> {
    if matches!(launch.plan.token, Token::AppContainer) {
        bail!(
            "AppContainer confinement is designed but not implemented: it needs a persistent \
             profile from CreateAppContainerProfile, read+execute ACEs for the package SID on \
             the plugin directory, and a decision on the internetClient capability. Use \
             Token::Restricted."
        );
    }
    if launch.plan.mode == Mode::Gpu && launch.adapter_luid.is_none() {
        bail!("a GPU-mode helper needs the engine adapter LUID to share a texture back");
    }
    Ok(())
}

fn make_inheritable(handle: &OwnedHandle) -> Result<()> {
    if unsafe {
        SetHandleInformation(
            handle.as_raw_handle().cast(),
            HANDLE_FLAG_INHERIT,
            HANDLE_FLAG_INHERIT,
        )
    } == 0
    {
        return Err(last_error()).context("SetHandleInformation(HANDLE_FLAG_INHERIT)");
    }
    Ok(())
}

fn clear_inheritable(handle: &OwnedHandle) -> Result<()> {
    if unsafe { SetHandleInformation(handle.as_raw_handle().cast(), HANDLE_FLAG_INHERIT, 0) } == 0 {
        return Err(last_error()).context("SetHandleInformation(clearing HANDLE_FLAG_INHERIT)");
    }
    Ok(())
}

fn minimal_environment() -> Vec<u16> {
    let mut block = Vec::new();
    for name in ["SystemRoot", "windir", "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS"] {
        if let Ok(value) = std::env::var(name) {
            block.extend(wide(&format!("{name}={value}")));
        }
    }
    block.push(0);
    block
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

    #[test]
    fn the_environment_block_is_double_nul_terminated() {
        let block = minimal_environment();
        assert_eq!(block.last(), Some(&0));
        if block.len() > 1 {
            assert_eq!(block[block.len() - 2], 0);
        }
        let text = String::from_utf16_lossy(&block);
        assert!(!text.contains("PATH="), "{text}");
        assert!(!text.to_ascii_uppercase().contains("TOKEN"), "{text}");
    }

    #[test]
    fn every_sid_string_parses() {
        for text in [
            SID_ADMINISTRATORS,
            SID_RESTRICTED,
            SID_USERS,
            SID_EVERYONE,
            Integrity::Low.sid(),
            Integrity::Medium.sid(),
        ] {
            assert!(Sid::parse(text).is_ok(), "{text}");
        }
    }

    #[test]
    fn a_restricted_low_integrity_token_can_be_built_from_our_own() {
        let token = restricted_token(Integrity::Low)
            .expect("a restricted token must be derivable without any privilege");
        drop(token);
    }

    #[test]
    fn a_job_carries_the_plan_and_kills_on_close() {
        for plan in [Confinement::gpu(), Confinement::software()] {
            let job = Job::new(&plan).expect("the job limits must be accepted");
            drop(job);
        }
    }

    #[test]
    fn the_attribute_list_sizes_and_deletes_itself() {
        let mut list = AttributeList::new(4).unwrap();
        assert!(!list.as_ptr().is_null());
    }

    #[test]
    fn appcontainer_is_refused_rather_than_half_applied() {
        let pipe = std::fs::File::open("NUL").unwrap();
        let segment = std::fs::File::open("NUL").unwrap();
        let plan = Confinement {
            token: Token::AppContainer,
            ..Confinement::software()
        };
        let outcome = Helper::spawn(&Launch {
            exe: Path::new(r"C:\nonexistent\uuav-ipc-helper.exe"),
            plan,
            integrity: Integrity::Low,
            pipe: &pipe.into(),
            segment: &segment.into(),
            cookie: 1,
            adapter_luid: None,
        });
        match outcome {
            Ok(_helper) => panic!("AppContainer must refuse, not drop to a weaker box"),
            Err(error) => assert!(format!("{error}").contains("AppContainer"), "{error}"),
        }
    }
}
