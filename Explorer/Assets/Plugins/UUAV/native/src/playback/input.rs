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

    pub(super) fn find_best_stream(&self, media_type: ff::AVMediaType) -> c_int {
        unsafe { ff::av_find_best_stream(self.fmt, media_type, -1, -1, ptr::null_mut(), 0) }
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
}
