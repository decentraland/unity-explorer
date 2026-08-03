
use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::sync::atomic::{AtomicPtr, Ordering};

use uuav_ipc::protocol::{SharedSegment, fetch_op, fetch_status};

const UUAV_FETCH_OK: c_int = 0;
const UUAV_FETCH_WOULDWAIT: c_int = 1;
const UUAV_FETCH_EOF: c_int = 2;
const UUAV_FETCH_ERR: c_int = 3;

static SEGMENT: AtomicPtr<SharedSegment> = AtomicPtr::new(ptr::null_mut());

type FetchBeginFn =
    extern "C" fn(op: u32, handle: u32, offset: u64, len: u32, url: *const c_char) -> u64;
type FetchPollFn = extern "C" fn(
    generation: u64,
    buf: *mut u8,
    cap: u32,
    out_n: *mut u32,
    out_size: *mut i64,
    out_handle: *mut u32,
) -> c_int;

unsafe extern "C" {
    fn av_uuav_fetch_register(begin: FetchBeginFn, poll: FetchPollFn);
}

pub struct FetchBridge;

impl FetchBridge {
    pub fn install(segment: &SharedSegment) -> Self {
        SEGMENT.store(ptr::from_ref(segment).cast_mut(), Ordering::Release);
        unsafe { av_uuav_fetch_register(fetch_begin, fetch_poll) };
        Self
    }
}

impl Drop for FetchBridge {
    fn drop(&mut self) {
        SEGMENT.store(ptr::null_mut(), Ordering::Release);
    }
}

fn segment() -> Option<&'static SharedSegment> {
    let pointer = SEGMENT.load(Ordering::Acquire);
    if pointer.is_null() {
        None
    } else {
        Some(unsafe { &*pointer })
    }
}

extern "C" fn fetch_begin(
    op: u32,
    handle: u32,
    offset: u64,
    len: u32,
    url: *const c_char,
) -> u64 {
    let Some(segment) = segment() else {
        return 0;
    };
    let url = if op == fetch_op::OPEN && !url.is_null() {
        match unsafe { CStr::from_ptr(url) }.to_str() {
            Ok(text) => text,
            Err(_) => return 0,
        }
    } else {
        ""
    };
    segment
        .fetch_request
        .publish(op, handle, offset, len, 0, url)
        .unwrap_or(0)
}

extern "C" fn fetch_poll(
    generation: u64,
    buf: *mut u8,
    cap: u32,
    out_n: *mut u32,
    out_size: *mut i64,
    out_handle: *mut u32,
) -> c_int {
    let Some(segment) = segment() else {
        return UUAV_FETCH_ERR;
    };
    let Some(response) = segment.fetch_response.read(generation) else {
        return UUAV_FETCH_WOULDWAIT;
    };

    if !out_size.is_null() {
        unsafe { out_size.write(response.size) };
    }
    if !out_handle.is_null() {
        unsafe { out_handle.write(response.out_handle) };
    }

    match response.status {
        fetch_status::OK => {
            let copied = if buf.is_null() || cap == 0 {
                0
            } else {
                let destination = unsafe { std::slice::from_raw_parts_mut(buf, cap as usize) };
                segment.fetch_bulk.copy_out(response.n as usize, destination)
            };
            if !out_n.is_null() {
                unsafe { out_n.write(copied as u32) };
            }
            UUAV_FETCH_OK
        }
        fetch_status::EOF => UUAV_FETCH_EOF,
        _ => UUAV_FETCH_ERR,
    }
}
