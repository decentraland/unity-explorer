
use std::ffi::{CStr, CString};
use std::io;
use std::ptr::NonNull;

use anyhow::{Context as _, Result, anyhow, bail};

use crate::protocol::{SEGMENT_BYTES, SHM_NAME_MAX, SharedSegment};

struct Fd(libc::c_int);

impl Drop for Fd {
    fn drop(&mut self) {
        if self.0 >= 0 {
            let _ = unsafe { libc::close(self.0) };
        }
    }
}

pub struct Mapping {
    segment: NonNull<SharedSegment>,
    bytes: usize,
}

unsafe impl Send for Mapping {}
unsafe impl Sync for Mapping {}

impl Mapping {
    pub fn create(name: &CStr) -> Result<Self> {
        check_name(name)?;
        let raw = unsafe {
            libc::shm_open(
                name.as_ptr(),
                libc::O_RDWR | libc::O_CREAT | libc::O_EXCL,
                0o600 as libc::c_uint,
            )
        };
        if raw < 0 {
            return Err(io::Error::last_os_error())
                .with_context(|| format!("shm_open({name:?}, O_CREAT|O_EXCL)"));
        }
        let fd = Fd(raw);

        let length = libc::off_t::try_from(SEGMENT_BYTES)
            .map_err(|_ignored| anyhow!("segment size does not fit an off_t"))?;
        if unsafe { libc::ftruncate(fd.0, length) } != 0 {
            let error = io::Error::last_os_error();
            let _ = unlink(name);
            return Err(error).with_context(|| format!("ftruncate({name:?}, {SEGMENT_BYTES})"));
        }

        Self::map(&fd).inspect_err(|_ignored| {
            let _ = unlink(name);
        })
    }

    pub fn attach(name: &CStr) -> Result<Self> {
        check_name(name)?;
        let raw = unsafe { libc::shm_open(name.as_ptr(), libc::O_RDWR, 0 as libc::c_uint) };
        if raw < 0 {
            return Err(io::Error::last_os_error())
                .with_context(|| format!("shm_open({name:?}, O_RDWR)"));
        }
        let fd = Fd(raw);

        let mut status: libc::stat = unsafe { std::mem::zeroed() };
        if unsafe { libc::fstat(fd.0, &raw mut status) } != 0 {
            return Err(io::Error::last_os_error()).with_context(|| format!("fstat({name:?})"));
        }
        let size = u64::try_from(status.st_size).unwrap_or(0);
        if size < SEGMENT_BYTES as u64 {
            bail!("{name:?} is {size} bytes; the protocol needs {SEGMENT_BYTES}");
        }

        Self::map(&fd)
    }

    pub const fn segment(&self) -> &SharedSegment {
        unsafe { self.segment.as_ref() }
    }

    pub const fn bytes(&self) -> usize {
        self.bytes
    }

    fn map(fd: &Fd) -> Result<Self> {
        let base = unsafe {
            libc::mmap(
                std::ptr::null_mut(),
                SEGMENT_BYTES,
                libc::PROT_READ | libc::PROT_WRITE,
                libc::MAP_SHARED,
                fd.0,
                0,
            )
        };
        if base == libc::MAP_FAILED {
            return Err(io::Error::last_os_error()).context("mmap of the shared segment");
        }

        let checked = unsafe { SharedSegment::from_mapping::<'_>(base.cast::<u8>(), SEGMENT_BYTES) };
        let pointer = match checked {
            Ok(segment) => NonNull::from(segment),
            Err(fault) => {
                let _ = unsafe { libc::munmap(base, SEGMENT_BYTES) };
                return Err(fault.into());
            }
        };

        Ok(Self {
            segment: pointer,
            bytes: SEGMENT_BYTES,
        })
    }
}

impl Drop for Mapping {
    fn drop(&mut self) {
        let _ = unsafe { libc::munmap(self.segment.as_ptr().cast::<libc::c_void>(), self.bytes) };
    }
}

pub fn unlink(name: &CStr) -> Result<()> {
    if unsafe { libc::shm_unlink(name.as_ptr()) } == 0 {
        return Ok(());
    }
    let error = io::Error::last_os_error();
    if error.raw_os_error() == Some(libc::ENOENT) {
        return Ok(());
    }
    Err(error).with_context(|| format!("shm_unlink({name:?})"))
}

pub struct NameGuard {
    name: CString,
    linked: bool,
}

impl NameGuard {
    pub const fn new(name: CString) -> Self {
        Self { name, linked: true }
    }

    pub fn name(&self) -> &CStr {
        self.name.as_c_str()
    }

    pub fn unlink_now(&mut self) {
        if self.linked {
            self.linked = false;
            let _ = unlink(self.name.as_c_str());
        }
    }
}

impl Drop for NameGuard {
    fn drop(&mut self) {
        self.unlink_now();
    }
}

fn check_name(name: &CStr) -> Result<()> {
    let bytes = name.to_bytes();
    if bytes.len() > SHM_NAME_MAX {
        bail!(
            "shared-memory name is {} bytes; macOS truncates past {SHM_NAME_MAX}",
            bytes.len()
        );
    }
    if bytes.first() != Some(&b'/') {
        bail!("shared-memory name {name:?} must start with '/'");
    }
    Ok(())
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
    use crate::protocol;

    fn unique_name() -> CString {
        protocol::segment_name(std::process::id(), protocol::nonce()).unwrap()
    }

    #[test]
    fn a_created_segment_can_be_attached_and_is_shared() {
        let name = unique_name();
        let host = Mapping::create(&name).unwrap();
        assert_eq!(host.bytes(), SEGMENT_BYTES);
        host.segment().initialise(std::process::id(), 0xdead_beef);

        let peer = Mapping::attach(&name).unwrap();
        peer.segment()
            .attach(std::process::id(), 0xdead_beef)
            .expect("the header the host stamped must satisfy the helper's check");

        assert_eq!(host.segment().helper_pid(), std::process::id());
        assert!(peer.segment().video.publish(&frame_record()));
        assert_eq!(host.segment().video.depth().unwrap(), 1);

        unlink(&name).unwrap();
    }

    #[test]
    fn attaching_with_the_wrong_cookie_is_rejected() {
        let name = unique_name();
        let host = Mapping::create(&name).unwrap();
        host.segment().initialise(std::process::id(), 1);
        let peer = Mapping::attach(&name).unwrap();
        let fault = peer
            .segment()
            .attach(std::process::id(), 2)
            .expect_err("a cookie mismatch must be refused");
        assert!(format!("{fault}").contains("cookie"), "{fault}");
        unlink(&name).unwrap();
    }

    #[test]
    fn a_second_create_on_the_same_name_fails() {
        let name = unique_name();
        let host = Mapping::create(&name).unwrap();
        assert!(
            Mapping::create(&name).is_err(),
            "O_EXCL must refuse the second creation rather than share it"
        );
        unlink(&name).unwrap();
        drop(host);
    }

    #[test]
    fn attaching_a_name_that_does_not_exist_fails() {
        let name = unique_name();
        assert!(Mapping::attach(&name).is_err());
    }

    #[test]
    fn unlink_is_idempotent_and_the_object_outlives_the_name() {
        let name = unique_name();
        let mapping = Mapping::create(&name).unwrap();
        unlink(&name).unwrap();
        unlink(&name).unwrap();
        mapping.segment().initialise(std::process::id(), 7);
        mapping
            .segment()
            .attach(std::process::id(), 7)
            .expect("the mapping still works after its name is gone");
    }

    #[test]
    fn the_guard_unlinks_once_and_on_drop() {
        let name = unique_name();
        let mapping = Mapping::create(&name).unwrap();
        {
            let mut guard = NameGuard::new(name.clone());
            assert_eq!(guard.name(), name.as_c_str());
            guard.unlink_now();
            guard.unlink_now();
        }
        assert!(
            Mapping::attach(&name).is_err(),
            "the guard must have removed the name"
        );
        drop(mapping);
    }

    #[test]
    fn names_that_the_platform_would_mangle_are_refused() {
        let long = CString::new("/".repeat(SHM_NAME_MAX + 1)).unwrap();
        assert!(Mapping::create(&long).is_err());
        let relative = CString::new("uuav.no.slash").unwrap();
        assert!(Mapping::create(&relative).is_err());
        assert!(Mapping::attach(&relative).is_err());
    }

    fn frame_record() -> protocol::FrameRecord {
        protocol::FrameRecord {
            info: protocol::FrameInfoWire {
                yuv_to_rgb: [0.0; 12],
                uv_transform: [1.0, 0.0, 0.0, 0.0, -1.0, 1.0],
                visible_width: 16,
                visible_height: 16,
                plane_width: [16, 8],
                plane_height: [16, 8],
                colorspace: 1,
                color_range: 1,
                color_primaries: 1,
                rotation: 0,
                bit_depth: 8,
            },
            flags: 0,
            pts: 0.0,
            sequence: 1,
            slot: 0,
            reserved: 0,
        }
    }
}
