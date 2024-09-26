use core::str;
use std::sync::{
    atomic::{AtomicU64, Ordering},
    Arc,
};

use segment::{message::BatchMessage, AutoBatcher, Batcher, HttpClient};
use tokio::sync::Mutex;

use crate::{operations, FfiCallbackFn, OperationHandleId, Response};

pub struct Context {
    pub batcher: AutoBatcher,
    callback_fn: Box<dyn Fn(OperationHandleId, Response) + Send + Sync>,
}

pub struct SegmentServer {
    pub async_runtime: tokio::runtime::Runtime,
    context: Arc<Mutex<Option<Context>>>,
    next_id: AtomicU64,
}

impl Default for SegmentServer {
    fn default() -> Self {
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()
            .unwrap();

        Self {
            async_runtime: runtime,
            next_id: AtomicU64::new(1), //0 is invalid
            context: Default::default(),
        }
    }
}

impl SegmentServer {
    pub fn next_id(&self) -> OperationHandleId {
        self.next_id.fetch_add(1, Ordering::Relaxed)
    }

    pub fn initialize(&'static self, writer_key: &str, callback_fn: FfiCallbackFn) {
        let client = HttpClient::default();
        let batcher = Batcher::new(None);
        let auto_batcher = AutoBatcher::new(client, batcher, writer_key.to_string());
        let runtime = &self.async_runtime;

        let operation = async move {
            let mut guard = self.context.lock().await;
            let context = Context {
                batcher: auto_batcher,
                callback_fn: Box::new(move |id, response| unsafe {
                    callback_fn(id, response);
                }),
            };
            *guard = Some(context);
        };

        runtime.spawn(operation);
    }

    pub async fn enqueue_track(
        &self,
        id: OperationHandleId,
        used_id: &str,
        event_name: &str,
        properties_json: &str,
        context_json: &str,
    ) {
        let msg = operations::new_track(used_id, event_name, properties_json, context_json);
        self.enqueue_if_ok(id, msg).await;
    }

    pub async fn enqueue_identify(
        &self,
        id: OperationHandleId,
        used_id: &str,
        traits_json: &str,
        context_json: &str,
    ) {
        let msg = operations::new_identify(used_id, traits_json, context_json);
        self.enqueue_if_ok(id, msg).await;
    }

    pub async fn enqueue(&self, id: OperationHandleId, msg: impl Into<BatchMessage>) {
        let arc = self.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let result = context.batcher.push(msg).await;

        let response_code = Self::result_as_response_code(result);
        context.call_callback(id, response_code);
    }

    pub async fn flush(&self, id: OperationHandleId) {
        let arc = self.context.clone();
        let mut guard = arc.lock().await;
        let context = (*guard).as_mut().unwrap();

        let result = context.batcher.flush().await;

        let response_code = Self::result_as_response_code(result);
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
        let guard = arc.lock().await;
        let context = (*guard).as_ref().unwrap();
        context.call_callback(id, code);
    }

    fn result_as_response_code(result: Result<(), segment::Error>) -> Response {
        return match result {
            Ok(_) => Response::Success,
            Err(error) => match error {
                segment::Error::MessageTooLarge => Response::ErrorMessageTooLarge,
                segment::Error::DeserializeError(_) => Response::ErrorDeserialize,
                segment::Error::NetworkError(_) => Response::ErrorNetwork,
            },
        };
    }
}

impl Context {
    pub fn call_callback(&self, id: OperationHandleId, code: Response) {
        self.callback_fn.as_ref()(id, code);
    }
}
