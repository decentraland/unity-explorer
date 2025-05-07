// created due MAX_BATCH_SIZE missconst in the depending crate, remove when the PR https://github.com/meilisearch/segment/pull/17 is merged into the external repo
//! Utilities for batching up messages.

use segment::{
    message::{Batch, BatchMessage},
    Error, Message, Result,
};
use serde_json::{Map, Value};
use time::OffsetDateTime;

const MAX_MESSAGE_SIZE: usize = 1024 * 32;
const MAX_BATCH_SIZE: usize = 1024 * 500;

/// A batcher can accept messages into an internal buffer, and report when
/// messages must be flushed.
///
/// The recommended usage pattern looks something like this:
///
/// ```
/// use segment::{Batcher, Client, HttpClient};
/// use segment::message::{BatchMessage, Track, User};
/// use serde_json::json;
///
/// let mut batcher = Batcher::new(None);
/// let client = HttpClient::default();
///
/// for i in 0..100 {
///     let msg = Track {
///         user: User::UserId { user_id: format!("user-{}", i) },
///         event: "Example".to_owned(),
///         properties: json!({ "foo": "bar" }),
///         ..Default::default()
///     };
///
///     // Batcher returns back ownership of a message if the internal buffer
///     // would overflow.
///     //
///     // When this occurs, we flush the batcher, create a new batcher, and add
///     // the message into the new batcher.
///     if let Some(msg) = batcher.push(msg).unwrap() {
///         client.send("your_write_key".to_string(), batcher.into_message());
///         batcher = Batcher::new(None);
///         batcher.push(msg).unwrap();
///     }
/// }
/// ```
///
/// Batcher will attempt to fit messages into maximally-sized batches, thus
/// reducing the number of round trips required with Segment's tracking API.
/// However, if you produce messages infrequently, this may significantly delay
/// the sending of messages to Segment.
///
/// If this delay is a concern, it is recommended that you periodically flush
/// the batcher on your own by calling `into_message`.
///
/// By default if the message you push in the batcher does not contains any
/// timestamp, the timestamp at the time of the push will be automatically
/// added to your message.
/// You can disable this behaviour with the [without_auto_timestamp] method
/// though.
#[derive(Clone, Debug)]
pub struct Batcher {
    pub(crate) buf: Vec<BatchMessage>,
    pub(crate) byte_count: usize,
    pub(crate) context: Option<Value>,
    pub(crate) auto_timestamp: bool,
}

pub fn timestamp_mut<'a>(m: &'a mut BatchMessage) -> &'a mut Option<OffsetDateTime> {
    match m {
        BatchMessage::Identify(identify) => &mut identify.timestamp,
        BatchMessage::Track(track) => &mut track.timestamp,
        BatchMessage::Page(page) => &mut page.timestamp,
        BatchMessage::Screen(screen) => &mut screen.timestamp,
        BatchMessage::Group(group) => &mut group.timestamp,
        BatchMessage::Alias(alias) => &mut alias.timestamp,
    }
}

impl Batcher {
    /// Construct a new, empty batcher.
    ///
    /// Optionally, you may specify a `context` that should be set on every
    /// batch returned by `into_message`.
    pub fn new(context: Option<Value>) -> Self {
        Self {
            buf: Vec::new(),
            byte_count: 0,
            context,
            auto_timestamp: true,
        }
    }

    pub fn without_auto_timestamp(&mut self) {
        self.auto_timestamp = false;
    }

    pub fn is_empty(&self) -> bool {
        self.buf.is_empty()
    }

    /// Push a message into the batcher.
    ///
    /// Returns `Ok(None)` if the message was accepted and is now owned by the
    /// batcher.
    ///
    /// Returns `Ok(Some(msg))` if the message was rejected because the current
    /// batch would be oversized if this message were accepted. The given
    /// message is returned back, and it is recommended that you flush the
    /// current batch before attempting to push `msg` in again.
    ///
    /// Returns an error if the message is too large to be sent to Segment's
    /// API.
    pub fn push(&mut self, msg: impl Into<BatchMessage>) -> Result<Option<BatchMessage>> {
        let mut msg: BatchMessage = msg.into();
        let timestamp = timestamp_mut(&mut msg);
        if self.auto_timestamp && timestamp.is_none() {
            *timestamp = Some(OffsetDateTime::now_utc());
        }
        let size = serde_json::to_vec(&msg)?.len();
        if size > MAX_MESSAGE_SIZE {
            return Err(Error::MessageTooLarge);
        }

        self.byte_count += size + 1; // +1 to account for Serialized data's extra commas
        if self.byte_count > MAX_BATCH_SIZE {
            return Ok(Some(msg));
        }

        self.buf.push(msg);
        Ok(None)
    }

    /// Consumes this batcher and converts it into a message that can be sent to
    /// Segment.
    pub fn into_message(self) -> Message {
        Message::Batch(Batch {
            batch: self.buf,
            context: self.context,
            integrations: None,
            extra: Map::default(),
        })
    }
}
