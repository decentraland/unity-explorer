use std::ffi::c_char;

use crate::{
    operations::{as_str, user_from},
    server::SegmentServer,
    FfiCallbackFn, FfiErrorCallbackFn, OperationHandleId, INVALID_OPERATION_HANDLE_ID,
    SEGMENT_SERVER,
};

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_initialize(
    queue_file_path: *const c_char,
    queue_count_limit: u32,
    segment_write_key: *const c_char,
    callback_fn: FfiCallbackFn,
    error_fn: FfiErrorCallbackFn,
) -> bool {
    let queue_file_path = as_str(queue_file_path).to_string();
    let write_key = as_str(segment_write_key).to_string();
    SEGMENT_SERVER.initialize(
        queue_file_path,
        queue_count_limit,
        write_key,
        callback_fn,
        error_fn,
    )
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
pub extern "C" fn segment_server_dispose() -> bool {
    SEGMENT_SERVER.dispose()
}
