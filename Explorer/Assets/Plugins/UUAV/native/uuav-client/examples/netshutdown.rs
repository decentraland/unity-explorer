//! Minimal clean-shutdown repro: init, open a network stream, reach
//! PLAYING, then `uuav_deinit` — no helper-kill, no recovery. Isolates
//! the teardown path so a memory tool (valgrind) can run the whole life
//! cycle patiently. `UUAV_TIMEOUT_SCALE` stretches the PLAYING wait for
//! the tool's slowdown, matching the client's handshake scaling.
//!
//! Linux only (the headless Vulkan probe); build the workspace, copy
//! `uuav-helper` next to the example, and
//! `cargo run -p uuav-client --example netshutdown <http-url>`.

use std::ffi::CStr;
use std::os::raw::{c_char, c_void};
use std::time::{Duration, Instant};
use uuav::{AudioOptionsRaw, UUAVState};

const AUDIO: AudioOptionsRaw = AudioOptionsRaw {
    sample_rate: 48_000,
    channels: 2,
};

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

#[cfg(target_os = "linux")]
fn with_probe<T>(init: impl FnOnce(*const c_void) -> T) -> T {
    uuav::test_install_headless_device().expect("headless Vulkan device");
    init(std::ptr::NonNull::<u8>::dangling().as_ptr().cast_const().cast())
}

fn main() {
    let url = std::env::args()
        .nth(1)
        .unwrap_or_else(|| "http://127.0.0.1:8796/tone_color_bands.mp4".to_owned());
    let scale: u32 = std::env::var("UUAV_TIMEOUT_SCALE")
        .ok()
        .and_then(|v| v.parse().ok())
        .unwrap_or(1);

    let whitelist = c"https,http,tls,tcp,crypto,data,file";
    let init = with_probe(|probe| unsafe {
        uuav::uuav_init(probe, AUDIO, Some(on_error), Some(on_warning), Some(on_log), whitelist.as_ptr(), 24)
    });
    assert!(init.error_message.is_null(), "uuav_init: {}", to_str(init.error_message));

    let created = uuav::uuav_player_new();
    assert!(created.error_message.is_null(), "player_new: {}", to_str(created.error_message));
    let player = created.player_id;

    let open = unsafe {
        let url = std::ffi::CString::new(url).unwrap();
        uuav::uuav_player_open_media_async(player, url.as_ptr())
    };
    assert!(open.error_message.is_null(), "open: {}", to_str(open.error_message));
    let play = uuav::uuav_player_play(player);
    assert!(play.error_message.is_null(), "play: {}", to_str(play.error_message));

    let started = Instant::now();
    let mut playing = false;
    while started.elapsed() < Duration::from_secs(20) * scale {
        if matches!(uuav::uuav_player_state(player), UUAVState::UUAV_PLAYING) {
            playing = true;
            println!("reached PLAYING");
            break;
        }
        std::thread::sleep(Duration::from_millis(100));
    }
    assert!(playing, "player never reached PLAYING");

    // drain audio for a while (matches the gaps harness, which crashed
    // where the 2s no-audio path does not), then tear down cleanly
    let secs: u64 = std::env::var("UUAV_PLAY_SECS").ok().and_then(|v| v.parse().ok()).unwrap_or(25);
    let mut buf = vec![0f32; 2048];
    let until = Instant::now() + Duration::from_secs(secs);
    while Instant::now() < until {
        unsafe {
            uuav::uuav_player_read_audio(player, buf.as_mut_ptr(), 1024);
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    println!("played {secs}s, tearing down");
    uuav::uuav_player_free(player);
    uuav::uuav_deinit();
    println!("clean deinit: ok");
}
