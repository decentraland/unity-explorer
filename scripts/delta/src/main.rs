//! `delta`: per-file `HDiffPatch` delta updates for Explorer releases. Design: docs/delta-updates.md.

use std::path::PathBuf;
use std::process::ExitCode;

use anyhow::{Context, Result};
use clap::builder::PossibleValuesParser;
use clap::{Parser, Subcommand};

use delta::io::{Io, Releases as _};
use delta::publishing::values::ZipName;
use delta::releases::github::{Release as PublishedRelease, explorer_releases, pick_bases, prune_targets};
use delta::releases::tag::{Platform, Tag};
use delta::run::document;
use delta::run::fsm::{Cmd, init};
use delta::run::runtime::{drain, log};
use delta::run::state::{Release, Request, Run, State};
use delta::run::summary::summary;
use delta::shell;
use delta::shell::hdiff::{HDiffPatch, fetch_tools};
use delta::shell::remote::GhReleases;

#[derive(Debug, Parser)]
#[command(name = "delta", about = "Per-file HDiffPatch delta updates for Explorer releases")]
struct Cli {
    #[command(subcommand)]
    operation: Operation,
}

/// What the command line asks the tool to do.
#[derive(Debug, Subcommand)]
enum Operation {
    /// Install the pinned `HDiffPatch`; prints the folder to add to PATH.
    FetchTools { tools: PathBuf },
    /// Exit 0 only when <tag> has no published release (`GITHUB_REPOSITORY`, `GH_TOKEN`).
    CheckUnpublished { tag: Tag },
    /// JSON array of the <count> most recently published releases other than <current>.
    PickBases { count: usize, current: Tag },
    /// Releases whose delta/ folder is to be removed, one per line.
    ListPrunable {
        keep: usize,
        window: usize,
        /// Never pruned.
        #[arg(long)]
        live: Option<Tag>,
    },
    /// Write a new run document: the Requested state and the first cmd.
    CreateRun {
        document: PathBuf,
        #[arg(long)]
        tag: Tag,
        #[arg(long)]
        platform: Platform,
        /// Name of the release zip, e.g. `Decentraland_windows64.zip`.
        #[arg(long)]
        zip: ZipName,
        /// Key prefix of all releases in the bucket, e.g. @dcl/unity-explorer/releases.
        #[arg(long)]
        bucket_prefix: String,
        /// JSON array of base tags.
        #[arg(long)]
        bases: String,
        /// Local work folder.
        #[arg(long)]
        work: PathBuf,
    },
    /// Resume the run document: execute its pending cmds until the machine completes.
    Run {
        document: PathBuf,
        /// Pause once this state is reached; the next `run` resumes from it.
        #[arg(long, value_parser = PossibleValuesParser::new(State::NAMES))]
        until: Option<String>,
        /// Use <folder> as the bucket, with nothing published and every signature valid.
        #[arg(long)]
        local: Option<PathBuf>,
    },
    /// Markdown summary of the run document.
    Summary { document: PathBuf },
}

fn published_releases() -> Result<Vec<PublishedRelease>> {
    explorer_releases(&GhReleases.list()?)
}

fn operate(operation: Operation) -> Result<ExitCode> {
    match operation {
        Operation::FetchTools { tools } => {
            println!("{}", fetch_tools(&tools)?.display());
        }
        Operation::CheckUnpublished { tag } => {
            if GhReleases.is_published(&tag)? {
                eprintln!(
                    "::error::{tag} is published; its delta patches are immutable and are not (re)generated or uploaded."
                );
                return Ok(ExitCode::FAILURE);
            }
            log(&format!("{tag} has no published release"));
        }
        Operation::PickBases { count, current } => {
            let bases: Vec<Tag> = pick_bases(&published_releases()?, count, &current);
            println!("{}", serde_json::to_string(&bases)?);
        }
        Operation::ListPrunable { keep, window, live } => {
            for tag in prune_targets(&published_releases()?, keep, window, live.as_ref()) {
                println!("{tag}");
            }
        }
        Operation::CreateRun {
            document: path,
            tag,
            platform,
            zip,
            bucket_prefix,
            bases,
            work,
        } => {
            let base_tags: Vec<Tag> = serde_json::from_str(&bases).context("--bases is not a JSON array of tags")?;
            let release: Release = Release {
                tag,
                platform,
                zip,
                bucket_prefix,
                work,
            };
            let (run, cmds): (Run, Vec<Cmd>) = init(Request::new(release, base_tags)?);
            document::save(&path, &run, &cmds)?;
            log(&format!("created {}", path.display()));
        }
        Operation::Run {
            document: path,
            until,
            local,
        } => {
            let hdiff: HDiffPatch = HDiffPatch::from_env();
            let io: Io = match local {
                Some(folder) => shell::folder(folder, hdiff),
                None => shell::ci(hdiff),
            };
            let run: Run = drain(&path, until.as_deref(), &io)?;
            if let State::Failed { reason } = &run.state {
                eprintln!("::error::delta run failed: {reason}");
                return Ok(ExitCode::FAILURE);
            }
        }
        Operation::Summary { document: path } => {
            let (run, _): (Run, Vec<Cmd>) = document::load(&path)?;
            print!("{}", summary(&run));
        }
    }
    Ok(ExitCode::SUCCESS)
}

fn main() -> ExitCode {
    let cli: Cli = Cli::parse();
    match operate(cli.operation) {
        Ok(code) => code,
        Err(error) => {
            eprintln!("::error::{error:#}");
            ExitCode::FAILURE
        }
    }
}
