
#![cfg(target_os = "macos")]
#![allow(clippy::unwrap_used, clippy::expect_used, clippy::indexing_slicing)]

use std::alloc::{GlobalAlloc, Layout, System};
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};

use uuav::uuav_player_read_audio;
use uuav_ipc::audio::{self as jitter, JitterRing, RingState};
use uuav_ipc::protocol::ClockWire;

static ALLOCATIONS: AtomicUsize = AtomicUsize::new(0);
static ARMED: AtomicBool = AtomicBool::new(false);

struct Counting;

unsafe impl GlobalAlloc for Counting {
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        note();
        unsafe { System.alloc(layout) }
    }

    unsafe fn dealloc(&self, pointer: *mut u8, layout: Layout) {
        note();
        unsafe { System.dealloc(pointer, layout) }
    }

    unsafe fn realloc(&self, pointer: *mut u8, layout: Layout, new_size: usize) -> *mut u8 {
        note();
        unsafe { System.realloc(pointer, layout, new_size) }
    }

    unsafe fn alloc_zeroed(&self, layout: Layout) -> *mut u8 {
        note();
        unsafe { System.alloc_zeroed(layout) }
    }
}

fn note() {
    if ARMED.load(Ordering::Relaxed) {
        ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
    }
}

#[global_allocator]
static ALLOCATOR: Counting = Counting;

const TASK_EVENTS_INFO: u32 = 2;
const TASK_EVENTS_INFO_COUNT: u32 = 8;

unsafe extern "C" {
    static mach_task_self_: u32;
    fn task_info(target: u32, flavor: u32, info: *mut i32, count: *mut u32) -> i32;
}

fn task_events() -> (i64, i64, i64) {
    let mut info = [0i32; TASK_EVENTS_INFO_COUNT as usize];
    let mut count = TASK_EVENTS_INFO_COUNT;
    let status = unsafe {
        task_info(
            mach_task_self_,
            TASK_EVENTS_INFO,
            info.as_mut_ptr(),
            &raw mut count,
        )
    };
    assert_eq!(status, 0, "task_info(TASK_EVENTS_INFO) failed");
    (i64::from(info[5]), i64::from(info[6]), i64::from(info[7]))
}

const SAMPLE_RATE: u32 = 48_000;
const CHANNELS: u32 = 2;
const DSP_FRAMES: usize = 1_024;
const PACKET: usize = 960;
const ROUNDS: usize = 500_000;

fn publish(ring: &JitterRing, base: f64) {
    ring.publish_state(RingState {
        clock: ClockWire {
            base,
            anchor_nanos: 0,
            rate: 1.0,
        },
        playing: true,
        sample_rate: SAMPLE_RATE,
        channels: CHANNELS,
    });
}

fn drain(id: u64, buffer: &mut [f32]) -> i32 {
    unsafe { uuav_player_read_audio(id, buffer.as_mut_ptr(), DSP_FRAMES as i32) }
}

#[test]
fn the_drain_allocates_nothing_and_makes_no_syscall() {
    let id = 8_201;
    let ring = jitter::acquire(id).expect("a free slot");
    publish(ring, 0.0);

    let mut buffer = vec![0.0f32; DSP_FRAMES * CHANNELS as usize];
    let packet = vec![0.5f32; PACKET];

    for _ in 0..2_000 {
        ring.push_packet(0.0, &packet, false);
        drain(id, &mut buffer);
    }

    ALLOCATIONS.store(0, Ordering::Relaxed);
    let mut pts = 0.0f64;
    for round in 0..ROUNDS {
        ring.push_packet(pts, &packet, false);
        pts += PACKET as f64 / f64::from(SAMPLE_RATE) / f64::from(CHANNELS);
        if round % 64 == 0 {
            publish(ring, pts);
        }
        ARMED.store(true, Ordering::Relaxed);
        drain(id, &mut buffer);
        ARMED.store(false, Ordering::Relaxed);
    }
    let allocations = ALLOCATIONS.load(Ordering::Relaxed);

    let control = measure(|| {
        let mut sink = 0u64;
        for round in 0..ROUNDS {
            sink = std::hint::black_box(sink.wrapping_add(round as u64));
        }
        std::hint::black_box(sink);
    });

    publish(ring, 0.0);
    for _ in 0..8 {
        ring.push_packet(0.0, &packet, false);
    }
    let observed = measure(|| {
        for _ in 0..ROUNDS {
            drain(id, &mut buffer);
        }
    });
    jitter::release(id);

    let budget = (ROUNDS / 10_000) as i64;
    println!(
        "over {ROUNDS} callbacks: allocations {allocations}; mach syscalls {} unix syscalls {} \
         context switches {} (control loop of the same length: {} / {} / {}); budget {budget}",
        observed.0, observed.1, observed.2, control.0, control.1, control.2
    );
    assert_eq!(allocations, 0, "the audio path must not touch the allocator");
    assert!(
        observed.0 <= budget,
        "mach traps: {} over {ROUNDS} callbacks",
        observed.0
    );
    assert!(
        observed.1 <= budget,
        "unix syscalls: {} over {ROUNDS} callbacks",
        observed.1
    );
}

fn measure(work: impl FnOnce()) -> (i64, i64, i64) {
    let before = task_events();
    work();
    let after = task_events();
    (
        after.0 - before.0,
        after.1 - before.1,
        after.2 - before.2,
    )
}
