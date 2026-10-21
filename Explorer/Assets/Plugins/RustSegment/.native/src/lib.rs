use lazy_static::lazy_static;
use std::os::raw::c_char;

pub mod cabi;
pub mod operations;
pub mod queue_file;
pub mod server;

pub type OperationHandleId = u64;

pub const INVALID_OPERATION_HANDLE_ID: OperationHandleId = 0;

#[repr(u8)]
#[derive(Debug, Clone, Copy)]
pub enum Response {
    Success = 0,
    // Errors are propagated vie the error callback
    Error = 1,
    // SQLITE_FULL from the persistent queue
    ErrorDiskFull = 2,
}

/// # SAFTEY: The "C" callback must be threadsafe and not block
pub type FfiCallbackFn = unsafe extern "C" fn(OperationHandleId, Response);

/// # SAFTEY: The "C" callback must be threadsafe and not block
pub type FfiErrorCallbackFn = unsafe extern "C" fn(msg: *const c_char);

/// How the queue keeps the events of the session, decided once when the server is initialized and returned by
/// `segment_server_initialize`. The values are part of the C ABI.
#[repr(u8)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum QueueHealthCode {
    /// This process owns the queue file; the events are kept on disk.
    Persistent = 0,
    /// Another process owns the queue file; the events of this process stay in memory.
    OwnedByAnotherInstance = 1,
    /// The queue file was corrupt and has been replaced by an empty one.
    Recreated = 2,
    /// The queue file is given up for the session: the disk is full.
    DegradedDiskFull = 3,
    /// The queue file is given up for the session: it cannot be written.
    DegradedReadOnly = 4,
    /// The queue file is given up for the session: the disk failed.
    DegradedIoError = 5,
    /// The queue file is given up for the session: the OS refused access.
    DegradedPermissionDenied = 6,
    /// The queue file is given up for the session: it cannot be opened.
    DegradedCannotOpen = 7,
    /// The queue file is given up for the session: it could not be recreated after it was found corrupt.
    DegradedCorrupt = 8,
    /// The queue file is given up for the session: it failed in a way that says nothing about the file or its
    /// environment, reported on the error channel.
    DegradedOther = 9,
    /// The server is not running: it was already initialized, or the initialization panicked and the panic was
    /// reported on the error channel.
    NotInitialized = 255,
}

/// The foreign functions the server reports through.
#[derive(Clone, Copy)]
pub struct FfiCallbacks {
    pub callback_fn: FfiCallbackFn,
    pub error_fn: Option<FfiErrorCallbackFn>,
}

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
    #[ignore = "needs SEGMENT_WRITE_KEY/SEGMENT_QUEUE_PATH and network access; see README - Testing"]
    fn test_integration() {
        let write_key = std::env::var("SEGMENT_WRITE_KEY").unwrap();
        let persistent_path = std::env::var("SEGMENT_QUEUE_PATH").unwrap();
        let api_host = std::env::var("SEGMENT_API_HOST").unwrap();

        SEGMENT_SERVER.initialize(
            persistent_path,
            100,
            write_key,
            api_host,
            FfiCallbacks {
                callback_fn: test_callback,
                error_fn: None,
            },
        );
        SEGMENT_SERVER.try_execute(|segment, id| {
            SegmentServer::enqueue_track(
                segment.clone(),
                id,
                segment::message::User::default(),
                "rust_check",
                "{}",
                "{}",
            )
        });
        SEGMENT_SERVER.try_execute(|segment, id| SegmentServer::flush(segment.clone(), id));
    }

    unsafe extern "C" fn test_callback(id: OperationHandleId, response: Response) {
        info!("id: {id}, response: {response:?}");
    }
}
