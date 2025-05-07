use segment::{message::BatchMessage, Client, HttpClient, Result};
use std::collections::VecDeque;

use crate::batcher::Batcher;

#[derive(Clone, Debug)]
pub struct QueueBatcher {
    client: HttpClient,
    queue: VecDeque<Batcher>,
    key: String,
}

impl QueueBatcher {
    pub fn new(client: HttpClient, key: String) -> Self {
        let queue = VecDeque::new();
        Self { client, queue, key }
    }

    pub async fn push(&mut self, msg: impl Into<BatchMessage>) -> Result<()> {
        if self.queue.is_empty() {
            let new_batcher = Batcher::new(None);
            self.queue.push_back(new_batcher);
        }

        //unwrap due the queue cannot be empty due the previous check in the current method
        let batcher = self.queue.back_mut().unwrap();

        if let Some(msg) = batcher.push(msg)? {
            let mut new_batcher = Batcher::new(None);
            // this can't return None: the batcher is empty and if the message is
            // larger than the max size of the batcher it's supposed to throw an error
            new_batcher.push(msg)?;
            self.queue.push_back(new_batcher);
        }

        Ok(())
    }

    pub async fn flush(&mut self) -> Result<()> {
        if self.queue.is_empty() {
            return Ok(());
        }

        if let Some(b) = self.queue.pop_front() {
            if b.is_empty() {
                return Ok(());
            }

            let message = b.into_message();
            self.client.send(self.key.to_string(), message).await?;
        }

        Ok(())
    }

    pub fn len(&self) -> usize {
        self.queue.len()
    }
}
