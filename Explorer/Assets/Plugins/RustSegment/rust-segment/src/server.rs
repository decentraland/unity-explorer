use anyhow::{anyhow, Context};
use core::str;
use std::{
    ffi::CString,
    sync::{
        atomic::{AtomicU64, Ordering},
        Arc, RwLock,
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

use crate::{operations, FfiCallbackFn, FfiErrorCallbackFn, OperationHandleId, Response};
use segment::queue::queued_batcher::QueuedBatcher;

pub struct SafeCallback {
    callback_fn: RwLock<Option<Box<dyn Fn(OperationHandleId, Response) + Send + Sync>>>,
    error_fn: RwLock<Option<Box<dyn Fn(&str) + Send + Sync>>>,
}

impl SafeCallback {
    fn new(
        callback_fn: Box<dyn Fn(OperationHandleId, Response) + Send + Sync>,
        error_fn: Box<dyn Fn(&str) + Send + Sync>,
    ) -> Self {
        Self {
            callback_fn: RwLock::new(Some(callback_fn)),
            error_fn: RwLock::new(Some(error_fn)),
        }
    }

    pub fn call_success(&self, id: OperationHandleId) {
        if let Ok(guard) = self.callback_fn.read() {
            if let Some(ref cb) = *guard {
                cb(id, Response::Success);
            }
        }
    }

    pub fn call_error(&self, message: &str) {
        if let Ok(guard) = self.error_fn.read() {
            if let Some(ref cb) = *guard {
                cb(message);
            }
        }
    }

    pub fn call_error_response(&self, id: OperationHandleId) {
        if let Ok(guard) = self.callback_fn.read() {
            if let Some(ref cb) = *guard {
                cb(id, Response::Error);
            }
        }
    }

    pub fn disable(&self) {
        if let Ok(mut guard) = self.callback_fn.write() {
            *guard = None;
        }
        if let Ok(mut guard) = self.error_fn.write() {
            *guard = None;
        }
    }
}

pub struct AppContext {
    pub batcher: QueuedBatcher,
    pub queue: Arc<Mutex<CombinedAnalyticsEventQueue>>,
    pub daemon: Arc<Mutex<AnalyticsEventSendDaemon<segment::HttpClient>>>,
    pub segment_client: segment::HttpClient,
    pub write_key: String,
    safe_callback: Arc<SafeCallback>,
}

pub struct SegmentServer {
    context: Arc<Mutex<AppContext>>,
    next_id: AtomicU64,
    safe_callback: Arc<SafeCallback>,
}

pub enum ServerState {
    Ready(Arc<SegmentServer>),
    Disposed,
}

pub struct Server {
    state: std::sync::Mutex<ServerState>,
    async_runtime: std::sync::Mutex<Option<tokio::runtime::Runtime>>,
}

impl Default for Server {
    fn default() -> Self {
        Self {
            async_runtime: std::sync::Mutex::new(None),
            state: std::sync::Mutex::new(ServerState::Disposed),
        }
    }
}

impl Server {
    fn create_runtime() -> tokio::runtime::Runtime {
        tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()
            .unwrap()
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
            ServerState::Ready(_) => false,
            ServerState::Disposed => {
                let runtime = Self::create_runtime();

                let server = SegmentServer::new(
                    queue_file_path,
                    queue_count_limit,
                    writer_key,
                    callback_fn,
                    error_fn,
                    &runtime,
                );

                if let Ok(mut rt_lock) = self.async_runtime.lock() {
                    *rt_lock = Some(runtime);
                }

                *state = ServerState::Ready(Arc::new(server));
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

        match &*state {
            ServerState::Disposed => false,
            ServerState::Ready(server) => {
                //Disable callbacks before anything else.
                //acquires write locks and waits for any in-flight callbacks to complete.
                // After this returns, no callbacks can ever be called again.
                server.safe_callback.disable();

                *state = ServerState::Disposed;

                if let Ok(mut rt_lock) = self.async_runtime.lock() {
                    if let Some(runtime) = rt_lock.take() {
                        runtime.shutdown_background();
                    }
                }

                true
            }
        }
    }

    pub fn try_execute(
        &self,
        func: &dyn Fn(Arc<SegmentServer>, OperationHandleId),
    ) -> OperationHandleId {
        let state_lock = self.state.lock();
        if state_lock.is_err() {
            return 0;
        }

        let state = state_lock.unwrap();

        match &*state {
            ServerState::Disposed => 0,
            ServerState::Ready(server) => {
                let id = server.next_id();
                func(server.clone(), id);
                id
            }
        }
    }

    pub fn spawn<F>(&self, future: F)
    where
        F: std::future::Future<Output = ()> + Send + 'static,
    {
        if let Ok(rt_lock) = self.async_runtime.lock() {
            if let Some(ref runtime) = *rt_lock {
                runtime.spawn(future);
            }
        }
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
        callback_fn: FfiCallbackFn,
        error_fn: Option<FfiErrorCallbackFn>,
        async_runtime: &tokio::runtime::Runtime,
    ) -> Self {
        let safe_callback = Arc::new(SafeCallback::new(
            Box::new(move |id, response| unsafe {
                callback_fn(id, response);
            }),
            Box::new(move |message: &str| {
                if let Some(cb) = error_fn {
                    if let Ok(cstr) = CString::new(message) {
                        unsafe {
                            cb(cstr.as_ptr());
                        }
                    }
                }
            }),
        ));

        let daemon_safe_callback = safe_callback.clone();
        let daemon_error_fn = Box::new(move |message: &str| {
            daemon_safe_callback.call_error(message);
        });

        let event_queue: CombinedAnalyticsEventQueueNewResult =
            CombinedAnalyticsEventQueue::new(queue_file_path, Some(queue_count_limit));
        let event_queue: CombinedAnalyticsEventQueue = match event_queue {
            CombinedAnalyticsEventQueueNewResult::Persistent(e) => e,
            CombinedAnalyticsEventQueueNewResult::FallbackToInMemory(e, err) => {
                let error_message =
                    format!("Cannot create persistent queue, fallback to in memory queue: {err}");
                safe_callback.call_error(&error_message);
                e
            }
        };
        let event_queue = Arc::new(Mutex::new(event_queue));

        let queue_batcher = QueuedBatcher::new(event_queue.clone(), None);

        let client = HttpClient::default();
        let send_daemon =
            AnalyticsEventSendDaemon::new(event_queue.clone(), None, writer_key.clone(), client);
        let send_daemon = Arc::new(Mutex::new(send_daemon));

        let moved_send_daemon = send_daemon.clone();
        async_runtime.spawn(async move {
            let mut guard = moved_send_daemon.lock().await;
            guard.start(daemon_error_fn);
        });
        let direct_client = HttpClient::default();

        let context = AppContext {
            batcher: queue_batcher,
            queue: event_queue,
            daemon: send_daemon,
            segment_client: direct_client,
            write_key: writer_key,
            safe_callback: safe_callback.clone(),
        };

        Self {
            next_id: AtomicU64::new(1), //0 is invalid,
            context: Arc::new(Mutex::new(context)),
            safe_callback,
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
                .report_error(Some(id), format!("Cannot enqueue: {}", e.to_string()));
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
            context.report_error(Some(id), format!("Cannot flush: {}", e.to_string()));
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
        self.safe_callback.call_success(id);
    }

    pub fn report_error(&self, id: Option<OperationHandleId>, message: String) {
        let message = match id {
            Some(id) => {
                format!("Operation {} failed: {}", id, message)
            }
            None => message,
        };

        self.safe_callback.call_error(&message);

        if let Some(id) = id {
            self.safe_callback.call_error_response(id);
        }
    }
}
