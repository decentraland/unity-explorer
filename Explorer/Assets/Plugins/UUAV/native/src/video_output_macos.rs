use anyhow::{Result, anyhow};
use objc2::Message as _;
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_core_video::{CVPixelBuffer, CVPixelBufferGetIOSurface};
use objc2_io_surface::IOSurfaceRef;
use objc2_metal::{MTLDevice, MTLPixelFormat, MTLStorageMode, MTLTexture, MTLTextureDescriptor};
use std::collections::{HashMap, VecDeque, hash_map::Entry};
use std::mem;
use std::os::raw::c_void;

use crate::frame_info::FrameInfo;
use crate::hw_device::HwDevice;
use crate::video_decoder::VideoFrame;

type Texture = Retained<ProtocolObject<dyn MTLTexture>>;
type Planes = [Texture; 2];

const RETAINED_FRAMES: usize = 4;

pub(crate) struct VideoTextureView {
    texture: Texture,
}

impl VideoTextureView {
    pub(crate) fn raw_ptr_mut(&self) -> *mut c_void {
        texture_ptr(&self.texture)
    }
}

pub(crate) struct VideoOutput {
    device: Retained<ProtocolObject<dyn MTLDevice>>,
    planes: HashMap<usize, Planes>,
    retired: HashMap<usize, Planes>,
    published: Option<Planes>,
    retained: VecDeque<VideoFrame>,
    info: Option<FrameInfo>,
    frames: u64,
    generation: u64,
}

#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for VideoOutput {}

impl VideoOutput {
    pub(crate) fn new(device: &HwDevice) -> Self {
        Self {
            device: device.metal_device().retain(),
            planes: HashMap::new(),
            retired: HashMap::new(),
            published: None,
            retained: VecDeque::new(),
            info: None,
            frames: 0,
            generation: 0,
        }
    }

    pub(crate) fn texture(&self, plane: i32) -> Option<VideoTextureView> {
        let index = usize::try_from(plane).ok()?;
        let texture = self.published.as_ref()?.get(index)?.clone();
        Some(VideoTextureView { texture })
    }

    pub(crate) fn state(&self) -> Option<FrameInfo> {
        let planes = self.published.as_ref()?;
        let mut info = self.info?;
        info.planes = [
            texture_ptr(&planes[0]) as usize,
            texture_ptr(&planes[1]) as usize,
        ];
        Some(info)
    }

    pub(crate) fn present(&mut self, frame: VideoFrame) -> Result<()> {
        let mut info = frame.info();
        if info.visible_width == 0 || info.visible_height == 0 {
            return Err(anyhow!("frame has no presentable size"));
        }

        let pixel_buffer = frame.pixel_buffer();
        if pixel_buffer.is_null() {
            return Err(anyhow!("decoded frame has no CVPixelBuffer"));
        }
        let pixel_buffer: &CVPixelBuffer = unsafe { &*pixel_buffer.cast::<CVPixelBuffer>() };

        let surface = CVPixelBufferGetIOSurface(Some(pixel_buffer))
            .ok_or_else(|| anyhow!("CVPixelBuffer is not IOSurface-backed"))?;
        let surface: &IOSurfaceRef = &surface;
        info.fit_planes([plane_size(surface, 0), plane_size(surface, 1)]);

        if self.info.is_some_and(|previous| {
            previous.plane_width != info.plane_width || previous.plane_height != info.plane_height
        }) {
            self.retired = mem::take(&mut self.planes);
            self.generation = self.generation.wrapping_add(1);
        } else {
            self.retired.clear();
        }

        let formats = if info.bit_depth > 8 {
            [MTLPixelFormat::R16Unorm, MTLPixelFormat::RG16Unorm]
        } else {
            [MTLPixelFormat::R8Unorm, MTLPixelFormat::RG8Unorm]
        };
        let published = match self.planes.entry(std::ptr::from_ref(surface) as usize) {
            Entry::Occupied(entry) => entry.into_mut(),
            Entry::Vacant(entry) => {
                let [y, uv] = formats;
                entry.insert([
                    wrap_plane(&self.device, surface, 0, y)?,
                    wrap_plane(&self.device, surface, 1, uv)?,
                ])
            }
        }
        .clone();

        self.frames = self.frames.wrapping_add(1);
        info.frame_index = self.frames;
        info.surface_generation = self.generation;
        self.published = Some(published);
        self.info = Some(info);

        self.retained.push_back(frame);
        while self.retained.len() > RETAINED_FRAMES {
            self.retained.pop_front();
        }
        Ok(())
    }
}

fn texture_ptr(texture: &Texture) -> *mut c_void {
    Retained::as_ptr(texture).cast_mut().cast::<c_void>()
}

fn plane_size(surface: &IOSurfaceRef, plane: usize) -> (u32, u32) {
    (
        surface.width_of_plane(plane) as u32,
        surface.height_of_plane(plane) as u32,
    )
}

fn wrap_plane(
    device: &ProtocolObject<dyn MTLDevice>,
    surface: &IOSurfaceRef,
    plane: usize,
    format: MTLPixelFormat,
) -> Result<Texture> {
    let descriptor = unsafe {
        MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
            format,
            surface.width_of_plane(plane),
            surface.height_of_plane(plane),
            false,
        )
    };
    descriptor.setStorageMode(MTLStorageMode::Shared);
    device
        .newTextureWithDescriptor_iosurface_plane(&descriptor, surface, plane)
        .ok_or_else(|| anyhow!("wrapping IOSurface plane {plane} as {format:?} failed"))
}
