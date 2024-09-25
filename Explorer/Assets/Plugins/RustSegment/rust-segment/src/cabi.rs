use core::str;
use std::ffi::{c_char, CStr};
use time::OffsetDateTime;

use segment::message::{Identify, Track, User};

use crate::{server::SegmentServer, FfiCallbackFn, OperationHandleId, Response, SEGMENT_SERVER};

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

    let user = as_str(used_id);
    let traits = as_str(traits_json);
    let context_json = as_str(context_json);

    let operation = async move {
        let arc = SEGMENT_SERVER.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let msg = Identify {
            user: User::UserId {
                user_id: user.to_string(),
            },
            traits: serde_json::from_str(traits).unwrap(),
            context: serde_json::from_str(context_json).unwrap(),
            timestamp: Some(OffsetDateTime::now_utc()),
            ..Default::default()
        };

        let result = context.batcher.push(msg).await;

        let response_code = as_response_code(result);
        context.call_callback(id, response_code);
    };

    SEGMENT_SERVER.dispatch_operation(id, operation)
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

    let user = as_str(used_id);
    let event_name = as_str(event_name);
    let properties_json = as_str(properties_json);
    let context_json = as_str(context_json);

    let operation = async move {
        let arc = SEGMENT_SERVER.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let msg = Track {
            user: User::UserId {
                user_id: user.to_string(),
            },
            event: event_name.to_string(),
            properties: serde_json::from_str(properties_json).unwrap(),
            context: serde_json::from_str(context_json).unwrap(),
            timestamp: Some(OffsetDateTime::now_utc()),
            ..Default::default()
        };

        let result = context.batcher.push(msg).await;

        let response_code = as_response_code(result);
        context.call_callback(id, response_code);
    };

    SEGMENT_SERVER.dispatch_operation(id, operation)
}

#[no_mangle]
pub unsafe extern "C" fn segment_server_flush() -> OperationHandleId {
    let id = SEGMENT_SERVER.next_id();

    let operation = async move {
        let arc = SEGMENT_SERVER.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let result = context.batcher.flush().await;

        let response_code = as_response_code(result);
        context.call_callback(id, response_code);
    };

    SEGMENT_SERVER.dispatch_operation(id, operation)
}

fn as_str<'a>(chars: *const c_char) -> &'a str {
    let c_str = unsafe { CStr::from_ptr(chars) };
    c_str.to_str().unwrap()
}

fn as_response_code(result: Result<(), segment::Error>) -> Response {
    return match result {
        Ok(_) => Response::Success,
        Err(error) => match error {
            segment::Error::MessageTooLarge => Response::ErrorMessageTooLarge,
            segment::Error::DeserializeError(_) => Response::ErrorDeserialize,
            segment::Error::NetworkError(_) => Response::ErrorNetwork,
        },
    };
}
