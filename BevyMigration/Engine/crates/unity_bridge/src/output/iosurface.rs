//! macOS: each output buffer is an `IOSurface` wrapped as a Metal texture twice, once on Bevy's
//! device (handed to wgpu through its hal) and once on Unity's (handed to the host for
//! `Texture2D.CreateExternalTexture`). Same memory, two devices, no copy (plan § 8 "Output sharing").

use super::{OUTPUT_FORMAT, OutputBuffer, texture_descriptor};
use crate::unity_plugin;
use bevy::{
    math::UVec2,
    render::{
        render_resource::{Texture, TextureView},
        renderer::RenderDevice,
    },
};
use metal::foreign_types::ForeignType;
use objc2::{rc::Retained, runtime::ProtocolObject};
use objc2_core_foundation::{CFDictionary, CFNumber, CFRetained, CFString, CFType};
use objc2_io_surface::{
    IOSurfaceRef, kIOSurfaceBytesPerElement, kIOSurfaceBytesPerRow, kIOSurfaceHeight,
    kIOSurfaceWidth,
};
use objc2_metal::{
    MTLDevice, MTLPixelFormat, MTLStorageMode, MTLTexture, MTLTextureDescriptor, MTLTextureUsage,
};
use std::ffi::c_void;

const BYTES_PER_ELEMENT: usize = 4;

struct Backing {
    _surface: CFRetained<IOSurfaceRef>,
    _host_texture: Retained<ProtocolObject<dyn MTLTexture>>,
}

// SAFETY: IOSurface and MTLTexture objects are reference counted and thread safe to retain and
// release; nothing here is touched from two threads at once.
unsafe impl Send for Backing {}

pub fn create(device: &RenderDevice, size: UVec2, index: u32) -> Result<OutputBuffer, String> {
    let unity_device = unity_plugin::unity_metal_device();

    if unity_device.is_null() {
        return Err("unsupported: Unity did not hand over a Metal device (plugin loaded outside Unity, or Unity is not on Metal)".to_owned());
    }

    let surface = new_surface(size)?;

    // SAFETY: Unity's id<MTLDevice> is valid for the process lifetime.
    let unity_device: &ProtocolObject<dyn MTLDevice> = unsafe {
        &*unity_device
            .cast::<ProtocolObject<dyn MTLDevice>>()
            .cast_const()
    };
    let host_texture = wrap_on(unity_device, &surface, size).ok_or_else(|| {
        format!("Unity's device refused the IOSurface texture for buffer {index}")
    })?;

    // SAFETY: wgpu's Metal backend is the only one this crate requests on macOS; the raw device is
    // cloned (retained) out of the hal lock and nothing else is touched.
    let bevy_metal: Option<metal::Device> = unsafe {
        device
            .wgpu_device()
            .as_hal::<wgpu::hal::api::Metal, _, _>(|hal| {
                hal.map(|hal| hal.raw_device().lock().clone())
            })
    };

    let bevy_metal =
        bevy_metal.ok_or_else(|| "unsupported: Bevy's render device is not Metal".to_owned())?;

    // SAFETY: metal::Device::as_ptr is the same id<MTLDevice> objc2 expects.
    let bevy_device: &ProtocolObject<dyn MTLDevice> = unsafe {
        &*bevy_metal
            .as_ptr()
            .cast::<ProtocolObject<dyn MTLDevice>>()
            .cast_const()
    };
    let bevy_texture = wrap_on(bevy_device, &surface, size)
        .ok_or_else(|| format!("Bevy's device refused the IOSurface texture for buffer {index}"))?;

    // SAFETY: into_raw transfers the retain to the metal crate wrapper, which releases it on drop;
    // the texture matches `texture_descriptor` in size, format, usage and mip count.
    let texture = unsafe {
        let raw =
            metal::Texture::from_ptr(Retained::into_raw(bevy_texture).cast::<metal::MTLTexture>());
        let hal = wgpu::hal::metal::Device::texture_from_raw(
            raw,
            OUTPUT_FORMAT,
            metal::MTLTextureType::D2,
            1,
            1,
            wgpu::hal::CopyExtent {
                width: size.x,
                height: size.y,
                depth: 1,
            },
        );
        let label = format!("bevy-migration iosurface output {index}");
        device
            .wgpu_device()
            .create_texture_from_hal::<wgpu::hal::api::Metal>(
                hal,
                &texture_descriptor(size, &label),
            )
    };

    let view = texture.create_view(&wgpu::TextureViewDescriptor::default());
    let io_surface = CFRetained::as_ptr(&surface).as_ptr().cast::<c_void>();
    let host_texture_ptr = Retained::as_ptr(&host_texture).cast_mut().cast::<c_void>();

    Ok(OutputBuffer {
        texture: Texture::from(texture),
        view: TextureView::from(view),
        host_texture: host_texture_ptr,
        io_surface,
        _backing: Box::new(Backing {
            _surface: surface,
            _host_texture: host_texture,
        }),
    })
}

fn new_surface(size: UVec2) -> Result<CFRetained<IOSurfaceRef>, String> {
    let width = size.x as usize;
    let height = size.y as usize;
    // SAFETY: plain property alignment query.
    let bytes_per_row =
        unsafe { IOSurfaceRef::align_property(kIOSurfaceBytesPerRow, width * BYTES_PER_ELEMENT) };

    // SAFETY: the keys are the framework's own constant strings.
    let keys: [&CFString; 4] = unsafe {
        [
            kIOSurfaceWidth,
            kIOSurfaceHeight,
            kIOSurfaceBytesPerElement,
            kIOSurfaceBytesPerRow,
        ]
    };
    let values = [
        CFNumber::new_i64(i64::try_from(width).unwrap_or(0)),
        CFNumber::new_i64(i64::try_from(height).unwrap_or(0)),
        CFNumber::new_i64(i64::try_from(BYTES_PER_ELEMENT).unwrap_or(0)),
        CFNumber::new_i64(i64::try_from(bytes_per_row).unwrap_or(0)),
    ];
    let value_refs: [&CFType; 4] = [
        values[0].as_ref(),
        values[1].as_ref(),
        values[2].as_ref(),
        values[3].as_ref(),
    ];
    let properties: CFRetained<CFDictionary<CFString, CFType>> =
        CFDictionary::from_slices(&keys, &value_refs);
    // SAFETY: erasing the element types for the untyped IOSurfaceCreate signature, as UUAV does.
    let properties = unsafe { CFRetained::cast_unchecked::<CFDictionary>(properties) };

    // SAFETY: a well-formed property dictionary.
    unsafe { IOSurfaceRef::new(&properties) }
        .ok_or_else(|| format!("IOSurfaceCreate({width}x{height}) failed"))
}

fn wrap_on(
    device: &ProtocolObject<dyn MTLDevice>,
    surface: &IOSurfaceRef,
    size: UVec2,
) -> Option<Retained<ProtocolObject<dyn MTLTexture>>> {
    // SAFETY: plain descriptor construction.
    let descriptor = unsafe {
        MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
            MTLPixelFormat::BGRA8Unorm_sRGB,
            size.x as usize,
            size.y as usize,
            false,
        )
    };
    descriptor.setUsage(MTLTextureUsage::RenderTarget | MTLTextureUsage::ShaderRead);
    // IOSurface-backed textures alias CPU-visible memory and must be Shared.
    descriptor.setStorageMode(MTLStorageMode::Shared);
    device.newTextureWithDescriptor_iosurface_plane(&descriptor, surface, 0)
}
