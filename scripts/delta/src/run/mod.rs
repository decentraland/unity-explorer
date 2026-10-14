//! One platform's delta run. Each YAML step invokes one workflow: the `Run` document is read,
//! its stage must be the one the workflow follows, the workflow moves it to the next stage, and
//! the document is written back. Events go to the log.
//!
//! ```text
//! Requested -> Confirmed -> Cleared -> Downloaded -> Extracted -> Inspected
//!           -> Encoded -> Verified -> Assembled -> Rechecked -> Uploaded -> Published
//! any stage --(run-level error, or every base dropped)--> Failed
//! ```

pub mod document;
pub mod event;
pub mod stage;
pub mod summary;
pub mod workflows;

use std::fmt;

use anyhow::{Result, bail};

use crate::capabilities::{Cdn, Differ, Releases, Signer, Storage};
use crate::run::event::Event;
use crate::run::stage::{Dropped, Run, Stage};
use crate::run::workflows::Outcome;

/// The workflows in lifecycle order, as named on the command line.
#[derive(Debug, Clone, Copy, PartialEq, Eq, clap::ValueEnum)]
pub enum Workflow {
    ConfirmUnpublished,
    ClearStale,
    Download,
    Extract,
    Inspect,
    Encode,
    Verify,
    Assemble,
    Recheck,
    UploadPatches,
    PublishManifest,
}

impl Workflow {
    /// The stage this workflow follows.
    pub fn follows(self) -> &'static str {
        match self {
            Workflow::ConfirmUnpublished => "Requested",
            Workflow::ClearStale => "Confirmed",
            Workflow::Download => "Cleared",
            Workflow::Extract => "Downloaded",
            Workflow::Inspect => "Extracted",
            Workflow::Encode => "Inspected",
            Workflow::Verify => "Encoded",
            Workflow::Assemble => "Verified",
            Workflow::Recheck => "Assembled",
            Workflow::UploadPatches => "Rechecked",
            Workflow::PublishManifest => "Uploaded",
        }
    }
}

impl fmt::Display for Workflow {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let name: &str = match self {
            Workflow::ConfirmUnpublished => "confirm-unpublished",
            Workflow::ClearStale => "clear-stale",
            Workflow::Download => "download",
            Workflow::Extract => "extract",
            Workflow::Inspect => "inspect",
            Workflow::Encode => "encode",
            Workflow::Verify => "verify",
            Workflow::Assemble => "assemble",
            Workflow::Recheck => "recheck",
            Workflow::UploadPatches => "upload-patches",
            Workflow::PublishManifest => "publish-manifest",
        };
        f.write_str(name)
    }
}

/// The I/O the workflows may use.
pub struct Capabilities<'a> {
    pub releases: &'a dyn Releases,
    pub storage: &'a dyn Storage,
    pub cdn: &'a dyn Cdn,
    pub differ: &'a dyn Differ,
    pub signer: &'a dyn Signer,
}

impl fmt::Debug for Capabilities<'_> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("Capabilities")
    }
}

/// The document after a workflow, and what happened.
#[derive(Debug)]
pub struct Advanced {
    pub run: Run,
    pub events: Vec<Event>,
}

struct Moved {
    stage: Stage,
    events: Vec<Event>,
    drops: Vec<Dropped>,
}

fn moved<S>(outcome: Outcome<S>, wrap: fn(S) -> Stage) -> Moved {
    Moved {
        stage: wrap(outcome.stage),
        events: outcome.events,
        drops: outcome.drops,
    }
}

fn dispatch(workflow: Workflow, stage: Stage, caps: &Capabilities<'_>) -> Result<Moved> {
    use crate::run::workflows as w;
    Ok(match (workflow, stage) {
        (Workflow::ConfirmUnpublished, Stage::Requested(s)) => {
            moved(w::confirm_unpublished(s, caps.releases)?, Stage::Confirmed)
        }
        (Workflow::ClearStale, Stage::Confirmed(s)) => moved(w::clear_stale(s, caps.storage)?, Stage::Cleared),
        (Workflow::Download, Stage::Cleared(s)) => moved(w::download(s, caps.storage)?, Stage::Downloaded),
        (Workflow::Extract, Stage::Downloaded(s)) => moved(w::extract(s)?, Stage::Extracted),
        (Workflow::Inspect, Stage::Extracted(s)) => moved(w::inspect(s, caps.signer)?, Stage::Inspected),
        (Workflow::Encode, Stage::Inspected(s)) => moved(w::encode(s, caps.differ)?, Stage::Encoded),
        (Workflow::Verify, Stage::Encoded(s)) => moved(w::verify(s, caps.differ, caps.signer)?, Stage::Verified),
        (Workflow::Assemble, Stage::Verified(s)) => moved(w::assemble(s)?, Stage::Assembled),
        (Workflow::Recheck, Stage::Assembled(s)) => {
            moved(w::recheck(s, caps.releases, caps.storage)?, Stage::Rechecked)
        }
        (Workflow::UploadPatches, Stage::Rechecked(s)) => moved(w::upload_patches(s, caps.cdn)?, Stage::Uploaded),
        (Workflow::PublishManifest, Stage::Uploaded(s)) => moved(w::publish_manifest(s, caps.cdn)?, Stage::Published),
        (workflow, stage) => bail!(
            "{workflow} follows {}, but the run is {}",
            workflow.follows(),
            stage.name()
        ),
    })
}

/// Runs `workflow` on the document. Any error, including a stage the workflow does not follow,
/// ends the run in `Failed`. A failed run stays as it is, keeping the reason it failed.
pub fn advance(run: Run, workflow: Workflow, caps: &Capabilities<'_>) -> Advanced {
    if matches!(run.stage, Stage::Failed { .. }) {
        return Advanced {
            run,
            events: Vec::new(),
        };
    }
    let Run {
        schema,
        stage,
        mut drops,
    } = run;
    match dispatch(workflow, stage, caps) {
        Ok(next) => {
            drops.extend(next.drops);
            Advanced {
                run: Run {
                    schema,
                    stage: next.stage,
                    drops,
                },
                events: next.events,
            }
        }
        Err(error) => {
            let failed: Stage = Stage::Failed {
                at: workflow.to_string(),
                reason: format!("{error:#}"),
            };
            Advanced {
                run: Run {
                    schema,
                    stage: failed,
                    drops,
                },
                events: Vec::new(),
            }
        }
    }
}
