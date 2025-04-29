use std::ffi::c_char;

use crate::{
    operations::{as_str, user_from},
    server::SegmentServer,
    FfiCallbackFn, OperationHandleId, INVALID_OPERATION_HANDLE_ID, SEGMENT_SERVER,
};

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
    anon_id: *const c_char,
    traits_json: *const c_char,
    context_json: *const c_char,
) -> OperationHandleId {
    let user = user_from(used_id, anon_id);

    if user.is_none() {
        return INVALID_OPERATION_HANDLE_ID;
    }

    let user = user.unwrap();

    SEGMENT_SERVER.try_execute(&|segment, id| {
        let user = user.clone();
        let segment = segment.clone();
        let traits_json = as_str(traits_json);
        let context_json = as_str(context_json);

        let operation =
            SegmentServer::enqueue_identify(segment, id, user, traits_json, context_json);
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_track(
    used_id: *const c_char,
    anon_id: *const c_char,
    event_name: *const c_char,
    properties_json: *const c_char,
    context_json: *const c_char,
) -> OperationHandleId {
    let user = user_from(used_id, anon_id);

    if user.is_none() {
        return INVALID_OPERATION_HANDLE_ID;
    }

    let user = user.unwrap();

    SEGMENT_SERVER.try_execute(&|segment, id| {
        let user = user.clone();
        let segment = segment.clone();
        let event_name = as_str(event_name);
        let properties_json = as_str(properties_json);
        let context_json = as_str(context_json);

        let operation = SegmentServer::enqueue_track(
            segment,
            id,
            user,
            event_name,
            properties_json,
            context_json,
        );
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_instant_track_and_flush(
    used_id: *const c_char,
    anon_id: *const c_char,
    event_name: *const c_char,
    properties_json: *const c_char,
    context_json: *const c_char,
) -> OperationHandleId {
    let user = user_from(used_id, anon_id);

    if user.is_none() {
        return INVALID_OPERATION_HANDLE_ID;
    }

    let user = user.unwrap();

    SEGMENT_SERVER.try_execute(&|segment, id| {
        let user = user.clone();
        let segment = segment.clone();
        let event_name = as_str(event_name);
        let properties_json = as_str(properties_json);
        let context_json = as_str(context_json);

        let operation = SegmentServer::instant_track_and_flush(
            segment,
            id,
            user,
            event_name,
            properties_json,
            context_json,
        );
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

#[no_mangle]
pub extern "C" fn segment_server_flush() -> OperationHandleId {
    SEGMENT_SERVER.try_execute(&|segment, id| {
        let segment = segment.clone();
        let operation = SegmentServer::flush(segment, id);
        SEGMENT_SERVER.async_runtime.spawn(operation);
    })
}

#[no_mangle]
pub extern "C" fn segment_server_unflushed_batches_count() -> u64 {
    SEGMENT_SERVER.unflushed_batches_count()
}

#[no_mangle]
pub extern "C" fn segment_server_dispose() -> bool {
    SEGMENT_SERVER.dispose()
}
