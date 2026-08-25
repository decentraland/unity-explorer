use anyhow::{Result, anyhow};
use objc2::Message as _;
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_core_video::{CVPixelBuffer, CVPixelBufferGetIOSurface};
use objc2_io_surface::IOSurfaceRef;
use objc2_metal::{
    MTLBlitCommandEncoder, MTLCommandBuffer, MTLCommandEncoder, MTLCommandQueue, MTLDevice,
    MTLOrigin, MTLPixelFormat, MTLSize, MTLStorageMode, MTLTexture, MTLTextureDescriptor,
    MTLTextureUsage,
};
use std::os::raw::c_void;

use crate::hw_device::HwDevice;
use crate::video_decoder::VideoFrame;

type Texture = Retained<ProtocolObject<dyn MTLTexture>>;

/// Non-owning view of one of [`VideoOutput`]'s presentation planes: one
/// `MTLTexture` per plane (0 = Y `R8Unorm`, 1 = UV `RG8Unorm`). The retain
/// held here only pins the texture while the view lives; `VideoOutput`
/// stays the owner and recreates both planes together on a resolution
/// change, after which consumers re-query both (contract of
/// `uuav_player_get_video_texture`).
pub(crate) struct VideoTextureView {
    texture: Texture,
}

impl VideoTextureView {
    pub(crate) fn raw_ptr_mut(&self) -> *mut c_void {
        Retained::as_ptr(&self.texture).cast_mut().cast::<c_void>()
    }
}

/// Presentation target: two owned per-plane textures (Y `R8Unorm`, UV
/// `RG8Unorm`) on the engine-provided device that decoded frames are blitted
/// into on the render thread. The engine consumes the raw `id<MTLTexture>`
/// pointers directly - unlike D3D11 there is no multi-plane resource to
/// create views over, so each plane is its own texture.
pub(crate) struct VideoOutput {
    device: Retained<ProtocolObject<dyn MTLDevice>>,
    queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
    planes: Option<SizedPlanes>,
    /// Previous plane generation, kept alive for one resolution change so
    /// the pointers the engine still wraps stay valid until its next poll
    /// notices the new plane-0 pointer.
    retired: Option<SizedPlanes>,
}

/// The plane textures together with the dimensions they were created for.
struct SizedPlanes {
    y: Texture,
    uv: Texture,
    width: u32,
    height: u32,
}

impl SizedPlanes {
    const fn matches(&self, width: u32, height: u32) -> bool {
        self.width == width && self.height == height
    }
}

// The Metal objects are only used behind the player's mutex and Metal
// devices/queues/textures are free-threaded; Retained<ProtocolObject<..>>
// merely lacks the marker because ObjC protocols leave it undeclared.
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for VideoOutput {}

impl VideoOutput {
    pub(crate) fn new(device: &HwDevice) -> Self {
        Self {
            device: device.metal_device().retain(),
            queue: device.command_queue().retain(),
            planes: None,
            retired: None,
        }
    }

    /// One plane texture, once the first frame has been presented.
    pub(crate) fn texture(&self, plane: i32) -> Option<VideoTextureView> {
        let planes = self.planes.as_ref()?;
        let texture = match plane {
            0 => planes.y.clone(),
            1 => planes.uv.clone(),
            _ => return None,
        };
        Some(VideoTextureView { texture })
    }

    /// Takes no decode context on purpose (the D3D11 sibling needs one for
    /// its immediate-context lock): the blit runs on the plugin's own queue
    /// against an immutable decoded surface.
    pub(crate) fn present(&mut self, frame: &VideoFrame) -> Result<()> {
        // NV12 requires even dimensions; negative decoder sizes fold to 0
        let width = u32::try_from(frame.width()).unwrap_or(0) & !1;
        let height = u32::try_from(frame.height()).unwrap_or(0) & !1;
        if width == 0 || height == 0 {
            return Err(anyhow!("frame has no presentable size"));
        }
        self.ensure_planes(width, height)?;
        let planes = self
            .planes
            .as_ref()
            .ok_or_else(|| anyhow!("presentation planes are missing"))?;

        let pixel_buffer = frame.pixel_buffer();
        if pixel_buffer.is_null() {
            return Err(anyhow!("decoded frame has no CVPixelBuffer"));
        }
        let pixel_buffer: &CVPixelBuffer = unsafe { &*pixel_buffer.cast::<CVPixelBuffer>() };

        // FFmpeg's VideoToolbox hwcontext requests IOSurface backing, so a
        // missing surface is a broken frame, not a fallback case
        let surface = CVPixelBufferGetIOSurface(Some(pixel_buffer))
            .ok_or_else(|| anyhow!("CVPixelBuffer is not IOSurface-backed"))?;

        // zero-copy views over the decoder surface planes, at the padded
        // (coded-size) dimensions the IOSurface actually has
        let src_y = self.wrap_surface_plane(&surface, 0, MTLPixelFormat::R8Unorm)?;
        let src_uv = self.wrap_surface_plane(&surface, 1, MTLPixelFormat::RG8Unorm)?;

        let buffer = self
            .queue
            .commandBuffer()
            .ok_or_else(|| anyhow!("command queue returned no command buffer"))?;
        let encoder = buffer
            .blitCommandEncoder()
            .ok_or_else(|| anyhow!("command buffer returned no blit encoder"))?;

        // the visible box only (Metal analog of the D3D11_BOX copy): the
        // IOSurface planes can be padded past the visible size
        let origin = MTLOrigin { x: 0, y: 0, z: 0 };
        unsafe {
            encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
                &src_y,
                0,
                0,
                origin,
                MTLSize {
                    width: width as usize,
                    height: height as usize,
                    depth: 1,
                },
                &planes.y,
                0,
                0,
                origin,
            );
            encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
                &src_uv,
                0,
                0,
                origin,
                MTLSize {
                    width: (width / 2) as usize,
                    height: (height / 2) as usize,
                    depth: 1,
                },
                &planes.uv,
                0,
                0,
                origin,
            );
        }
        encoder.endEncoding();
        buffer.commit();
        // synchronous on purpose: orders the copy against Unity's own queue
        // and proves the borrowed frame/IOSurface outlives the GPU reads.
        // Optimization headroom (MTLSharedEvent, IUnityGraphicsMetal) is
        // deliberately deferred.
        buffer.waitUntilCompleted();
        Ok(())
    }

    /// Ensures the owned plane textures match the size, recreating them on
    /// change and retiring the previous generation for one more poll cycle.
    fn ensure_planes(&mut self, width: u32, height: u32) -> Result<()> {
        if self
            .planes
            .as_ref()
            .is_some_and(|planes| planes.matches(width, height))
        {
            return Ok(());
        }

        let y = self.new_plane(MTLPixelFormat::R8Unorm, width, height)?;
        let uv = self.new_plane(MTLPixelFormat::RG8Unorm, width / 2, height / 2)?;
        // the generation before last dies here; the engine has had a full
        // poll cycle to stop wrapping it
        let _previous = self.retired.take();
        self.retired = self.planes.take();
        self.planes = Some(SizedPlanes {
            y,
            uv,
            width,
            height,
        });
        Ok(())
    }

    /// One owned destination plane the engine samples from.
    fn new_plane(&self, format: MTLPixelFormat, width: u32, height: u32) -> Result<Texture> {
        let descriptor = unsafe {
            MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
                format,
                width as usize,
                height as usize,
                false,
            )
        };
        descriptor.setUsage(MTLTextureUsage::ShaderRead);
        descriptor.setStorageMode(MTLStorageMode::Private);
        self.device
            .newTextureWithDescriptor(&descriptor)
            .ok_or_else(|| anyhow!("newTexture({format:?} {width}x{height}) returned no texture"))
    }

    /// Zero-copy `MTLTexture` view over one IOSurface plane of the decoded
    /// frame. Chosen over `CVMetalTextureCache`: no cache object to own and
    /// flush, no `CVMetalTextureRef` lifetime to manage.
    fn wrap_surface_plane(
        &self,
        surface: &IOSurfaceRef,
        plane: usize,
        format: MTLPixelFormat,
    ) -> Result<Texture> {
        let width = surface.width_of_plane(plane);
        let height = surface.height_of_plane(plane);
        let descriptor = unsafe {
            MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
                format, width, height, false,
            )
        };
        // IOSurface-backed textures must be Shared (they alias CPU-visible
        // memory); the default usage (ShaderRead) is fine for a blit source
        descriptor.setStorageMode(MTLStorageMode::Shared);
        self.device
            .newTextureWithDescriptor_iosurface_plane(&descriptor, surface, plane)
            .ok_or_else(|| anyhow!("wrapping IOSurface plane {plane} as {format:?} failed"))
    }
}
