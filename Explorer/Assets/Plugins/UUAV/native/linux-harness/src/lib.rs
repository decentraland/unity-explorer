//! Linux-runnable tests over the platform-neutral pieces of the frozen core.
//!
//! The core crate only builds on Windows/macOS (its hardware paths), but
//! `ffutil.rs` and the software NV12 fallback are plain FFmpeg code. Mounting
//! those files here compiles them against the host's FFmpeg 8.1 - the same
//! release the core pins - so the open gates and the converter run for real
//! under `cargo test` on this box. See `run.sh` for the required environment.
#![allow(dead_code)]

#[path = "../../src/ffutil.rs"]
mod ffutil;

#[path = "../../src/sw_nv12.rs"]
mod sw_nv12;

#[cfg(test)]
mod tests {
    use super::ffutil::{AvDict, StreamingProtocol, check};
    use anyhow::{Context as _, Result};
    use ffmpeg_sys_next as ff;
    use std::ffi::{CStr, CString};
    use std::path::PathBuf;
    use std::ptr;

    fn fixture_url(name: &str) -> String {
        let path = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("fixtures")
            .join(name);
        format!("file://{}", path.display())
    }

    fn protocol(whitelist: &CStr) -> Result<StreamingProtocol> {
        // SAFETY: a valid NUL-terminated C string.
        unsafe { StreamingProtocol::new(whitelist.as_ptr()) }
    }

    /// Mirrors `src/playback/input.rs::Input::open` (minus the interrupt
    /// callback), then demands one video packet, so a stream whose key or
    /// segments cannot be opened fails regardless of which stage FFmpeg
    /// surfaces the error at.
    fn open_and_read_one_packet(url: &str, whitelist: &CStr) -> Result<()> {
        let url_c = CString::new(url)?;
        let protocol = protocol(whitelist)?;
        let mut opts = AvDict::open_options(&protocol);

        unsafe {
            let mut fmt = ff::avformat_alloc_context();
            anyhow::ensure!(!fmt.is_null(), "avformat_alloc_context failed");
            check(
                "avformat_open_input",
                ff::avformat_open_input(&mut fmt, url_c.as_ptr(), ptr::null(), opts.as_mut_ptr()),
            )?;
            let result = (|| -> Result<()> {
                opts.ensure_gates_applied()?;
                check(
                    "avformat_find_stream_info",
                    ff::avformat_find_stream_info(fmt, ptr::null_mut()),
                )?;

                let mut packet = ff::av_packet_alloc();
                anyhow::ensure!(!packet.is_null(), "av_packet_alloc failed");
                let read = ff::av_read_frame(fmt, packet);
                ff::av_packet_free(&mut packet);
                check("av_read_frame", read)?;
                Ok(())
            })();
            ff::avformat_close_input(&mut fmt);
            result
        }
    }

    /// The value `open_options` stages for a dict key, `None` when absent.
    fn staged_value(opts: &mut AvDict, key: &CStr) -> Option<String> {
        let entry =
            unsafe { ff::av_dict_get(*opts.as_mut_ptr(), key.as_ptr(), ptr::null(), 0) };
        if entry.is_null() {
            return None;
        }
        Some(
            unsafe { CStr::from_ptr((*entry).value) }
                .to_string_lossy()
                .into_owned(),
        )
    }

    /// AES-128 HLS with the near-universal `.key` key URI. Over `file:` the
    /// hls demuxer enforces `allowed_extensions` on every sub-open, and the
    /// stock list has no `key` entry, so this only plays when `open_options`
    /// widens the list.
    #[test]
    fn aes128_hls_with_dot_key_uri_plays() -> Result<()> {
        open_and_read_one_packet(&fixture_url("enc.m3u8"), c"file,crypto,data")
    }

    /// The widened list must actually reach the demuxer: staged before the
    /// open (with the `key` entry), consumed by the hls demuxer during it.
    #[test]
    fn allowed_extensions_staged_and_consumed_by_hls() -> Result<()> {
        let protocol = protocol(c"file,crypto,data")?;
        let mut opts = AvDict::open_options(&protocol);
        let staged = staged_value(&mut opts, c"allowed_extensions")
            .context("open_options stages no allowed_extensions")?;
        anyhow::ensure!(
            staged.split(',').any(|extension| extension == "key"),
            "allowed_extensions has no 'key' entry: {staged}"
        );

        let url_c = CString::new(fixture_url("enc.m3u8"))?;
        unsafe {
            let mut fmt = ff::avformat_alloc_context();
            anyhow::ensure!(!fmt.is_null(), "avformat_alloc_context failed");
            check(
                "avformat_open_input",
                ff::avformat_open_input(&mut fmt, url_c.as_ptr(), ptr::null(), opts.as_mut_ptr()),
            )?;
            let leftover = staged_value(&mut opts, c"allowed_extensions");
            ff::avformat_close_input(&mut fmt);
            anyhow::ensure!(
                leftover.is_none(),
                "hls left allowed_extensions unconsumed; was the option renamed?"
            );
        }
        Ok(())
    }

    /// The staged `allowed_extensions` reaches whichever demuxer matched, not
    /// just hls: dashdec.c has its own option with its own default
    /// (`aac,m4a,m4s,m4v,mov,mp4,webm,ts`, dashdec.c:2376) and blocks any
    /// `file:` segment open whose extension is off the staged list. A value
    /// that only covers hls therefore breaks local DASH with .webm segments -
    /// this only plays when the staged list is the union of both defaults.
    #[test]
    fn local_dash_with_webm_segment_plays() -> Result<()> {
        open_and_read_one_packet(&fixture_url("tiny.mpd"), c"file")
    }

    /// `allowed_extensions` is hls-private: every non-HLS open leaves it in
    /// the dict, so `ensure_gates_applied` must not treat it as a gate key -
    /// this open fails if it ever does.
    #[test]
    fn plain_mp4_still_passes_the_gate_check() -> Result<()> {
        open_and_read_one_packet(&fixture_url("tiny.mp4"), c"file")
    }

    /// Whitelist enforcement still holds with the widened options: `file` off
    /// the list must fail before the filesystem (mirrors the core's own
    /// macOS/Windows-run test in input.rs).
    #[test]
    fn file_denied_without_whitelist_entry() {
        let denied = open_and_read_one_packet(&fixture_url("tiny.mp4"), c"https,tls,tcp");
        let message = format!("{:?}", denied.expect_err("file: must be blocked"));
        assert!(
            !message.to_lowercase().contains("no such file"),
            "file: reached the filesystem: {message}"
        );
    }

    // ---- the Windows software-decode fallback's NV12 converter ----

    use super::ffutil::OwnedFrame;
    use super::sw_nv12::Nv12Converter;
    use std::os::raw::c_int;

    /// A YUV420P frame with every plane (padding included) at one value.
    fn uniform_yuv420p(width: c_int, height: c_int, y: u8, u: u8, v: u8) -> OwnedFrame {
        let mut frame = OwnedFrame::new().expect("av_frame_alloc");
        unsafe {
            let raw = frame.as_mut_ptr();
            (*raw).width = width;
            (*raw).height = height;
            (*raw).format = ff::AVPixelFormat::AV_PIX_FMT_YUV420P as c_int;
            assert_eq!(ff::av_frame_get_buffer(raw, 0), 0, "av_frame_get_buffer");
            for (plane, value) in [(0usize, y), (1, u), (2, v)] {
                let rows = if plane == 0 { height } else { (height + 1) / 2 } as usize;
                let stride = (*raw).linesize[plane] as usize;
                let base = (*raw).data[plane];
                for row in 0..rows {
                    ptr::write_bytes(base.add(row * stride), value, stride);
                }
            }
        }
        frame
    }

    fn assert_uniform_nv12(bytes: &[u8], width: usize, height: usize, y: u8, u: u8, v: u8) {
        let luma = width * height;
        assert_eq!(bytes.len(), luma + luma / 2, "NV12 byte count");
        for (index, &sample) in bytes[..luma].iter().enumerate() {
            assert!(
                (i16::from(sample) - i16::from(y)).abs() <= 2,
                "luma sample {index} is {sample}, expected ~{y}"
            );
        }
        for (index, pair) in bytes[luma..].chunks(2).enumerate() {
            assert!(
                (i16::from(pair[0]) - i16::from(u)).abs() <= 2,
                "U sample {index} is {}, expected ~{u}",
                pair[0]
            );
            assert!(
                (i16::from(pair[1]) - i16::from(v)).abs() <= 2,
                "V sample {index} is {}, expected ~{v}",
                pair[1]
            );
        }
    }

    /// Odd frame dimensions land as the even-masked size the D3D11 presenter
    /// needs, with the planes in the documented UpdateSubresource layout.
    #[test]
    fn converter_masks_odd_frames_to_even_nv12() {
        let frame = uniform_yuv420p(65, 33, 120, 90, 200);
        let mut converter = Nv12Converter::new();
        let buffer = converter.convert(&frame).expect("convert 65x33");
        assert_eq!((buffer.width(), buffer.height()), (64, 32));
        assert_eq!(buffer.stride(), 64);
        assert_uniform_nv12(buffer.bytes(), 64, 32, 120, 90, 200);
    }

    /// A resolution change mid-stream recreates the cached context.
    #[test]
    fn converter_survives_a_resolution_change() {
        let mut converter = Nv12Converter::new();
        let first = converter
            .convert(&uniform_yuv420p(64, 48, 50, 60, 70))
            .expect("convert 64x48");
        assert_eq!((first.width(), first.height()), (64, 48));
        let second = converter
            .convert(&uniform_yuv420p(32, 32, 200, 110, 130))
            .expect("convert 32x32");
        assert_eq!((second.width(), second.height()), (32, 32));
        assert_uniform_nv12(second.bytes(), 32, 32, 200, 110, 130);
    }

    /// The software fallback flow end-to-end minus D3D11: open the fixture
    /// through the gated options, build the decoder the way
    /// `VideoDecoder::new` does without a hardware context, decode one real
    /// h264 frame, convert it. What cannot run here is only the D3D11
    /// preference/upload half.
    #[test]
    fn software_decode_then_convert_flows() -> Result<()> {
        use super::ffutil::{Decoded, OwnedDecoder, apply_decode_limits};

        let url_c = CString::new(fixture_url("tiny.mp4"))?;
        let protocol = protocol(c"file")?;
        let mut opts = AvDict::open_options(&protocol);

        unsafe {
            let mut fmt = ff::avformat_alloc_context();
            anyhow::ensure!(!fmt.is_null(), "avformat_alloc_context failed");
            check(
                "avformat_open_input",
                ff::avformat_open_input(&mut fmt, url_c.as_ptr(), ptr::null(), opts.as_mut_ptr()),
            )?;
            let result = (|| -> Result<()> {
                opts.ensure_gates_applied()?;
                check(
                    "avformat_find_stream_info",
                    ff::avformat_find_stream_info(fmt, ptr::null_mut()),
                )?;
                let index = check(
                    "av_find_best_stream",
                    ff::av_find_best_stream(
                        fmt,
                        ff::AVMediaType::AVMEDIA_TYPE_VIDEO,
                        -1,
                        -1,
                        ptr::null_mut(),
                        0,
                    ),
                )?;
                let stream = super::ffutil::Stream::from_raw(*(*fmt).streams.offset(index as isize));

                let codec = stream.find_decoder()?;
                let mut ctx = OwnedDecoder::new(codec)?;
                let raw = ctx.as_mut_ptr();
                check(
                    "avcodec_parameters_to_context",
                    ff::avcodec_parameters_to_context(raw, stream.codecpar()),
                )?;
                apply_decode_limits(raw);
                check("avcodec_open2", ff::avcodec_open2(raw, codec, ptr::null_mut()))?;

                let mut packet = ff::av_packet_alloc();
                anyhow::ensure!(!packet.is_null(), "av_packet_alloc failed");
                let mut converter = Nv12Converter::new();
                let decoded = loop {
                    let read = ff::av_read_frame(fmt, packet);
                    if read < 0 {
                        check("avcodec_send_packet(drain)", {
                            ff::avcodec_send_packet(raw, ptr::null())
                        })?;
                    } else {
                        if (*packet).stream_index != index {
                            ff::av_packet_unref(packet);
                            continue;
                        }
                        check("avcodec_send_packet", ff::avcodec_send_packet(raw, packet))?;
                        ff::av_packet_unref(packet);
                    }

                    let mut frame = OwnedFrame::new()?;
                    let ret = ff::avcodec_receive_frame(raw, frame.as_mut_ptr());
                    if ret == super::ffutil::AVERROR_EAGAIN {
                        continue;
                    }
                    check("avcodec_receive_frame", ret)?;
                    break Decoded::Frame(converter.convert(&frame)?);
                };
                ff::av_packet_free(&mut packet);

                let Decoded::Frame(buffer) = decoded else {
                    anyhow::bail!("no frame decoded");
                };
                anyhow::ensure!(
                    (buffer.width(), buffer.height()) == (64, 64),
                    "unexpected converted size {}x{}",
                    buffer.width(),
                    buffer.height()
                );
                Ok(())
            })();
            ff::avformat_close_input(&mut fmt);
            result
        }
    }

    /// A 1x1 frame masks to nothing and must fail cleanly, not divide or
    /// allocate its way into nonsense.
    #[test]
    fn converter_rejects_unpresentable_sizes() {
        let frame = uniform_yuv420p(1, 1, 0, 0, 0);
        let error = match Nv12Converter::new().convert(&frame) {
            Ok(_) => panic!("1x1 must not convert"),
            Err(error) => error,
        };
        let message = format!("{error:?}");
        assert!(
            message.contains("no presentable size"),
            "unexpected error: {message}"
        );
    }
}
