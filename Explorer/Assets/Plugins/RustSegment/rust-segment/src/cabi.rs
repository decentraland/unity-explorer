use core::str;
use time::OffsetDateTime;

use segment::message::{Identify, Track, User};

use crate::{server::SegmentServer, FfiCallbackFn, OperationHandleId, Response, SEGMENT_SERVER};

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_initialize(
    segment_write_key: *const u8,
    segment_write_key_len: usize,
    callback_fn: FfiCallbackFn,
) -> bool {
    let write_key = as_utf8_array(segment_write_key, segment_write_key_len);

    let key_string = match str::from_utf8(write_key) {
        Ok(result) => result,
        Err(_) => return false,
    };

    SEGMENT_SERVER.initialize(key_string, callback_fn);
    true
}

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn segment_server_identify(
    used_id: *const u8,
    user_id_len: usize,
    traits_json: *const u8,
    traits_json_len: usize,
    context_json: *const u8,
    context_json_len: usize,
) -> OperationHandleId {
    let id = SEGMENT_SERVER.next_id();

    let user = as_utf8_array(used_id, user_id_len);
    let traits = as_utf8_array(traits_json, traits_json_len);
    let context_json = as_utf8_array(context_json, context_json_len);

    let operation = async move {
        let arc = SEGMENT_SERVER.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let str_user_id = match str::from_utf8(user) {
            Ok(result) => result,
            Err(_) => {
                context.call_callback(id, Response::ErrorUtf8Decode);
                return;
            }
        };
        let msg = Identify {
            user: User::UserId {
                user_id: str_user_id.to_string(),
            },
            traits: serde_json::from_slice(traits).unwrap(),
            context: serde_json::from_slice(context_json).unwrap(),
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
    used_id: *const u8,
    user_id_len: usize,
    event_name: *const u8,
    event_name_len: usize,
    properties_json: *const u8,
    properties_json_len: usize,
    context_json: *const u8,
    context_json_len: usize,
) -> OperationHandleId {
    let id = SEGMENT_SERVER.next_id();

    let user = as_utf8_array(used_id, user_id_len);
    let event_name = as_utf8_array(event_name, event_name_len);
    let properties_json = as_utf8_array(properties_json, properties_json_len);
    let context_json = as_utf8_array(context_json, context_json_len);

    let operation = async move {
        let arc = SEGMENT_SERVER.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let str_user_id = match str::from_utf8(user) {
            Ok(result) => result,
            Err(_) => {
                context.call_callback(id, Response::ErrorUtf8Decode);
                return;
            }
        };

        let str_event_name = match str::from_utf8(event_name) {
            Ok(result) => result,
            Err(_) => {
                context.call_callback(id, Response::ErrorUtf8Decode);
                return;
            }
        };

        let msg = Track {
            user: User::UserId {
                user_id: str_user_id.to_string(),
            },
            event: str_event_name.to_string(),
            properties: serde_json::from_slice(properties_json).unwrap(),
            context: serde_json::from_slice(context_json).unwrap(),
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

fn as_utf8_array<'a>(content: *const u8, len: usize) -> &'a [u8] {
    return unsafe { std::slice::from_raw_parts(content, len) };
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
