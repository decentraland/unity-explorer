//! Drains the cmds of a run like `FsmRuntime.Drain`: pop the front, execute it, feed the msg
//! back to `update`, append the cmds the new state asks for, save the document after every
//! transition. `Cmd::Complete` ends the drain. `until` pauses the drain once the named state is
//! reached, with the cmds that state waits on saved for the next invocation. The document is
//! never checked on entry: the run proceeds with the state and the cmds it was paused with.

use std::collections::VecDeque;
use std::path::Path;

use anyhow::{Context, Result};

use crate::io::Io;
use crate::run::document;
use crate::run::execute::execute;
use crate::run::fsm::{Cmd, Msg, update};
use crate::run::state::Run;

pub fn log(line: &str) {
    eprintln!("[delta] {line}");
}

fn names(cmds: &VecDeque<Cmd>) -> String {
    cmds.iter().map(ToString::to_string).collect::<Vec<String>>().join(", ")
}

pub fn drain(path: &Path, until: Option<&str>, io: &Io) -> Result<Run> {
    let (mut run, cmds): (Run, Vec<Cmd>) = document::load(path)?;
    let mut queue: VecDeque<Cmd> = VecDeque::from(cmds);
    loop {
        let cmd: Cmd = queue
            .pop_front()
            .with_context(|| format!("the run document {} has no pending cmd", path.display()))?;
        if cmd == Cmd::Complete {
            log(&format!("complete: {}", run.state));
            return Ok(run);
        }
        log(&format!("cmd: {cmd}"));
        let msg: Msg = execute(cmd, io);
        log(&format!("msg: {msg}"));
        let dropped_before: usize = run.dropped.len();
        let (next, cmds): (Run, Vec<Cmd>) = update(run, msg);
        run = next;
        for dropped in run.dropped.iter().skip(dropped_before) {
            log(&format!("  {} dropped: {}", dropped.base, dropped.reason));
        }
        queue.extend(cmds);
        log(&format!("state: {} cmds: {}", run.state, names(&queue)));
        let pending: Vec<Cmd> = queue.iter().cloned().collect();
        document::save(path, &run, &pending)?;
        if until == Some(run.state.name()) {
            log(&format!("paused at {}", run.state));
            return Ok(run);
        }
    }
}
