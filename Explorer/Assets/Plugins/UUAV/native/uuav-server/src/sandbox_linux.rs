//! The Linux sandbox the helper applies to itself — the Landlock analog
//! of the macOS Seatbelt module.
//!
//! FFmpeg demuxes attacker-controlled media, so before the IPC channel
//! is adopted and any untrusted byte arrives, the helper drops to a
//! deny-by-default filesystem policy: read-only on the system library
//! trees and the plugin directory, read/write/ioctl on `/dev` (the GPU
//! and VAAPI node zoo; POSIX permissions still apply beneath Landlock),
//! everything else denied — including `Execute` everywhere, so a
//! decoder exploit cannot launch a payload, and every file write, so it
//! cannot persist. TCP binds are denied (the helper listens for
//! nothing); outbound TCP/UDP stay open because FFmpeg fetches media
//! itself. A small seccomp filter then turns the introspection family
//! (`ptrace`, `process_vm_*`) and `execve`/`execveat` into `EPERM` as
//! defense-in-depth.
//!
//! Fail closed, like Seatbelt: if the base ruleset cannot be enforced
//! (kernel without Landlock) the helper exits and the client's recovery
//! surfaces the error; there is no bypass switch. `--allow-file-read`
//! adds a read-only rule on `/` for the Editor's `file:` protocol —
//! writes and exec stay denied regardless.

use anyhow::{Context as _, Result, bail};
use landlock::{
    ABI, Access, AccessFs, AccessNet, CompatLevel, Compatible as _, Ruleset, RulesetAttr as _,
    RulesetCreatedAttr as _, RulesetStatus, path_beneath_rules,
};

/// The ABI whose full access set we ask the kernel to handle; rights
/// the running kernel lacks are dropped by the best-effort mode.
const FULL_ABI: ABI = ABI::V5;

/// Applies the sandbox; returns the confirmation line for the
/// post-Hello log (the Seatbelt line's analog).
pub fn apply(allow_media_file_read: bool) -> Result<String> {
    // Landlock path rules match the resolved file, so symlinked trees
    // (NixOS /lib -> /nix/store) are covered by listing both ends;
    // nonexistent paths are skipped by path_beneath_rules.
    let exe = std::env::current_exe()
        .and_then(|p| p.canonicalize())
        .context("resolve helper executable path")?;
    let plugin_dir = exe
        .parent()
        .context("helper executable has no parent directory")?
        .to_path_buf();

    let read_only = [
        std::path::Path::new("/usr"),
        std::path::Path::new("/lib"),
        std::path::Path::new("/lib64"),
        std::path::Path::new("/etc"),
        std::path::Path::new("/sys"),
        std::path::Path::new("/proc"),
        std::path::Path::new("/run"),
        std::path::Path::new("/nix/store"),
        plugin_dir.as_path(),
    ];

    let status = Ruleset::default()
        // rights the kernel doesn't know are silently dropped...
        .set_compatibility(CompatLevel::BestEffort)
        .handle_access(AccessFs::from_all(FULL_ABI))
        .context("handle filesystem access")?
        .handle_access(AccessNet::BindTcp)
        .context("handle network access")?
        .create()
        .context("create Landlock ruleset")?
        // deliberately not from_read(): that set includes Execute, and
        // no path in this process may ever be executable
        .add_rules(path_beneath_rules(
            read_only,
            AccessFs::ReadFile | AccessFs::ReadDir,
        ))
        .context("add read-only rules")?
        // the GPU stack (DRM render nodes, NVIDIA's device zoo, VAAPI)
        // needs open/mmap/ioctl on device nodes; POSIX permissions keep
        // guarding what Landlock lets through
        .add_rules(path_beneath_rules(
            ["/dev"],
            AccessFs::ReadFile | AccessFs::ReadDir | AccessFs::WriteFile | AccessFs::IoctlDev,
        ))
        .context("add device rules")?
        // the NVIDIA userspace writes its own proc files at cuInit
        // (thread names via task/<tid>/comm and the like) and treats the
        // failure as fatal; /proc/self resolves to this helper's own pid
        // at ruleset build, so the write grant reaches no other process
        .add_rules(path_beneath_rules(
            ["/proc/self"],
            AccessFs::ReadFile | AccessFs::ReadDir | AccessFs::WriteFile | AccessFs::Truncate,
        ))
        .context("add own-proc rules")?
        // shm_open creates/unlinks backing files here (the NVIDIA
        // userspace does at driver init); still no Execute
        .add_rules(path_beneath_rules(
            ["/dev/shm"],
            AccessFs::ReadFile
                | AccessFs::ReadDir
                | AccessFs::WriteFile
                | AccessFs::MakeReg
                | AccessFs::RemoveFile
                | AccessFs::Truncate,
        ))
        .context("add shm rules")?
        .add_rules(if allow_media_file_read {
            path_beneath_rules(vec!["/"], AccessFs::ReadFile | AccessFs::ReadDir)
        } else {
            path_beneath_rules(Vec::<&str>::new(), AccessFs::ReadFile | AccessFs::ReadDir)
        })
        .context("add file-read rule")?
        // no BindTcp rules on purpose: the helper listens for nothing
        .restrict_self()
        .context("restrict self")?;

    // best-effort covers newer *rights*; a kernel that cannot enforce
    // the ruleset at all is a hard failure
    let enforcement = match status.ruleset {
        RulesetStatus::FullyEnforced => "fully enforced",
        RulesetStatus::PartiallyEnforced => "partially enforced (older kernel)",
        RulesetStatus::NotEnforced => {
            bail!("Landlock is unavailable on this kernel; refusing to run unsandboxed")
        }
    };

    apply_seccomp()?;
    Ok(format!(
        "uuav-helper: landlock sandbox active ({enforcement}) + seccomp"
    ))
}

/// Denies the syscalls a decode process has no business making, with
/// `EPERM` (not kill: FFmpeg probes must fail cleanly, not crash).
/// `execve` is already impossible under Landlock (no `Execute` right is
/// ever granted); this is defense-in-depth plus the introspection
/// family Landlock does not cover.
fn apply_seccomp() -> Result<()> {
    const DENIED: [libc::c_long; 4] = [
        libc::SYS_execve,
        libc::SYS_execveat,
        libc::SYS_ptrace,
        libc::SYS_process_vm_readv,
    ];
    // process_vm_writev rides along below; array kept explicit

    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    const fn nr(call: libc::c_long) -> u32 {
        call as u32
    }

    const AUDIT_ARCH_X86_64: u32 = 0xC000_003E;
    const SECCOMP_RET_ALLOW: u32 = 0x7FFF_0000;
    const SECCOMP_RET_ERRNO: u32 = 0x0005_0000;
    const EPERM: u32 = libc::EPERM as u32;

    const fn stmt(code: u16, k: u32) -> libc::sock_filter {
        libc::sock_filter {
            code,
            jt: 0,
            jf: 0,
            k,
        }
    }
    const fn jeq(k: u32, jt: u8) -> libc::sock_filter {
        libc::sock_filter {
            // BPF_JMP | BPF_JEQ | BPF_K
            code: 0x15,
            jt,
            jf: 0,
            k,
        }
    }

    // load arch; kill on foreign arch (no x32 escape), load nr, compare
    let filter = [
        stmt(0x20, 4),                       // BPF_LD | BPF_W | BPF_ABS  arch
        jeq(AUDIT_ARCH_X86_64, 1),           // == x86_64 -> continue
        stmt(0x06, SECCOMP_RET_ERRNO | EPERM), // foreign arch -> EPERM everything
        stmt(0x20, 0),                       // load syscall nr
        jeq(nr(DENIED[0]), 5),
        jeq(nr(DENIED[1]), 4),
        jeq(nr(DENIED[2]), 3),
        jeq(nr(DENIED[3]), 2),
        jeq(nr(libc::SYS_process_vm_writev), 1),
        stmt(0x06, SECCOMP_RET_ALLOW),       // default: allow
        stmt(0x06, SECCOMP_RET_ERRNO | EPERM), // denied list lands here
    ];
    let program = libc::sock_fprog {
        len: filter.len() as u16,
        filter: filter.as_ptr().cast_mut(),
    };

    // restrict_self already set no_new_privs; re-assert for the case
    // where the seccomp path changes independently
    if unsafe { libc::prctl(libc::PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0) } != 0 {
        return Err(std::io::Error::last_os_error()).context("set no_new_privs");
    }
    let rc = unsafe {
        libc::syscall(
            libc::SYS_seccomp,
            1, // SECCOMP_SET_MODE_FILTER
            0,
            std::ptr::from_ref(&program),
        )
    };
    if rc != 0 {
        bail!(
            "seccomp filter rejected: {}",
            std::io::Error::last_os_error()
        );
    }
    Ok(())
}
