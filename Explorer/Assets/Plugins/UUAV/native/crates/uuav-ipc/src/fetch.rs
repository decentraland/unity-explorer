
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicPtr, Ordering};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use uuav_abi::PlayerId;

use crate::protocol::{FETCH_SLOT_BYTES, SharedSegment, fetch_op, fetch_status};

const IDLE_POLL: Duration = Duration::from_micros(250);

const DROP_JOIN_GRACE: Duration = Duration::from_millis(100);

#[repr(C)]
pub struct FetchExchange {
    pub op: u32,
    pub handle: u32,
    pub offset: u64,
    pub len: u32,
    pub flags: u32,
    pub url: *const u8,
    pub url_len: u32,
    pub buf: *mut u8,
    pub buf_cap: u32,
    pub status: u32,
    pub n: u32,
    pub size: i64,
    pub out_handle: u32,
}

pub type FetchProvider = extern "C" fn(*mut FetchExchange);

static PROVIDER: AtomicPtr<()> = AtomicPtr::new(std::ptr::null_mut());

pub fn set_provider(provider: Option<FetchProvider>) {
    let raw = provider.map_or(std::ptr::null_mut(), |p| p as *mut ());
    PROVIDER.store(raw, Ordering::Release);
}

fn provider() -> Option<FetchProvider> {
    let raw = PROVIDER.load(Ordering::Acquire);
    if raw.is_null() {
        None
    } else {
        Some(unsafe { std::mem::transmute::<*mut (), FetchProvider>(raw) })
    }
}

pub trait SegmentSource {
    fn segment(&self) -> &SharedSegment;
}

pub struct FetchResponder {
    stop: Arc<AtomicBool>,
    handle: Option<JoinHandle<()>>,
}

impl FetchResponder {
    pub fn spawn<M>(id: PlayerId, mapping: Arc<M>) -> std::io::Result<Self>
    where
        M: SegmentSource + Send + Sync + 'static,
    {
        Self::spawn_on(
            thread::Builder::new().name(format!("uuav-fetch-{id}")),
            mapping,
        )
    }

    fn spawn_on<M>(builder: thread::Builder, mapping: Arc<M>) -> std::io::Result<Self>
    where
        M: SegmentSource + Send + Sync + 'static,
    {
        let stop = Arc::new(AtomicBool::new(false));
        let stop_for_thread = Arc::clone(&stop);
        let handle = builder.spawn(move || run(&mapping, &stop_for_thread))?;
        Ok(Self {
            stop,
            handle: Some(handle),
        })
    }
}

impl Drop for FetchResponder {
    fn drop(&mut self) {
        self.stop.store(true, Ordering::Release);
        let Some(handle) = self.handle.take() else {
            return;
        };
        handle.thread().unpark();
        let started = Instant::now();
        loop {
            if handle.is_finished() {
                let _ = handle.join();
                return;
            }
            if started.elapsed() >= DROP_JOIN_GRACE {
                return;
            }
            thread::sleep(Duration::from_millis(1));
        }
    }
}

fn run<M: SegmentSource>(mapping: &Arc<M>, stop: &AtomicBool) {
    let segment = mapping.segment();
    let mut scratch = vec![0u8; FETCH_SLOT_BYTES];
    let mut last_seen = 0u64;

    while !stop.load(Ordering::Acquire) {
        let Some(request) = segment.fetch_request.take(last_seen) else {
            thread::park_timeout(IDLE_POLL);
            continue;
        };
        last_seen = request.generation;
        if stop.load(Ordering::Acquire) {
            break;
        }
        service(segment, &request, &mut scratch);
    }
}

fn service(segment: &SharedSegment, request: &crate::protocol::FetchRequest, scratch: &mut [u8]) {
    let mut exchange = FetchExchange {
        op: request.op,
        handle: request.handle,
        offset: request.offset,
        len: request.len,
        flags: request.flags,
        url: request.url.as_ptr(),
        url_len: request.url.len() as u32,
        buf: scratch.as_mut_ptr(),
        buf_cap: scratch.len() as u32,
        status: fetch_status::ERR,
        n: 0,
        size: -1,
        out_handle: 0,
    };

    match provider() {
        Some(provider) => provider(&raw mut exchange),
        None => exchange.status = fetch_status::ERR,
    }

    let staged = if request.op == fetch_op::READ && exchange.status == fetch_status::OK {
        let n = (exchange.n as usize).min(scratch.len());
        segment.fetch_bulk.stage(scratch.get(..n).unwrap_or_default())
    } else {
        0
    };

    segment.fetch_response.publish(
        request.generation,
        exchange.status,
        staged,
        exchange.size,
        exchange.out_handle,
    );
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::panic,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              keeps them out of the shipped paths"
)]
mod tests {
    use std::sync::{Condvar, Mutex, MutexGuard, PoisonError};
    use std::time::Instant;

    use super::*;

    struct BoxedSegment(Box<SharedSegment>);
    impl SegmentSource for BoxedSegment {
        fn segment(&self) -> &SharedSegment {
            &self.0
        }
    }

    fn provider_lock() -> MutexGuard<'static, ()> {
        static LOCK: Mutex<()> = Mutex::new(());
        LOCK.lock().unwrap_or_else(PoisonError::into_inner)
    }

    extern "C" fn pattern_provider(exchange: *mut FetchExchange) {
        let ex = unsafe { &mut *exchange };
        match ex.op {
            fetch_op::OPEN => {
                ex.status = fetch_status::OK;
                ex.size = 1_000_000;
                ex.out_handle = 42;
            }
            fetch_op::READ => {
                let n = ex.len.min(ex.buf_cap) as usize;
                let out = unsafe { std::slice::from_raw_parts_mut(ex.buf, n) };
                for (i, byte) in out.iter_mut().enumerate() {
                    *byte = (ex.offset as usize + i) as u8;
                }
                ex.status = fetch_status::OK;
                ex.n = n as u32;
            }
            _ => ex.status = fetch_status::OK,
        }
    }

    #[test]
    fn the_responder_services_open_then_read_end_to_end() {
        let _guard = provider_lock();
        set_provider(Some(pattern_provider));
        let mapping = Arc::new(BoxedSegment(SharedSegment::boxed_zeroed()));
        let responder = FetchResponder::spawn(1, Arc::clone(&mapping)).unwrap();
        let segment = mapping.segment();

        let g_open = segment
            .fetch_request
            .publish(fetch_op::OPEN, 0, 0, 0, 0, "https://cdn/x.mp4")
            .unwrap();
        let opened = wait_for(segment, g_open);
        assert_eq!(opened.status, fetch_status::OK);
        assert_eq!(opened.size, 1_000_000);
        assert_eq!(opened.out_handle, 42);

        let g_read = segment
            .fetch_request
            .publish(fetch_op::READ, 42, 500, 64, 0, "")
            .unwrap();
        let read = wait_for(segment, g_read);
        assert_eq!(read.status, fetch_status::OK);
        assert_eq!(read.n, 64);
        let mut out = vec![0u8; 64];
        segment.fetch_bulk.copy_out(read.n as usize, &mut out);
        for (i, byte) in out.iter().enumerate() {
            assert_eq!(*byte, (500 + i) as u8);
        }

        drop(responder);
        set_provider(None);
    }

    fn wait_for(segment: &SharedSegment, generation: u64) -> crate::protocol::FetchResponse {
        for _ in 0..100_000 {
            if let Some(response) = segment.fetch_response.read(generation) {
                return response;
            }
            thread::sleep(Duration::from_micros(50));
        }
        panic!("responder never answered generation {generation}");
    }

    static STUCK: (Mutex<StuckState>, Condvar) = (
        Mutex::new(StuckState {
            entered: false,
            release: false,
        }),
        Condvar::new(),
    );

    struct StuckState {
        entered: bool,
        release: bool,
    }

    extern "C" fn stuck_provider(exchange: *mut FetchExchange) {
        let (lock, condvar) = &STUCK;
        let mut state = lock.lock().unwrap_or_else(PoisonError::into_inner);
        state.entered = true;
        condvar.notify_all();
        while !state.release {
            state = condvar
                .wait(state)
                .unwrap_or_else(PoisonError::into_inner);
        }
        drop(state);
        unsafe { (*exchange).status = fetch_status::ERR };
    }

    #[test]
    fn dropping_a_responder_stuck_in_the_provider_does_not_hang_the_closer() {
        let _guard = provider_lock();
        set_provider(Some(stuck_provider));
        let mapping = Arc::new(BoxedSegment(SharedSegment::boxed_zeroed()));
        let responder = FetchResponder::spawn(2, Arc::clone(&mapping)).unwrap();

        mapping
            .segment()
            .fetch_request
            .publish(fetch_op::OPEN, 0, 0, 0, 0, "https://cdn/never-answers")
            .unwrap();

        {
            let (lock, condvar) = &STUCK;
            let mut state = lock.lock().unwrap_or_else(PoisonError::into_inner);
            let started = Instant::now();
            while !state.entered {
                let (next, timeout) = condvar
                    .wait_timeout(state, Duration::from_millis(100))
                    .unwrap_or_else(PoisonError::into_inner);
                state = next;
                assert!(
                    !timeout.timed_out() || started.elapsed() < Duration::from_secs(5),
                    "responder never picked the request up"
                );
            }
            drop(state);
        }

        let (done_tx, done_rx) = std::sync::mpsc::channel();
        let dropper = thread::spawn(move || {
            drop(responder);
            let _ = done_tx.send(());
        });
        let outcome = done_rx.recv_timeout(Duration::from_secs(2));

        {
            let (lock, condvar) = &STUCK;
            let mut state = lock.lock().unwrap_or_else(PoisonError::into_inner);
            state.release = true;
            drop(state);
            condvar.notify_all();
        }
        dropper.join().unwrap();
        set_provider(None);
        assert!(
            outcome.is_ok(),
            "dropping a responder blocked in the provider hung the closer"
        );
    }

    #[test]
    fn a_responder_thread_that_cannot_spawn_surfaces_the_error() {
        let mapping = Arc::new(BoxedSegment(SharedSegment::boxed_zeroed()));
        let builder = thread::Builder::new().stack_size(1 << 55);
        assert!(
            FetchResponder::spawn_on(builder, mapping).is_err(),
            "an unspawnable responder thread must be an error, not a silent stall"
        );
    }
}
