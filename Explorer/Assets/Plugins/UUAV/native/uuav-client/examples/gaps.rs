//! Audio-gap probe: plays a url for a fixed span while pulling audio at
//! real-time pace (like Unity's audio thread) and driving render events
//! (like Unity's render thread), then reports every silent gap it heard
//! and the pipeline's own gap counters. Chunk-interleaved files used to
//! produce one fixed-size gap per interleave chunk; a healthy run reports
//! zero gaps after the lead-in.
//!
//! Deploy `uuav-helper[.exe]` (plus the FFmpeg DLLs on Windows) next to the
//! example binary like for `smoke`, then:
//! `cargo run -p uuav-client --example gaps [media-url] [seconds]`

use std::ffi::CStr;
use std::os::raw::c_char;
use std::time::{Duration, Instant};
use uuav::{AudioOptionsRaw, AudioStats, ResultFFI, UUAVState};

const AUDIO: AudioOptionsRaw = AudioOptionsRaw {
    sample_rate: 48_000,
    channels: 2,
};

/// Silent spans shorter than this are ignored (decoder lead-in, loop seams).
const MIN_GAP: Duration = Duration::from_millis(50);

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
}

/// Stands in for Unity's probe: any live `id<MTLTexture>` works, `uuav_init`
/// only derives the device (and blit queue) from it.
#[cfg(target_os = "macos")]
fn with_probe<T>(init: impl FnOnce(*const std::ffi::c_void) -> T) -> T {
    use objc2_metal::{
        MTLCreateSystemDefaultDevice, MTLDevice as _, MTLPixelFormat, MTLTextureDescriptor,
    };

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
    init(objc2::rc::Retained::as_ptr(&probe).cast())
}

/// Stands in for Unity's probe: any live `ID3D11Texture2D*` works,
/// `uuav_init` only derives the device (and its adapter LUID) from it.
#[cfg(target_os = "windows")]
fn with_probe<T>(init: impl FnOnce(*const std::ffi::c_void) -> T) -> T {
    use windows::Win32::Foundation::HMODULE;
    use windows::Win32::Graphics::Direct3D::D3D_DRIVER_TYPE_HARDWARE;
    use windows::Win32::Graphics::Direct3D11::{
        D3D11_BIND_SHADER_RESOURCE, D3D11_CREATE_DEVICE_FLAG, D3D11_SDK_VERSION,
        D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, D3D11CreateDevice, ID3D11Device,
        ID3D11Texture2D,
    };
    use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_SAMPLE_DESC};
    use windows::core::Interface as _;

    let mut device: Option<ID3D11Device> = None;
    unsafe {
        D3D11CreateDevice(
            None,
            D3D_DRIVER_TYPE_HARDWARE,
            HMODULE::default(),
            D3D11_CREATE_DEVICE_FLAG(0),
            None,
            D3D11_SDK_VERSION,
            Some(&mut device),
            None,
            None,
        )
    }
    .expect("D3D11CreateDevice failed");
    let device = device.expect("D3D11CreateDevice returned no device");

    let desc = D3D11_TEXTURE2D_DESC {
        Width: 1,
        Height: 1,
        MipLevels: 1,
        ArraySize: 1,
        Format: DXGI_FORMAT_R8G8B8A8_UNORM,
        SampleDesc: DXGI_SAMPLE_DESC {
            Count: 1,
            Quality: 0,
        },
        Usage: D3D11_USAGE_DEFAULT,
        BindFlags: D3D11_BIND_SHADER_RESOURCE.0 as u32,
        CPUAccessFlags: 0,
        MiscFlags: 0,
    };
    let mut probe: Option<ID3D11Texture2D> = None;
    unsafe { device.CreateTexture2D(&desc, None, Some(&mut probe)) }
        .expect("probe CreateTexture2D failed");
    let probe = probe.expect("CreateTexture2D returned no texture");
    init(probe.as_raw().cast_const())
}

fn main() {
    let url = std::env::args().nth(1).unwrap_or_else(|| {
        "https://media.w3.org/2010/05/sintel/trailer.mp4".to_owned()
    });
    let seconds: u64 = std::env::args()
        .nth(2)
        .and_then(|s| s.parse().ok())
        .unwrap_or(25);

    let whitelist = c"https,http,tls,tcp,crypto,data,file";
    check(
        "uuav_init",
        with_probe(|probe| unsafe {
            uuav::uuav_init(
                probe,
                AUDIO,
                Some(on_error),
                Some(on_warning),
                Some(on_log),
                whitelist.as_ptr(),
                24, // AV_LOG_WARNING
            )
        }),
    );

    let created = uuav::uuav_player_new();
    assert!(
        created.error_message.is_null(),
        "uuav_player_new: {}",
        to_str(created.error_message)
    );
    let player = created.player_id;

    check("open_media", unsafe {
        let url = std::ffi::CString::new(url).unwrap();
        uuav::uuav_player_open_media_async(player, url.as_ptr())
    });
    check("play", uuav::uuav_player_play(player));

    let render = uuav::uuav_get_render_callback();
    let opened = Instant::now();
    while !matches!(uuav::uuav_player_state(player), UUAVState::UUAV_PLAYING) {
        assert!(
            opened.elapsed() < Duration::from_secs(20),
            "player never reached PLAYING"
        );
        std::thread::sleep(Duration::from_millis(50));
    }
    println!("playing; sampling audio for {seconds}s...");

    // pull the exact real-time sample deficit every ~10ms tick, like
    // Unity's audio thread; track silent spans after the first signal
    let mut buffer = vec![0.0f32; AUDIO.sample_rate as usize * AUDIO.channels as usize];
    let started = Instant::now();
    let mut pulled_frames: u64 = 0;
    let mut heard_first = false;
    let mut silent_since: Option<f64> = None;
    let mut gaps: Vec<(f64, f64)> = Vec::new();
    let mut silent_total = 0.0_f64;

    while started.elapsed() < Duration::from_secs(seconds) {
        render(player as i32);

        let due = (started.elapsed().as_secs_f64() * f64::from(AUDIO.sample_rate)) as u64;
        let deficit = (due - pulled_frames).min(AUDIO.sample_rate as u64) as i32;
        if deficit > 0 {
            let frames = unsafe {
                uuav::uuav_player_read_audio(player, buffer.as_mut_ptr(), deficit)
            };
            let span = frames.max(deficit); // unread deficit plays as silence
            let audible = buffer
                [..(frames.max(0) as usize) * AUDIO.channels as usize]
                .iter()
                .any(|s| s.abs() > 1e-4);
            let now = started.elapsed().as_secs_f64();
            if audible {
                heard_first = true;
                if let Some(from) = silent_since.take() {
                    let len = now - from;
                    silent_total += len;
                    if len >= MIN_GAP.as_secs_f64() {
                        gaps.push((from, len));
                    }
                }
            } else if heard_first && silent_since.is_none() {
                silent_since = Some(now);
            }
            pulled_frames += span as u64;
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    if let Some(from) = silent_since {
        let len = started.elapsed().as_secs_f64() - from;
        silent_total += len;
        if len >= MIN_GAP.as_secs_f64() {
            gaps.push((from, len));
        }
    }

    assert!(heard_first, "no audible samples arrived at all");

    println!("gaps >= {}ms after first signal: {}", MIN_GAP.as_millis(), gaps.len());
    for (at, len) in &gaps {
        println!("  at {at:7.3}s  silent {:6.0}ms", len * 1000.0);
    }
    println!("total silence while playing: {:.3}s", silent_total);

    let mut stats = AudioStats::default();
    let r = unsafe { uuav::uuav_player_audio_stats(player, &mut stats) };
    if r.error_message.is_null() {
        println!(
            "pipeline: underruns={} drift_dropped={} silence_pulls={} ring_stalls={}",
            stats.jitter_underruns,
            stats.core_drift_dropped_samples,
            stats.core_silence_pulls,
            stats.core_ring_stalls,
        );
    } else {
        unsafe { uuav::uuav_string_free(r.error_message.cast_mut()) };
    }

    uuav::uuav_player_free(player);
    uuav::uuav_deinit();

    // exit code communicates the verdict for scripted before/after runs
    std::process::exit(i32::from(!gaps.is_empty()));
}
