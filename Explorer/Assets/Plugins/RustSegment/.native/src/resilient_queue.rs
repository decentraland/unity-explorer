use std::path::{Path, PathBuf};

use rusqlite::ErrorCode;
use segment::{
    queue::event_queue::{
        AnalyticsEvent, AnalyticsEventQueue, CombinedAnalyticsEventQueue, EnqueError,
        InMemoryAnalyticsEventQueue, PeekError, PersistentAnalyticsEventQueue,
    },
    Message,
};

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

/// Why the queue file was given up for the rest of the session.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DegradeReason {
    DiskFull,
    ReadOnly,
    IoError,
    PermissionDenied,
    CannotOpen,
    /// The file was corrupt a second time, or could not be recreated.
    Corrupt,
}

/// A change in how the queue keeps its events.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum QueueHealth {
    /// Another process owns the queue file; the events of this process stay in memory.
    OwnedByAnotherInstance,
    /// The queue file was corrupt and has been replaced by an empty one.
    Recreated,
    /// The queue file is given up for the rest of the session; the events stay in memory.
    DegradedToMemory(DegradeReason),
}

pub type HealthObserver = Box<dyn Fn(QueueHealth) + Send + Sync>;

/// A persistent queue that keeps the session going when its file cannot be used: a file owned by another process or an
/// unwritable one is replaced by an in-memory queue, a corrupt one is recreated once. Each switch is reported to the observer.
pub struct ResilientAnalyticsEventQueue {
    inner: CombinedAnalyticsEventQueue,
    path: PathBuf,
    event_count_limit: u32,
    recreated: bool,
    on_health: HealthObserver,
}

impl ResilientAnalyticsEventQueue {
    /// `Err` carries a failure that says nothing about the file or its environment.
    pub fn new<P: AsRef<Path>>(
        path: P,
        event_count_limit: u32,
        on_health: HealthObserver,
    ) -> Result<Self, rusqlite::Error> {
        let mut queue = Self {
            inner: in_memory(event_count_limit),
            path: path.as_ref().to_path_buf(),
            event_count_limit,
            recreated: false,
            on_health,
        };

        match PersistentAnalyticsEventQueue::new(&queue.path, event_count_limit) {
            Ok(persistent) => queue.inner = CombinedAnalyticsEventQueue::Persistent(persistent),
            Err(e) => match SqliteCondition::of(&e) {
                SqliteCondition::OwnedByAnotherInstance => {
                    queue.report(QueueHealth::OwnedByAnotherInstance)
                }
                SqliteCondition::Corrupt => queue.recover_from_corruption(),
                SqliteCondition::DiskFull => queue.degrade(DegradeReason::DiskFull),
                SqliteCondition::ReadOnly => queue.degrade(DegradeReason::ReadOnly),
                SqliteCondition::IoError => queue.degrade(DegradeReason::IoError),
                SqliteCondition::PermissionDenied => queue.degrade(DegradeReason::PermissionDenied),
                SqliteCondition::CannotOpen => queue.degrade(DegradeReason::CannotOpen),
                SqliteCondition::Other => return Err(e),
            },
        }

        Ok(queue)
    }

    /// A queue without a file. It never reports a health change.
    pub fn in_memory(event_count_limit: u32, on_health: HealthObserver) -> Self {
        Self {
            inner: in_memory(event_count_limit),
            path: PathBuf::new(),
            event_count_limit,
            recreated: true,
            on_health,
        }
    }

    pub fn is_persistent(&self) -> bool {
        matches!(self.inner, CombinedAnalyticsEventQueue::Persistent(_))
    }

    fn report(&self, health: QueueHealth) {
        (self.on_health)(health);
    }

    fn degrade(&mut self, reason: DegradeReason) {
        self.inner = in_memory(self.event_count_limit);
        self.report(QueueHealth::DegradedToMemory(reason));
    }

    /// The connection is dropped before the file is touched: its exclusive lock would keep the file from being deleted on Windows.
    fn recover_from_corruption(&mut self) {
        self.inner = in_memory(self.event_count_limit);

        if self.recreated {
            self.report(QueueHealth::DegradedToMemory(DegradeReason::Corrupt));
            return;
        }

        self.recreated = true;

        match self.recreate_file() {
            Ok(persistent) => {
                self.inner = CombinedAnalyticsEventQueue::Persistent(persistent);
                self.report(QueueHealth::Recreated);
            }
            Err(_) => self.report(QueueHealth::DegradedToMemory(DegradeReason::Corrupt)),
        }
    }

    fn recreate_file(&self) -> Result<PersistentAnalyticsEventQueue, rusqlite::Error> {
        for suffix in ["", "-journal", "-wal", "-shm"] {
            let mut name = self.path.as_os_str().to_owned();
            name.push(suffix);
            // A file that cannot be removed makes the open below fail, which is reported there.
            let _ = std::fs::remove_file(&name);
        }

        PersistentAnalyticsEventQueue::new(&self.path, self.event_count_limit)
    }

    /// Whether the failure was absorbed by switching the storage, so the operation can be retried on the new storage.
    fn absorb(&mut self, error: &rusqlite::Error) -> bool {
        match SqliteCondition::of(error) {
            SqliteCondition::Corrupt => self.recover_from_corruption(),
            SqliteCondition::ReadOnly => self.degrade(DegradeReason::ReadOnly),
            SqliteCondition::IoError => self.degrade(DegradeReason::IoError),
            SqliteCondition::PermissionDenied => self.degrade(DegradeReason::PermissionDenied),
            SqliteCondition::CannotOpen => self.degrade(DegradeReason::CannotOpen),
            SqliteCondition::OwnedByAnotherInstance
            | SqliteCondition::DiskFull
            | SqliteCondition::Other => return false,
        }

        true
    }
}

impl AnalyticsEventQueue for ResilientAnalyticsEventQueue {
    /// A full disk keeps the file and fails the write; the error tells the caller so.
    fn enque(&mut self, msg: Message) -> Result<(), EnqueError> {
        if !self.is_persistent() {
            return self.inner.enque(msg);
        }

        let retry = msg.clone();

        match self.inner.enque(msg) {
            Err(EnqueError::Sqlite(e)) if self.absorb(&e) => self.inner.enque(retry),
            result => result,
        }
    }

    fn peek(&mut self) -> Result<Option<AnalyticsEvent>, PeekError> {
        match self.inner.peek() {
            // The storage that replaced the file holds nothing yet.
            Err(PeekError::Sqlite(e)) if self.absorb(&e) => Ok(None),
            result => result,
        }
    }

    fn consume(&mut self, id: u64) {
        self.inner.consume(id);
    }
}

fn in_memory(event_count_limit: u32) -> CombinedAnalyticsEventQueue {
    CombinedAnalyticsEventQueue::InMemory(InMemoryAnalyticsEventQueue::new(event_count_limit))
}

#[cfg(test)]
mod tests {
    use super::*;
    use segment::message::{Track, User};
    use std::sync::{Arc, Mutex};

    type Healths = Arc<Mutex<Vec<QueueHealth>>>;

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

    fn observer() -> (Healths, HealthObserver) {
        let healths: Healths = Arc::new(Mutex::new(Vec::new()));
        let sink = healths.clone();
        (
            healths,
            Box::new(move |health| sink.lock().unwrap().push(health)),
        )
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
        let (healths, on_health) = observer();

        let queue = ResilientAnalyticsEventQueue::new(dir.file(), 10, on_health).unwrap();

        assert!(queue.is_persistent());
        assert!(healths.lock().unwrap().is_empty());
    }

    #[test]
    fn keeps_events_in_memory_when_another_instance_owns_the_file() {
        let dir = TempDir::new("owned");
        let _owner = PersistentAnalyticsEventQueue::new(dir.file(), 10).unwrap();
        let (healths, on_health) = observer();

        let mut queue = ResilientAnalyticsEventQueue::new(dir.file(), 10, on_health).unwrap();
        queue.enque(track("a")).unwrap();

        assert!(!queue.is_persistent());
        assert_eq!(
            *healths.lock().unwrap(),
            vec![QueueHealth::OwnedByAnotherInstance]
        );
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }

    #[test]
    fn recreates_a_corrupt_file_once() {
        let dir = TempDir::new("corrupt");
        std::fs::write(
            dir.file(),
            b"garbage instead of a database header and pages",
        )
        .unwrap();
        let (healths, on_health) = observer();

        let mut queue = ResilientAnalyticsEventQueue::new(dir.file(), 10, on_health).unwrap();
        queue.enque(track("a")).unwrap();

        assert!(queue.is_persistent());
        assert_eq!(*healths.lock().unwrap(), vec![QueueHealth::Recreated]);
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }

    #[test]
    fn gives_up_the_file_on_a_second_corruption() {
        let dir = TempDir::new("corrupt-twice");
        std::fs::write(
            dir.file(),
            b"garbage instead of a database header and pages",
        )
        .unwrap();
        let (healths, on_health) = observer();
        let mut queue = ResilientAnalyticsEventQueue::new(dir.file(), 10, on_health).unwrap();

        assert!(queue.absorb(&sqlite_error(rusqlite::ffi::SQLITE_CORRUPT)));
        queue.enque(track("a")).unwrap();

        assert!(!queue.is_persistent());
        assert_eq!(
            *healths.lock().unwrap(),
            vec![
                QueueHealth::Recreated,
                QueueHealth::DegradedToMemory(DegradeReason::Corrupt)
            ]
        );
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }

    #[test]
    fn degrades_to_memory_when_the_file_cannot_be_created() {
        let dir = TempDir::new("cannot-open");
        let path = dir.0.join("missing-directory").join("q.sqlite3");
        let (healths, on_health) = observer();

        let mut queue = ResilientAnalyticsEventQueue::new(path, 10, on_health).unwrap();
        queue.enque(track("a")).unwrap();

        assert!(!queue.is_persistent());
        assert_eq!(
            *healths.lock().unwrap(),
            vec![QueueHealth::DegradedToMemory(DegradeReason::CannotOpen)]
        );
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "a");
    }

    #[test]
    fn a_full_disk_is_not_absorbed() {
        let dir = TempDir::new("full");
        let (healths, on_health) = observer();
        let mut queue = ResilientAnalyticsEventQueue::new(dir.file(), 10, on_health).unwrap();

        assert!(!queue.absorb(&sqlite_error(rusqlite::ffi::SQLITE_FULL)));
        assert!(queue.is_persistent());
        assert!(healths.lock().unwrap().is_empty());
    }

    #[test]
    fn an_in_memory_queue_never_reports() {
        let (healths, on_health) = observer();

        let mut queue = ResilientAnalyticsEventQueue::in_memory(2, on_health);
        queue.enque(track("a")).unwrap();
        queue.enque(track("b")).unwrap();
        queue.enque(track("c")).unwrap();

        assert!(!queue.is_persistent());
        assert!(healths.lock().unwrap().is_empty());
        assert_eq!(event_name(&queue.peek().unwrap().unwrap()), "c");
    }
}
