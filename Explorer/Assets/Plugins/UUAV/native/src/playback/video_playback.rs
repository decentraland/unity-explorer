use anyhow::Result;
use crossbeam_channel::{Receiver, Sender, TrySendError, bounded};
use parking_lot::Mutex;
use std::os::raw::c_int;
use std::ptr;
use std::sync::Arc;
use std::thread;
use std::time::Instant;

use super::util::{AtomicSeekSlot, PLAYBACK_POLL, ReadOnlyCancelToken};
use crate::ffutil::{Decoded, OwnedPacket, Stream};
use crate::hw_device::HwDeviceContext;
use crate::video_decoder::{VideoDecoder, VideoFrame};

const VIDEO_QUEUE_CAP: usize = 4;

type HeldFrame = Arc<Mutex<Option<VideoFrame>>>;

#[derive(Clone)]
pub(super) struct VideoQueue {
    tx: Sender<VideoFrame>,
    drain: Receiver<VideoFrame>,
    held: HeldFrame,
}

impl VideoQueue {
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

    fn try_push(&self, frame: VideoFrame) -> Result<(), VideoFrame> {
        self.tx.try_send(frame).map_err(TrySendError::into_inner)
    }

    fn flush(&self) {
        let mut held = self.held.lock();
        held.take();
        while self.drain.try_recv().is_ok() {}
    }

    fn is_drained(&self) -> bool {
        self.tx.is_empty() && self.held.lock().is_none()
    }
}

pub(super) struct VideoReader {
    rx: Receiver<VideoFrame>,
    held: HeldFrame,
}

impl VideoReader {
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

pub(super) struct VideoPlayback {
    decoder: VideoDecoder,
    queue: VideoQueue,
    stream_index: c_int,
    decoded_count: u64,
    first_decode: Option<Instant>,
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
            decoded_count: 0,
            first_decode: None,
        })
    }

    pub(super) const fn handles(&self, stream_index: c_int) -> bool {
        self.stream_index == stream_index
    }

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

    pub(super) fn flush_for_seek(&mut self) {
        self.decoder.flush();
        self.queue.flush();
    }

    pub(super) fn is_drained(&self) -> bool {
        self.queue.is_drained()
    }

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
                    let now = Instant::now();
                    let start = *self.first_decode.get_or_insert(now);
                    self.decoded_count = self.decoded_count.wrapping_add(1);
                    if self.decoded_count.is_multiple_of(8) {
                        let elapsed = now.duration_since(start).as_secs_f64();
                        let rate = if elapsed > 0.0 {
                            self.decoded_count as f64 / elapsed
                        } else {
                            0.0
                        };
                        crate::diag_log(&format!(
                            "uuav-core: decoded={} in {:.2}s = {:.1}/s",
                            self.decoded_count, elapsed, rate
                        ));
                    }
                    frame.shift_pts(start_offset);
                    loop {
                        if cancel.is_cancelled() || seek.is_pending() {
                            return Ok(());
                        }
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
