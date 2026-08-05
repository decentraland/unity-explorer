use anyhow::Result;
use crossbeam_channel::{Receiver, Sender, TrySendError, bounded};
use parking_lot::Mutex;
use std::collections::VecDeque;
use std::os::raw::c_int;
use std::ptr;
use std::sync::Arc;

use crate::ffutil::{Decoded, OwnedPacket, Stream};
use crate::hw_device::HwDeviceContext;
use crate::video_decoder::{VideoDecoder, VideoFrame};

/// Decoded frames buffered ahead of the clock. Each entry pins a decoder
/// surface, so it must stay below [`VideoDecoder::EXTRA_HW_FRAMES`].
const VIDEO_QUEUE_CAP: usize = 4;

/// Bounds of the compressed-packet buffer held in front of the decoder.
/// Chunk-interleaved files (e.g. Vimeo progressive mp4s mux ~0.5 s of video
/// packets, then ~0.5 s of audio packets) would otherwise head-of-line block
/// audio demuxing behind the presentation-paced frame queue, starving the
/// audio ring once per chunk. Compressed packets are cheap, so the bounds are
/// backstops: in steady state the buffer holds at most one interleave chunk.
const MAX_PENDING_PACKETS: usize = 256;
const MAX_PENDING_BYTES: usize = 16 * 1024 * 1024;

/// The frame the render thread popped but whose time has not come yet.
type HeldFrame = Arc<Mutex<Option<VideoFrame>>>;

/// Worker half of the presentation queue: the sender it fills, plus a
/// second receiver and the reader's held-frame slot so a seek can discard
/// everything buffered.
#[derive(Clone)]
pub(super) struct VideoQueue {
    tx: Sender<VideoFrame>,
    /// Drains the channel on seek; frames dropped here release their
    /// decoder surfaces immediately.
    drain: Receiver<VideoFrame>,
    held: HeldFrame,
}

impl VideoQueue {
    /// Creates the bounded queue and the render-thread reader draining it.
    pub(super) fn channel() -> (Self, VideoReader) {
        let (tx, rx) = bounded(VIDEO_QUEUE_CAP);
        let held = Arc::new(Mutex::new(None));
        (
            Self {
                tx,
                drain: rx.clone(),
                held: Arc::clone(&held),
            },
            VideoReader { rx, held },
        )
    }

    /// Queues a frame for presentation; gives it back when the queue is
    /// full.
    fn try_push(&self, frame: VideoFrame) -> Result<(), VideoFrame> {
        self.tx.try_send(frame).map_err(TrySendError::into_inner)
    }

    /// Discards every buffered frame, including the one the reader holds.
    /// Keeping the slot locked across the drain stops the reader from
    /// picking up a pre-flush frame halfway through.
    fn flush(&self) {
        let mut held = self.held.lock();
        held.take();
        while self.drain.try_recv().is_ok() {}
    }

    /// Whether the render thread has consumed every queued frame.
    fn is_drained(&self) -> bool {
        self.tx.is_empty() && self.held.lock().is_none()
    }
}

/// Render-thread half of the presentation queue.
pub(super) struct VideoReader {
    rx: Receiver<VideoFrame>,
    held: HeldFrame,
}

impl VideoReader {
    /// [render thread] The frame to present at `now`: drops frames whose
    /// time already passed, returns the most recent due one, and keeps an
    /// undue frame held for a later call.
    pub(super) fn next_due(&self, now: f64) -> Option<VideoFrame> {
        let mut held = self.held.lock();
        let mut due = None;
        loop {
            if held.is_none() {
                match self.rx.try_recv() {
                    Ok(frame) => *held = Some(frame),
                    Err(_) => break,
                }
            }
            match held.as_ref() {
                Some(frame) if frame.pts().is_none_or(|pts| pts <= now) => {
                    due = held.take();
                }
                _ => break,
            }
        }
        due
    }
}

/// The video half of a playback: decodes the video stream and fills the
/// presentation queue the render thread consumes.
///
/// Nothing here blocks: packets wait compressed in `pending`, decoded frames
/// wait in the frame queue (plus one `held` overflow slot), and the playback
/// thread's [`Self::service`] calls move whatever fits. Demuxing therefore
/// never stalls behind presentation-paced video while the audio ring runs dry.
pub(super) struct VideoPlayback {
    decoder: VideoDecoder,
    queue: VideoQueue,
    stream_index: c_int,
    /// Compressed packets waiting for the decoder, in demux order.
    pending: VecDeque<OwnedPacket>,
    /// Payload bytes buffered in `pending`.
    pending_bytes: usize,
    /// A decoded frame the full frame queue could not take. At most one, so
    /// the pinned-surface envelope stays at `VIDEO_QUEUE_CAP + 1`, matching
    /// the headroom [`VideoDecoder::EXTRA_HW_FRAMES`] provides.
    held: Option<VideoFrame>,
    /// End-of-stream was sent to the decoder.
    eos_sent: bool,
    /// The decoder returned its last frame after end-of-stream.
    finished: bool,
}

impl VideoPlayback {
    pub(super) fn new(
        stream: Stream,
        stream_index: c_int,
        hw: &HwDeviceContext,
        queue: VideoQueue,
    ) -> Result<Self> {
        Ok(Self {
            decoder: VideoDecoder::new(stream, hw)?,
            queue,
            stream_index,
            pending: VecDeque::new(),
            pending_bytes: 0,
            held: None,
            eos_sent: false,
            finished: false,
        })
    }

    pub(super) const fn handles(&self, stream_index: c_int) -> bool {
        self.stream_index == stream_index
    }

    /// Takes the packet into the pending buffer; declines when the buffer
    /// is at capacity (the caller retries after servicing the sinks).
    pub(super) fn try_enqueue(&mut self, packet: &mut OwnedPacket) -> Result<bool> {
        if self.pending.len() >= MAX_PENDING_PACKETS || self.pending_bytes >= MAX_PENDING_BYTES {
            return Ok(false);
        }
        let owned = OwnedPacket::stolen_from(packet)?;
        self.pending_bytes = self.pending_bytes.saturating_add(owned.size());
        self.pending.push_back(owned);
        Ok(true)
    }

    /// Moves whatever fits without waiting: held/decoded frames into the
    /// presentation queue, then pending packets into the decoder. With `eos`
    /// set and the pending buffer empty, signals end-of-stream and drains the
    /// decoder's tail. Returns whether anything moved.
    pub(super) fn service(&mut self, start_offset: f64, eos: bool) -> Result<bool> {
        let mut progress = false;
        loop {
            if let Some(frame) = self.held.take() {
                match self.queue.try_push(frame) {
                    Ok(()) => progress = true,
                    Err(frame) => {
                        // frame queue full: presentation sets the pace
                        self.held = Some(frame);
                        return Ok(progress);
                    }
                }
            }
            if self.finished {
                return Ok(progress);
            }
            match self.decoder.receive()? {
                Decoded::Frame(mut frame) => {
                    frame.shift_pts(start_offset);
                    self.held = Some(frame);
                }
                Decoded::Again => {
                    if let Some(mut packet) = self.pending.pop_front() {
                        self.pending_bytes = self.pending_bytes.saturating_sub(packet.size());
                        self.decoder.send(packet.as_mut_ptr())?;
                        progress = true;
                        continue;
                    }
                    if eos && !self.eos_sent {
                        self.decoder.send(ptr::null())?;
                        self.eos_sent = true;
                        progress = true;
                        continue;
                    }
                    return Ok(progress);
                }
                Decoded::Eof => {
                    self.finished = true;
                    return Ok(progress);
                }
            }
        }
    }

    /// Discards everything belonging to the pre-seek position.
    pub(super) fn flush_for_seek(&mut self) {
        self.decoder.flush();
        self.queue.flush();
        self.pending.clear();
        self.pending_bytes = 0;
        self.held = None;
        self.eos_sent = false;
        self.finished = false;
    }

    /// Whether the stream was decoded to its end and the render thread has
    /// consumed every queued frame.
    pub(super) fn is_drained(&self) -> bool {
        self.finished && self.pending.is_empty() && self.held.is_none() && self.queue.is_drained()
    }
}
