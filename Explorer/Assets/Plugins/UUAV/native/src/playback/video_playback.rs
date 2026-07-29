use anyhow::Result;
use crossbeam_channel::{Receiver, Sender, TrySendError, bounded};
use parking_lot::Mutex;
use std::os::raw::c_int;
use std::ptr;
use std::sync::Arc;
use std::thread;

use super::util::{AtomicSeekSlot, PLAYBACK_POLL, ReadOnlyCancelToken};
use crate::ffutil::{Decoded, OwnedPacket, Stream};
use crate::hw_device::HwDeviceContext;
use crate::video_decoder::{VideoDecoder, VideoFrame};

/// Decoded frames buffered ahead of the clock. Each entry pins a decoder
/// surface, so it must stay below [`VideoDecoder::EXTRA_HW_FRAMES`].
const VIDEO_QUEUE_CAP: usize = 4;

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
pub(super) struct VideoPlayback {
    decoder: VideoDecoder,
    queue: VideoQueue,
    stream_index: c_int,
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
        })
    }

    pub(super) const fn handles(&self, stream_index: c_int) -> bool {
        self.stream_index == stream_index
    }

    /// Sends the packet and moves every ready frame into the queue.
    pub(super) fn handle_packet(
        &mut self,
        packet: &mut OwnedPacket,
        start_offset: f64,
        cancel: &ReadOnlyCancelToken,
        seek: &AtomicSeekSlot,
        poll_controls: &dyn Fn(),
    ) -> Result<()> {
        self.decoder.send(packet.as_mut_ptr())?;
        self.pump(start_offset, cancel, seek, poll_controls)
    }

    /// Sends end-of-stream and moves the remaining frames into the queue.
    pub(super) fn drain(
        &mut self,
        start_offset: f64,
        cancel: &ReadOnlyCancelToken,
        seek: &AtomicSeekSlot,
        poll_controls: &dyn Fn(),
    ) -> Result<()> {
        self.decoder.send(ptr::null())?;
        self.pump(start_offset, cancel, seek, poll_controls)
    }

    /// Discards everything belonging to the pre-seek position.
    pub(super) fn flush_for_seek(&mut self) {
        self.decoder.flush();
        self.queue.flush();
    }

    /// Whether the render thread has consumed every queued frame.
    pub(super) fn is_drained(&self) -> bool {
        self.queue.is_drained()
    }

    /// Moves every frame the decoder has ready into the presentation queue,
    /// waiting for space while staying responsive to stop/seek commands.
    /// The wait also keeps `poll_controls` applied: while not playing the
    /// queue does not drain, so a queued play would otherwise never land.
    fn pump(
        &mut self,
        start_offset: f64,
        cancel: &ReadOnlyCancelToken,
        seek: &AtomicSeekSlot,
        poll_controls: &dyn Fn(),
    ) -> Result<()> {
        loop {
            match self.decoder.receive()? {
                Decoded::Frame(mut frame) => {
                    frame.shift_pts(start_offset);
                    loop {
                        if cancel.is_cancelled() || seek.is_pending() {
                            // the frame is obsolete: the queue is about to
                            // be flushed
                            return Ok(());
                        }
                        // after the cancel check: a retired unit must not
                        // consume a command meant for its successor
                        poll_controls();
                        match self.queue.try_push(frame) {
                            Ok(()) => break,
                            Err(returned) => {
                                frame = returned;
                                thread::sleep(PLAYBACK_POLL);
                            }
                        }
                    }
                }
                Decoded::Again | Decoded::Eof => return Ok(()),
            }
        }
    }
}
