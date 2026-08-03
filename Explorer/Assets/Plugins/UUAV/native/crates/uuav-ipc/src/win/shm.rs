
use std::io;
use std::os::windows::io::{AsRawHandle as _, FromRawHandle as _, OwnedHandle};
use std::ptr;
use std::ptr::NonNull;

use anyhow::{Context as _, Result, anyhow};
use windows_sys::Win32::System::Memory::{
    CreateFileMappingW, FILE_MAP_READ, FILE_MAP_WRITE, MapViewOfFile, MEMORY_MAPPED_VIEW_ADDRESS,
    PAGE_READWRITE, UnmapViewOfFile,
};

use crate::protocol::{SEGMENT_BYTES, SharedSegment};
use crate::win::wire;

pub struct Mapping {
    section: OwnedHandle,
    segment: NonNull<SharedSegment>,
    bytes: usize,
}

unsafe impl Send for Mapping {}
unsafe impl Sync for Mapping {}

impl Mapping {
    pub fn create() -> Result<Self> {
        let size = u64::try_from(SEGMENT_BYTES)
            .map_err(|_ignored| anyhow!("segment size does not fit a section size"))?;
        let section = unsafe {
            CreateFileMappingW(
                windows_sys::Win32::Foundation::INVALID_HANDLE_VALUE,
                ptr::null(),
                PAGE_READWRITE,
                (size >> 32) as u32,
                size as u32,
                ptr::null(),
            )
        };
        if section.is_null() {
            return Err(io::Error::last_os_error())
                .with_context(|| format!("CreateFileMappingW({SEGMENT_BYTES} bytes, anonymous)"));
        }
        let section = unsafe { OwnedHandle::from_raw_handle(section.cast()) };
        Self::map(section)
    }

    pub fn from_inherited(raw_value: u64) -> Result<Self> {
        wire::validate_handle_value(0, raw_value)
            .map_err(|fault| anyhow!("inherited segment handle is not a handle: {fault}"))?;
        let section = unsafe { OwnedHandle::from_raw_handle(raw_value as usize as *mut _) };
        Self::map(section)
    }

    pub const fn segment(&self) -> &SharedSegment {
        unsafe { self.segment.as_ref() }
    }

    pub const fn bytes(&self) -> usize {
        self.bytes
    }

    pub const fn section(&self) -> &OwnedHandle {
        &self.section
    }

    fn map(section: OwnedHandle) -> Result<Self> {
        let view = unsafe {
            MapViewOfFile(
                section.as_raw_handle().cast(),
                FILE_MAP_READ | FILE_MAP_WRITE,
                0,
                0,
                SEGMENT_BYTES,
            )
        };
        if view.Value.is_null() {
            return Err(io::Error::last_os_error())
                .with_context(|| format!("MapViewOfFile({SEGMENT_BYTES} bytes)"));
        }

        let checked =
            unsafe { SharedSegment::from_mapping::<'_>(view.Value.cast::<u8>(), SEGMENT_BYTES) };
        let segment = match checked {
            Ok(segment) => NonNull::from(segment),
            Err(fault) => {
                unsafe { UnmapViewOfFile(view) };
                return Err(fault.into());
            }
        };

        Ok(Self {
            section,
            segment,
            bytes: SEGMENT_BYTES,
        })
    }
}

impl Drop for Mapping {
    fn drop(&mut self) {
        unsafe {
            UnmapViewOfFile(MEMORY_MAPPED_VIEW_ADDRESS {
                Value: self.segment.as_ptr().cast(),
            })
        };
    }
}

pub struct PlaneSection {
    section: OwnedHandle,
    base: NonNull<u8>,
    bytes: usize,
}

unsafe impl Send for PlaneSection {}
unsafe impl Sync for PlaneSection {}

impl PlaneSection {
    pub fn create(bytes: usize) -> Result<Self> {
        let size =
            u64::try_from(bytes).map_err(|_ignored| anyhow!("plane section size does not fit"))?;
        let section = unsafe {
            CreateFileMappingW(
                windows_sys::Win32::Foundation::INVALID_HANDLE_VALUE,
                ptr::null(),
                PAGE_READWRITE,
                (size >> 32) as u32,
                size as u32,
                ptr::null(),
            )
        };
        if section.is_null() {
            return Err(io::Error::last_os_error())
                .with_context(|| format!("CreateFileMappingW({bytes} bytes, plane section)"));
        }
        let section = unsafe { OwnedHandle::from_raw_handle(section.cast()) };
        Self::map(section, bytes, FILE_MAP_READ | FILE_MAP_WRITE)
    }

    pub fn open_read_only(section: OwnedHandle, bytes: usize) -> Result<Self> {
        Self::map(section, bytes, FILE_MAP_READ)
    }

    fn map(section: OwnedHandle, bytes: usize, access: u32) -> Result<Self> {
        if bytes == 0 {
            return Err(anyhow!("a plane section of zero bytes is not a frame"));
        }
        let view = unsafe { MapViewOfFile(section.as_raw_handle().cast(), access, 0, 0, bytes) };
        let Some(base) = NonNull::new(view.Value.cast::<u8>()) else {
            return Err(io::Error::last_os_error())
                .with_context(|| format!("MapViewOfFile({bytes} bytes, plane section)"));
        };
        Ok(Self {
            section,
            base,
            bytes,
        })
    }

    pub const fn bytes(&self) -> usize {
        self.bytes
    }

    pub const fn section(&self) -> &OwnedHandle {
        &self.section
    }

    pub fn view(&self, offset: usize, length: usize) -> Option<&[u8]> {
        let end = offset.checked_add(length)?;
        if end > self.bytes {
            return None;
        }
        Some(unsafe { std::slice::from_raw_parts(self.base.as_ptr().add(offset), length) })
    }

    pub unsafe fn view_mut(&mut self, offset: usize, length: usize) -> Option<&mut [u8]> {
        let end = offset.checked_add(length)?;
        if end > self.bytes {
            return None;
        }
        Some(unsafe { std::slice::from_raw_parts_mut(self.base.as_ptr().add(offset), length) })
    }
}

impl Drop for PlaneSection {
    fn drop(&mut self) {
        unsafe {
            UnmapViewOfFile(MEMORY_MAPPED_VIEW_ADDRESS {
                Value: self.base.as_ptr().cast(),
            })
        };
    }
}

pub const fn nv12_bytes(stride: usize, height: usize) -> usize {
    let luma = stride.saturating_mul(height);
    let chroma = stride.saturating_mul(height.div_ceil(2));
    luma.saturating_add(chroma)
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
    fn a_created_segment_is_shared_between_two_mappings_of_the_same_section() {
        let host = Mapping::create().unwrap();
        assert_eq!(host.bytes(), SEGMENT_BYTES);
        host.segment().initialise(std::process::id(), 0xdead_beef);

        let duplicate = host.section().try_clone().unwrap();
        let peer = Mapping::from_inherited(duplicate.as_raw_handle() as usize as u64).unwrap();
        std::mem::forget(duplicate);

        peer.segment()
            .attach(std::process::id(), 0xdead_beef)
            .expect("the header the host stamped must satisfy the helper's check");
        assert_eq!(host.segment().helper_pid(), std::process::id());
    }

    #[test]
    fn a_garbage_handle_value_is_refused_before_the_kernel_sees_it() {
        for hostile in [0u64, u64::MAX, 0xFFFF_FFFF, 0x1_0000_0000, 0x1001] {
            assert!(Mapping::from_inherited(hostile).is_err(), "{hostile:#x}");
        }
    }

    #[test]
    fn plane_views_are_bounded_by_the_mapped_length() {
        let section = PlaneSection::create(4096).unwrap();
        assert!(section.view(0, 4096).is_some());
        assert!(section.view(4096, 0).is_some());
        assert!(section.view(0, 4097).is_none());
        assert!(section.view(4096, 1).is_none());
        assert!(section.view(usize::MAX, 1).is_none());
        assert!(section.view(1, usize::MAX).is_none());
    }

    #[test]
    fn nv12_sizing_rounds_the_chroma_plane_up() {
        assert_eq!(nv12_bytes(1920, 1080), 1920 * 1080 + 1920 * 540);
        assert_eq!(nv12_bytes(640, 361), 640 * 361 + 640 * 181);
        assert_eq!(nv12_bytes(usize::MAX, usize::MAX), usize::MAX);
    }
}
