use anyhow::{Context, Result, ensure};
use ffmpeg_sys_next as ff;
use std::mem;
use std::os::raw::c_int;
use std::ptr;

use crate::AudioOptions;
use crate::ffutil::{
    AVERROR_EAGAIN, Decoded, OwnedChannelLayout, OwnedDecoder, OwnedFrame, OwnedSwr, Stream,
    av_err, check, q2d,
};

/// Interleaved f32 samples in the configured output format.
pub(crate) struct AudioFrame {
    pts: Option<f64>,
    samples: Vec<f32>,
}

impl AudioFrame {
    /// Presentation time in seconds of the first sample, `None` when the
    /// container provides no usable timestamp.
    pub(crate) const fn pts(&self) -> Option<f64> {
        self.pts
    }

    pub(crate) fn samples(&self) -> &[f32] {
        &self.samples
    }
}

/// Description of the samples entering the resampler;
/// a change (rare, but legal mid-stream) triggers a rebuild.
struct SwrInput {
    format: c_int,
    rate: c_int,
    layout: OwnedChannelLayout,
}

impl SwrInput {
    fn from_frame_options(frame: &OwnedFrame) -> Result<Self> {
        Ok(Self {
            format: frame.format(),
            rate: frame.sample_rate(),
            layout: OwnedChannelLayout::copied_from(frame.ch_layout())?,
        })
    }
}

/// Resampler converting decoded frames to interleaved `f32` at the output
/// rate and channel count it was built for
struct Resampler {
    swr: OwnedSwr,
    input: SwrInput,
    output: AudioOptions,
    /// The input rate the resampler was configured with: the stream's real
    /// rate scaled by the playback rate (varispeed), so one media second
    /// converts into `1 / rate` engine seconds and pitch follows.
    scaled_in_rate: c_int,
}

impl Resampler {
    /// Builds a resampler converting from the frame's sample format to
    /// interleaved `f32` at `output`, sped up by `playback_rate`.
    fn new(frame: &OwnedFrame, output: AudioOptions, playback_rate: f64) -> Result<Self> {
        let input = SwrInput::from_frame_options(frame)?;
        let scaled = (f64::from(input.rate) * playback_rate).round();
        ensure!(
            scaled >= 1.0 && scaled <= f64::from(c_int::MAX),
            "playback rate {playback_rate} is out of range for a {} Hz stream",
            input.rate
        );
        let scaled_in_rate = scaled as c_int;
        let out_layout = OwnedChannelLayout::default_for(output.channels);
        let swr = OwnedSwr::new(
            out_layout.as_ref(),
            ff::AVSampleFormat::AV_SAMPLE_FMT_FLT,
            output.sample_rate,
            frame.ch_layout(),
            unsafe { mem::transmute::<c_int, ff::AVSampleFormat>(input.format) },
            scaled_in_rate,
        )?;

        Ok(Self {
            swr,
            input,
            output,
            scaled_in_rate,
        })
    }

    /// Whether the frame still matches the input this resampler was built
    /// for.
    fn matches(&self, frame: &OwnedFrame) -> bool {
        self.input.format == frame.format()
            && self.input.rate == frame.sample_rate()
            && self.input.layout.matches(frame.ch_layout())
    }

    /// Converts the frame's samples into interleaved `f32` at the output
    /// rate.
    fn convert(&mut self, frame: &OwnedFrame) -> Result<Vec<f32>> {
        let in_rate = frame.sample_rate();
        ensure!(in_rate > 0, "invalid audio frame sample rate: {in_rate}");
        let in_samples = frame.nb_samples();
        // the resampler counts input samples in its own (rate-scaled) domain
        let delay = self
            .swr
            .apply_delay_and_modify(i64::from(self.scaled_in_rate));

        let out_capacity: c_int = {
            let pending_in_samples = delay as f64 + f64::from(in_samples);
            let rate_ratio = self.output.sample_rate_f64() / f64::from(self.scaled_in_rate);
            let exact_out_samples = pending_in_samples * rate_ratio;
            // generous headroom over the exact rescale to absorb rounding
            (exact_out_samples.ceil() + 32.0) as c_int
        };

        let channels = self.output.channels_usize();
        let out_capacity_usize =
            usize::try_from(out_capacity).context("negative resampler output capacity")?;
        let out_len = out_capacity_usize
            .checked_mul(channels.get())
            .context("resampler output size overflows usize")?;

        let mut samples = vec![0.0_f32; out_len];

        let out_planes = [samples.as_mut_ptr().cast::<u8>()];

        // SAFETY: `samples` has room for `out_capacity` interleaved output
        // samples; the frame's planes hold `in_samples` input samples
        let converted = unsafe {
            self.swr.convert(
                out_planes.as_ptr(),
                out_capacity,
                frame.extended_data(),
                in_samples,
            )
        }?;

        let converted_len = usize::try_from(converted)
            .context("negative converted sample count")?
            .checked_mul(channels.get())
            .context("converted sample count overflows usize")?;
        samples.truncate(converted_len);
        Ok(samples)
    }
}

/// Audio decoder + resampler producing interleaved `f32` at the sample rate
/// and channel count of the engine's audio output.
pub(crate) struct AudioDecoder {
    ctx: OwnedDecoder,
    time_base: ff::AVRational,
    resampler: Option<Resampler>,
    audio_options: AudioOptions,
    /// Varispeed factor baked into the resampler ratio; see
    /// [`Resampler::scaled_in_rate`].
    playback_rate: f64,
}

impl AudioDecoder {
    pub(crate) fn new(stream: Stream, audio_options: AudioOptions) -> Result<Self> {
        let codec = stream.find_decoder().context("audio stream")?;
        let mut ctx = OwnedDecoder::new(codec)?;

        unsafe {
            check(
                "avcodec_parameters_to_context(audio)",
                ff::avcodec_parameters_to_context(ctx.as_mut_ptr(), stream.codecpar()),
            )?;
            (*ctx.as_mut_ptr()).pkt_timebase = stream.time_base();
            check(
                "avcodec_open2(audio)",
                ff::avcodec_open2(ctx.as_mut_ptr(), codec, ptr::null_mut()),
            )?;
        }

        Ok(Self {
            ctx,
            time_base: stream.time_base(),
            resampler: None,
            audio_options,
            playback_rate: 1.0,
        })
    }

    /// Applies new output options. Returns `true` when the format changed
    /// (callers should discard already-buffered samples).
    pub(crate) fn set_output(&mut self, options: AudioOptions) -> bool {
        if options == self.audio_options {
            return false;
        }
        self.audio_options = options;
        self.resampler = None;
        true
    }

    /// Applies a new playback rate. Returns `true` when it changed
    /// (callers should discard already-buffered samples).
    pub(crate) fn set_rate(&mut self, rate: f64) -> bool {
        if rate.to_bits() == self.playback_rate.to_bits() {
            return false;
        }
        self.playback_rate = rate;
        self.resampler = None;
        true
    }

    pub(crate) const fn audio_options(&self) -> AudioOptions {
        self.audio_options
    }

    /// Sends a packet; pass `null` to signal end of stream.
    pub(crate) fn send(&mut self, packet: *const ff::AVPacket) -> Result<()> {
        let ret = unsafe { ff::avcodec_send_packet(self.ctx.as_mut_ptr(), packet) };
        if ret < 0 && ret != ff::AVERROR_EOF {
            return Err(av_err("avcodec_send_packet(audio)", ret));
        }
        Ok(())
    }

    pub(crate) fn receive(&mut self) -> Result<Decoded<AudioFrame>> {
        let mut frame = OwnedFrame::new()?;
        let ret = unsafe { ff::avcodec_receive_frame(self.ctx.as_mut_ptr(), frame.as_mut_ptr()) };
        if ret == AVERROR_EAGAIN {
            return Ok(Decoded::Again);
        }
        if ret == ff::AVERROR_EOF {
            return Ok(Decoded::Eof);
        }
        check("avcodec_receive_frame(audio)", ret)?;

        let resampler = self.relevant_resampler_for_frame_or_new(&frame)?;
        let samples = resampler.convert(&frame)?;

        let ts = frame.best_effort_timestamp();
        let pts = self.pts_from_timestamp(ts);

        Ok(Decoded::Frame(AudioFrame { pts, samples }))
    }

    const fn pts_from_timestamp(&self, timestamp: i64) -> Option<f64> {
        if timestamp == ff::AV_NOPTS_VALUE {
            None
        } else {
            Some(timestamp as f64 * q2d(self.time_base))
        }
    }

    fn relevant_resampler_for_frame_or_new(
        &mut self,
        frame: &OwnedFrame,
    ) -> Result<&mut Resampler> {
        let relevant = match self.resampler.take() {
            Some(resampler) => {
                if resampler.matches(frame) {
                    resampler
                } else {
                    Resampler::new(frame, self.audio_options, self.playback_rate)?
                }
            }
            None => Resampler::new(frame, self.audio_options, self.playback_rate)?,
        };

        let relevant = self.resampler.insert(relevant);
        Ok(relevant)
    }

    pub(crate) fn flush(&mut self) {
        unsafe { ff::avcodec_flush_buffers(self.ctx.as_mut_ptr()) };
        // discard resampler history along with the decoder state
        self.resampler = None;
    }
}
