use anyhow::{Result, anyhow};
use ffmpeg_sys_next as ff;
use parking_lot::Mutex;
use std::sync::Once;
use std::thread;

use super::audio_playback::{AudioPlayback, AudioReader};
use super::control::ControlConsume;
use super::input::Input;
use super::transport::{AtomicTransport, PlaybackState};
use super::util::{AtomicSeekSlot, PLAYBACK_POLL, ReadOnlyCancelToken};
use super::video_playback::{VideoPlayback, VideoQueue, VideoReader};
use crate::ffutil::{OwnedPacket, Stream, StreamingProtocol, av_err, check, copy_c_name, q2d};
use crate::hw_device::{HwDevice, HwDeviceContext};
use crate::video_output::{VideoOutput, VideoTextureView};
use crate::{AudioOptionsView, ErrorCallback, MediaInfo, UUAVState, VideoSize};
use std::mem;
use std::os::raw::c_int;

static NETWORK_INIT: Once = Once::new();

/// A video frame is presented once the clock is within this of its pts.
const VIDEO_PRESENT_BIAS: f64 = 0.005;

pub const DEFAULT_PLAYBACK_RATE: f64 = 1.0;

/// The demux/decode half of a playback: everything [`PlaybackUnit::run_blocking`]
/// needs mutable and exclusive on the playback thread.
pub(crate) struct Pipeline {
    input: Input,
    video: Option<VideoPlayback>,
    audio: Option<AudioPlayback>,
    /// Timestamp of the first frame; the media timeline is normalized so
    /// playback starts at 0.
    start_offset: f64,
}

pub(crate) struct UnitControls {
    /// `true` = a play queued while no unit was live, `false` = a pause
    pub(crate) play_or_pause: ControlConsume<bool>,
    /// Wrap at EOF on the playback thread instead of settling into ENDED.
    pub(crate) looping: ControlConsume<bool>,
    /// Desired playback rate, in media seconds per wall second.
    pub(crate) rate: ControlConsume<f64>,
}

/// Playback of a single url: the shared state the engine-facing threads
/// (control, render, audio) and the playback thread work against.
///
/// A unit that exists is fully open — [`PlaybackUnit::open`] builds it
/// from an already-opened media, so every field here is plain data.
pub(crate) struct PlaybackUnit {
    url: String,
    cancel: ReadOnlyCancelToken,
    /// Coalescing seek command; the playback thread services it.
    seek: AtomicSeekSlot,
    controls: UnitControls,
    /// Playback state and media clock, as one atomic snapshot.
    transport: AtomicTransport,
    /// Audio-thread half of the playback's audio; the pipeline's
    /// [`AudioPlayback`] feeds it.
    audio: AudioReader,
    /// Render-thread half of the presentation queue; the pipeline's
    /// [`VideoPlayback`] feeds it.
    video: VideoReader,
    /// Windows-Only D3D11VA device context frames decode through; `None` for
    /// audio-only media.
    #[cfg(target_os = "windows")]
    hw_ctx: Option<HwDeviceContext>,
    /// Total duration in seconds, `None` for realtime streams.
    duration: Option<f64>,
    video_size: Option<VideoSize>,
    /// Source stream parameters, captured while probing in [`Self::open`].
    media_info: MediaInfo,
    /// Presentation target; created lazily on the render thread.
    output: Mutex<Option<VideoOutput>>,
    /// Engine device the presentation output is created on.
    device: HwDevice,
    error_callback: ErrorCallback,
}

/// Fills the video half of `info` from the probed stream's parameters.
fn probe_video_info(info: &mut MediaInfo, stream: Stream) {
    let par = stream.codecpar();
    info.has_video = 1;
    copy_c_name(&mut info.video_codec, unsafe {
        ff::avcodec_get_name(stream.codec_id())
    });
    // SAFETY: the value was probed by FFmpeg itself, so it is a valid
    // AVPixelFormat (or -1 = NONE, for which the name lookup returns null)
    let pix_fmt = unsafe { mem::transmute::<c_int, ff::AVPixelFormat>((*par).format) };
    copy_c_name(&mut info.pixel_format, unsafe {
        ff::av_get_pix_fmt_name(pix_fmt)
    });
    unsafe {
        info.width = u32::try_from((*par).width).unwrap_or(0);
        info.height = u32::try_from((*par).height).unwrap_or(0);
        info.video_bitrate = (*par).bit_rate.max(0);
    }
    info.framerate = q2d(stream.avg_frame_rate());
}

/// Fills the audio half of `info` from the probed stream's parameters.
fn probe_audio_info(info: &mut MediaInfo, stream: Stream) {
    let par = stream.codecpar();
    info.has_audio = 1;
    copy_c_name(&mut info.audio_codec, unsafe {
        ff::avcodec_get_name(stream.codec_id())
    });
    // SAFETY: the value was probed by FFmpeg itself, so it is a valid
    // AVSampleFormat (or -1 = NONE, for which the name lookup returns null)
    let sample_fmt = unsafe { mem::transmute::<c_int, ff::AVSampleFormat>((*par).format) };
    copy_c_name(&mut info.sample_format, unsafe {
        ff::av_get_sample_fmt_name(sample_fmt)
    });
    unsafe {
        info.sample_rate = (*par).sample_rate.max(0);
        info.channels = (*par).ch_layout.nb_channels.max(0);
        info.audio_bitrate = (*par).bit_rate.max(0);
    }
}

impl PlaybackUnit {
    /// [playback thread] Opens the media at `url`, blocking until its
    /// streams are probed and the decoders are built. Aborted through
    /// `cancel` while blocked on demuxer I/O.
    pub(crate) fn open(
        url: String,
        device: HwDevice,
        audio_out: AudioOptionsView,
        cancel: ReadOnlyCancelToken,
        error_callback: ErrorCallback,
        protocol_whitelist: &StreamingProtocol,
        controls: UnitControls,
    ) -> Result<(Self, Pipeline)> {
        NETWORK_INIT.call_once(|| {
            unsafe { ff::avformat_network_init() };
        });

        let input = Input::open(cancel.clone(), &url, protocol_whitelist)?;
        let video_index = input.find_best_stream(ff::AVMediaType::AVMEDIA_TYPE_VIDEO);
        let audio_index = input.find_best_stream(ff::AVMediaType::AVMEDIA_TYPE_AUDIO);
        if video_index < 0 && audio_index < 0 {
            return Err(anyhow!("media has no playable video or audio stream"));
        }

        let media_info = {
            let mut media_info = MediaInfo::empty();
            if let Some(d) = input.duration() {
                media_info.duration = d;
            }
            if video_index >= 0 {
                probe_video_info(&mut media_info, input.stream_at(video_index));
            }
            if audio_index >= 0 {
                probe_audio_info(&mut media_info, input.stream_at(audio_index));
            }
            media_info
        };

        let audio_reader = AudioReader::new(audio_out.clone());
        let (video_queue, video_reader) = VideoQueue::channel();

        let (hw_ctx, video, video_size) = if video_index >= 0 {
            #[cfg(target_os = "windows")]
            let hw = HwDeviceContext::new(&device)?;
            #[cfg(target_os = "macos")]
            let hw = HwDeviceContext::new()?;

            let playback =
                VideoPlayback::new(input.stream_at(video_index), video_index, &hw, video_queue)?;
            let par = input.stream_at(video_index).codecpar();
            let (width, height) = unsafe { ((*par).width, (*par).height) };
            let size = VideoSize {
                width: u32::try_from(width).map_err(|_| anyhow!("invalid video width: {width}"))?,
                height: u32::try_from(height)
                    .map_err(|_| anyhow!("invalid video height: {height}"))?,
            };
            (Some(hw), Some(playback), Some(size))
        } else {
            (None, None, None)
        };

        let audio = if audio_index >= 0 {
            Some(AudioPlayback::new(
                input.stream_at(audio_index),
                audio_index,
                audio_out,
                audio_reader.rx_slot(),
            )?)
        } else {
            None
        };

        #[cfg(target_os = "macos")]
        drop(hw_ctx);

        let unit = Self {
            duration: input.duration(),
            video_size,
            media_info,
            cancel,
            seek: AtomicSeekSlot::new(),
            controls,
            transport: AtomicTransport::new(),
            audio: audio_reader,
            video: video_reader,
            #[cfg(target_os = "windows")]
            hw_ctx,
            output: Mutex::new(None),
            device,
            error_callback,
            url,
        };
        let pipeline = Pipeline {
            start_offset: input.start_offset(),
            input,
            video,
            audio,
        };
        Ok((unit, pipeline))
    }

    /// [playback thread] Demuxes and decodes until the player cancels.
    /// A cancelled run is a clean exit; a runtime error propagates to the
    /// player, which retires this unit.
    pub(crate) fn run_blocking(&self, pipeline: Pipeline) -> Result<()> {
        match self.run(pipeline) {
            Err(e) if !self.cancel.is_cancelled() => Err(e),
            Err(_) | Ok(()) => Ok(()),
        }
    }

    fn run(&self, pipeline: Pipeline) -> Result<()> {
        let Pipeline {
            input,
            mut video,
            mut audio,
            start_offset,
        } = pipeline;

        let mut packet = OwnedPacket::new()?;
        // whether the demuxer ran out of input; playback then drains the sinks
        let mut eof = false;
        // varispeed this unit currently runs at; fresh units start at 1x
        let mut applied_rate = 1.0_f64;
        // the sinks poll this while waiting for queue space: not playing
        // means nothing drains, so a blocked pump is the only place a
        // queued play can be noticed
        let poll_controls = || self.apply_play_or_pause();

        loop {
            if self.cancel.is_cancelled() {
                return Ok(());
            }

            self.apply_play_or_pause();

            // peek, not consume: a fresh unit picks up the standing rate
            // even though a previous unit already took the update.
            // realtime streams (no duration) stay at 1x
            let rate = if self.duration.is_some() {
                self.controls.rate.peek().unwrap_or(DEFAULT_PLAYBACK_RATE)
            } else {
                DEFAULT_PLAYBACK_RATE
            };
            if rate.to_bits() != applied_rate.to_bits() {
                self.transport.set_rate(rate);
                if let Some(audio) = audio.as_mut() {
                    audio.set_rate(rate);
                }
                applied_rate = rate;
            }

            if let Some(target) = self.seek.take() {
                // non-fatal: an unseekable (e.g. realtime) stream keeps playing
                if let Err(e) = self.apply_seek(
                    &input,
                    video.as_mut(),
                    audio.as_mut(),
                    &mut eof,
                    target,
                    start_offset,
                ) {
                    self.error_callback
                        .report(format!("seek to {target}s failed: {e}"));
                }
                continue;
            }

            if eof {
                if self.loop_wrap(
                    &input,
                    video.as_mut(),
                    audio.as_mut(),
                    &mut eof,
                    start_offset,
                ) {
                    continue;
                }
                self.settle_ended(video.as_ref(), audio.as_ref());
                thread::sleep(PLAYBACK_POLL);
                continue;
            }

            let ret = unsafe { ff::av_read_frame(input.as_ptr(), packet.as_mut_ptr()) };
            if ret == ff::AVERROR_EOF {
                if let Some(video) = video.as_mut() {
                    video.drain(start_offset, &self.cancel, &self.seek, &poll_controls)?;
                }
                if let Some(audio) = audio.as_mut() {
                    audio.drain(start_offset, &self.cancel, &self.seek, &poll_controls)?;
                }
                eof = true;
                continue;
            }
            if ret == ff::AVERROR(libc::EAGAIN) {
                thread::sleep(PLAYBACK_POLL);
                continue;
            }
            if ret < 0 {
                return Err(av_err("av_read_frame", ret));
            }

            let stream_index = packet.stream_index();
            if let Some(video) = video.as_mut()
                && video.handles(stream_index)
            {
                video.handle_packet(
                    &mut packet,
                    start_offset,
                    &self.cancel,
                    &self.seek,
                    &poll_controls,
                )?;
            } else if let Some(audio) = audio.as_mut()
                && audio.handles(stream_index)
            {
                audio.handle_packet(
                    &mut packet,
                    start_offset,
                    &self.cancel,
                    &self.seek,
                    &poll_controls,
                )?;
            }
            packet.unref();
        }
    }

    /// [playback thread] At EOF with looping on: once the sinks have
    /// played the tail out, wraps back to the start right here — the
    /// input and the decoders stay warm, and no ENDED/seek round-trip is
    /// exposed to the engine. Returns whether the wrap happened.
    fn loop_wrap(
        &self,
        input: &Input,
        video: Option<&mut VideoPlayback>,
        audio: Option<&mut AudioPlayback>,
        eof: &mut bool,
        start_offset: f64,
    ) -> bool {
        if !self.controls.looping.peek().unwrap_or(false) || !self.transport.is_playing() {
            return false;
        }

        let drained = video.as_deref().is_none_or(VideoPlayback::is_drained)
            && audio.as_deref().is_none_or(AudioPlayback::is_drained);
        if !drained {
            return false;
        }

        // non-fatal, like a user seek; falling through settles into ENDED,
        // where is_playing turns false, so a failed wrap does not retry
        if let Err(e) = self.apply_seek(input, video, audio, eof, 0.0, start_offset) {
            self.error_callback
                .report(format!("loop restart failed: {e}"));
            return false;
        }
        true
    }

    /// Fully decoded; once the sinks have drained, holds the clock and
    /// flags ENDED.
    fn settle_ended(&self, video: Option<&VideoPlayback>, audio: Option<&AudioPlayback>) {
        let drained = video.is_none_or(VideoPlayback::is_drained)
            && audio.is_none_or(AudioPlayback::is_drained);
        if drained && self.transport.is_playing() {
            self.transport.ended();
        }
    }

    fn apply_seek(
        &self,
        input: &Input,
        video: Option<&mut VideoPlayback>,
        audio: Option<&mut AudioPlayback>,
        eof: &mut bool,
        target: f64,
        start_offset: f64,
    ) -> Result<()> {
        let ts = ((target + start_offset) * f64::from(ff::AV_TIME_BASE)) as i64;
        check("avformat_seek_file", unsafe {
            ff::avformat_seek_file(input.as_ptr(), -1, i64::MIN, ts, i64::MAX, 0)
        })?;

        if let Some(video) = video {
            video.flush_for_seek();
        }
        if let Some(audio) = audio {
            audio.flush_for_seek();
        }
        self.transport.rebase(target);
        *eof = false;
        Ok(())
    }

    pub(crate) fn state(&self) -> UUAVState {
        self.transport.state().into()
    }

    /// Applies a queued play/pause command, if any.
    fn apply_play_or_pause(&self) {
        if let Some(play) = self.controls.play_or_pause.consume() {
            if play {
                self.play();
            } else {
                self.transport.pause();
            }
        }
    }

    fn play(&self) {
        match self.transport.state() {
            PlaybackState::Ready | PlaybackState::Paused => {}
            PlaybackState::Playing => return,
            PlaybackState::Ended => {
                // restart from the beginning
                self.seek.request(0.0);
            }
        }
        self.transport.play();
    }

    pub(crate) fn seek_intent(&self, time: f64) {
        self.seek.request(time.max(0.0));
    }

    pub(crate) const fn duration(&self) -> Option<f64> {
        self.duration
    }

    pub(crate) fn current_time(&self) -> f64 {
        let now = self.transport.now();
        self.duration.map_or(now, |d| now.clamp(0.0, d))
    }

    /// The playback slaves itself to the externally provided master clock.
    pub(crate) fn assign_master_clock(&self, current_time: f64) {
        self.transport.sync_to_master(current_time);
    }

    pub(crate) fn video_size(&self) -> Option<VideoSize> {
        self.video_size.clone()
    }

    pub(crate) const fn media_info(&self) -> MediaInfo {
        self.media_info
    }

    pub(crate) fn video_texture(
        &self,
        #[cfg(target_os = "macos")] plane: i32,
    ) -> Option<VideoTextureView> {
        #[cfg(target_os = "windows")]
        let texture = self.output.lock().as_ref().and_then(VideoOutput::texture);
        #[cfg(target_os = "macos")]
        let texture = self
            .output
            .lock()
            .as_ref()
            .and_then(|output| output.texture(plane));

        texture
    }

    /// [render thread] Presents the frame that is due per the clock.
    pub(crate) fn on_render_event(&self) {
        let now = self.transport.now() + VIDEO_PRESENT_BIAS;

        // audio-only media never queues frames, so this also gates on video
        let Some(frame) = self.video.next_due(now) else {
            return;
        };

        let mut output = self.output.lock();
        let out = output.get_or_insert_with(|| VideoOutput::new(&self.device));

        // D3D11 copies through the decode context (the immediate context
        // under the FFmpeg device lock); Metal blits on the plugin's own
        // queue and takes nothing from the decode context
        #[cfg(target_os = "windows")]
        let presented = match self.hw_ctx.as_ref() {
            Some(hw) => out.present(hw, &frame),
            None => return,
        };
        #[cfg(target_os = "macos")]
        let presented = out.present(&frame);

        if let Err(e) = presented {
            let message = format!("video present failed for {}: {e}", self.url);
            // Exit the guard scope before calling external FFI function
            drop(output);
            self.error_callback.report(message);
        }
    }

    /// [audio thread] Fills interleaved FLT; pads silence on underrun,
    /// never blocks; returns frames actually copied.
    pub(crate) fn read_audio(&self, dst: *mut f32, frames: usize) -> i32 {
        self.audio.read(&self.transport, dst, frames)
    }
}
