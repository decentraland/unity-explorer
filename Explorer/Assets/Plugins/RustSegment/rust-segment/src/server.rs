use core::str;
use std::{
    clone,
    future::Future,
    sync::{
        atomic::{AtomicU64, Ordering},
        Arc,
    },
};

use segment::{AutoBatcher, Batcher, HttpClient};
use tokio::sync::Mutex;

use crate::{FfiCallbackFn, OperationHandleId, Response};

pub struct Context {
    pub batcher: AutoBatcher,
    callback_fn: Box<dyn Fn(OperationHandleId, Response) + Send + Sync>,
}

pub struct SegmentServer {
    pub context: Arc<Mutex<Option<Context>>>,

    async_runtime: tokio::runtime::Runtime,
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

    pub fn dispatch_operation(
        &self,
        id: OperationHandleId,
        operation: impl Future<Output = ()> + Send + 'static,
    ) -> OperationHandleId {
        self.async_runtime.spawn(operation);
        id
    }
}

impl Context {
    pub fn call_callback(&self, id: OperationHandleId, code: Response) {
        self.callback_fn.as_ref()(id, code);
    }
}
