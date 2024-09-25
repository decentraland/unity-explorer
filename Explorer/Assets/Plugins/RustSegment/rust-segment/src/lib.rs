pub mod cabi;
pub mod server;

use lazy_static::lazy_static;

pub type OperationHandleId = u64;

#[repr(u8)]
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
    pub static ref SEGMENT_SERVER: server::SegmentServer = server::SegmentServer::default();
}
