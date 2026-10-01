use anyhow::{Result, anyhow};
use ffmpeg_sys_next as ff;
use std::ffi::CString;
use std::os::raw::{c_int, c_void};
use std::ptr;
use std::sync::atomic::{AtomicBool, Ordering};

use super::util::ReadOnlyCancelToken;
use crate::ffutil::{AvDict, Stream, StreamingProtocol, check};

/// Aborts blocking demuxer I/O when the playback is being closed.
unsafe extern "C" fn interrupt_cb(opaque: *mut c_void) -> c_int {
    if opaque.is_null() {
        return 0;
    }
    let cancelled = unsafe { &*opaque.cast::<AtomicBool>() };
    c_int::from(cancelled.load(Ordering::Relaxed))
}

/// Owning wrapper around the demuxer of one opened url.
pub(super) struct Input {
    fmt: *mut ff::AVFormatContext,
    /// Never read: keeps the flag the demuxer's interrupt callback points
    /// at alive for as long as the context holds the raw pointer.
    _cancel: ReadOnlyCancelToken,
}

impl Input {
    /// Opens the media and probes its streams. Demuxer I/O is aborted
    /// through `cancel` (see [`interrupt_cb`]); `protocol_whitelist` bounds the
    /// protocols the demuxer and any nested (HLS segment) contexts may use.
    pub(super) fn open(
        cancel_token: ReadOnlyCancelToken,
        url: &str,
        protocol_whitelist: &StreamingProtocol,
    ) -> Result<Self> {
        let url_c = CString::new(url).map_err(|_| anyhow!("url contains a NUL byte"))?;
        let mut opts = AvDict::from(protocol_whitelist);

        // The HLS demuxer's segment-extension heuristic (extension_picky,
        // default on since FFmpeg 7.1) rejects every segment whose URL has no
        // path extension - the normal shape for tokenized CDN media, YouTube's
        // googlevideo /videoplayback URLs included. Deliberately unscoped:
        // extensionless segments appear in playlists this layer cannot tell
        // apart from scene input (YouTube's native HLS manifests among them),
        // and the heuristic hardens an in-process parser - this one already
        // runs sandboxed on the assumption it will be compromised.
        opts.set(c"extension_picky", c"0");

        let input = unsafe {
            let mut fmt = ff::avformat_alloc_context();
            if fmt.is_null() {
                return Err(anyhow!("avformat_alloc_context failed"));
            }
            (*fmt).interrupt_callback = ff::AVIOInterruptCB {
                callback: Some(interrupt_cb),
                opaque: cancel_token.as_flag_ptr().cast_mut().cast::<c_void>(),
            };

            // avformat_open_input frees the context on failure
            check(
                "avformat_open_input",
                ff::avformat_open_input(&mut fmt, url_c.as_ptr(), ptr::null(), opts.as_mut_ptr()),
            )?;

            Self {
                fmt,
                _cancel: cancel_token,
            }
        };

        check("avformat_find_stream_info", unsafe {
            ff::avformat_find_stream_info(input.fmt, ptr::null_mut())
        })?;
        Ok(input)
    }

    pub(super) const fn as_ptr(&self) -> *mut ff::AVFormatContext {
        self.fmt
    }

    pub(super) fn nb_streams(&self) -> c_int {
        c_int::try_from(unsafe { (*self.fmt).nb_streams }).unwrap_or(c_int::MAX)
    }

    /// Index of the best stream of `media_type`, negative when there is
    /// none. With `related >= 0` the search is confined to the program of
    /// that stream (an HLS variant); a media without programs searches all
    /// streams regardless.
    pub(super) fn find_best_stream(&self, media_type: ff::AVMediaType, related: c_int) -> c_int {
        unsafe { ff::av_find_best_stream(self.fmt, media_type, -1, related, ptr::null_mut(), 0) }
    }

    /// SAFETY: the streams belong to the context, which outlives the view
    pub(super) fn stream_at(&self, index: c_int) -> Stream {
        unsafe { Stream::from_raw(*(*self.fmt).streams.offset(index as isize)) }
    }

    /// Timestamp of the first frame in seconds; the media timeline is
    /// normalized so playback starts at 0.
    pub(super) fn start_offset(&self) -> f64 {
        let start_time = unsafe { (*self.fmt).start_time };
        if start_time == ff::AV_NOPTS_VALUE {
            0.0
        } else {
            start_time as f64 / f64::from(ff::AV_TIME_BASE)
        }
    }

    /// Total duration in seconds, `None` for realtime streams.
    pub(super) fn duration(&self) -> Option<f64> {
        let duration = unsafe { (*self.fmt).duration };
        if duration == ff::AV_NOPTS_VALUE || duration <= 0 {
            None
        } else {
            Some(duration as f64 / f64::from(ff::AV_TIME_BASE))
        }
    }
}

impl Drop for Input {
    fn drop(&mut self) {
        unsafe { ff::avformat_close_input(&mut self.fmt) };
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::playback::util::CancelToken;

    fn cancel() -> ReadOnlyCancelToken {
        CancelToken::new().into()
    }

    // A path that does not exist, so `file:` never reads anything real: the
    // point is only whether FFmpeg is *allowed* to reach the filesystem.
    const FILE_URL: &str = "file:///nonexistent/uuav-protocol-whitelist-test.mp4";

    // `Input` is not `Debug`
    fn open_error(whitelist: &str) -> Result<String> {
        let whitelist = CString::new(whitelist)?;
        // SAFETY: `whitelist` is a valid NUL-terminated C string.
        let protocol = unsafe { StreamingProtocol::new(whitelist.as_ptr()) }?;
        match Input::open(cancel(), FILE_URL, &protocol) {
            Ok(_) => Err(anyhow!("opening a nonexistent file must fail")),
            Err(e) => Ok(e.to_string().to_lowercase()),
        }
    }

    #[test]
    fn file_allowed_when_whitelisted_reaches_filesystem() -> Result<()> {
        // Protocol allowed → the failure is the missing file, meaning FFmpeg
        // got past the whitelist gate and hit the filesystem.
        let msg = open_error("file")?;
        assert!(
            msg.contains("no such file"),
            "expected a filesystem error, got: {msg}"
        );
        Ok(())
    }

    #[test]
    fn file_denied_before_touching_filesystem() -> Result<()> {
        // Blocked at the gate → FFmpeg never reached the filesystem, so this
        // is NOT a "no such file" error.
        let msg = open_error("https,tls,tcp")?;
        assert!(
            !msg.contains("no such file"),
            "file: must be blocked before the filesystem, got: {msg}"
        );
        Ok(())
    }

    /// A format context with two programs (HLS variants), each carrying one
    /// video and one audio stream. Nothing is opened: the layout is built
    /// through the public muxing-side API, so no I/O is involved.
    ///
    /// Per program: (video bitrate, audio bitrate).
    fn two_programs(input_bitrates: [(i64, i64); 2]) -> Result<Input> {
        let fmt = unsafe { ff::avformat_alloc_context() };
        anyhow::ensure!(!fmt.is_null(), "avformat_alloc_context failed");
        let input = Input {
            fmt,
            _cancel: cancel(),
        };

        for (program_id, (video_bitrate, audio_bitrate)) in (0..).zip(input_bitrates) {
            let program = unsafe { ff::av_new_program(fmt, program_id) };
            anyhow::ensure!(!program.is_null(), "av_new_program failed");

            for (codec_type, bitrate) in [
                (ff::AVMediaType::AVMEDIA_TYPE_VIDEO, video_bitrate),
                (ff::AVMediaType::AVMEDIA_TYPE_AUDIO, audio_bitrate),
            ] {
                let stream = unsafe { ff::avformat_new_stream(fmt, ptr::null()) };
                anyhow::ensure!(!stream.is_null(), "avformat_new_stream failed");
                unsafe {
                    let par = (*stream).codecpar;
                    (*par).codec_type = codec_type;
                    (*par).bit_rate = bitrate;
                    // av_find_best_stream skips audio without a layout
                    (*par).ch_layout.nb_channels = 2;
                    (*par).sample_rate = 48_000;
                    ff::av_program_add_stream_index(fmt, program_id, (*stream).index as u32);
                }
            }
        }
        Ok(input)
    }

    #[test]
    fn related_search_stays_in_the_video_stream_program() -> Result<()> {
        // program 0: best video, weaker audio; program 1: weaker video,
        // best audio. Streams are laid out as [v0, a1, v2, a3].
        let input = two_programs([(2_000_000, 64_000), (1_000_000, 128_000)])?;

        let video = input.find_best_stream(ff::AVMediaType::AVMEDIA_TYPE_VIDEO, -1);
        assert_eq!(video, 0, "highest-bitrate video");

        let unrelated = input.find_best_stream(ff::AVMediaType::AVMEDIA_TYPE_AUDIO, -1);
        assert_eq!(unrelated, 3, "an unrelated search crosses programs");

        let related = input.find_best_stream(ff::AVMediaType::AVMEDIA_TYPE_AUDIO, video);
        assert_eq!(related, 1, "a related search stays in the video's program");
        assert_eq!(input.nb_streams(), 4);
        Ok(())
    }

    #[test]
    fn hls_demuxer_is_linked() {
        // The hls demuxer is a build-configuration dependency with no other
        // assertion — losing it would only surface as silent playback failures.
        assert!(!unsafe { ff::av_find_input_format(c"hls".as_ptr()) }.is_null());
    }
}
