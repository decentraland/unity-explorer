//! zmq DEALER<->DEALER channel helpers shared by both sides.
//!
//! The client binds before spawning the helper and passes the resolved
//! endpoint in argv; the helper connects back. macOS uses `ipc://` (UDS,
//! filesystem-permission protected); Windows uses loopback TCP because
//! libzmq's `ipc://` transport never gained `AF_UNIX` support on Windows.

use anyhow::{Context as _, anyhow};
use serde::{Serialize, de::DeserializeOwned};

/// HWM on both directions: a stalled peer turns into send backpressure
/// instead of unbounded queueing.
const HIGH_WATER_MARK: i32 = 1000;

/// Bounded flush window on close: long enough for the last queued frames
/// (the client's Shutdown, the helper's final events) to reach the wire,
/// short enough that teardown never hangs on a gone peer.
const LINGER_MS: i32 = 200;

pub fn dealer(context: &zmq::Context) -> anyhow::Result<zmq::Socket> {
    let socket = context.socket(zmq::DEALER)?;
    socket.set_sndhwm(HIGH_WATER_MARK)?;
    socket.set_rcvhwm(HIGH_WATER_MARK)?;
    socket.set_linger(LINGER_MS)?;
    Ok(socket)
}

/// The endpoint the client binds; `uuid` isolates concurrent instances
/// (editor + player). On Windows the wildcard port resolves at bind time —
/// read the result back with [`bound_endpoint`].
pub fn bind_endpoint(uuid: &str) -> String {
    #[cfg(target_os = "macos")]
    {
        let dir = std::env::temp_dir();
        format!("ipc://{}/uuav-{uuid}.sock", dir.to_string_lossy())
    }
    #[cfg(target_os = "windows")]
    {
        let _ = uuid;
        "tcp://127.0.0.1:*".to_owned()
    }
}

/// The concrete endpoint after `bind`, suitable for the helper's argv.
pub fn bound_endpoint(socket: &zmq::Socket) -> anyhow::Result<String> {
    socket
        .get_last_endpoint()?
        .map_err(|_| anyhow!("bound endpoint is not valid UTF-8"))
}

pub fn send<T: Serialize>(socket: &zmq::Socket, message: &T) -> anyhow::Result<()> {
    let bytes = postcard::to_allocvec(message).context("serialize message")?;
    socket.send(bytes, 0).context("send message")
}

/// Blocking receive of one typed message (honors the socket's rcv timeout).
pub fn recv<T: DeserializeOwned>(socket: &zmq::Socket) -> anyhow::Result<T> {
    let bytes = socket.recv_bytes(0).context("recv message")?;
    postcard::from_bytes(&bytes).context("deserialize message")
}

/// True when at least one inbound message is ready within `timeout_ms`.
pub fn poll_readable(socket: &zmq::Socket, timeout_ms: i64) -> anyhow::Result<bool> {
    Ok(socket.poll(zmq::POLLIN, timeout_ms)? > 0)
}

/// Non-blocking receive: `Ok(None)` when no message is pending.
pub fn try_recv<T: DeserializeOwned>(socket: &zmq::Socket) -> anyhow::Result<Option<T>> {
    match socket.recv_bytes(zmq::DONTWAIT) {
        Ok(bytes) => Ok(Some(postcard::from_bytes(&bytes).context("deserialize")?)),
        Err(zmq::Error::EAGAIN) => Ok(None),
        Err(e) => Err(e.into()),
    }
}
