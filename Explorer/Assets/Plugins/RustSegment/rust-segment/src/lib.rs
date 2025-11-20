use lazy_static::lazy_static;
use std::os::raw::c_char;

pub mod cabi;
pub mod operations;
pub mod server;

pub type OperationHandleId = u64;

pub const INVALID_OPERATION_HANDLE_ID: OperationHandleId = 0;

#[repr(u8)]
#[derive(Debug)]
pub enum Response {
    Success = 0,
    // Errors are propagated vie the error callback
    Error = 1,
}

/// # SAFTEY: The "C" callback must be threadsafe and not block
pub type FfiCallbackFn = unsafe extern "C" fn(OperationHandleId, Response);

/// # SAFTEY: The "C" callback must be threadsafe and not block
pub type FfiErrorCallbackFn = unsafe extern "C" fn(msg: *const c_char);

lazy_static! {
    pub static ref SEGMENT_SERVER: server::Server = server::Server::default();
}

#[cfg(test)]
mod tests {

    use server::SegmentServer;

    use super::*;

    #[warn(unused_imports)]
    use std::{println as info, println as warn};

    #[test]
    fn test_integration() {
        let write_key = std::env::var("SEGMENT_WRITE_KEY").unwrap();
        let persistent_path = std::env::var("SEGMENT_QUEUE_PATH").unwrap();

        SEGMENT_SERVER.initialize(
            persistent_path,
            100,
            write_key,
            test_callback,
            error_callback,
        );
        SEGMENT_SERVER.try_execute(&|segment, id| {
            let operation = SegmentServer::enqueue_track(
                segment,
                id,
                segment::message::User::default(),
                "rust_check",
                "{}",
                "{}",
            );
            SEGMENT_SERVER.async_runtime.block_on(operation);
        });
        SEGMENT_SERVER.try_execute(&|segment, id| {
            let operation = SegmentServer::flush(segment, id);
            SEGMENT_SERVER.async_runtime.block_on(operation);
        });
    }

    unsafe extern "C" fn test_callback(id: OperationHandleId, response: Response) {
        info!("id: {id}, response: {response:?}");
    }

    unsafe extern "C" fn error_callback(_: *const c_char) {
        // ignore
    }
}
