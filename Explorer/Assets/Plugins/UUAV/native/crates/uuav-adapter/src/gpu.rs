
use anyhow::Result;

use crate::core::Core;

pub use platform::{Frames, SurfaceHandle};

const SLOTS_PER_GENERATION: usize = 16;
const _: () = assert!(
    SLOTS_PER_GENERATION
        >= uuav_ipc::protocol::VIDEO_RING_CAPACITY + uuav_ipc::protocol::RETAINED_FRAMES + 1,
    "SLOTS_PER_GENERATION must cover the video ring + retained window + copy-in-flight (else the ~7fps starvation stall)",
);

const REPORT_EVERY_COPIES: u64 = 8;

pub const VERIFY_ENV: &str = "UUAV_ADAPTER_VERIFY";

pub fn service(
    frames: &mut Option<Frames>,
    core: &Core,
    send_surface: &mut dyn FnMut(u32, u64, SurfaceHandle) -> Result<()>,
) -> Result<()> {
    match frames.as_mut() {
        Some(frames) => frames.service(core, send_surface),
        None => Ok(()),
    }
}

#[cfg(target_os = "macos")]
mod platform {
    use std::os::raw::c_void;
    use std::ptr::NonNull;
    use std::slice;
    use std::sync::atomic::{AtomicU64, Ordering};
    use std::sync::{Arc, Mutex, MutexGuard, PoisonError};

    use anyhow::{Result, anyhow, bail};
    use block2::RcBlock;
    use objc2::rc::Retained;
    use objc2::runtime::ProtocolObject;
    use objc2_core_foundation::{
        CFDictionary, CFRetained, CFString, kCFTypeDictionaryKeyCallBacks,
        kCFTypeDictionaryValueCallBacks,
    };
    use objc2_core_video::{
        CVPixelBuffer, CVPixelBufferCreate, CVPixelBufferGetIOSurface,
        kCVPixelBufferIOSurfacePropertiesKey,
    };
    use objc2_io_surface::{IOSurfaceLockOptions, IOSurfaceRef};
    use objc2_metal::{
        MTLBlitCommandEncoder, MTLCommandBuffer, MTLCommandEncoder as _, MTLCommandQueue, MTLDevice,
        MTLOrigin, MTLPixelFormat, MTLResource as _, MTLSize, MTLStorageMode, MTLTexture,
        MTLTextureDescriptor, MTLTextureUsage,
    };
    use uuav_abi::FrameInfo;
    use uuav_ipc::mach_ipc::SendRight;
    use uuav_ipc::protocol::{
        FRAME_FLAG_HAS_PTS, FrameInfoWire, FrameRecord, LogLevel, RELEASE_RING_CAPACITY,
        SURFACE_SLOT_COUNT, SharedSegment, checksum_luma,
    };

    use super::{Core, REPORT_EVERY_COPIES, SLOTS_PER_GENERATION, VERIFY_ENV};

    pub type SurfaceHandle = SendRight;

    type Texture = Retained<ProtocolObject<dyn MTLTexture>>;

    const CV_NV12: u32 = 0x3432_3076;
    const CV_P010: u32 = 0x7834_3230;

    const HANDLER_DRAIN_SPINS: u32 = 10_000_000;

    fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
        mutex.lock().unwrap_or_else(PoisonError::into_inner)
    }

    #[derive(Clone, Copy, PartialEq, Eq)]
    struct Shape {
        luma: (u32, u32),
        chroma: (u32, u32),
        bit_depth: u32,
    }

    impl Shape {
        fn of(info: &FrameInfo) -> Option<Self> {
            let luma = (*info.plane_width.first()?, *info.plane_height.first()?);
            let chroma = (*info.plane_width.get(1)?, *info.plane_height.get(1)?);
            if luma.0 == 0 || luma.1 == 0 || chroma.0 == 0 || chroma.1 == 0 {
                return None;
            }
            Some(Self {
                luma,
                chroma,
                bit_depth: info.bit_depth,
            })
        }

        const fn cv_pixel_format(self) -> u32 {
            if self.bit_depth > 8 { CV_P010 } else { CV_NV12 }
        }

        const fn plane_formats(self) -> (MTLPixelFormat, MTLPixelFormat) {
            if self.bit_depth > 8 {
                (MTLPixelFormat::R16Unorm, MTLPixelFormat::RG16Unorm)
            } else {
                (MTLPixelFormat::R8Unorm, MTLPixelFormat::RG8Unorm)
            }
        }
    }

    struct Slot {
        index: u32,
        _buffer: CFRetained<CVPixelBuffer>,
        surface: CFRetained<IOSurfaceRef>,
        planes: [Texture; 2],
        generation: u64,
        holding: Option<u64>,
    }

    struct Publisher {
        segment: *const SharedSegment,
        in_flight: AtomicU64,
        verify: bool,
        refused: Mutex<Vec<u64>>,
    }

    unsafe impl Send for Publisher {}
    unsafe impl Sync for Publisher {}

    impl Publisher {
        const fn segment(&self) -> &SharedSegment {
            unsafe { &*self.segment }
        }

        fn take_refused(&self) -> Vec<u64> {
            std::mem::take(&mut *lock(&self.refused))
        }
    }

    struct HandlerSurface(CFRetained<IOSurfaceRef>);

    #[allow(
        clippy::non_send_fields_in_send_ty,
        reason = "the point of the type: IOSurface is thread-safe, CFRetained merely lacks \
                  the marker. Same reason video_output_macos.rs carries the same allow."
    )]
    unsafe impl Send for HandlerSurface {}

    struct Gpu {
        device: Retained<ProtocolObject<dyn MTLDevice>>,
        queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
    }

    impl Gpu {
        fn adopt(source: &Texture) -> Result<Self> {
            let device = source.device();
            let queue = device
                .newCommandQueue()
                .ok_or_else(|| anyhow!("newCommandQueue returned nil on the core's device"))?;
            Ok(Self { device, queue })
        }
    }

    #[derive(Clone, Copy, Default)]
    struct Stats {
        copies: u64,
        gated: u64,
        starved: u64,
        exported: u64,
    }

    pub struct Frames {
        publisher: Arc<Publisher>,
        gpu: Option<Gpu>,
        slots: Vec<Slot>,
        shape: Option<Shape>,
        generation: u64,
        export: u64,
        sequence: u64,
        copied_frame: Option<u64>,
        stats: Stats,
        reported: u64,
    }

    impl Frames {
        #[allow(
            clippy::unnecessary_wraps,
            reason = "both platform arms return Some today; the Option is the driver's \
                      seam for a platform with no GPU frame delivery"
        )]
        pub fn start(segment: &SharedSegment) -> Option<Self> {
            let verify = std::env::var_os(VERIFY_ENV).is_some_and(|value| value != "0");
            Some(Self {
                publisher: Arc::new(Publisher {
                    segment: std::ptr::from_ref(segment),
                    in_flight: AtomicU64::new(0),
                    verify,
                    refused: Mutex::new(Vec::new()),
                }),
                gpu: None,
                slots: Vec::new(),
                shape: None,
                generation: 0,
                export: 0,
                sequence: 0,
                copied_frame: None,
                stats: Stats::default(),
                reported: 0,
            })
        }

        pub fn service(
            &mut self,
            core: &Core,
            send_surface: &mut dyn FnMut(u32, u64, SurfaceHandle) -> Result<()>,
        ) -> Result<()> {
            self.reclaim();

            let Some(info) = core.frame_info() else {
                return Ok(());
            };
            if self.copied_frame == Some(info.frame_index) {
                self.stats.gated = self.stats.gated.wrapping_add(1);
                return Ok(());
            }
            let Some(shape) = Shape::of(&info) else {
                bail!(
                    "the core published a frame with plane geometry {:?} x {:?}",
                    info.plane_width,
                    info.plane_height
                );
            };
            if self.shape != Some(shape) {
                self.retire(shape);
            }

            let source = source_planes(&info)?;
            if self.gpu.is_none() {
                let Some(luma) = source.first() else {
                    bail!("the core published no luma plane");
                };
                self.gpu = Some(Gpu::adopt(luma)?);
            }

            let Some(position) = self.claim_slot(shape, send_surface)? else {
                self.stats.starved = self.stats.starved.wrapping_add(1);
                return Ok(());
            };

            self.copy(position, &info, &source, core.current_time())?;
            self.copied_frame = Some(info.frame_index);
            self.stats.copies = self.stats.copies.wrapping_add(1);
            self.report();
            Ok(())
        }

        fn segment(&self) -> &SharedSegment {
            self.publisher.segment()
        }

        fn retire(&mut self, shape: Shape) {
            self.generation = self.generation.wrapping_add(1);
            self.shape = Some(shape);
            self.segment().log.emit(
                LogLevel::Info,
                &format!(
                    "uuav-gpu: texture generation {} is {}x{} luma, {}x{} chroma, {}-bit",
                    self.generation,
                    shape.luma.0,
                    shape.luma.1,
                    shape.chroma.0,
                    shape.chroma.1,
                    shape.bit_depth,
                ),
            );
        }

        fn reclaim(&mut self) {
            let Self {
                publisher,
                slots,
                generation,
                ..
            } = self;
            let segment = publisher.segment();
            for _ in 0..RELEASE_RING_CAPACITY.saturating_mul(2) {
                match segment.release.take() {
                    Ok(Some(sequence)) => release(slots, sequence),
                    Ok(None) | Err(_) => break,
                }
            }
            for sequence in publisher.take_refused() {
                release(slots, sequence);
            }
            let live = *generation;
            slots.retain(|slot| slot.generation == live || slot.holding.is_some());
        }

        fn claim_slot(
            &mut self,
            shape: Shape,
            send_surface: &mut dyn FnMut(u32, u64, SurfaceHandle) -> Result<()>,
        ) -> Result<Option<usize>> {
            let live = self.generation;
            if let Some(position) = self
                .slots
                .iter()
                .position(|slot| slot.generation == live && slot.holding.is_none())
            {
                return Ok(Some(position));
            }
            let allocated = self
                .slots
                .iter()
                .filter(|slot| slot.generation == live)
                .count();
            if allocated >= SLOTS_PER_GENERATION {
                return Ok(None);
            }
            let Some(index) = self.free_index() else {
                return Ok(None);
            };
            let Some(gpu) = self.gpu.as_ref() else {
                bail!("no Metal device has been adopted yet");
            };

            let slot = Slot::allocate(gpu, index, shape, live)?;
            self.export = self.export.wrapping_add(1);
            let port = slot.surface.create_mach_port();
            if port == 0 {
                bail!("IOSurfaceCreateMachPort returned MACH_PORT_NULL for slot {index}");
            }
            let right = unsafe { SendRight::from_raw(port) };
            send_surface(index, self.export, right)?;
            self.stats.exported = self.stats.exported.wrapping_add(1);

            self.slots.push(slot);
            Ok(Some(self.slots.len().saturating_sub(1)))
        }

        fn free_index(&self) -> Option<u32> {
            (0..SURFACE_SLOT_COUNT)
                .map(|index| index as u32)
                .find(|index| self.slots.iter().all(|slot| slot.index != *index))
        }

        fn copy(
            &mut self,
            position: usize,
            info: &FrameInfo,
            source: &[Texture; 2],
            pts: Option<f64>,
        ) -> Result<()> {
            let (queue, destination, index, surface) = {
                let Some(gpu) = self.gpu.as_ref() else {
                    bail!("no Metal device has been adopted yet");
                };
                let Some(slot) = self.slots.get(position) else {
                    bail!("slot position {position} is not in the pool");
                };
                (
                    gpu.queue.clone(),
                    slot.planes.clone(),
                    slot.index,
                    slot.surface.clone(),
                )
            };

            let commands = queue
                .commandBuffer()
                .ok_or_else(|| anyhow!("commandBuffer returned nil"))?;
            let encoder = commands
                .blitCommandEncoder()
                .ok_or_else(|| anyhow!("blitCommandEncoder returned nil"))?;
            for (from, to) in source.iter().zip(destination.iter()) {
                let origin = MTLOrigin { x: 0, y: 0, z: 0 };
                let extent = MTLSize {
                    width: from.width().min(to.width()),
                    height: from.height().min(to.height()),
                    depth: 1,
                };
                unsafe {
                    encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
                        from, 0, 0, origin, extent, to, 0, 0, origin,
                    );
                }
            }
            encoder.endEncoding();

            self.sequence = self.sequence.wrapping_add(1);
            let sequence = self.sequence;
            let (flags, pts) = pts
                .filter(|value| value.is_finite())
                .map_or((0, 0.0), |value| (FRAME_FLAG_HAS_PTS, value));
            let record = FrameRecord {
                info: wire_of(info),
                flags,
                pts,
                sequence,
                slot: index,
                reserved: 0,
            };
            let held = HandlerSurface(surface);
            let visible = (info.visible_width, info.visible_height);

            let publisher = Arc::clone(&self.publisher);
            publisher.in_flight.fetch_add(1, Ordering::AcqRel);
            let handler = RcBlock::new(
                move |_finished: NonNull<ProtocolObject<dyn MTLCommandBuffer>>| {
                    if publisher.segment().video.publish(&record) {
                        if publisher.verify {
                            emit_reference(publisher.segment(), &held.0, &record, visible);
                        }
                    } else {
                        lock(&publisher.refused).push(sequence);
                    }
                    publisher.in_flight.fetch_sub(1, Ordering::AcqRel);
                },
            );
            unsafe { commands.addCompletedHandler(RcBlock::as_ptr(&handler)) };

            if let Some(slot) = self.slots.get_mut(position) {
                slot.holding = Some(sequence);
            }
            commands.commit();
            Ok(())
        }

        fn report(&mut self) {
            if self.stats.copies < self.reported.wrapping_add(REPORT_EVERY_COPIES) {
                return;
            }
            self.reported = self.stats.copies;
            let generation = self.generation;
            let held = self
                .slots
                .iter()
                .filter(|slot| slot.holding.is_some())
                .count();
            self.segment().log.emit(
                LogLevel::Info,
                &format!(
                    "uuav-gpu: copies={} gated={} starved={} exported={} slots={} held={held} \
                     generation={generation}",
                    self.stats.copies,
                    self.stats.gated,
                    self.stats.starved,
                    self.stats.exported,
                    self.slots.len(),
                ),
            );
        }
    }

    impl Drop for Frames {
        fn drop(&mut self) {
            for _ in 0..HANDLER_DRAIN_SPINS {
                if self.publisher.in_flight.load(Ordering::Acquire) == 0 {
                    return;
                }
                std::hint::spin_loop();
            }
        }
    }

    impl Slot {
        fn allocate(gpu: &Gpu, index: u32, shape: Shape, generation: u64) -> Result<Self> {
            let (buffer, surface) = new_biplanar(
                shape.luma.0 as usize,
                shape.luma.1 as usize,
                shape.cv_pixel_format(),
            )?;

            if surface.plane_count() != 2 {
                bail!(
                    "the allocated slot surface has {} planes, not 2",
                    surface.plane_count()
                );
            }
            for (plane, claimed) in [shape.luma, shape.chroma].into_iter().enumerate() {
                let allocated = (surface.width_of_plane(plane), surface.height_of_plane(plane));
                if allocated.0 < claimed.0 as usize || allocated.1 < claimed.1 as usize {
                    bail!(
                        "slot plane {plane} allocated {}x{} for a {}x{} frame",
                        allocated.0,
                        allocated.1,
                        claimed.0,
                        claimed.1
                    );
                }
            }

            let (luma_format, chroma_format) = shape.plane_formats();
            let luma = wrap_plane(&gpu.device, &surface, 0, luma_format)?;
            let chroma = wrap_plane(&gpu.device, &surface, 1, chroma_format)?;

            Ok(Self {
                index,
                _buffer: buffer,
                surface,
                planes: [luma, chroma],
                generation,
                holding: None,
            })
        }
    }

    fn release(slots: &mut [Slot], sequence: u64) {
        for slot in slots.iter_mut() {
            if slot.holding == Some(sequence) {
                slot.holding = None;
                return;
            }
        }
    }

    fn source_planes(info: &FrameInfo) -> Result<[Texture; 2]> {
        let mut planes: [Option<Texture>; 2] = [None, None];
        for (slot, pointer) in planes.iter_mut().zip(info.planes.iter()) {
            let pointer = *pointer as *mut ProtocolObject<dyn MTLTexture>;
            *slot = unsafe { Retained::retain(pointer) };
        }
        let [Some(luma), Some(chroma)] = planes else {
            return Err(anyhow!(
                "the core published planes {:?}, which is not a two-plane frame",
                info.planes
            ));
        };
        Ok([luma, chroma])
    }

    const fn wire_of(info: &FrameInfo) -> FrameInfoWire {
        FrameInfoWire {
            yuv_to_rgb: info.yuv_to_rgb,
            uv_transform: info.uv_transform,
            visible_width: info.visible_width,
            visible_height: info.visible_height,
            plane_width: info.plane_width,
            plane_height: info.plane_height,
            colorspace: info.colorspace,
            color_range: info.color_range,
            color_primaries: info.color_primaries,
            rotation: info.rotation,
            bit_depth: info.bit_depth,
        }
    }

    fn new_biplanar(
        width: usize,
        height: usize,
        pixel_format: u32,
    ) -> Result<(CFRetained<CVPixelBuffer>, CFRetained<IOSurfaceRef>)> {
        let empty = unsafe {
            CFDictionary::new(
                None,
                std::ptr::null_mut(),
                std::ptr::null_mut(),
                0,
                &raw const kCFTypeDictionaryKeyCallBacks,
                &raw const kCFTypeDictionaryValueCallBacks,
            )
        }
        .ok_or_else(|| anyhow!("CFDictionaryCreate returned NULL"))?;

        let key: &CFString = unsafe { kCVPixelBufferIOSurfacePropertiesKey };
        let mut keys: [*const c_void; 1] = [std::ptr::from_ref::<CFString>(key).cast()];
        let mut values: [*const c_void; 1] =
            [std::ptr::from_ref::<CFDictionary>(&empty).cast::<c_void>()];
        let attributes = unsafe {
            CFDictionary::new(
                None,
                keys.as_mut_ptr(),
                values.as_mut_ptr(),
                1,
                &raw const kCFTypeDictionaryKeyCallBacks,
                &raw const kCFTypeDictionaryValueCallBacks,
            )
        }
        .ok_or_else(|| anyhow!("CFDictionaryCreate returned NULL"))?;

        let mut out: *mut CVPixelBuffer = std::ptr::null_mut();
        let code = unsafe {
            CVPixelBufferCreate(
                None,
                width,
                height,
                pixel_format,
                Some(&attributes),
                NonNull::from(&mut out),
            )
        };
        if code != 0 {
            bail!("CVPixelBufferCreate({width}x{height}, {pixel_format:#010x}) -> {code}");
        }
        let buffer = NonNull::new(out)
            .map(|pointer| {
                unsafe { CFRetained::from_raw(pointer) }
            })
            .ok_or_else(|| anyhow!("CVPixelBufferCreate produced NULL"))?;
        let surface = CVPixelBufferGetIOSurface(Some(&buffer))
            .ok_or_else(|| anyhow!("the allocated pixel buffer is not IOSurface-backed"))?;
        Ok((buffer, surface))
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
        descriptor.setUsage(MTLTextureUsage::ShaderRead);
        device
            .newTextureWithDescriptor_iosurface_plane(&descriptor, surface, plane)
            .ok_or_else(|| anyhow!("wrapping slot plane {plane} as {format:?} failed"))
    }

    fn emit_reference(
        segment: &SharedSegment,
        surface: &IOSurfaceRef,
        record: &FrameRecord,
        visible: (u32, u32),
    ) {
        const READ_ONLY: IOSurfaceLockOptions = IOSurfaceLockOptions::ReadOnly;
        let status = unsafe { surface.lock(READ_ONLY, std::ptr::null_mut()) };
        if status != 0 {
            segment.log.emit(
                LogLevel::Warning,
                &format!("uuav-verify: IOSurfaceLock failed ({status})"),
            );
            return;
        }
        let stride = surface.bytes_per_row_of_plane(0);
        let rows = surface.height_of_plane(0);
        let measured = stride.checked_mul(rows).map(|bytes| {
            let plane = unsafe {
                slice::from_raw_parts(
                    surface.base_address_of_plane(0).as_ptr().cast::<u8>(),
                    bytes,
                )
            };
            checksum_luma(plane, stride, visible.0 as usize, visible.1 as usize)
        });
        unsafe { surface.unlock(READ_ONLY, std::ptr::null_mut()) };

        let Some((checksum, covered)) = measured else {
            return;
        };
        segment.log.emit(
            LogLevel::Info,
            &format!(
                "uuav-verify seq={} slot={} visible={}x{} checksum={checksum:#018x} bytes={covered}",
                record.sequence, record.slot, visible.0, visible.1,
            ),
        );
    }
}

#[cfg(windows)]
mod platform {
    use std::os::raw::c_void;
    use std::slice;
    use std::time::Instant;

    use anyhow::{Result, anyhow, bail};
    use uuav_abi::FrameInfo;
    use uuav_ipc::protocol::{
        FRAME_FLAG_HAS_PTS, FrameInfoWire, FrameRecord, LogLevel, RELEASE_RING_CAPACITY,
        SURFACE_SLOT_COUNT, SharedSegment, VerifyEntry, checksum_luma, uptime_nanos,
    };
    use uuav_ipc::win::bridge::{Admission, DeepFrameGate};
    use uuav_ipc::win::gpu::{self, HelperDevice, SharedSurface};
    use windows::Win32::Graphics::Direct3D11::{
        D3D11_BOX, D3D11_CPU_ACCESS_READ, D3D11_MAP_READ, D3D11_MAPPED_SUBRESOURCE,
        D3D11_TEXTURE2D_DESC, D3D11_USAGE_STAGING, ID3D11DeviceContext, ID3D11Texture2D,
    };
    use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_NV12, DXGI_SAMPLE_DESC};
    use windows::core::Interface as _;

    use super::{Core, REPORT_EVERY_COPIES, SLOTS_PER_GENERATION, VERIFY_ENV};

    pub type SurfaceHandle = u64;

    #[derive(Clone, Copy, PartialEq, Eq)]
    struct Shape {
        luma: (u32, u32),
        chroma: (u32, u32),
        bit_depth: u32,
    }

    impl Shape {
        fn of(info: &FrameInfo) -> Option<Self> {
            let luma = (*info.plane_width.first()?, *info.plane_height.first()?);
            let chroma = (*info.plane_width.get(1)?, *info.plane_height.get(1)?);
            if luma.0 == 0 || luma.1 == 0 || chroma.0 == 0 || chroma.1 == 0 {
                return None;
            }
            Some(Self {
                luma,
                chroma,
                bit_depth: info.bit_depth,
            })
        }
    }

    struct Slot {
        index: u32,
        surface: SharedSurface,
        generation: u64,
        holding: Option<u64>,
    }

    struct Gpu {
        device: HelperDevice,
        context: ID3D11DeviceContext,
    }

    impl Gpu {
        fn adopt(source: &ID3D11Texture2D) -> Result<Self> {
            let device = unsafe { source.GetDevice() }
                .map_err(|error| anyhow!("GetDevice on the core's texture: {error}"))?;
            let context = unsafe { device.GetImmediateContext() }
                .map_err(|error| anyhow!("GetImmediateContext: {error}"))?;
            Ok(Self {
                device: HelperDevice::adopt(device),
                context,
            })
        }
    }

    #[derive(Clone, Copy, Default)]
    struct Stats {
        copies: u64,
        gated: u64,
        starved: u64,
        copy_failed: u64,
        idle: u64,
        deep: u64,
        exported: u64,
    }

    struct Staging {
        texture: ID3D11Texture2D,
        width: u32,
        height: u32,
    }

    pub struct Frames {
        segment: *const SharedSegment,
        ticks: u64,
        gpu: Option<Gpu>,
        slots: Vec<Slot>,
        shape: Option<Shape>,
        deep: DeepFrameGate,
        verify: bool,
        staging: Option<Staging>,
        probe_copies: u64,
        generation: u64,
        export: u64,
        sequence: u64,
        copied_frame: Option<u64>,
        stats: Stats,
        reported: u64,
        occ_min: u64,
        occ_max: u64,
        occ_sum: u64,
        occ_samples: u64,
        last_seen_index: Option<u64>,
        advances: u64,
        loop_window: Instant,
    }

    impl Frames {
        #[allow(
            clippy::unnecessary_wraps,
            reason = "both platform arms return Some today; the Option is the driver's \
                      seam for a platform with no GPU frame delivery"
        )]
        pub fn start(segment: &SharedSegment) -> Option<Self> {
            let verify = std::env::var_os(VERIFY_ENV).is_some_and(|value| value != "0");
            Some(Self {
                segment: std::ptr::from_ref(segment),
                ticks: 0,
                gpu: None,
                slots: Vec::new(),
                shape: None,
                deep: DeepFrameGate::new(),
                verify,
                staging: None,
                probe_copies: 0,
                generation: 0,
                export: 0,
                sequence: 0,
                copied_frame: None,
                stats: Stats::default(),
                reported: 0,
                occ_min: u64::MAX,
                occ_max: 0,
                occ_sum: 0,
                occ_samples: 0,
                last_seen_index: None,
                advances: 0,
                loop_window: Instant::now(),
            })
        }

        pub fn service(
            &mut self,
            core: &Core,
            send_surface: &mut dyn FnMut(u32, u64, SurfaceHandle) -> Result<()>,
        ) -> Result<()> {
            self.ticks = self.ticks.wrapping_add(1);
            if self.ticks.is_multiple_of(4096) {
                if self.stats.copies == 0 {
                    self.segment().log.emit(
                        LogLevel::Info,
                        &format!(
                            "uuav-gpu: no copy after {} ticks: idle={} gated={} starved={} \
                             copy_failed={} deep={} exported={}",
                            self.ticks,
                            self.stats.idle,
                            self.stats.gated,
                            self.stats.starved,
                            self.stats.copy_failed,
                            self.stats.deep,
                            self.stats.exported,
                        ),
                    );
                } else if self.stats.copies == self.probe_copies {
                    self.segment().log.emit(
                        LogLevel::Warning,
                        &format!(
                            "uuav-gpu: stalled at {} copies for 4096 ticks: idle={} gated={} \
                             starved={} copy_failed={} deep={} held={}",
                            self.stats.copies,
                            self.stats.idle,
                            self.stats.gated,
                            self.stats.starved,
                            self.stats.copy_failed,
                            self.stats.deep,
                            self.slots.iter().filter(|slot| slot.holding.is_some()).count(),
                        ),
                    );
                }
                self.probe_copies = self.stats.copies;
            }
            self.reclaim();

            let depth = self.segment().video.depth().unwrap_or(0);
            self.occ_min = self.occ_min.min(depth);
            self.occ_max = self.occ_max.max(depth);
            self.occ_sum = self.occ_sum.wrapping_add(depth);
            self.occ_samples = self.occ_samples.wrapping_add(1);
            if self.loop_window.elapsed().as_secs_f64() >= 1.0 {
                self.report_loop();
            }

            let Some(info) = core.frame_info() else {
                self.stats.idle = self.stats.idle.wrapping_add(1);
                return Ok(());
            };
            if self.last_seen_index != Some(info.frame_index) {
                self.last_seen_index = Some(info.frame_index);
                self.advances = self.advances.wrapping_add(1);
            }
            if self.copied_frame == Some(info.frame_index) {
                self.stats.gated = self.stats.gated.wrapping_add(1);
                return Ok(());
            }
            let Some(shape) = Shape::of(&info) else {
                bail!(
                    "the core published a frame with plane geometry {:?} x {:?}",
                    info.plane_width,
                    info.plane_height
                );
            };
            match self.deep.admit(shape.bit_depth) {
                Admission::Admit => {}
                admission @ (Admission::SkipAndWarn | Admission::Skip) => {
                    if admission == Admission::SkipAndWarn {
                        self.segment().log.emit(
                            LogLevel::Warning,
                            &format!(
                                "uuav-gpu: the shared-surface path carries NV12 only; skipping \
                                 {}-bit frames",
                                shape.bit_depth
                            ),
                        );
                    }
                    self.stats.deep = self.stats.deep.wrapping_add(1);
                    self.copied_frame = Some(info.frame_index);
                    return Ok(());
                }
            }
            if self.shape != Some(shape) {
                self.retire(shape);
            }

            let source = source_texture(&info)?;
            if self.gpu.is_none() {
                self.gpu = Some(Gpu::adopt(&source)?);
            }

            let Some(position) = self.claim_slot(shape, send_surface)? else {
                self.stats.starved = self.stats.starved.wrapping_add(1);
                return Ok(());
            };

            if self.copy(position, &info, &source, core.current_time())? {
                self.copied_frame = Some(info.frame_index);
                self.stats.copies = self.stats.copies.wrapping_add(1);
                self.report();
            } else {
                self.stats.copy_failed = self.stats.copy_failed.wrapping_add(1);
            }
            Ok(())
        }

        fn segment(&self) -> &SharedSegment {
            unsafe { &*self.segment }
        }

        fn retire(&mut self, shape: Shape) {
            self.generation = self.generation.wrapping_add(1);
            self.shape = Some(shape);
            self.segment().log.emit(
                LogLevel::Info,
                &format!(
                    "uuav-gpu: texture generation {} is {}x{} luma, {}x{} chroma, {}-bit",
                    self.generation,
                    shape.luma.0,
                    shape.luma.1,
                    shape.chroma.0,
                    shape.chroma.1,
                    shape.bit_depth,
                ),
            );
        }

        fn reclaim(&mut self) {
            let live = self.generation;
            let segment = unsafe { &*self.segment };
            let slots = &mut self.slots;
            for _ in 0..RELEASE_RING_CAPACITY.saturating_mul(2) {
                match segment.release.take() {
                    Ok(Some(sequence)) => release(slots, sequence),
                    Ok(None) | Err(_) => break,
                }
            }
            slots.retain(|slot| slot.generation == live || slot.holding.is_some());
        }

        fn claim_slot(
            &mut self,
            shape: Shape,
            send_surface: &mut dyn FnMut(u32, u64, SurfaceHandle) -> Result<()>,
        ) -> Result<Option<usize>> {
            let live = self.generation;
            if let Some(position) = self
                .slots
                .iter()
                .position(|slot| slot.generation == live && slot.holding.is_none())
            {
                return Ok(Some(position));
            }
            let allocated = self
                .slots
                .iter()
                .filter(|slot| slot.generation == live)
                .count();
            if allocated >= SLOTS_PER_GENERATION {
                return Ok(None);
            }
            let Some(index) = self.free_index() else {
                return Ok(None);
            };
            let Some(gpu) = self.gpu.as_ref() else {
                bail!("no D3D11 device has been adopted yet");
            };

            let surface = gpu.device.create_shared_nv12(shape.luma.0, shape.luma.1)?;
            self.export = self.export.wrapping_add(1);
            send_surface(index, self.export, surface.handle_value())?;
            self.stats.exported = self.stats.exported.wrapping_add(1);

            self.slots.push(Slot {
                index,
                surface,
                generation: live,
                holding: None,
            });
            Ok(Some(self.slots.len().saturating_sub(1)))
        }

        fn free_index(&self) -> Option<u32> {
            (0..SURFACE_SLOT_COUNT)
                .map(|index| index as u32)
                .find(|index| self.slots.iter().all(|slot| slot.index != *index))
        }

        fn ensure_staging(&mut self, position: usize) -> Result<ID3D11Texture2D> {
            let Some(slot) = self.slots.get(position) else {
                bail!("slot position {position} is not in the pool");
            };
            let (width, height) = slot.surface.size();
            if let Some(existing) = self.staging.as_ref()
                && existing.width == width
                && existing.height == height
            {
                return Ok(existing.texture.clone());
            }
            let Some(gpu) = self.gpu.as_ref() else {
                bail!("no D3D11 device has been adopted yet");
            };
            let description = D3D11_TEXTURE2D_DESC {
                Width: width,
                Height: height,
                MipLevels: 1,
                ArraySize: 1,
                Format: DXGI_FORMAT_NV12,
                SampleDesc: DXGI_SAMPLE_DESC {
                    Count: 1,
                    Quality: 0,
                },
                Usage: D3D11_USAGE_STAGING,
                BindFlags: 0,
                CPUAccessFlags: D3D11_CPU_ACCESS_READ.0 as u32,
                MiscFlags: 0,
            };
            let mut created: Option<ID3D11Texture2D> = None;
            unsafe {
                gpu.device
                    .device()
                    .CreateTexture2D(&description, None, Some(&raw mut created))
            }
            .map_err(|error| anyhow!("CreateTexture2D(staging NV12 {width}x{height}): {error}"))?;
            let texture = created
                .ok_or_else(|| anyhow!("CreateTexture2D reported success but produced nothing"))?;
            self.staging = Some(Staging {
                texture: texture.clone(),
                width,
                height,
            });
            Ok(texture)
        }

        fn copy(
            &mut self,
            position: usize,
            info: &FrameInfo,
            source: &ID3D11Texture2D,
            pts: Option<f64>,
        ) -> Result<bool> {
            let staging = if self.verify {
                match self.ensure_staging(position) {
                    Ok(texture) => Some(texture),
                    Err(error) => {
                        self.verify = false;
                        self.segment().log.emit(
                            LogLevel::Warning,
                            &format!("uuav-verify: no staging texture, verification is off: {error:#}"),
                        );
                        None
                    }
                }
            } else {
                None
            };
            let Some(gpu) = self.gpu.as_ref() else {
                bail!("no D3D11 device has been adopted yet");
            };
            let Some(slot) = self.slots.get(position) else {
                bail!("slot position {position} is not in the pool");
            };
            let (Some(&luma_width), Some(&luma_height)) =
                (info.plane_width.first(), info.plane_height.first())
            else {
                bail!("the core published no luma plane");
            };
            let (slot_width, slot_height) = slot.surface.size();
            let width = slot_width.min(luma_width) & !1;
            let height = slot_height.min(luma_height) & !1;
            if width == 0 || height == 0 {
                bail!("frame has no copyable size ({luma_width}x{luma_height})");
            }
            let region = D3D11_BOX {
                left: 0,
                top: 0,
                front: 0,
                right: width,
                bottom: height,
                back: 1,
            };

            let copied = gpu::with_key(slot.surface.mutex(), gpu::KEY, gpu::KEY, 0, || {
                unsafe {
                    gpu.context.CopySubresourceRegion(
                        slot.surface.texture(),
                        0,
                        0,
                        0,
                        0,
                        source,
                        0,
                        Some(&raw const region),
                    );
                }
                if let Some(staging) = &staging {
                    unsafe { gpu.context.CopyResource(staging, slot.surface.texture()) };
                }
                Ok(())
            })?;
            if copied.is_none() {
                return Ok(false);
            }
            unsafe { gpu.context.Flush() };

            self.sequence = self.sequence.wrapping_add(1);
            let sequence = self.sequence;
            let (flags, pts) = pts
                .filter(|value| value.is_finite())
                .map_or((0, 0.0), |value| (FRAME_FLAG_HAS_PTS, value));
            let record = FrameRecord {
                info: wire_of(info),
                flags,
                pts,
                sequence,
                slot: slot.index,
                reserved: 0,
            };
            if let Some(staging) = &staging {
                emit_reference(
                    self.segment(),
                    &gpu.context,
                    staging,
                    &record,
                    (info.visible_width, info.visible_height),
                );
            }
            let published = self.segment().video.publish(&record);
            if published && let Some(slot) = self.slots.get_mut(position) {
                slot.holding = Some(sequence);
            }
            Ok(true)
        }

        fn report_loop(&mut self) {
            let elapsed = self.loop_window.elapsed().as_secs_f64();
            let avg = if self.occ_samples > 0 {
                self.occ_sum as f64 / self.occ_samples as f64
            } else {
                0.0
            };
            let min = if self.occ_min == u64::MAX { 0 } else { self.occ_min };
            let max = self.occ_max;
            let holding = self
                .slots
                .iter()
                .filter(|slot| slot.holding.is_some())
                .count();
            let created = self.slots.len();
            let free = created.saturating_sub(holding);
            let cap = SLOTS_PER_GENERATION;
            let advance_rate = if elapsed > 0.0 {
                self.advances as f64 / elapsed
            } else {
                0.0
            };
            let tick_rate = if elapsed > 0.0 {
                self.occ_samples as f64 / elapsed
            } else {
                0.0
            };
            self.segment().log.emit(
                LogLevel::Info,
                &format!(
                    "uuav-ring: video_depth min={min} avg={avg:.1} max={max} | \
                     slots_created={created} holding={holding} free={free} cap={cap} | \
                     core_advance={advance_rate:.1}/s ticks={tick_rate:.0}/s"
                ),
            );
            self.occ_min = u64::MAX;
            self.occ_max = 0;
            self.occ_sum = 0;
            self.occ_samples = 0;
            self.advances = 0;
            self.loop_window = Instant::now();
        }

        fn report(&mut self) {
            if self.stats.copies < self.reported.wrapping_add(REPORT_EVERY_COPIES) {
                return;
            }
            self.reported = self.stats.copies;
            let generation = self.generation;
            let held = self
                .slots
                .iter()
                .filter(|slot| slot.holding.is_some())
                .count();
            self.segment().log.emit(
                LogLevel::Info,
                &format!(
                    "uuav-gpu: copies={} gated={} idle={} starved={} copy_failed={} \
                     exported={} slots={} held={held} generation={generation}",
                    self.stats.copies,
                    self.stats.gated,
                    self.stats.idle,
                    self.stats.starved,
                    self.stats.copy_failed,
                    self.stats.exported,
                    self.slots.len(),
                ),
            );
        }
    }

    fn release(slots: &mut [Slot], sequence: u64) {
        for slot in slots.iter_mut() {
            if slot.holding == Some(sequence) {
                slot.holding = None;
                return;
            }
        }
    }

    fn emit_reference(
        segment: &SharedSegment,
        context: &ID3D11DeviceContext,
        staging: &ID3D11Texture2D,
        record: &FrameRecord,
        visible: (u32, u32),
    ) {
        let mut description = D3D11_TEXTURE2D_DESC::default();
        unsafe { staging.GetDesc(&raw mut description) };

        let mut mapped = D3D11_MAPPED_SUBRESOURCE::default();
        let outcome = unsafe { context.Map(staging, 0, D3D11_MAP_READ, 0, Some(&raw mut mapped)) };
        if let Err(error) = outcome {
            segment.log.emit(
                LogLevel::Warning,
                &format!("uuav-verify: Map failed ({error})"),
            );
            return;
        }
        let stride = mapped.RowPitch as usize;
        let rows = description.Height as usize;
        let measured = stride.checked_mul(rows).and_then(|bytes| {
            if mapped.pData.is_null() {
                return None;
            }
            let plane =
                unsafe { slice::from_raw_parts(mapped.pData.cast_const().cast::<u8>(), bytes) };
            Some(checksum_luma(
                plane,
                stride,
                visible.0 as usize,
                visible.1 as usize,
            ))
        });
        unsafe { context.Unmap(staging, 0) };

        let Some((checksum, covered)) = measured else {
            return;
        };
        segment.verify.publish(VerifyEntry {
            sequence: record.sequence,
            checksum,
            byte_count: covered,
            decode_ready_nanos: 0,
            published_nanos: uptime_nanos(),
        });
        segment.log.emit(
            LogLevel::Info,
            &format!(
                "uuav-verify seq={} slot={} visible={}x{} checksum={checksum:#018x} bytes={covered}",
                record.sequence, record.slot, visible.0, visible.1,
            ),
        );
    }

    fn source_texture(info: &FrameInfo) -> Result<ID3D11Texture2D> {
        let Some(&raw) = info.planes.first() else {
            bail!("the core published no planes");
        };
        if raw == 0 {
            bail!("the core published a null presentation texture");
        }
        let pointer = raw as *mut c_void;
        let borrowed = unsafe { ID3D11Texture2D::from_raw_borrowed(&pointer) }
            .ok_or_else(|| anyhow!("the core's plane pointer is not a COM interface"))?;
        Ok(borrowed.clone())
    }

    const fn wire_of(info: &FrameInfo) -> FrameInfoWire {
        FrameInfoWire {
            yuv_to_rgb: info.yuv_to_rgb,
            uv_transform: info.uv_transform,
            visible_width: info.visible_width,
            visible_height: info.visible_height,
            plane_width: info.plane_width,
            plane_height: info.plane_height,
            colorspace: info.colorspace,
            color_range: info.color_range,
            color_primaries: info.color_primaries,
            rotation: info.rotation,
            bit_depth: info.bit_depth,
        }
    }
}
