use core::str;
use std::ffi::{c_char, CStr};

use crate::{server::SegmentServer, FfiCallbackFn, OperationHandleId, SEGMENT_SERVER};

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_initialize(
    segment_write_key: *const c_char,
    callback_fn: FfiCallbackFn,
) -> bool {
    let write_key = as_str(segment_write_key).to_string();
    SEGMENT_SERVER.initialize(write_key, callback_fn)
}

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_identify(
    used_id: *const c_char,
    traits_json: *const c_char,
    context_json: *const c_char,
) -> OperationHandleId {
    SEGMENT_SERVER.try_execute(&|segment, id| {
        let segment = segment.clone();
        let used_id = as_str(used_id);
        let traits_json = as_str(traits_json);
        let context_json = as_str(context_json);

        let operation =
            SegmentServer::enqueue_identify(segment, id, used_id, traits_json, context_json);
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_track(
    used_id: *const c_char,
    event_name: *const c_char,
    properties_json: *const c_char,
    context_json: *const c_char,
) -> OperationHandleId {
    SEGMENT_SERVER.try_execute(&|segment, id| {
        let segment = segment.clone();
        let used_id = as_str(used_id);
        let event_name = as_str(event_name);
        let properties_json = as_str(properties_json);
        let context_json = as_str(context_json);

        let operation = SegmentServer::enqueue_track(
            segment,
            id,
            used_id,
            event_name,
            properties_json,
            context_json,
        );
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

#[no_mangle]
pub unsafe extern "C" fn segment_server_flush() -> OperationHandleId {
    SEGMENT_SERVER.try_execute(&|segment, id| {
        let segment = segment.clone();
        let operation = SegmentServer::flush(segment, id);
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

#[no_mangle]
pub unsafe extern "C" fn segment_server_dispose() -> bool {
    SEGMENT_SERVER.dispose()
}

fn as_str<'a>(chars: *const c_char) -> &'a str {
    let c_str = unsafe { CStr::from_ptr(chars) };
    c_str.to_str().unwrap()
}
