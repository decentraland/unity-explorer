#![allow(clippy::all, clippy::pedantic, clippy::nursery)]

use std::panic::{catch_unwind, AssertUnwindSafe};
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::Arc;
use std::time::Instant;

use uuav_ipc::protocol::{
    ClockWire, FrameInfoWire, FrameRecord, SharedSegment, SurfaceGeometry, FRAME_FLAG_HAS_PTS,
    MAX_PLANE_DIMENSION, SEGMENT_BYTES, SURFACE_SLOT_COUNT, VIDEO_RING_CAPACITY,
};


struct Rng(u64);

impl Rng {
    fn new(seed: u64) -> Self {
        Self(seed)
    }
    fn next_u64(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }
    fn next_u32(&mut self) -> u32 {
        self.next_u64() as u32
    }
    fn nasty_u32(&mut self) -> u32 {
        const CORNERS: [u32; 12] = [
            0,
            1,
            2,
            8,
            10,
            MAX_PLANE_DIMENSION - 1,
            MAX_PLANE_DIMENSION,
            MAX_PLANE_DIMENSION + 1,
            0x0000_ffff,
            0x7fff_ffff,
            0xffff_fffe,
            u32::MAX,
        ];
        if self.next_u64() & 3 != 0 {
            CORNERS[(self.next_u64() as usize) % CORNERS.len()]
        } else {
            self.next_u32()
        }
    }
    fn nasty_f32(&mut self) -> f32 {
        const CORNERS: [f32; 8] = [
            0.0,
            -0.0,
            1.0,
            -1.0,
            f32::NAN,
            f32::INFINITY,
            f32::NEG_INFINITY,
            f32::MAX,
        ];
        if self.next_u64() & 1 != 0 {
            CORNERS[(self.next_u64() as usize) % CORNERS.len()]
        } else {
            f32::from_bits(self.next_u32())
        }
    }
    fn nasty_f64(&mut self) -> f64 {
        const CORNERS: [f64; 8] = [
            0.0,
            -0.0,
            1.0,
            -1.0,
            f64::NAN,
            f64::INFINITY,
            f64::NEG_INFINITY,
            f64::MAX,
        ];
        if self.next_u64() & 1 != 0 {
            CORNERS[(self.next_u64() as usize) % CORNERS.len()]
        } else {
            f64::from_bits(self.next_u64())
        }
    }
}


fn check_valid_frame(
    v: &uuav_ipc::protocol::ValidFrame,
    surf: &SurfaceGeometry,
    last_sequence: u64,
) -> Result<(), String> {
    if v.slot >= SURFACE_SLOT_COUNT {
        return Err(format!("slot {} >= SURFACE_SLOT_COUNT", v.slot));
    }
    if v.sequence <= last_sequence {
        return Err(format!("sequence {} <= last {}", v.sequence, last_sequence));
    }
    let info = &v.info;
    for (i, (&w, &h)) in info.plane_width.iter().zip(info.plane_height.iter()).enumerate() {
        if w == 0 || w > MAX_PLANE_DIMENSION || h == 0 || h > MAX_PLANE_DIMENSION {
            return Err(format!("plane {i} dim out of range: {w}x{h}"));
        }
        if w > surf.plane_width[i] || h > surf.plane_height[i] {
            return Err(format!(
                "plane {i} claim {w}x{h} exceeds surface {}x{}",
                surf.plane_width[i], surf.plane_height[i]
            ));
        }
    }
    if info.visible_width == 0
        || info.visible_width > MAX_PLANE_DIMENSION
        || info.visible_height == 0
        || info.visible_height > MAX_PLANE_DIMENSION
    {
        return Err(format!(
            "visible {}x{} out of range",
            info.visible_width, info.visible_height
        ));
    }
    if info.visible_width > info.plane_width[0] || info.visible_height > info.plane_height[0] {
        return Err("visible exceeds luma plane".into());
    }
    if info.bit_depth != 8 && info.bit_depth != 10 {
        return Err(format!("bit_depth {}", info.bit_depth));
    }
    if !matches!(info.rotation, 0 | 90 | 180 | 270) {
        return Err(format!("rotation {}", info.rotation));
    }
    for &c in info.yuv_to_rgb.iter().chain(info.uv_transform.iter()) {
        if !c.is_finite() || c.abs() > 16.0 {
            return Err(format!("shader constant {c} not finite/clamped"));
        }
    }
    if let Some(pts) = v.pts {
        if !pts.is_finite() {
            return Err(format!("pts {pts} not finite"));
        }
    }
    Ok(())
}


fn hostile_info(rng: &mut Rng) -> FrameInfoWire {
    let mut yuv = [0f32; 12];
    for v in &mut yuv {
        *v = rng.nasty_f32();
    }
    let mut uv = [0f32; 6];
    for v in &mut uv {
        *v = rng.nasty_f32();
    }
    FrameInfoWire {
        yuv_to_rgb: yuv,
        uv_transform: uv,
        visible_width: rng.nasty_u32(),
        visible_height: rng.nasty_u32(),
        plane_width: [rng.nasty_u32(), rng.nasty_u32()],
        plane_height: [rng.nasty_u32(), rng.nasty_u32()],
        colorspace: rng.next_u32() as i32,
        color_range: rng.next_u32() as i32,
        color_primaries: rng.next_u32() as i32,
        rotation: {
            let angles = [0, 90, 180, 270];
            if rng.next_u64() & 1 != 0 {
                angles[(rng.next_u64() as usize) % 4]
            } else {
                rng.next_u32() as i32
            }
        },
        bit_depth: {
            let ok = [8u32, 10];
            if rng.next_u64() & 1 != 0 {
                ok[(rng.next_u64() as usize) % 2]
            } else {
                rng.nasty_u32()
            }
        },
    }
}

fn hostile_record(rng: &mut Rng) -> FrameRecord {
    FrameRecord {
        info: hostile_info(rng),
        flags: if rng.next_u64() & 1 != 0 {
            FRAME_FLAG_HAS_PTS
        } else {
            rng.next_u32()
        },
        pts: rng.nasty_f64(),
        sequence: if rng.next_u64() & 1 != 0 {
            rng.next_u64()
        } else {
            rng.next_u64() & 0xff
        },
        slot: if rng.next_u64() & 1 != 0 {
            rng.next_u32()
        } else {
            rng.next_u32() % (SURFACE_SLOT_COUNT as u32 + 4)
        },
        reserved: rng.next_u32(),
    }
}

fn hostile_geometry(rng: &mut Rng) -> SurfaceGeometry {
    SurfaceGeometry {
        plane_width: [rng.nasty_u32(), rng.nasty_u32()],
        plane_height: [rng.nasty_u32(), rng.nasty_u32()],
        plane_count: {
            let corners = [0u32, 1, 2, 3, rng.next_u32()];
            corners[(rng.next_u64() as usize) % corners.len()]
        },
    }
}


#[test]
fn fuzz_validate_never_panics_or_leaks_a_bad_frame() {
    let iters: u64 = std::env::var("FUZZ_VALIDATE_ITERS")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(3_000_000);
    let base_seed: u64 = std::env::var("FUZZ_SEED")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(0xC0FF_EE00_1234_5678);

    let mut rng = Rng::new(base_seed);
    let start = Instant::now();

    let mut accepted = 0u64;
    let mut too_few_planes = 0u64;
    let mut bad_dim = 0u64;
    let mut plane_exceeds = 0u64;
    let mut visible_exceeds = 0u64;
    let mut bad_depth = 0u64;
    let mut bad_rot = 0u64;
    let mut nonfinite = 0u64;
    let mut slot_oor = 0u64;
    let mut seq_not_adv = 0u64;

    for i in 0..iters {
        let rec = hostile_record(&mut rng);
        let surf = hostile_geometry(&mut rng);
        let last = if rng.next_u64() & 1 != 0 {
            rng.next_u64()
        } else {
            rng.next_u64() & 0xff
        };

        let outcome = catch_unwind(AssertUnwindSafe(|| rec.validate(last, &surf)));
        let result = match outcome {
            Ok(r) => r,
            Err(_) => panic!(
                "PANIC in validate: seed={base_seed} iter={i}\n record={rec:?}\n surface={surf:?}\n last={last}"
            ),
        };

        use uuav_ipc::protocol::FrameFault::*;
        match result {
            Ok(valid) => {
                if let Err(why) = check_valid_frame(&valid, &surf, last) {
                    panic!(
                        "BAD VALUE ACCEPTED: {why}\n seed={base_seed} iter={i}\n record={rec:?}\n surface={surf:?}\n last={last}\n valid={valid:?}"
                    );
                }
                accepted += 1;
            }
            Err(TooFewPlanes { .. }) => too_few_planes += 1,
            Err(BadDimension { .. }) => bad_dim += 1,
            Err(PlaneExceedsSurface { .. }) => plane_exceeds += 1,
            Err(VisibleExceedsPlane) => visible_exceeds += 1,
            Err(BadBitDepth { .. }) => bad_depth += 1,
            Err(BadRotation { .. }) => bad_rot += 1,
            Err(NonFinitePts) => nonfinite += 1,
            Err(SlotOutOfRange { .. }) => slot_oor += 1,
            Err(SequenceNotAdvancing { .. }) => seq_not_adv += 1,
            Err(SlotNotImported { .. }) => {}
        }
    }

    let secs = start.elapsed().as_secs_f64();
    println!(
        "validate fuzz: {iters} iters in {secs:.2}s ({:.0}/s)\n  accepted={accepted} too_few_planes={too_few_planes} bad_dim={bad_dim} plane_exceeds={plane_exceeds} visible_exceeds={visible_exceeds} bad_depth={bad_depth} bad_rot={bad_rot} nonfinite_pts={nonfinite} slot_oor={slot_oor} seq_not_adv={seq_not_adv}",
        iters as f64 / secs
    );
    assert!(accepted > 0, "no frame ever validated - generator too hostile to be meaningful");
    assert!(too_few_planes > 0 && bad_dim > 0 && plane_exceeds > 0);
}


#[test]
fn fuzz_segment_accessors_hold_against_arbitrary_bytes() {
    let iters: u64 = std::env::var("FUZZ_SEGMENT_ITERS")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(400_000);
    let base_seed: u64 = std::env::var("FUZZ_SEED")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(0x1357_9BDF_2468_ACE0);

    let mut rng = Rng::new(base_seed);

    let mut boxed = SharedSegment::boxed_zeroed();
    let base: *mut u8 = std::ptr::addr_of_mut!(*boxed) as *mut u8;
    assert_eq!(SEGMENT_BYTES, std::mem::size_of::<SharedSegment>());

    unsafe {
        let mut off = 0usize;
        while off + 8 <= SEGMENT_BYTES {
            base.add(off).cast::<u64>().write_unaligned(rng.next_u64());
            off += 8;
        }
    }

    let mut audio_buf = vec![0f32; 4096];
    let start = Instant::now();
    let mut breaches: Vec<String> = Vec::new();

    for i in 0..iters {
        unsafe {
            let muts = 4 + (rng.next_u64() % 24);
            for _ in 0..muts {
                let word = (rng.next_u64() as usize) % (SEGMENT_BYTES / 8);
                let val = match rng.next_u64() % 4 {
                    0 => rng.next_u64(),
                    1 => rng.next_u64() & 1,
                    2 => u64::MAX - (rng.next_u64() & 0xffff),
                    _ => rng.next_u64() & 0xff,
                };
                base.add(word * 8).cast::<u64>().write_unaligned(val);
            }
        }

        let seg: &SharedSegment = unsafe { &*(base as *const SharedSegment) };
        let rand_seq = rng.next_u64();
        let rand_last = rng.next_u64();
        let rand_surf = hostile_geometry(&mut rng);
        let skip_n = (rng.next_u64() as usize) & 0x1_ffff;
        let audio_take = (rng.next_u64() as usize) % audio_buf.len();

        let r = catch_unwind(AssertUnwindSafe(|| {
            let tr = seg.transport.read();
            if let uuav_ipc::protocol::TransportRead::Fresh(snap) = tr {
                let t = snap.clock.now(rng_seed_free(rand_seq));
                assert!(!t.is_nan(), "clock.now produced NaN");
            }
            let _ = ClockWire {
                base: f64::from_bits(rand_seq),
                anchor_nanos: rand_last,
                rate: f64::from_bits(rand_seq ^ rand_last),
            }
            .now(rand_last);

            let mf = seg.media.read();
            if let Ok(Some(f)) = mf {
                if f.has_video {
                    assert!(f.visible_width >= 1 && f.visible_width <= MAX_PLANE_DIMENSION);
                    assert!(f.visible_height >= 1 && f.visible_height <= MAX_PLANE_DIMENSION);
                }
                if f.has_audio {
                    assert!(f.sample_rate >= 1 && f.sample_rate <= 768_000);
                    assert!(f.channels >= 1 && f.channels <= 64);
                }
            }

            let _ = seg.video.depth();
            if let Ok(Some(rec)) = seg.video.peek() {
                if let Ok(valid) = rec.validate(rand_last, &rand_surf) {
                    check_valid_frame(&valid, &rand_surf, rand_last)
                        .map_err(|e| format!("video.peek->validate: {e}"))?;
                }
                seg.video.commit();
            }

            let _ = seg.verify.lookup(rand_seq);

            let _ = seg.audio.occupied();
            let _ = seg.audio.generation();
            let _ = seg.audio.read_position();
            let _ = seg.audio.take_marker();
            let _ = seg.audio.pop_into(&mut audio_buf[..audio_take]);
            let _ = seg.audio.skip(skip_n);

            let _ = seg.log.take();

            let _ = seg.audio_format.read();
            let _ = seg.controls_echo.read();
            let _ = seg.controls.requested_rate();
            let _ = seg.controls.master_clock();
            let _ = seg.controls.looping();
            let _ = seg.protocol_whitelist.read();

            let _ = seg.open.take(rand_seq);
            let _ = seg.seek.is_pending();
            let _ = seg.seek.take();

            let _ = seg.attach(rng_seed_free(rand_seq) as u32, rand_last);

            Ok::<(), String>(())
        }));

        match r {
            Err(_) => panic!(
                "PANIC in a segment accessor: seed={base_seed} iter={i} (build with -C overflow-checks=on to see arithmetic overflow; a bounds panic is an OOB)"
            ),
            Ok(Err(why)) => {
                breaches.push(format!("iter {i}: {why}"));
                if breaches.len() > 20 {
                    break;
                }
            }
            Ok(Ok(())) => {}
        }
    }

    let secs = start.elapsed().as_secs_f64();
    println!(
        "segment fuzz: {iters} iters in {secs:.2}s ({:.0}/s) over {} host accessors/iter",
        iters as f64 / secs,
        22
    );
    assert!(
        breaches.is_empty(),
        "silently-accepted bad values:\n{}",
        breaches.join("\n")
    );
}

fn rng_seed_free(x: u64) -> u64 {
    x
}


#[test]
fn fuzz_concurrent_torn_reads_terminate_and_hold() {
    use std::thread;
    use uuav_ipc::protocol::{
        AudioMarker, MediaFactsValue, PlaybackState, TransportSnapshot,
    };

    let segment: Arc<SharedSegment> = Arc::from(SharedSegment::boxed_zeroed());
    let stop = Arc::new(AtomicBool::new(false));
    let breach = Arc::new(AtomicU64::new(0));
    let duration = std::time::Duration::from_secs(
        std::env::var("FUZZ_CONC_SECS")
            .ok()
            .and_then(|s| s.parse().ok())
            .unwrap_or(6),
    );

    let w_seg = segment.clone();
    let w_stop = stop.clone();
    let writer_meta = thread::spawn(move || {
        let mut rng = Rng::new(0xA5A5_1111);
        let states = [
            PlaybackState::Ready,
            PlaybackState::Playing,
            PlaybackState::Paused,
            PlaybackState::Ended,
        ];
        while !w_stop.load(Ordering::Relaxed) {
            w_seg.transport.publish(TransportSnapshot {
                state: states[(rng.next_u64() as usize) % 4],
                clock: ClockWire {
                    base: rng.nasty_f64(),
                    anchor_nanos: rng.next_u64(),
                    rate: rng.nasty_f64(),
                },
            });
            w_seg.media.publish(MediaFactsValue {
                open_generation: rng.next_u64(),
                duration: rng.nasty_f64(),
                visible_width: rng.nasty_u32(),
                visible_height: rng.nasty_u32(),
                has_video: rng.next_u64() & 1 != 0,
                has_audio: rng.next_u64() & 1 != 0,
                sample_rate: rng.nasty_u32(),
                channels: rng.nasty_u32(),
            });
            w_seg
                .audio_format
                .publish(rng.nasty_u32(), rng.nasty_u32(), rng.next_u64(), rng.next_u64());
            w_seg
                .controls_echo
                .publish(rng.next_u64() & 1 != 0, rng.next_u64());
        }
    });

    let v_seg = segment.clone();
    let v_stop = stop.clone();
    let writer_video = thread::spawn(move || {
        let mut rng = Rng::new(0x5A5A_2222);
        while !v_stop.load(Ordering::Relaxed) {
            let rec = hostile_record(&mut rng);
            let _ = v_seg.video.publish(&rec);
        }
    });

    let a_seg = segment.clone();
    let a_stop = stop.clone();
    let writer_audio = thread::spawn(move || {
        let mut rng = Rng::new(0x3C3C_3333);
        let mut samples = [0f32; 512];
        while !a_stop.load(Ordering::Relaxed) {
            for s in &mut samples {
                *s = rng.nasty_f32();
            }
            a_seg.audio.push(&samples);
            a_seg.audio.push_marker(AudioMarker {
                position: rng.next_u64(),
                pts: rng.nasty_f64(),
            });
            if rng.next_u64() & 0x3ff == 0 {
                a_seg.audio.restart();
            }
            use uuav_ipc::protocol::LogLevel;
            a_seg.log.emit(
                match rng.next_u64() % 3 {
                    0 => LogLevel::Error,
                    1 => LogLevel::Warning,
                    _ => LogLevel::Info,
                },
                "hostile\x00\x1b[31m log \n line \u{fffd} with control bytes and non-ascii \u{1f4a9}",
            );
        }
    });

    let mut readers = Vec::new();
    for tid in 0..3u64 {
        let r_seg = segment.clone();
        let r_stop = stop.clone();
        let r_breach = breach.clone();
        readers.push(thread::spawn(move || {
            let mut rng = Rng::new(0xDEAD_0000 ^ tid);
            let mut buf = vec![0f32; 1024];
            let mut count: u64 = 0;
            while !r_stop.load(Ordering::Relaxed) {
                count += 1;
                if let uuav_ipc::protocol::TransportRead::Fresh(s) = r_seg.transport.read() {
                    if s.clock.now(rng.next_u64()).is_nan() {
                        r_breach.fetch_add(1, Ordering::Relaxed);
                    }
                }
                if let Ok(Some(f)) = r_seg.media.read() {
                    if f.has_video
                        && (f.visible_width == 0
                            || f.visible_width > MAX_PLANE_DIMENSION
                            || f.visible_height == 0
                            || f.visible_height > MAX_PLANE_DIMENSION)
                    {
                        r_breach.fetch_add(1, Ordering::Relaxed);
                    }
                }
                if let Ok(Some(rec)) = r_seg.video.peek() {
                    let surf = hostile_geometry(&mut rng);
                    let last = rng.next_u64();
                    if let Ok(v) = rec.validate(last, &surf) {
                        if check_valid_frame(&v, &surf, last).is_err() {
                            r_breach.fetch_add(1, Ordering::Relaxed);
                        }
                    }
                    r_seg.video.commit();
                }
                let _ = r_seg.video.depth();
                let _ = r_seg.audio.occupied();
                let _ = r_seg.audio.take_marker();
                let _ = r_seg.audio.pop_into(&mut buf);
                let _ = r_seg.audio.skip((rng.next_u64() as usize) & 0xff);
                let _ = r_seg.audio_format.read();
                let _ = r_seg.controls_echo.read();
                if let Ok(Some(entry)) = r_seg.log.take() {
                    if entry.text.bytes().any(|b| b == 0 || (b < 0x20 && b != b' ') || b == 0x7f) {
                        r_breach.fetch_add(1, Ordering::Relaxed);
                    }
                }
                let _ = r_seg.verify.lookup(rng.next_u64());
            }
            count
        }));
    }

    thread::sleep(duration);
    stop.store(true, Ordering::Release);

    writer_meta.join().unwrap();
    writer_video.join().unwrap();
    writer_audio.join().unwrap();
    let total: u64 = readers.into_iter().map(|h| h.join().unwrap()).sum();

    println!(
        "concurrent fuzz: {:.1}s, {total} reader iterations across 3 threads, {} writer threads",
        duration.as_secs_f64(),
        3
    );
    assert_eq!(
        breach.load(Ordering::Relaxed),
        0,
        "a torn read produced a value that violates an accessor invariant"
    );
    assert!(total > 0);
    let _ = VIDEO_RING_CAPACITY;
}
