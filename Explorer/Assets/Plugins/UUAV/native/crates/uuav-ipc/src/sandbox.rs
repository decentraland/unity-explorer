
use anyhow::{Result, anyhow, ensure};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};
use std::path::Path;
use std::ptr;

const PROFILE: &str = include_str!("decoder.sb");

const PARAM_WORKDIR: &str = "SANDBOX_WORKDIR";

const PARAM_BOOTSTRAP: &str = "HOST_BOOTSTRAP";

unsafe extern "C" {
    fn sandbox_init_with_parameters(
        profile: *const c_char,
        flags: u64,
        parameters: *const *const c_char,
        errorbuf: *mut *mut c_char,
    ) -> c_int;

    fn sandbox_free_error(errorbuf: *mut c_char);
}

pub fn enter(bootstrap_name: &str, dylib_dir: &Path) -> Result<()> {
    let profile = CString::new(PROFILE)
        .map_err(|_| anyhow!("the compiled seatbelt profile contains a NUL byte"))?;

    let workdir = dylib_dir
        .canonicalize()
        .map_err(|error| anyhow!("dylib dir {}: {error}", dylib_dir.display()))?;
    let workdir = workdir
        .to_str()
        .ok_or_else(|| anyhow!("dylib dir is not valid UTF-8: {}", dylib_dir.display()))?;

    let owned = parameter_strings(bootstrap_name, workdir)?;
    let mut pointers: Vec<*const c_char> = owned.iter().map(|value| value.as_ptr()).collect();
    pointers.push(ptr::null());

    let mut error: *mut c_char = ptr::null_mut();
    let code = unsafe {
        sandbox_init_with_parameters(profile.as_ptr(), 0, pointers.as_ptr(), &raw mut error)
    };
    if code != 0 {
        let detail = if error.is_null() {
            String::from("no detail")
        } else {
            unsafe { CStr::from_ptr(error) }.to_string_lossy().into_owned()
        };
        if !error.is_null() {
            unsafe { sandbox_free_error(error) };
        }
        return Err(anyhow!("sandbox_init_with_parameters failed ({code}): {detail}"));
    }
    Ok(())
}

fn parameter_strings(bootstrap_name: &str, workdir: &str) -> Result<Vec<CString>> {
    ensure!(!bootstrap_name.is_empty(), "bootstrap name is empty");
    ensure!(!workdir.is_empty(), "workdir is empty");
    let mut out = Vec::with_capacity(4);
    for value in [PARAM_WORKDIR, workdir, PARAM_BOOTSTRAP, bootstrap_name] {
        out.push(
            CString::new(value)
                .map_err(|_| anyhow!("sandbox parameter {value:?} contains a NUL byte"))?,
        );
    }
    Ok(out)
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
    fn profile_declares_the_denials_it_must() {
        assert!(PROFILE.contains("(deny default)"), "profile must deny by default");
        assert!(!PROFILE.contains("(allow process-fork"), "fork must stay denied");
        assert!(!PROFILE.contains("(allow process-exec"), "exec must stay denied");
        assert!(!PROFILE.contains("(allow file-write"), "file writes must stay denied");
        assert!(!PROFILE.contains("(allow network-inbound"), "inbound must stay denied");
        assert!(!PROFILE.contains("(allow network-bind"), "bind must stay denied");
    }

    #[test]
    fn read_grant_is_narrowed_and_egress_is_port_limited() {
        assert!(
            !PROFILE.contains("(allow file-read*)"),
            "the global read grant must stay narrowed: reads are filtered by path"
        );
        assert!(
            PROFILE.contains(r#"(subpath (param "SANDBOX_WORKDIR"))"#),
            "the parameterised read root must be the child's own non-system read scope"
        );
        assert!(PROFILE.contains(r#"(remote tcp "*:443")"#), "https egress allowed");
        assert!(PROFILE.contains(r#"(remote tcp "*:80")"#), "plaintext http egress allowed");
        assert!(
            !PROFILE.contains(r#"(remote tcp "*:*")"#)
                && !PROFILE.contains("(allow network-outbound)"),
            "egress must stay port-limited, never blanket outbound"
        );
    }

    #[test]
    fn profile_allows_what_the_decoder_needs() {
        assert!(PROFILE.contains("SANDBOX_WORKDIR"), "read-root parameter referenced");
        assert!(PROFILE.contains("HOST_BOOTSTRAP"), "bootstrap parameter referenced");
        assert!(
            PROFILE.contains(r"^com\.decentraland\.uuav\.helper\."),
            "bootstrap mach-lookup regex present"
        );
        assert!(PROFILE.contains("ipc-posix-shm"), "segment shm access allowed");
        assert!(
            PROFILE.contains("com.apple.coremedia.videodecoder"),
            "the VideoToolbox decoder XPC is allowed"
        );
        assert!(PROFILE.contains(r#"(subpath "/System")"#), "system roots are readable");
        assert!(
            PROFILE.contains("/private/var/run/mDNSResponder"),
            "the resolver socket is reachable"
        );
    }

    #[test]
    fn profile_is_nul_free_and_compilable_as_a_c_string() {
        assert!(CString::new(PROFILE).is_ok(), "profile must be a valid C string");
    }

    #[test]
    fn parameter_list_has_the_flattened_key_value_shape() {
        let params = parameter_strings("com.decentraland.uuav.helper.42.deadbeef", "/opt/uuav")
            .expect("well-formed parameters");
        assert_eq!(params.len(), 4, "two key/value pairs");
        assert_eq!(params[0].to_str().unwrap(), PARAM_WORKDIR);
        assert_eq!(params[1].to_str().unwrap(), "/opt/uuav");
        assert_eq!(params[2].to_str().unwrap(), PARAM_BOOTSTRAP);
        assert_eq!(params[3].to_str().unwrap(), "com.decentraland.uuav.helper.42.deadbeef");
    }

    #[test]
    fn parameter_list_rejects_empty_inputs() {
        assert!(parameter_strings("", "/opt/uuav").is_err());
        assert!(parameter_strings("com.x", "").is_err());
    }
}
