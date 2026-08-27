//! The Windows sandbox the helper is spawned into. FFmpeg demuxes
//! attacker-controlled media, so the helper runs with the least authority
//! that still allows GPU decoding and outbound network access:
//!
//! - a **restricted primary token** (`LUA_TOKEN` drops elevation,
//!   `DISABLE_MAX_PRIVILEGE` strips every privilege but
//!   `SeChangeNotifyPrivilege`) stamped with the **low integrity** label,
//!   so the helper cannot write outside low-IL locations or open
//!   higher-integrity processes for anything but query/synchronize;
//! - a **job object** limiting the helper to a single process (no child
//!   processes) with kill-on-job-close: the client holds the only job
//!   handle, so kernel handle cleanup terminates the helper when Unity
//!   exits for any reason, crash included;
//! - a **process-creation mitigation policy** enabling DEP, mandatory +
//!   bottom-up + high-entropy ASLR, control-flow guard, strict handle
//!   checks, and heap terminate-on-corruption. Win32k lockdown and
//!   dynamic-code prohibition are deliberately absent: GPU drivers and
//!   the D3D11 runtime need both.
//!
//! Winsock is unaffected by integrity level, so media playback keeps its
//! outbound network access.

use anyhow::{Context as _, Result};
use windows::Win32::Foundation::{CloseHandle, DuplicateHandle, HANDLE};
use windows::Win32::Security::{
    CreateRestrictedToken, CreateWellKnownSid, DISABLE_MAX_PRIVILEGE, LUA_TOKEN, PSID,
    SECURITY_MAX_SID_SIZE, SID_AND_ATTRIBUTES, SetTokenInformation, TOKEN_ACCESS_MASK,
    TOKEN_ADJUST_DEFAULT, TOKEN_ASSIGN_PRIMARY, TOKEN_DUPLICATE, TOKEN_MANDATORY_LABEL,
    TOKEN_QUERY, TokenIntegrityLevel, WinLowLabelSid,
};
use windows::Win32::System::JobObjects::{
    AssignProcessToJobObject, CreateJobObjectW, JOB_OBJECT_LIMIT_ACTIVE_PROCESS,
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE, JOBOBJECT_EXTENDED_LIMIT_INFORMATION,
    JobObjectExtendedLimitInformation, SetInformationJobObject,
};
use windows::Win32::System::Threading::{GetCurrentProcess, OpenProcessToken};
use windows::core::PCWSTR;

/// Close-on-drop guard for the raw kernel handles this module creates.
pub struct OwnedHandle(HANDLE);

// raw kernel handles are process-global; every OwnedHandle here is
// single-owner
unsafe impl Send for OwnedHandle {}
unsafe impl Sync for OwnedHandle {}

impl OwnedHandle {
    pub const fn raw(&self) -> HANDLE {
        self.0
    }
}

impl Drop for OwnedHandle {
    fn drop(&mut self) {
        unsafe { _ = CloseHandle(self.0) };
    }
}

/// `PROCESS_CREATION_MITIGATION_POLICY_*` values (winbase.h); windows-rs
/// only exposes the 32-bit DEP subset, so the 64-bit flags live here.
const MITIGATION_DEP_ENABLE: u64 = 0x0000_0000_0000_0001;
const MITIGATION_FORCE_RELOCATE_IMAGES_ALWAYS_ON: u64 = 0x0000_0000_0000_0100;
const MITIGATION_HEAP_TERMINATE_ALWAYS_ON: u64 = 0x0000_0000_0000_1000;
const MITIGATION_BOTTOM_UP_ASLR_ALWAYS_ON: u64 = 0x0000_0000_0001_0000;
const MITIGATION_HIGH_ENTROPY_ASLR_ALWAYS_ON: u64 = 0x0000_0000_0010_0000;
const MITIGATION_STRICT_HANDLE_CHECKS_ALWAYS_ON: u64 = 0x0000_0000_0100_0000;
const MITIGATION_CONTROL_FLOW_GUARD_ALWAYS_ON: u64 = 0x0000_0100_0000_0000;

/// The mitigation-policy word for `PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY`.
/// `FORCE_RELOCATE` (not the `REQ_RELOCS` variant) rebases every relocatable
/// image without refusing the rare stripped one, and CFG enforcement only
/// applies to CFG-instrumented modules — both safe next to the mingw-built
/// FFmpeg DLLs and GPU driver DLLs the helper loads.
pub const MITIGATION_POLICY: u64 = MITIGATION_DEP_ENABLE
    | MITIGATION_FORCE_RELOCATE_IMAGES_ALWAYS_ON
    | MITIGATION_HEAP_TERMINATE_ALWAYS_ON
    | MITIGATION_BOTTOM_UP_ASLR_ALWAYS_ON
    | MITIGATION_HIGH_ENTROPY_ASLR_ALWAYS_ON
    | MITIGATION_STRICT_HANDLE_CHECKS_ALWAYS_ON
    | MITIGATION_CONTROL_FLOW_GUARD_ALWAYS_ON;

/// `SE_GROUP_INTEGRITY` (winnt.h): marks the label SID as the token's
/// mandatory integrity level.
const SE_GROUP_INTEGRITY: u32 = 0x0000_0020;

/// `SYNCHRONIZE` (winnt.h): wait-only access for the parent handle the
/// helper's orphan watch inherits.
const SYNCHRONIZE: u32 = 0x0010_0000;

/// The restricted low-integrity primary token the helper runs under.
/// `CreateProcessAsUserW` accepts it without `SeAssignPrimaryTokenPrivilege`
/// because it is a restricted version of the caller's own primary token.
pub fn restricted_low_integrity_token() -> Result<OwnedHandle> {
    // the restricted token receives the same access rights as this handle;
    // ADJUST_DEFAULT is what lets the integrity label below be set on it
    let mut primary = HANDLE::default();
    unsafe {
        OpenProcessToken(
            GetCurrentProcess(),
            TOKEN_ACCESS_MASK(
                TOKEN_DUPLICATE.0 | TOKEN_QUERY.0 | TOKEN_ASSIGN_PRIMARY.0
                    | TOKEN_ADJUST_DEFAULT.0,
            ),
            &mut primary,
        )
    }
    .context("open own process token")?;
    let primary = OwnedHandle(primary);

    let mut restricted = HANDLE::default();
    unsafe {
        CreateRestrictedToken(
            primary.raw(),
            LUA_TOKEN | DISABLE_MAX_PRIVILEGE,
            None,
            None,
            None,
            &mut restricted,
        )
    }
    .context("create restricted helper token")?;
    let restricted = OwnedHandle(restricted);

    let mut sid = [0u8; SECURITY_MAX_SID_SIZE as usize];
    let mut sid_len = u32::try_from(sid.len()).context("SID buffer size")?;
    let sid = PSID(sid.as_mut_ptr().cast());
    unsafe { CreateWellKnownSid(WinLowLabelSid, None, Some(sid), &mut sid_len) }
        .context("create low-integrity label SID")?;

    let label = TOKEN_MANDATORY_LABEL {
        Label: SID_AND_ATTRIBUTES {
            Sid: sid,
            Attributes: SE_GROUP_INTEGRITY,
        },
    };
    unsafe {
        SetTokenInformation(
            restricted.raw(),
            TokenIntegrityLevel,
            std::ptr::from_ref(&label).cast(),
            u32::try_from(std::mem::size_of::<TOKEN_MANDATORY_LABEL>())
                .context("TOKEN_MANDATORY_LABEL size")?
                .checked_add(sid_len)
                .context("integrity label size")?,
        )
    }
    .context("set low integrity level on helper token")?;

    Ok(restricted)
}

/// The job the helper is assigned into before its first instruction runs:
/// one process, killed when the last job handle closes — the client keeps
/// that handle for the helper's lifetime, so parent exit (including a
/// crash, via kernel handle cleanup) terminates the helper.
pub fn helper_job() -> Result<OwnedHandle> {
    let job = OwnedHandle(
        unsafe { CreateJobObjectW(None, PCWSTR::null()) }.context("create helper job object")?,
    );

    let mut limits = JOBOBJECT_EXTENDED_LIMIT_INFORMATION::default();
    limits.BasicLimitInformation.LimitFlags =
        JOB_OBJECT_LIMIT_ACTIVE_PROCESS | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
    limits.BasicLimitInformation.ActiveProcessLimit = 1;
    unsafe {
        SetInformationJobObject(
            job.raw(),
            JobObjectExtendedLimitInformation,
            std::ptr::from_ref(&limits).cast(),
            u32::try_from(std::mem::size_of::<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>())
                .context("job limits size")?,
        )
    }
    .context("limit helper job to one process, kill on close")?;

    Ok(job)
}

/// Puts `process` (created suspended) in the job.
pub fn assign_to_job(job: &OwnedHandle, process: HANDLE) -> Result<()> {
    unsafe { AssignProcessToJobObject(job.raw(), process) }
        .context("assign helper to its job object")
}

/// An inheritable wait-only (`SYNCHRONIZE`) handle to this process for the
/// helper's orphan watch — the low-integrity helper may not be able to
/// `OpenProcess` its medium-integrity parent, and wait-only carries no
/// authority worth stealing.
pub fn inheritable_parent_handle() -> Result<OwnedHandle> {
    let mut duplicated = HANDLE::default();
    unsafe {
        DuplicateHandle(
            GetCurrentProcess(),
            GetCurrentProcess(),
            GetCurrentProcess(),
            &mut duplicated,
            SYNCHRONIZE,
            true,
            windows::Win32::Foundation::DUPLICATE_HANDLE_OPTIONS(0),
        )
    }
    .context("duplicate wait-only parent handle")?;
    Ok(OwnedHandle(duplicated))
}

/// A `PROCESS_DUP_HANDLE` handle to the helper, for pulling the shared
/// texture handles it announces out of its process (the sandboxed helper
/// cannot push them into ours).
pub fn duplication_source(process: HANDLE) -> Result<OwnedHandle> {
    /// `PROCESS_DUP_HANDLE` (winnt.h).
    const PROCESS_DUP_HANDLE: u32 = 0x0040;

    let mut duplicated = HANDLE::default();
    unsafe {
        DuplicateHandle(
            GetCurrentProcess(),
            process,
            GetCurrentProcess(),
            &mut duplicated,
            PROCESS_DUP_HANDLE,
            false,
            windows::Win32::Foundation::DUPLICATE_HANDLE_OPTIONS(0),
        )
    }
    .context("duplicate helper process handle for texture-handle pulls")?;
    Ok(OwnedHandle(duplicated))
}

/// Duplicates helper-local handle values into this process. All-or-nothing:
/// a partial failure closes what was already pulled and reports the error
/// (the helper died or already retired the generation — the set is stale
/// either way).
pub fn pull_handles(source: &OwnedHandle, values: &[u64]) -> Result<Vec<u64>> {
    let mut pulled = Vec::with_capacity(values.len());
    for &value in values {
        let mut local = HANDLE::default();
        let result = unsafe {
            DuplicateHandle(
                source.raw(),
                HANDLE(value as usize as *mut std::ffi::c_void),
                GetCurrentProcess(),
                &mut local,
                0,
                false,
                windows::Win32::Foundation::DUPLICATE_SAME_ACCESS,
            )
        };
        if let Err(e) = result {
            crate::present::close_handles(&pulled);
            return Err(e).context("pull shared texture handle from helper");
        }
        pulled.push(local.0 as usize as u64);
    }
    Ok(pulled)
}
