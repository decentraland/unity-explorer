use std::path::Path;

use rusqlite::ErrorCode;
use segment::queue::event_queue::{
    CombinedAnalyticsEventQueue, InMemoryAnalyticsEventQueue, PersistentAnalyticsEventQueue,
};

use crate::QueueHealthCode;

/// What a sqlite failure says about the queue file and its environment.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SqliteCondition {
    /// Another connection holds the file lock: another Explorer process opened the queue.
    OwnedByAnotherInstance,
    /// The file is not a readable database.
    Corrupt,
    /// The disk has no room for the write.
    DiskFull,
    /// The file or its directory cannot be written.
    ReadOnly,
    /// The disk failed the read or the write.
    IoError,
    /// The OS refused access to the file.
    PermissionDenied,
    /// The file cannot be opened.
    CannotOpen,
    /// Any other failure.
    Other,
}

impl SqliteCondition {
    pub fn of(error: &rusqlite::Error) -> Self {
        match error.sqlite_error_code() {
            Some(ErrorCode::DatabaseBusy | ErrorCode::DatabaseLocked) => {
                Self::OwnedByAnotherInstance
            }
            Some(ErrorCode::DatabaseCorrupt | ErrorCode::NotADatabase) => Self::Corrupt,
            Some(ErrorCode::DiskFull) => Self::DiskFull,
            Some(ErrorCode::ReadOnly) => Self::ReadOnly,
            Some(ErrorCode::SystemIoFailure) => Self::IoError,
            Some(ErrorCode::PermissionDenied) => Self::PermissionDenied,
            Some(ErrorCode::CannotOpen) => Self::CannotOpen,
            _ => Self::Other,
        }
    }
}

/// Opens the queue file as its owner, deciding once how the session keeps its events: a file owned by another process or
/// an unusable one is replaced by an in-memory queue, a corrupt one is recreated once. The code says which it is.
/// `Err` carries a failure that says nothing about the file or its environment.
pub fn open(
    path: &Path,
    event_count_limit: u32,
) -> Result<(CombinedAnalyticsEventQueue, QueueHealthCode), rusqlite::Error> {
    let error = match PersistentAnalyticsEventQueue::new(path, event_count_limit) {
        Ok(persistent) => {
            return Ok((
                CombinedAnalyticsEventQueue::Persistent(persistent),
                QueueHealthCode::Persistent,
            ))
        }
        Err(error) => error,
    };

    let code = match SqliteCondition::of(&error) {
        SqliteCondition::OwnedByAnotherInstance => QueueHealthCode::OwnedByAnotherInstance,
        SqliteCondition::Corrupt => return Ok(recreate(path, event_count_limit)),
        SqliteCondition::DiskFull => QueueHealthCode::DegradedDiskFull,
        SqliteCondition::ReadOnly => QueueHealthCode::DegradedReadOnly,
        SqliteCondition::IoError => QueueHealthCode::DegradedIoError,
        SqliteCondition::PermissionDenied => QueueHealthCode::DegradedPermissionDenied,
        SqliteCondition::CannotOpen => QueueHealthCode::DegradedCannotOpen,
        SqliteCondition::Other => return Err(error),
    };

    Ok((in_memory(event_count_limit), code))
}

pub fn in_memory(event_count_limit: u32) -> CombinedAnalyticsEventQueue {
    CombinedAnalyticsEventQueue::InMemory(InMemoryAnalyticsEventQueue::new(event_count_limit))
}

/// Replaces a corrupt file by an empty one. The failed connection is already closed: an open one would keep the file from
/// being deleted on Windows.
fn recreate(path: &Path, event_count_limit: u32) -> (CombinedAnalyticsEventQueue, QueueHealthCode) {
    for suffix in ["", "-journal", "-wal", "-shm"] {
        let mut name = path.as_os_str().to_owned();
        name.push(suffix);
        // A file that cannot be removed makes the open below fail.
        let _ = std::fs::remove_file(&name);
    }

    match PersistentAnalyticsEventQueue::new(path, event_count_limit) {
        Ok(persistent) => (
            CombinedAnalyticsEventQueue::Persistent(persistent),
            QueueHealthCode::Recreated,
        ),
        Err(_) => (
            in_memory(event_count_limit),
            QueueHealthCode::DegradedCorrupt,
        ),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use segment::message::{Track, User};
    use segment::queue::event_queue::{AnalyticsEvent, AnalyticsEventQueue};
    use segment::Message;
    use std::path::PathBuf;

    /// A fresh directory under the OS temp dir, removed on drop.
    struct TempDir(PathBuf);

    impl TempDir {
        fn new(name: &str) -> Self {
            let path = std::env::temp_dir().join(format!(
                "rust-segment-{}-{name}-{:?}",
                std::process::id(),
                std::thread::current().id()
            ));
            let _ = std::fs::remove_dir_all(&path);
            std::fs::create_dir_all(&path).unwrap();
            Self(path)
        }

        fn file(&self) -> PathBuf {
            self.0.join("q.sqlite3")
        }
    }

    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    fn track(event: &str) -> Message {
        Message::Track(Track {
            user: User::AnonymousId {
                anonymous_id: "anon".to_owned(),
            },
            event: event.to_owned(),
            timestamp: Some(time::OffsetDateTime::UNIX_EPOCH),
            ..Default::default()
        })
    }

    fn event_name(event: &AnalyticsEvent) -> &str {
        match &event.message {
            Message::Track(track) => &track.event,
            other => panic!("not a track: {other:?}"),
        }
    }

    fn is_persistent(queue: &CombinedAnalyticsEventQueue) -> bool {
        matches!(queue, CombinedAnalyticsEventQueue::Persistent(_))
    }

    fn sqlite_error(code: std::os::raw::c_int) -> rusqlite::Error {
        rusqlite::Error::SqliteFailure(rusqlite::ffi::Error::new(code), None)
    }

    #[test]
    fn sqlite_codes_map_to_conditions() {
        let cases = [
            (
                rusqlite::ffi::SQLITE_BUSY,
                SqliteCondition::OwnedByAnotherInstance,
            ),
            (
                rusqlite::ffi::SQLITE_LOCKED,
                SqliteCondition::OwnedByAnotherInstance,
            ),
            (rusqlite::ffi::SQLITE_CORRUPT, SqliteCondition::Corrupt),
            (rusqlite::ffi::SQLITE_NOTADB, SqliteCondition::Corrupt),
            (rusqlite::ffi::SQLITE_FULL, SqliteCondition::DiskFull),
            (rusqlite::ffi::SQLITE_READONLY, SqliteCondition::ReadOnly),
            (rusqlite::ffi::SQLITE_IOERR, SqliteCondition::IoError),
            (
                rusqlite::ffi::SQLITE_PERM,
                SqliteCondition::PermissionDenied,
            ),
            (rusqlite::ffi::SQLITE_CANTOPEN, SqliteCondition::CannotOpen),
            (rusqlite::ffi::SQLITE_MISUSE, SqliteCondition::Other),
        ];

        for (code, expected) in cases {
            assert_eq!(
                SqliteCondition::of(&sqlite_error(code)),
                expected,
                "code {code}"
            );
        }

        assert_eq!(
            SqliteCondition::of(&rusqlite::Error::QueryReturnedNoRows),
            SqliteCondition::Other
        );
    }

    #[test]
    fn owns_a_free_file() {
        let dir = TempDir::new("free");

        let (queue, code) = open(&dir.file(), 10).unwrap();

        assert!(is_persistent(&queue));
        assert_eq!(code, QueueHealthCode::Persistent);
    }

    #[test]
    fn keeps_events_in_memory_when_another_instance_owns_the_file() {
        let dir = TempDir::new("owned");
        let _owner = PersistentAnalyticsEventQueue::new(dir.file(), 10).unwrap();

        let (mut queue, code) = open(&dir.file(), 10).unwrap();
        queue.enque(track("a")).unwrap();

        assert!(!is_persistent(&queue));
        assert_eq!(code, QueueHealthCode::OwnedByAnotherInstance);
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }

    #[test]
    fn recreates_a_corrupt_file() {
        let dir = TempDir::new("corrupt");
        std::fs::write(
            dir.file(),
            b"garbage instead of a database header and pages",
        )
        .unwrap();

        let (mut queue, code) = open(&dir.file(), 10).unwrap();
        queue.enque(track("a")).unwrap();

        assert!(is_persistent(&queue));
        assert_eq!(code, QueueHealthCode::Recreated);
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }

    #[test]
    fn keeps_events_in_memory_when_the_file_cannot_be_created() {
        let dir = TempDir::new("cannot-open");
        let path = dir.0.join("missing-directory").join("q.sqlite3");

        let (mut queue, code) = open(&path, 10).unwrap();
        queue.enque(track("a")).unwrap();

        assert!(!is_persistent(&queue));
        assert_eq!(code, QueueHealthCode::DegradedCannotOpen);
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }
}
