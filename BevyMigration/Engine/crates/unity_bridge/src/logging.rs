//! Routes `tracing` (Bevy, this crate) and `log` (wgpu) to the host's callback; stderr when no host
//! is attached, which is the case in tests and between a domain reload's shutdown and init.

use abi::{BmLogCallback, BmLogLevel};
use parking_lot::Mutex;
use std::{ffi::CString, fmt::Write as _, sync::OnceLock};
use tracing::{
    Event, Level, Subscriber,
    field::{Field, Visit},
};
use tracing_subscriber::{Layer, layer::Context, prelude::*};

static HOST: Mutex<BmLogCallback> = Mutex::new(None);
static INSTALLED: OnceLock<()> = OnceLock::new();

pub fn install(callback: BmLogCallback) {
    *HOST.lock() = callback;

    INSTALLED.get_or_init(|| {
        let _ = tracing_log::LogTracer::init();
        let subscriber = tracing_subscriber::registry()
            .with(HostLayer.with_filter(tracing_subscriber::filter::LevelFilter::INFO));
        let _ = tracing::subscriber::set_global_default(subscriber);
        std::panic::set_hook(Box::new(|info| error(&format!("panic: {info}"))));
    });
}

pub fn clear_host() {
    *HOST.lock() = None;
}

pub fn emit(level: BmLogLevel, message: &str) {
    let callback = *HOST.lock();

    match (callback, CString::new(message)) {
        // SAFETY: the host promised a callback that accepts a NUL-terminated string for the call's duration.
        (Some(callback), Ok(text)) => unsafe { callback(level, text.as_ptr()) },
        _ => eprintln!("[bevy-migration] {level:?}: {message}"),
    }
}

pub fn error(message: &str) {
    emit(BmLogLevel::Error, message);
}

pub fn panic_message(payload: &(dyn std::any::Any + Send)) -> String {
    payload
        .downcast_ref::<&str>()
        .map(|s| (*s).to_owned())
        .or_else(|| payload.downcast_ref::<String>().cloned())
        .unwrap_or_else(|| "non-string panic payload".to_owned())
}

struct HostLayer;

impl<S: Subscriber> Layer<S> for HostLayer {
    fn on_event(&self, event: &Event<'_>, _: Context<'_, S>) {
        let mut message = String::new();
        let _ = write!(message, "[{}] ", event.metadata().target());
        event.record(&mut MessageVisitor(&mut message));

        let level = match *event.metadata().level() {
            Level::ERROR => BmLogLevel::Error,
            Level::WARN => BmLogLevel::Warning,
            _ => BmLogLevel::Info,
        };

        emit(level, &message);
    }
}

struct MessageVisitor<'a>(&'a mut String);

impl Visit for MessageVisitor<'_> {
    fn record_debug(&mut self, field: &Field, value: &dyn std::fmt::Debug) {
        if field.name() == "message" {
            let _ = write!(self.0, "{value:?}");
        } else {
            let _ = write!(self.0, " {}={value:?}", field.name());
        }
    }

    fn record_str(&mut self, field: &Field, value: &str) {
        if field.name() == "message" {
            self.0.push_str(value);
        } else {
            let _ = write!(self.0, " {}={value}", field.name());
        }
    }
}
