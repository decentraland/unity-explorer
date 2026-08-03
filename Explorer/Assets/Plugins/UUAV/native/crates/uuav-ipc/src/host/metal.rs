
use anyhow::{Result, anyhow};
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_foundation::NSString;
use objc2_io_surface::IOSurfaceRef;
use objc2_metal::{
    MTLBuffer, MTLCommandBuffer, MTLCommandEncoder, MTLCommandQueue, MTLComputeCommandEncoder,
    MTLComputePipelineState, MTLCreateSystemDefaultDevice, MTLDevice, MTLLibrary, MTLPixelFormat,
    MTLResourceOptions, MTLSize, MTLStorageMode, MTLTexture, MTLTextureDescriptor, MTLTextureUsage,
};
use std::os::raw::c_void;
use std::ptr::NonNull;

use crate::protocol::{LUMA_CHECKSUM_FUNCTION, LUMA_CHECKSUM_MSL, SurfaceGeometry};

type Texture = Retained<ProtocolObject<dyn MTLTexture>>;

const CHECKSUMMABLE_PIXEL_FORMATS: [u32; 2] = [
    0x3432_3076,
    0x3432_3066,
];

const TEN_BIT_PIXEL_FORMATS: [u32; 6] = [
    0x7834_3230,
    0x7866_3230,
    0x7834_3232,
    0x7866_3232,
    0x7834_3434,
    0x7866_3434,
];

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct PlaneFormats {
    luma: MTLPixelFormat,
    chroma: MTLPixelFormat,
    bit_depth: u32,
    checksum: Option<MTLPixelFormat>,
}

impl PlaneFormats {
    fn of(pixel_format: u32) -> Self {
        if TEN_BIT_PIXEL_FORMATS.contains(&pixel_format) {
            Self {
                luma: MTLPixelFormat::R16Unorm,
                chroma: MTLPixelFormat::RG16Unorm,
                bit_depth: 10,
                checksum: None,
            }
        } else {
            Self {
                luma: MTLPixelFormat::R8Unorm,
                chroma: MTLPixelFormat::RG8Unorm,
                bit_depth: 8,
                checksum: CHECKSUMMABLE_PIXEL_FORMATS
                    .contains(&pixel_format)
                    .then_some(MTLPixelFormat::R8Uint),
            }
        }
    }
}

pub struct SurfaceTextures {
    y: Texture,
    uv: Option<Texture>,
    y_uint: Option<Texture>,
    bit_depth: u32,
}

impl SurfaceTextures {
    pub fn plane_pointers(&self) -> [usize; 2] {
        [
            texture_pointer(&self.y),
            self.uv.as_ref().map_or(0, texture_pointer),
        ]
    }

    pub const fn is_checksummable(&self) -> bool {
        self.y_uint.is_some()
    }

    pub const fn bit_depth(&self) -> u32 {
        self.bit_depth
    }
}

fn texture_pointer(texture: &Texture) -> usize {
    Retained::as_ptr(texture) as usize
}

pub struct MetalContext {
    device: Retained<ProtocolObject<dyn MTLDevice>>,
    queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
    pipeline: Retained<ProtocolObject<dyn MTLComputePipelineState>>,
    max_threads_per_group: usize,
}

impl MetalContext {
    pub fn new() -> Result<Self> {
        let device = MTLCreateSystemDefaultDevice()
            .ok_or_else(|| anyhow!("no system default MTLDevice"))?;
        let queue = device
            .newCommandQueue()
            .ok_or_else(|| anyhow!("newCommandQueue failed"))?;

        let source = NSString::from_str(LUMA_CHECKSUM_MSL);
        let library = device
            .newLibraryWithSource_options_error(&source, None)
            .map_err(|error| anyhow!("compiling LUMA_CHECKSUM_MSL failed: {error}"))?;
        let name = NSString::from_str(LUMA_CHECKSUM_FUNCTION);
        let function = library
            .newFunctionWithName(&name)
            .ok_or_else(|| anyhow!("{LUMA_CHECKSUM_FUNCTION} missing from the compiled library"))?;
        let pipeline = device
            .newComputePipelineStateWithFunction_error(&function)
            .map_err(|error| anyhow!("compute pipeline failed: {error}"))?;
        let max_threads_per_group = pipeline.maxTotalThreadsPerThreadgroup().max(1);

        Ok(Self {
            device,
            queue,
            pipeline,
            max_threads_per_group,
        })
    }

    pub fn device_name(&self) -> String {
        self.device.name().to_string()
    }

    pub fn wrap(
        &self,
        surface: &IOSurfaceRef,
        geometry: &SurfaceGeometry,
    ) -> Result<SurfaceTextures> {
        let (Some(&luma_width), Some(&luma_height)) = (
            geometry.plane_width.first(),
            geometry.plane_height.first(),
        ) else {
            return Err(anyhow!("geometry has no luma plane"));
        };

        let formats = PlaneFormats::of(surface.pixel_format());

        let y = self.wrap_plane(surface, 0, luma_width, luma_height, formats.luma)?;

        let uv = if geometry.plane_count >= 2 {
            let (Some(&width), Some(&height)) = (
                geometry.plane_width.get(1),
                geometry.plane_height.get(1),
            ) else {
                return Err(anyhow!("geometry claims two planes but sizes only one"));
            };
            Some(self.wrap_plane(surface, 1, width, height, formats.chroma)?)
        } else {
            None
        };

        let y_uint = match formats.checksum {
            Some(format) => Some(self.wrap_plane(surface, 0, luma_width, luma_height, format)?),
            None => None,
        };

        Ok(SurfaceTextures {
            y,
            uv,
            y_uint,
            bit_depth: formats.bit_depth,
        })
    }

    fn wrap_plane(
        &self,
        surface: &IOSurfaceRef,
        plane: usize,
        width: u32,
        height: u32,
        format: MTLPixelFormat,
    ) -> Result<Texture> {
        let descriptor = unsafe {
            MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
                format,
                width as usize,
                height as usize,
                false,
            )
        };
        descriptor.setStorageMode(MTLStorageMode::Shared);
        descriptor.setUsage(MTLTextureUsage::ShaderRead);
        self.device
            .newTextureWithDescriptor_iosurface_plane(&descriptor, surface, plane)
            .ok_or_else(|| anyhow!("wrapping IOSurface plane {plane} as {format:?} failed"))
    }

    pub fn checksum(&self, textures: &SurfaceTextures, width: u32, height: u32) -> Result<u64> {
        let Some(luma) = textures.y_uint.as_ref() else {
            return Err(anyhow!(
                "surface has no R8Uint luma view; a {}-bit surface is verified by \
                 CPU readback, not the GPU byte checksum",
                textures.bit_depth
            ));
        };
        if width == 0 || height == 0 {
            return Err(anyhow!("visible rectangle is {width}x{height}"));
        }
        if width as usize > luma.width() || height as usize > luma.height() {
            return Err(anyhow!(
                "visible {width}x{height} exceeds the luma texture {}x{}",
                luma.width(),
                luma.height()
            ));
        }

        let total = self
            .device
            .newBufferWithLength_options(4, MTLResourceOptions::StorageModeShared)
            .ok_or_else(|| anyhow!("allocating the checksum accumulator failed"))?;
        unsafe { total.contents().cast::<u32>().write(0) };

        let extent: [u32; 2] = [width, height];
        let commands = self
            .queue
            .commandBuffer()
            .ok_or_else(|| anyhow!("commandBuffer failed"))?;
        let encoder = commands
            .computeCommandEncoder()
            .ok_or_else(|| anyhow!("computeCommandEncoder failed"))?;
        encoder.setComputePipelineState(&self.pipeline);
        unsafe {
            encoder.setTexture_atIndex(Some(luma), 0);
            encoder.setBuffer_offset_atIndex(Some(&total), 0, 0);
            encoder.setBytes_length_atIndex(
                NonNull::from(&extent).cast::<c_void>(),
                size_of_val(&extent),
                1,
            );
        }
        let rows = height as usize;
        encoder.dispatchThreads_threadsPerThreadgroup(
            MTLSize {
                width: rows,
                height: 1,
                depth: 1,
            },
            MTLSize {
                width: self.max_threads_per_group.min(rows),
                height: 1,
                depth: 1,
            },
        );
        encoder.endEncoding();
        commands.commit();
        commands.waitUntilCompleted();
        if let Some(error) = commands.error() {
            return Err(anyhow!("checksum dispatch failed: {error}"));
        }

        let value = unsafe { total.contents().cast::<u32>().read() };
        Ok(u64::from(value))
    }
}

#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for MetalContext {}

#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for SurfaceTextures {}
