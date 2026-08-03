
use anyhow::{Result, anyhow};
use objc2_core_foundation::{CFDictionary, CFRetained, CFString};
use objc2_core_video::{
    CVPixelBuffer, CVPixelBufferCreate, CVPixelBufferGetBaseAddressOfPlane,
    CVPixelBufferGetBytesPerRowOfPlane, CVPixelBufferGetHeightOfPlane, CVPixelBufferGetIOSurface,
    CVPixelBufferLockBaseAddress, CVPixelBufferLockFlags, CVPixelBufferUnlockBaseAddress,
    kCVPixelBufferIOSurfacePropertiesKey, kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange,
    kCVReturnSuccess,
};
use objc2_io_surface::IOSurfaceRef;
use std::ptr::NonNull;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::mpsc::{Receiver, Sender, TryRecvError, channel};
use std::thread::{self, JoinHandle};
use std::time::Duration;

use crate::protocol::{
    ClockWire, FRAME_FLAG_HAS_PTS, FrameInfoWire, FrameRecord, LogLevel, MediaFactsValue,
    PlaybackState, SURFACE_SLOT_COUNT, SharedSegment, TransportSnapshot, VerifyEntry, checksum_luma,
    uptime_nanos,
};

const BACKOFF: Duration = Duration::from_micros(200);

const MAX_SEQUENCES: u64 = 100_000;

const BT709_LIMITED: [f32; 12] = [
    1.1644, 0.0, 1.7927, -0.9729,
    1.1644, -0.2132, -0.5329, 0.3015,
    1.1644, 2.1124, 0.0, -1.1334,
];


pub struct SurfacePool {
    buffers: Vec<CFRetained<CVPixelBuffer>>,
    visible_width: u32,
    visible_height: u32,
}

impl SurfacePool {
    pub fn new(count: usize, visible_width: u32, visible_height: u32) -> Result<Self> {
        if count == 0 || count > SURFACE_SLOT_COUNT {
            return Err(anyhow!(
                "pool of {count} surfaces is outside 1..={SURFACE_SLOT_COUNT}"
            ));
        }
        if visible_width == 0 || visible_height == 0 {
            return Err(anyhow!("pool geometry is {visible_width}x{visible_height}"));
        }

        let inner = CFDictionary::<CFString, CFString>::empty();
        let key: &CFString = unsafe { kCVPixelBufferIOSurfacePropertiesKey };
        let attributes =
            CFDictionary::<CFString, CFDictionary>::from_slices(&[key], &[inner.as_opaque()]);

        let mut buffers = Vec::with_capacity(count);
        for _ in 0..count {
            buffers.push(create_nv12(
                visible_width,
                visible_height,
                attributes.as_opaque(),
            )?);
        }
        Ok(Self {
            buffers,
            visible_width,
            visible_height,
        })
    }

    pub const fn visible(&self) -> (u32, u32) {
        (self.visible_width, self.visible_height)
    }

    pub const fn len(&self) -> usize {
        self.buffers.len()
    }

    pub const fn is_empty(&self) -> bool {
        self.buffers.is_empty()
    }

    pub fn surface(&self, index: usize) -> Result<CFRetained<IOSurfaceRef>> {
        let buffer = self
            .buffers
            .get(index)
            .ok_or_else(|| anyhow!("pool index {index} out of range"))?;
        CVPixelBufferGetIOSurface(Some(buffer))
            .ok_or_else(|| anyhow!("pool entry {index} is not IOSurface-backed"))
    }

    pub fn luma_plane(&self, index: usize) -> Result<(u32, u32)> {
        let surface = self.surface(index)?;
        let geometry = crate::host::measure(&surface)?;
        let (Some(&width), Some(&height)) = (
            geometry.plane_width.first(),
            geometry.plane_height.first(),
        ) else {
            return Err(anyhow!("pool entry {index} has no luma plane"));
        };
        Ok((width, height))
    }

    pub fn paint(&mut self, index: usize, seed: u32) -> Result<(u64, u64)> {
        let buffer = self
            .buffers
            .get(index)
            .ok_or_else(|| anyhow!("pool index {index} out of range"))?;
        let guard = LockedPlanes::acquire(buffer)?;

        let mut state = seed ^ 0x9e37_79b9;
        for plane in 0..2usize {
            let bytes = guard.plane_bytes_mut(plane)?;
            for byte in bytes {
                state = state
                    .wrapping_mul(1_664_525)
                    .wrapping_add(1_013_904_223);
                *byte = (state >> 24) as u8;
            }
        }

        let luma = guard.plane_bytes(0)?;
        Ok(checksum_luma(
            luma,
            guard.stride(0)?,
            self.visible_width as usize,
            self.visible_height as usize,
        ))
    }

    pub fn flip_one_visible_bit(&mut self, index: usize) -> Result<u64> {
        let buffer = self
            .buffers
            .get(index)
            .ok_or_else(|| anyhow!("pool index {index} out of range"))?;
        let guard = LockedPlanes::acquire(buffer)?;
        let stride = guard.stride(0)?;
        let row = (self.visible_height as usize) / 2;
        let column = (self.visible_width as usize) / 2;
        let offset = row
            .checked_mul(stride)
            .and_then(|start| start.checked_add(column))
            .ok_or_else(|| anyhow!("flip offset overflows"))?;
        {
            let bytes = guard.plane_bytes_mut(0)?;
            let cell = bytes
                .get_mut(offset)
                .ok_or_else(|| anyhow!("flip offset {offset} is outside the plane"))?;
            *cell ^= 0x01;
        }
        let luma = guard.plane_bytes(0)?;
        Ok(checksum_luma(
            luma,
            stride,
            self.visible_width as usize,
            self.visible_height as usize,
        )
        .0)
    }
}

struct LockedPlanes<'a> {
    buffer: &'a CVPixelBuffer,
}

impl<'a> LockedPlanes<'a> {
    fn acquire(buffer: &'a CVPixelBuffer) -> Result<Self> {
        let status = unsafe { CVPixelBufferLockBaseAddress(buffer, CVPixelBufferLockFlags(0)) };
        if status != kCVReturnSuccess {
            return Err(anyhow!("CVPixelBufferLockBaseAddress -> {status}"));
        }
        Ok(Self { buffer })
    }

    fn stride(&self, plane: usize) -> Result<usize> {
        let stride = CVPixelBufferGetBytesPerRowOfPlane(self.buffer, plane);
        if stride == 0 {
            return Err(anyhow!("plane {plane} has a zero stride"));
        }
        Ok(stride)
    }

    fn extent(&self, plane: usize) -> Result<(usize, usize)> {
        let stride = self.stride(plane)?;
        let rows = CVPixelBufferGetHeightOfPlane(self.buffer, plane);
        let bytes = stride
            .checked_mul(rows)
            .ok_or_else(|| anyhow!("plane {plane} extent overflows"))?;
        Ok((bytes, rows))
    }

    fn base(&self, plane: usize) -> Result<*mut u8> {
        let base = CVPixelBufferGetBaseAddressOfPlane(self.buffer, plane);
        if base.is_null() {
            return Err(anyhow!("plane {plane} has no base address"));
        }
        Ok(base.cast::<u8>())
    }

    fn plane_bytes(&self, plane: usize) -> Result<&[u8]> {
        let (bytes, _) = self.extent(plane)?;
        let base = self.base(plane)?;
        Ok(unsafe { std::slice::from_raw_parts(base.cast_const(), bytes) })
    }

    #[allow(
        clippy::mut_from_ref,
        reason = "the lock guard is the exclusive handle to these bytes for its \
                  whole lifetime; taking &mut self here would only move the \
                  aliasing argument, not strengthen it"
    )]
    fn plane_bytes_mut(&self, plane: usize) -> Result<&mut [u8]> {
        let (bytes, _) = self.extent(plane)?;
        let base = self.base(plane)?;
        Ok(unsafe { std::slice::from_raw_parts_mut(base, bytes) })
    }
}

impl Drop for LockedPlanes<'_> {
    fn drop(&mut self) {
        let _ = unsafe { CVPixelBufferUnlockBaseAddress(self.buffer, CVPixelBufferLockFlags(0)) };
    }
}

fn create_nv12(width: u32, height: u32, attributes: &CFDictionary) -> Result<CFRetained<CVPixelBuffer>> {
    let mut out: *mut CVPixelBuffer = std::ptr::null_mut();
    let status = unsafe {
        CVPixelBufferCreate(
            None,
            width as usize,
            height as usize,
            kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange,
            Some(attributes),
            NonNull::from(&mut out),
        )
    };
    if status != kCVReturnSuccess {
        return Err(anyhow!("CVPixelBufferCreate({width}x{height}) -> {status}"));
    }
    let pointer = NonNull::new(out).ok_or_else(|| anyhow!("CVPixelBufferCreate returned null"))?;
    Ok(unsafe { CFRetained::from_raw(pointer) })
}


pub struct Announcement {
    pub slot: usize,
    pub generation: u64,
    surface: CFRetained<IOSurfaceRef>,
}

impl Announcement {
    pub fn into_surface(self) -> CFRetained<IOSurfaceRef> {
        self.surface
    }
}

#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for Announcement {}


#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RecycleMode {
    None,
    CorruptHeld,
    EarlyUnpin,
    RingExit,
}

#[derive(Clone, Copy, Debug)]
pub struct SyntheticOptions {
    pub visible_width: u32,
    pub visible_height: u32,
    pub pool: usize,
    pub fps: f64,
    pub lead: f64,
    pub recycle_mode: RecycleMode,
}

impl Default for SyntheticOptions {
    fn default() -> Self {
        Self {
            visible_width: 640,
            visible_height: 360,
            pool: 6,
            fps: 30.0,
            lead: 0.20,
            recycle_mode: RecycleMode::None,
        }
    }
}

pub struct SyntheticSource {
    stop: Arc<AtomicBool>,
    published: Arc<AtomicU64>,
    announcements: Receiver<Announcement>,
    worker: Option<JoinHandle<Result<()>>>,
}

impl SyntheticSource {
    pub fn spawn(
        segment: Arc<SharedSegment>,
        options: SyntheticOptions,
        wedge: bool,
    ) -> Result<Self> {
        if options.pool <= crate::protocol::RETAINED_FRAMES {
            return Err(anyhow!(
                "a pool of {} cannot outrun a retained window of {}",
                options.pool,
                crate::protocol::RETAINED_FRAMES
            ));
        }
        if !(options.fps.is_finite() && options.fps > 0.0) {
            return Err(anyhow!("fps {} is not a rate", options.fps));
        }

        let stop = Arc::new(AtomicBool::new(false));
        let published = Arc::new(AtomicU64::new(0));
        let (sender, announcements) = channel();
        let worker = {
            let stop = Arc::clone(&stop);
            let published = Arc::clone(&published);
            thread::Builder::new()
                .name("uuav-synthetic".to_owned())
                .spawn(move || produce(&segment, options, wedge, &sender, &stop, &published))?
        };

        Ok(Self {
            stop,
            published,
            announcements,
            worker: Some(worker),
        })
    }

    pub fn try_announcement(&self) -> Result<Option<Announcement>> {
        match self.announcements.try_recv() {
            Ok(announcement) => Ok(Some(announcement)),
            Err(TryRecvError::Empty | TryRecvError::Disconnected) => Ok(None),
        }
    }

    pub fn published(&self) -> u64 {
        self.published.load(Ordering::Acquire)
    }

    pub fn abandon(&mut self) -> Result<()> {
        self.stop.store(true, Ordering::Release);
        let Some(worker) = self.worker.take() else {
            return Ok(());
        };
        worker
            .join()
            .map_err(|_| anyhow!("the synthetic producer panicked"))?
    }
}

impl Drop for SyntheticSource {
    fn drop(&mut self) {
        self.stop.store(true, Ordering::Release);
        if let Some(worker) = self.worker.take() {
            let _ = worker.join();
        }
    }
}

struct Credit {
    pending: [Option<u64>; SURFACE_SLOT_COUNT],
    announced: [bool; SURFACE_SLOT_COUNT],
}

impl Credit {
    const fn new() -> Self {
        Self {
            pending: [None; SURFACE_SLOT_COUNT],
            announced: [false; SURFACE_SLOT_COUNT],
        }
    }

    fn drain(&mut self, segment: &SharedSegment) -> Result<()> {
        while let Some(sequence) = segment.release.take()? {
            for cell in &mut self.pending {
                if *cell == Some(sequence) {
                    *cell = None;
                }
            }
        }
        Ok(())
    }

    fn free_slot(&self, pool: usize) -> Option<usize> {
        (0..pool).find(|slot| matches!(self.pending.get(*slot), Some(None)))
    }
}

fn produce(
    segment: &SharedSegment,
    options: SyntheticOptions,
    wedge: bool,
    announce: &Sender<Announcement>,
    stop: &AtomicBool,
    published: &AtomicU64,
) -> Result<()> {
    let mut pool = SurfacePool::new(options.pool, options.visible_width, options.visible_height)?;
    let shape = pool.luma_plane(0)?;
    let mut credit = Credit::new();
    let clock = open(segment, options);

    if wedge {
        segment.log.emit(LogLevel::Info, "synthetic source WEDGED after open");
        while !stopping(segment, stop) {
            thread::sleep(BACKOFF);
        }
        return Ok(());
    }

    let mut sequence = 0u64;
    let mut pts = 0.0f64;
    while sequence < MAX_SEQUENCES && !stopping(segment, stop) {
        sequence = sequence.wrapping_add(1);

        if !wait(segment, stop, &mut credit, pace(clock, pts, options.lead))? {
            break;
        }
        let Some(slot) = acquire(segment, stop, &mut credit, options.pool)? else {
            break;
        };

        let decode_ready_nanos = uptime_nanos();
        let (checksum, byte_count) = pool.paint(slot, sequence as u32)?;
        announce_once(announce, &pool, &mut credit, slot)?;
        segment.verify.publish(VerifyEntry {
            sequence,
            checksum,
            byte_count,
            decode_ready_nanos,
            published_nanos: uptime_nanos(),
        });

        let record = build(options, shape, sequence, slot, pts);
        if !publish(segment, stop, &mut credit, &record)? {
            break;
        }
        if let Some(cell) = credit.pending.get_mut(slot) {
            *cell = Some(sequence);
        }
        published.store(sequence, Ordering::Release);

        if options.recycle_mode != RecycleMode::None {
            credit.drain(segment)?;
            recycle_violation(segment, &mut pool, &credit, options.recycle_mode, options.pool, slot)?;
        }
        pts = (sequence as f64) / options.fps;
    }

    finish(segment, clock, pts);
    Ok(())
}

fn open(segment: &SharedSegment, options: SyntheticOptions) -> ClockWire {
    segment.media.publish(MediaFactsValue {
        open_generation: 1,
        duration: 0.0,
        visible_width: options.visible_width,
        visible_height: options.visible_height,
        has_video: true,
        has_audio: false,
        sample_rate: 0,
        channels: 0,
    });
    segment.transport.publish(TransportSnapshot {
        state: PlaybackState::Ready,
        clock: ClockWire::HELD_AT_ZERO,
    });
    let clock = ClockWire {
        base: 0.0,
        anchor_nanos: uptime_nanos(),
        rate: 1.0,
    };
    segment.transport.publish(TransportSnapshot {
        state: PlaybackState::Playing,
        clock,
    });
    segment.log.emit(
        LogLevel::Info,
        &format!(
            "synthetic source open {}x{} pool={} fps={}",
            options.visible_width, options.visible_height, options.pool, options.fps
        ),
    );
    clock
}

fn pace(clock: ClockWire, pts: f64, lead: f64) -> u64 {
    let ahead = (pts - lead - clock.base) / clock.rate;
    if !ahead.is_finite() || ahead <= 0.0 {
        return clock.anchor_nanos;
    }
    clock
        .anchor_nanos
        .saturating_add((ahead * 1e9) as u64)
}

fn wait(
    segment: &SharedSegment,
    stop: &AtomicBool,
    credit: &mut Credit,
    deadline_nanos: u64,
) -> Result<bool> {
    while uptime_nanos() < deadline_nanos {
        if stopping(segment, stop) {
            return Ok(false);
        }
        credit.drain(segment)?;
        thread::sleep(BACKOFF);
    }
    Ok(true)
}

fn acquire(
    segment: &SharedSegment,
    stop: &AtomicBool,
    credit: &mut Credit,
    pool: usize,
) -> Result<Option<usize>> {
    loop {
        credit.drain(segment)?;
        if let Some(slot) = credit.free_slot(pool) {
            return Ok(Some(slot));
        }
        if stopping(segment, stop) {
            return Ok(None);
        }
        thread::sleep(BACKOFF);
    }
}

fn announce_once(
    announce: &Sender<Announcement>,
    pool: &SurfacePool,
    credit: &mut Credit,
    slot: usize,
) -> Result<()> {
    if credit.announced.get(slot) != Some(&false) {
        return Ok(());
    }
    announce
        .send(Announcement {
            slot,
            generation: 0,
            surface: pool.surface(slot)?,
        })
        .map_err(|_| anyhow!("the host stopped listening for surface announcements"))?;
    if let Some(cell) = credit.announced.get_mut(slot) {
        *cell = true;
    }
    Ok(())
}

fn build(
    options: SyntheticOptions,
    shape: (u32, u32),
    sequence: u64,
    slot: usize,
    pts: f64,
) -> FrameRecord {
    let (plane_width, plane_height) = shape;
    FrameRecord {
        info: FrameInfoWire {
            yuv_to_rgb: BT709_LIMITED,
            uv_transform: uv_transform(
                options.visible_width,
                options.visible_height,
                plane_width,
                plane_height,
            ),
            visible_width: options.visible_width,
            visible_height: options.visible_height,
            plane_width: [plane_width, plane_width / 2],
            plane_height: [plane_height, plane_height / 2],
            colorspace: 1,
            color_range: 1,
            color_primaries: 1,
            rotation: 0,
            bit_depth: 8,
        },
        flags: FRAME_FLAG_HAS_PTS,
        pts,
        sequence,
        slot: slot as u32,
        reserved: 0,
    }
}

fn publish(
    segment: &SharedSegment,
    stop: &AtomicBool,
    credit: &mut Credit,
    record: &FrameRecord,
) -> Result<bool> {
    while !segment.video.publish(record) {
        if stopping(segment, stop) {
            return Ok(false);
        }
        credit.drain(segment)?;
        thread::sleep(BACKOFF);
    }
    Ok(true)
}

fn recycle_violation(
    segment: &SharedSegment,
    pool: &mut SurfacePool,
    credit: &Credit,
    mode: RecycleMode,
    count: usize,
    just_published: usize,
) -> Result<()> {
    let tail_sequence = match segment.video.peek() {
        Ok(Some(record)) => record.sequence,
        _ => u64::MAX,
    };

    for slot in 0..count {
        if slot == just_published {
            continue;
        }
        let Some(&Some(held)) = credit.pending.get(slot) else {
            continue;
        };
        let still_in_ring = held >= tail_sequence;
        let verified_and_held = held.saturating_add(1) < tail_sequence;
        let hit = match mode {
            RecycleMode::None => false,
            RecycleMode::CorruptHeld => true,
            RecycleMode::EarlyUnpin => still_in_ring,
            RecycleMode::RingExit => verified_and_held,
        };
        if !hit {
            continue;
        }
        pool.paint(slot, 0xdead_0000_u32.wrapping_add(slot as u32))?;
        segment.log.emit(
            LogLevel::Error,
            &format!(
                "NEGATIVE[{mode:?}]: overwrote slot {slot}, still held for sequence {held} \
                 (ring tail sequence {tail_sequence})"
            ),
        );
    }
    Ok(())
}

fn stopping(segment: &SharedSegment, stop: &AtomicBool) -> bool {
    stop.load(Ordering::Acquire) || segment.cancel.is_set()
}

fn finish(segment: &SharedSegment, clock: ClockWire, pts: f64) {
    segment.transport.publish(TransportSnapshot {
        state: PlaybackState::Ended,
        clock: ClockWire {
            base: pts,
            anchor_nanos: 0,
            rate: clock.rate,
        },
    });
}

fn uv_transform(
    visible_width: u32,
    visible_height: u32,
    plane_width: u32,
    plane_height: u32,
) -> [f32; 6] {
    let sx = visible_ratio(visible_width, plane_width);
    let sy = visible_ratio(visible_height, plane_height);
    [sx, 0.0, 0.0, 0.0, -sy, sy]
}

fn visible_ratio(visible: u32, allocated: u32) -> f32 {
    if visible == 0 || allocated == 0 || visible >= allocated {
        1.0
    } else {
        visible as f32 / allocated as f32
    }
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
    fn a_pool_that_cannot_outrun_the_window_is_refused() {
        let segment: Arc<SharedSegment> = Arc::from(SharedSegment::boxed_zeroed());
        let options = SyntheticOptions {
            pool: crate::protocol::RETAINED_FRAMES,
            ..SyntheticOptions::default()
        };
        assert!(SyntheticSource::spawn(segment, options, false).is_err());
    }

}
