use core::str;
use std::sync::{
    atomic::{AtomicU64, Ordering},
    Arc,
};

use segment::{
    message::{BatchMessage, User}, Client, HttpClient
};
use tokio::sync::Mutex;

use crate::{operations, queue_batcher::QueueBatcher, FfiCallbackFn, OperationHandleId, Response};

pub struct Context {
    pub batcher: QueueBatcher,
    pub segment_client: segment::HttpClient,
    pub write_key: String,
    callback_fn: Box<dyn Fn(OperationHandleId, Response) + Send + Sync>,
}

pub struct SegmentServer {
    context: Arc<Mutex<Context>>,
    next_id: AtomicU64,
    unflushed_count_cache: AtomicU64,
}

pub enum ServerState {
    Ready(Arc<SegmentServer>),
    Disposed,
}

pub struct Server {
    state: std::sync::Mutex<ServerState>,
    pub async_runtime: tokio::runtime::Runtime,
}

impl Default for Server {
    fn default() -> Self {
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()
            .unwrap();

        Self {
            async_runtime: runtime,
            state: std::sync::Mutex::new(ServerState::Disposed),
        }
    }
}

impl Server {
    pub fn initialize(&self, writer_key: String, callback_fn: FfiCallbackFn) -> bool {
        let state_lock = self.state.lock();
        if state_lock.is_err() {
            return false;
        }

        let mut state = state_lock.unwrap();

        match *state {
            ServerState::Ready(_) => false,
            ServerState::Disposed => {
                let server = SegmentServer::new(writer_key, callback_fn);
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

        match *state {
            ServerState::Disposed => false,
            ServerState::Ready(_) => {
                *state = ServerState::Disposed;
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

    pub fn unflushed_batches_count(&self) -> u64 {
        let state_lock = self.state.lock();
        if state_lock.is_err() {
            return 0;
        }

        let state = state_lock.unwrap();

        match &*state {
            ServerState::Disposed => 0,
            ServerState::Ready(server) => server.unflushed_count_cache.load(Ordering::Relaxed),
        }
    }
}

impl SegmentServer {
    pub fn next_id(&self) -> OperationHandleId {
        self.next_id.fetch_add(1, Ordering::Relaxed)
    }

    fn new(writer_key: String, callback_fn: FfiCallbackFn) -> Self {
        let client = HttpClient::default();
        let queue_batcher = QueueBatcher::new(client, writer_key.clone());
        
        let direct_client = HttpClient::default();

        let context = Context {
            batcher: queue_batcher,
            segment_client: direct_client,
            write_key: writer_key,
            callback_fn: Box::new(move |id, response| unsafe {
                callback_fn(id, response);
            }),
        };

        Self {
            next_id: AtomicU64::new(1), //0 is invalid,
            unflushed_count_cache: AtomicU64::new(0),
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
        let msg = operations::new_track(user, event_name, properties_json, context_json);
        match msg {
            Some(m) => {
                let guard = instance.context.lock().await;
                let key = guard.write_key.clone();
                let result = guard.segment_client.send(key, m.into()).await;
                let response = SegmentServer::result_as_response_code(result);
                instance.call_callback(id, response).await;
            },
            None => {
                instance.call_callback(id, Response::ErrorDeserialize).await;
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
        let msg = operations::new_track(user, event_name, properties_json, context_json);
        instance.enqueue_if_ok(id, msg).await;
    }

    pub async fn enqueue_identify(
        instance: Arc<Self>,
        id: OperationHandleId,
        user: User,
        traits_json: &str,
        context_json: &str,
    ) {
        let msg = operations::new_identify(user, traits_json, context_json);
        instance.enqueue_if_ok(id, msg).await;
    }

    pub async fn enqueue(&self, id: OperationHandleId, msg: impl Into<BatchMessage>) {
        let arc = self.context.clone();
        let mut context = arc.lock().await;

        let result = context.batcher.push(msg).await;

        let response_code = Self::result_as_response_code(result);
        self.update_unflushed_count_cache(&context, &response_code);
        context.call_callback(id, response_code);
    }

    pub async fn flush(instance: Arc<Self>, id: OperationHandleId) {
        let arc = instance.context.clone();
        let mut context = arc.lock().await;

        let result = context.batcher.flush().await;

        let response_code = Self::result_as_response_code(result);
        instance.update_unflushed_count_cache(&context, &response_code);
        context.call_callback(id, response_code);
    }

    async fn enqueue_if_ok(&self, id: OperationHandleId, msg: Option<impl Into<BatchMessage>>) {
        match msg {
            Some(m) => self.enqueue(id, m).await,
            None => {
                self.call_callback(id, Response::ErrorDeserialize).await;
            }
        }
    }

    async fn call_callback(&self, id: OperationHandleId, code: Response) {
        let arc = self.context.clone();
        let context = arc.lock().await;
        context.call_callback(id, code);
    }

    fn result_as_response_code(result: Result<(), segment::Error>) -> Response {
        match result {
            Ok(_) => Response::Success,
            Err(error) => match error {
                segment::Error::MessageTooLarge => Response::ErrorMessageTooLarge,
                segment::Error::DeserializeError(_) => Response::ErrorDeserialize,
                segment::Error::NetworkError(_) => Response::ErrorNetwork,
            },
        }
    }

    fn update_unflushed_count_cache(&self, context: &Context, response: &Response) {
        if matches!(response, Response::Success) {
            let len = context.batcher.len() as u64;
            self.unflushed_count_cache.store(len, Ordering::Relaxed);
        }
    }
}

impl Context {
    pub fn call_callback(&self, id: OperationHandleId, code: Response) {
        self.callback_fn.as_ref()(id, code);
    }
}
