pub mod cabi;
pub mod operations;
pub mod server;

use lazy_static::lazy_static;

pub type OperationHandleId = u64;

#[repr(u8)]
#[derive(Debug)]
pub enum Response {
    Success = 0,
    FailInitializedWrongKey = 1,
    ErrorMessageTooLarge = 2,
    ErrorDeserialize = 3,
    ErrorNetwork = 4,
    ErrorUtf8Decode = 5,
}

/// # SAFTEY: The "C" callback must be threadsafe and not block
pub type FfiCallbackFn = unsafe extern "C" fn(OperationHandleId, Response);

lazy_static! {
    pub static ref SEGMENT_SERVER: server::Server = server::Server::default();
}

#[cfg(test)]
mod tests {

    use super::*;
    use std::{println as info, println as warn};

    #[test]
    fn test_integration() {
        let write_key = std::env::var("SEGMENT_WRITE_KEY").unwrap();
        let id: OperationHandleId = 0;

        SEGMENT_SERVER.initialize(write_key.as_str(), test_callback);
        // SEGMENT_SERVER
        //     .async_runtime
        //     .block_on(SEGMENT_SERVER.enqueue_track(id, "id", "rust_check", "{}", "{}"));
        // SEGMENT_SERVER
        //     .async_runtime
        //     .block_on(SEGMENT_SERVER.flush(id));
    }

    unsafe extern "C" fn test_callback(id: OperationHandleId, response: Response) {
        info!("id: {}, response: {:?}", id, response);
    }
}
