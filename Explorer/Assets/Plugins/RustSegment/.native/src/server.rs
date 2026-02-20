use anyhow::{anyhow, Context};
use core::str;
use futures::future::FutureExt;
use std::{
    ffi::CString,
    panic::AssertUnwindSafe,
    sync::{
        atomic::{AtomicU64, Ordering},
        Arc, Weak,
    },
};
use tokio::sync::Mutex;

use segment::{
    message::{BatchMessage, User},
    queue::{
        event_queue::{CombinedAnalyticsEventQueue, CombinedAnalyticsEventQueueNewResult},
        event_send_daemon::AnalyticsEventSendDaemon,
    },
    Client, HttpClient,
};

use std::future::Future;
use std::marker::Send;

use crate::{operations, FfiCallbackFn, FfiErrorCallbackFn, OperationHandleId, Response};
use segment::queue::queued_batcher::QueuedBatcher;

pub struct AppContext {
    pub batcher: QueuedBatcher,
    pub queue: Arc<Mutex<CombinedAnalyticsEventQueue>>,
    pub daemon: Arc<Mutex<AnalyticsEventSendDaemon<segment::HttpClient>>>,
    pub segment_client: segment::HttpClient,
    pub write_key: String,
    callback_fn: Box<dyn Fn(OperationHandleId, Response) + Send + Sync>,
    error_fn: Box<dyn Fn(&str) + Send + Sync>,
}

pub struct SegmentServer {
    context: Arc<Mutex<AppContext>>,
    next_id: AtomicU64,
}

pub enum ServerState {
    Ready(Arc<SegmentServer>, tokio::runtime::Runtime, CallbacksBundle),
    Disposed,
}

pub struct Server {
    state: std::sync::Mutex<ServerState>,
}

impl Default for Server {
    fn default() -> Self {
        Self {
            state: std::sync::Mutex::new(ServerState::Disposed),
        }
    }
}

impl Server {
    pub fn initialize(
        &self,
        queue_file_path: String,
        queue_count_limit: u32,
        writer_key: String,
        callback_fn: FfiCallbackFn,
        error_fn: Option<FfiErrorCallbackFn>,
    ) -> bool {
        let state_lock = self.state.lock();
        if state_lock.is_err() {
            return false;
        }

        let mut state = state_lock.unwrap();

        match *state {
            ServerState::Ready(_, _, _) => false,
            ServerState::Disposed => {
                let new_runtime = tokio::runtime::Builder::new_multi_thread()
                    .enable_all()
                    .build()
                    .unwrap();

                let callbacks_bundle = CallbacksBundle::new(callback_fn, error_fn);

                let server = SegmentServer::new(
                    queue_file_path,
                    queue_count_limit,
                    writer_key,
                    callbacks_bundle.callback_fn(),
                    callbacks_bundle.error_fn(),
                    &new_runtime,
                );

                *state = ServerState::Ready(Arc::new(server), new_runtime, callbacks_bundle);
                true
            }
        }
    }

    pub fn dispose(&self) -> bool {
        let state_lock = self.state.lock();
        if state_lock.is_err() {
            return false;
        }

        let mut state = state_lock.unwrap();
        let extracted = std::mem::replace(&mut *state, ServerState::Disposed);

        match extracted {
            ServerState::Disposed => false,
            ServerState::Ready(_, runtime, callbacks_bundle) => {
                drop(callbacks_bundle);
                runtime.shutdown_background();
                *state = ServerState::Disposed;
                true
            }
        }
    }

    pub fn try_execute<
        F: FnOnce(Arc<SegmentServer>, OperationHandleId) -> Fut,
        Fut: Future + Send + 'static,
    >(
        &self,
        func: F, // returns a task to schedule
    ) -> OperationHandleId
    where
        <Fut as Future>::Output: Send,
    {
        let state_lock = self.state.lock();
        if state_lock.is_err() {
            return 0;
        }

        let state = state_lock.unwrap();

        match &*state {
            ServerState::Disposed => 0,
            ServerState::Ready(server, runtime, _) => {
                let id = server.next_id();
                let future_task = func(server.clone(), id);
                let future_task = AssertUnwindSafe(future_task);
                runtime.spawn(future_task.catch_unwind());
                id
            }
        }
    }
}

pub struct CallbacksBundle {
    callback_fn: Arc<FfiCallbackFn>,
    error_fn: Arc<Option<FfiErrorCallbackFn>>,
}

impl CallbacksBundle {
    pub fn new(callback_fn: FfiCallbackFn, error_fn: Option<FfiErrorCallbackFn>) -> Self {
        Self {
            callback_fn: Arc::new(callback_fn),
            error_fn: Arc::new(error_fn),
        }
    }

    pub fn callback_fn(&self) -> Weak<FfiCallbackFn> {
        Arc::downgrade(&self.callback_fn)
    }

    pub fn error_fn(&self) -> Weak<Option<FfiErrorCallbackFn>> {
        Arc::downgrade(&self.error_fn)
    }
}

impl SegmentServer {
    pub fn next_id(&self) -> OperationHandleId {
        self.next_id.fetch_add(1, Ordering::Relaxed)
    }

    fn new(
        queue_file_path: String,
        queue_count_limit: u32,
        writer_key: String,
        callback_fn: Weak<FfiCallbackFn>,
        error_fn: Weak<Option<FfiErrorCallbackFn>>,
        async_runtime: &tokio::runtime::Runtime,
    ) -> Self {
        let error_fn = Box::new(move |message: &str| {
            if let Some(upgrade) = error_fn.upgrade() {
                if let Some(cb) = *upgrade {
                    if let Ok(cstr) = CString::new(message) {
                        unsafe {
                            cb(cstr.as_ptr());
                        }
                    };
                };
            };
        });

        let event_queue: CombinedAnalyticsEventQueueNewResult =
            CombinedAnalyticsEventQueue::new(queue_file_path, Some(queue_count_limit));
        let event_queue: CombinedAnalyticsEventQueue = match event_queue {
            CombinedAnalyticsEventQueueNewResult::Persistent(e) => e,
            CombinedAnalyticsEventQueueNewResult::FallbackToInMemory(e, err) => {
                let error_message =
                    format!("Cannot create persistent queue, fallback to in memory queue: {err}");
                error_fn(&error_message);
                e
            }
        };
        let event_queue = Arc::new(Mutex::new(event_queue));

        let queue_batcher = QueuedBatcher::new(event_queue.clone(), None);

        let client = HttpClient::default();
        let send_daemon =
            AnalyticsEventSendDaemon::new(event_queue.clone(), None, writer_key.clone(), client);
        let send_daemon = Arc::new(Mutex::new(send_daemon));

        let moved_error_fn = error_fn.clone();
        let moved_send_daemon = send_daemon.clone();
        async_runtime.spawn(
            AssertUnwindSafe(async move {
                let mut guard = moved_send_daemon.lock().await;
                guard.start(moved_error_fn);
            })
            .catch_unwind(),
        );
        let direct_client = HttpClient::default();

        let context = AppContext {
            batcher: queue_batcher,
            queue: event_queue,
            daemon: send_daemon,
            segment_client: direct_client,
            write_key: writer_key,
            callback_fn: Box::new(move |id, response| unsafe {
                if let Some(upgrade) = callback_fn.upgrade() {
                    upgrade(id, response);
                }
            }),
            error_fn,
        };

        Self {
            next_id: AtomicU64::new(1), //0 is invalid,
            context: Arc::new(Mutex::new(context)),
        }
    }

    pub async fn instant_track_and_flush(
        instance: Arc<Self>,
        id: OperationHandleId,
        user: User,
        event_name: &str,
        properties_json: &str,
        context_json: &str,
    ) {
        let msg = operations::new_track(user.clone(), event_name, properties_json, context_json)
            .with_context(|| format!("Cannot create new track: id - {id}, user - {user}, event_name - {event_name}, properties_json - {properties_json}, context_json - {context_json}"));
        match msg {
            Ok(m) => {
                let context = instance.context.lock().await;
                let key = context.write_key.clone();
                match context.segment_client.send(key, m.into()).await {
                    Ok(()) => {
                        context.report_success(id);
                    }
                    Err(e) => {
                        context.report_error(Some(id), e.to_string());
                    }
                }
            }
            Err(e) => {
                instance
                    .context
                    .lock()
                    .await
                    .report_error(Some(id), e.to_string());
            }
        }
    }

    pub async fn enqueue_track(
        instance: Arc<Self>,
        id: OperationHandleId,
        user: User,
        event_name: &str,
        properties_json: &str,
        context_json: &str,
    ) {
        let msg = operations::new_track(user.clone(), event_name, properties_json, context_json)
            .with_context(|| {
                format!("Failed new_track: {user}, {event_name}, {properties_json}, {context_json}")
            });
        instance.try_enqueue(id, msg).await;
    }

    pub async fn enqueue_identify(
        instance: Arc<Self>,
        id: OperationHandleId,
        user: User,
        traits_json: &str,
        context_json: &str,
    ) {
        let msg = operations::new_identify(user.clone(), traits_json, context_json)
            .with_context(|| format!("Failed new_track: {user}, {traits_json}, {context_json}"));
        instance.try_enqueue(id, msg).await;
    }

    pub async fn enqueue(&self, id: OperationHandleId, msg: impl Into<BatchMessage>) {
        if let Err(e) = self.enqueue_internal(msg).await {
            self.context
                .lock()
                .await
                .report_error(Some(id), format!("Cannot enqueue: {e}"));
        } else {
            self.context.lock().await.report_success(id);
        }
    }

    async fn enqueue_internal(&self, msg: impl Into<BatchMessage>) -> anyhow::Result<()> {
        let mut context = self.context.lock().await;
        match context.batcher.push(msg) {
            Ok(option) => {
                // if something returned then it has not been enqued
                if let Some(msg) = option {
                    context.batcher.flush().await?;
                    context
                        .batcher
                        .push(msg)
                        .context("Cannot push message even after flush:")?;
                }
                Ok(())
            }
            Err(e) => Err(anyhow!("Cannot push message to batcher: {e}")),
        }
    }

    pub async fn flush(instance: Arc<Self>, id: OperationHandleId) {
        let mut context = instance.context.lock().await;

        if let Err(e) = context.batcher.flush().await {
            context.report_error(Some(id), format!("Cannot flush: {e}"));
        } else {
            context.report_success(id);
        }
    }

    async fn try_enqueue(
        &self,
        id: OperationHandleId,
        msg: anyhow::Result<impl Into<BatchMessage>>,
    ) {
        match msg {
            Ok(m) => self.enqueue(id, m).await,
            Err(e) => {
                self.context
                    .lock()
                    .await
                    .report_error(Some(id), e.to_string());
            }
        }
    }
}

impl AppContext {
    pub fn report_success(&self, id: OperationHandleId) {
        self.callback_fn.as_ref()(id, Response::Success);
    }

    pub fn report_error(&self, id: Option<OperationHandleId>, message: String) {
        let message = match id {
            Some(id) => {
                format!("Operation {id} failed: {message}")
            }
            None => message,
        };
        self.error_fn.as_ref()(message.as_str());

        if let Some(id) = id {
            self.callback_fn.as_ref()(id, Response::Error);
        };
    }
}
