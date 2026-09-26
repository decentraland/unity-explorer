//! Headless audio-pacing soak: consumes audio exactly like Unity's DSP
//! thread (1024-frame blocks at the block rate, optionally skewed) and
//! prints the per-second audio-path counters. A healthy pipeline shows
//! underruns/wm-drop/drift-drop flat after priming with the jitter fill
//! hovering near the helper's 80 ms target.
//!
//! Run like the smoke example (helper + FFmpeg DLLs next to the binary):
//! `cargo run -p uuav-client --example soak [media-url] [skew]`
//! where `skew` scales the consumption rate (1.005 = consume 0.5% fast).

use std::ffi::CStr;
use std::os::raw::c_char;
use std::time::{Duration, Instant};
use uuav::{AudioOptionsRaw, UUAVState};

const AUDIO: AudioOptionsRaw = AudioOptionsRaw {
    sample_rate: 48_000,
    channels: 2,
};

const DSP_BLOCK_FRAMES: usize = 1024;
const SOAK: Duration = Duration::from_secs(25);

extern "C" fn on_error(line: *const c_char) {
    eprintln!("[error] {}", to_str(line));
}

extern "C" fn on_warning(line: *const c_char) {
    eprintln!("[warn ] {}", to_str(line));
}

extern "C" fn on_log(_line: *const c_char) {}

fn to_str(line: *const c_char) -> String {
    if line.is_null() {
        return String::new();
    }
    unsafe { CStr::from_ptr(line) }.to_string_lossy().into_owned()
}

fn check(step: &str, result: uuav::ResultFFI) {
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

#[allow(clippy::too_many_lines)]
fn main() {
    let url = std::env::args().nth(1).unwrap_or_else(|| {
        "https://media.w3.org/2010/05/sintel/trailer.mp4".to_owned()
    });
    let skew: f64 = std::env::args()
        .nth(2)
        .and_then(|s| s.parse().ok())
        .unwrap_or(1.0);

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
                24,
            )
        }),
    );

    let created = uuav::uuav_player_new();
    assert!(created.error_message.is_null());
    let player = created.player_id;

    check("open_media", unsafe {
        let url = std::ffi::CString::new(url).unwrap();
        uuav::uuav_player_open_media_async(player, url.as_ptr())
    });
    check("set_looping", uuav::uuav_player_set_looping(player, 1));
    check("play", uuav::uuav_player_play(player));

    // wait for PLAYING
    let started = Instant::now();
    while !matches!(uuav::uuav_player_state(player), UUAVState::UUAV_PLAYING) {
        assert!(started.elapsed() < Duration::from_secs(20), "never reached PLAYING");
        std::thread::sleep(Duration::from_millis(50));
    }
    println!("PLAYING; consuming {DSP_BLOCK_FRAMES}-frame blocks, skew x{skew}");

    let mut dst = vec![0.0_f32; DSP_BLOCK_FRAMES * AUDIO.channels as usize];
    let block = Duration::from_secs_f64(
        DSP_BLOCK_FRAMES as f64 / (f64::from(AUDIO.sample_rate) * skew),
    );

    let soak_start = Instant::now();
    let mut next_block = Instant::now();
    let mut next_report = Instant::now() + Duration::from_secs(1);
    let mut requested: u64 = 0;
    let mut returned: u64 = 0;

    while soak_start.elapsed() < SOAK {
        let now = Instant::now();
        if now < next_block {
            std::thread::sleep(next_block - now);
        }
        next_block += block;

        let read = unsafe {
            uuav::uuav_player_read_audio(player, dst.as_mut_ptr(), DSP_BLOCK_FRAMES as i32)
        };
        requested += DSP_BLOCK_FRAMES as u64;
        returned += read.max(0) as u64;

        if Instant::now() >= next_report {
            next_report += Duration::from_secs(1);
            let mut stats = uuav::AudioStats::default();
            let r = unsafe { uuav::uuav_player_audio_stats(player, &mut stats) };
            if !r.error_message.is_null() {
                unsafe { uuav::uuav_string_free(r.error_message.cast_mut()) };
                continue;
            }
            let mut media_time = 0.0_f64;
            let t = unsafe { uuav::uuav_player_current_time(player, &mut media_time) };
            if !t.error_message.is_null() {
                unsafe { uuav::uuav_string_free(t.error_message.cast_mut()) };
            }
            let per_second = f64::from(AUDIO.sample_rate) * f64::from(AUDIO.channels);
            println!(
                "t={:>5.1}s media={:>6.2}s | jitter fill {:>4.0}ms underruns {:>3} wm-drop {:>5.0}ms primed:{} | core ring {:>4.0}ms drift-drop {:>6.0}ms silence {:>4} stalls {:>5} | dsp ret {:>3.0}%",
                soak_start.elapsed().as_secs_f64(),
                media_time,
                stats.jitter_fill_samples as f64 * 1000.0 / per_second,
                stats.jitter_underruns,
                stats.jitter_watermark_dropped as f64 * 1000.0 / per_second,
                u8::from(stats.jitter_primed == 1),
                stats.core_ring_fill_samples as f64 * 1000.0 / per_second,
                stats.core_drift_dropped_samples as f64 * 1000.0 / per_second,
                stats.core_silence_pulls,
                stats.core_ring_stalls,
                if requested == 0 { 0.0 } else { returned as f64 * 100.0 / requested as f64 },
            );
        }
    }

    let mut stats = uuav::AudioStats::default();
    let r = unsafe { uuav::uuav_player_audio_stats(player, &mut stats) };
    assert!(r.error_message.is_null());
    println!(
        "final: underruns {} wm-drop {} drift-drop {} returned {:.1}%",
        stats.jitter_underruns,
        stats.jitter_watermark_dropped,
        stats.core_drift_dropped_samples,
        returned as f64 * 100.0 / requested as f64,
    );

    uuav::uuav_player_free(player);
    uuav::uuav_deinit();
}
