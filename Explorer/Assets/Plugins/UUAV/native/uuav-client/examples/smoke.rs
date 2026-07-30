//! Headless end-to-end smoke of the out-of-process pipeline, no Unity
//! involved: creates a real Metal probe texture (standing in for Unity's),
//! walks the C ABI exactly like `UUAVRuntime`/`UUAVPlayer` do, and expects
//! the helper process to take a stream to PLAYING.
//!
//! Run: `cargo build --workspace && cp .target/debug/uuav-helper .target/debug/examples/`
//! then `cargo run -p uuav-client --example smoke [media-url]`.

#![cfg(target_os = "macos")]

use objc2_metal::{MTLCreateSystemDefaultDevice, MTLDevice as _, MTLPixelFormat, MTLTextureDescriptor};
use std::ffi::CStr;
use std::os::raw::c_char;
use std::time::{Duration, Instant};
use uuav::{AudioOptionsRaw, ResultFFI, UUAVState};

extern "C" fn on_error(line: *const c_char) {
    eprintln!("[error] {}", to_str(line));
}

extern "C" fn on_warning(line: *const c_char) {
    eprintln!("[warn ] {}", to_str(line));
}

extern "C" fn on_log(line: *const c_char) {
    eprintln!("[log  ] {}", to_str(line));
}

fn to_str(line: *const c_char) -> String {
    if line.is_null() {
        return String::new();
    }
    unsafe { CStr::from_ptr(line) }.to_string_lossy().into_owned()
}

fn check(step: &str, result: ResultFFI) {
    if !result.error_message.is_null() {
        let message = to_str(result.error_message);
        unsafe { uuav::uuav_string_free(result.error_message.cast_mut()) };
        panic!("{step}: {message}");
    }
    println!("{step}: ok");
}

fn main() {
    let url = std::env::args().nth(1).unwrap_or_else(|| {
        "https://media.w3.org/2010/05/sintel/trailer.mp4".to_owned()
    });

    // stands in for Unity's probe: any live id<MTLTexture> works
    let device = MTLCreateSystemDefaultDevice().expect("no Metal device");
    let descriptor = unsafe {
        MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
            MTLPixelFormat::RGBA8Unorm,
            1,
            1,
            false,
        )
    };
    let probe = device
        .newTextureWithDescriptor(&descriptor)
        .expect("no probe texture");

    let whitelist = c"https,http,tls,tcp,crypto,data,file";
    check("uuav_init", unsafe {
        uuav::uuav_init(
            objc2::rc::Retained::as_ptr(&probe).cast(),
            AudioOptionsRaw {
                sample_rate: 48_000,
                channels: 2,
            },
            Some(on_error),
            Some(on_warning),
            Some(on_log),
            whitelist.as_ptr(),
            24, // AV_LOG_WARNING
        )
    });

    let created = uuav::uuav_player_new();
    assert!(
        created.error_message.is_null(),
        "uuav_player_new: {}",
        to_str(created.error_message)
    );
    let player = created.player_id;
    println!("uuav_player_new: id {player}");

    check("open_media", unsafe {
        let url = std::ffi::CString::new(url).unwrap();
        uuav::uuav_player_open_media_async(player, url.as_ptr())
    });
    check("play", uuav::uuav_player_play(player));

    let started = Instant::now();
    let mut last_state = UUAVState::UUAV_UNKNOWN as i32;
    let mut reached_playing = false;
    while started.elapsed() < Duration::from_secs(20) {
        let state = uuav::uuav_player_state(player);
        if state as i32 != last_state {
            last_state = state as i32;
            let mut time = 0.0_f64;
            let has_time =
                unsafe { uuav::uuav_player_current_time(player, &mut time) }.error_message;
            if !has_time.is_null() {
                unsafe { uuav::uuav_string_free(has_time.cast_mut()) };
            }
            println!("state -> {state:?} (t = {time:.2}s)");
        }
        if matches!(state, UUAVState::UUAV_PLAYING) {
            reached_playing = true;
            let mut t0 = 0.0_f64;
            let mut t1 = 0.0_f64;
            let r0 = unsafe { uuav::uuav_player_current_time(player, &mut t0) };
            std::thread::sleep(Duration::from_millis(500));
            let r1 = unsafe { uuav::uuav_player_current_time(player, &mut t1) };
            if r0.error_message.is_null() && r1.error_message.is_null() {
                println!("time advanced {t0:.3}s -> {t1:.3}s over 500ms");
                assert!(t1 > t0, "media time did not advance");
                break;
            }
        }
        std::thread::sleep(Duration::from_millis(100));
    }
    assert!(reached_playing, "player never reached PLAYING");

    let status = uuav::uuav_status();
    println!(
        "status: initialized={} players={}",
        status.initialized, status.players_count
    );

    uuav::uuav_player_free(player);
    uuav::uuav_deinit();
    println!("deinit: ok (helper should be gone)");
}
