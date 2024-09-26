use core::str;
use std::ffi::{c_char, CStr};

use crate::{FfiCallbackFn, OperationHandleId, SEGMENT_SERVER};

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_initialize(
    segment_write_key: *const c_char,
    callback_fn: FfiCallbackFn,
) -> bool {
    let write_key = as_str(segment_write_key);
    SEGMENT_SERVER.initialize(write_key, callback_fn);
    true
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
    let id = SEGMENT_SERVER.next_id();

    let used_id = as_str(used_id);
    let traits_json = as_str(traits_json);
    let context_json = as_str(context_json);

    let operation = SEGMENT_SERVER.enqueue_identify(id, used_id, traits_json, context_json);
    SEGMENT_SERVER.async_runtime.spawn(operation);
    id
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
    let id = SEGMENT_SERVER.next_id();

    let used_id = as_str(used_id);
    let event_name = as_str(event_name);
    let properties_json = as_str(properties_json);
    let context_json = as_str(context_json);

    let operation =
        SEGMENT_SERVER.enqueue_track(id, used_id, event_name, properties_json, context_json);
    SEGMENT_SERVER.async_runtime.spawn(operation);
    id
}

#[no_mangle]
pub unsafe extern "C" fn segment_server_flush() -> OperationHandleId {
    let id = SEGMENT_SERVER.next_id();
    let operation = SEGMENT_SERVER.flush(id);
    SEGMENT_SERVER.async_runtime.spawn(operation);
    id
}

fn as_str<'a>(chars: *const c_char) -> &'a str {
    let c_str = unsafe { CStr::from_ptr(chars) };
    c_str.to_str().unwrap()
}
