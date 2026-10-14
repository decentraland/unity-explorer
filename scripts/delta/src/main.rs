//! `delta`: per-file `HDiffPatch` delta updates for Explorer releases. Design: docs/delta-updates.md.

use std::path::{Path, PathBuf};
use std::process::ExitCode;

use anyhow::{Context, Result};
use clap::{Parser, Subcommand};

use delta::io::{Io, Releases};
use delta::publishing::values::ZipName;
use delta::releases::github::{Release, explorer_releases, pick_bases, prune_targets};
use delta::releases::tag::{Platform, Tag};
use delta::run::document;
use delta::run::execute::execute;
use delta::run::fsm::{Command, Msg, Report, Step, update};
use delta::run::state::{Request, Run, State, Target};
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
    /// Write a new run document, in the Idle state.
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
        releases: String,
        /// JSON array of base tags.
        #[arg(long)]
        bases: String,
        /// Local work folder.
        #[arg(long)]
        work: PathBuf,
    },
    /// Run one step on the run document; it must be in the state the step follows.
    Step {
        step: Step,
        document: PathBuf,
        /// Use <folder> as the bucket, with nothing published and every signature valid.
        #[arg(long)]
        local: Option<PathBuf>,
    },
    /// Markdown summary of the run document.
    Summary { document: PathBuf },
}

fn log(line: &str) {
    eprintln!("[delta] {line}");
}

fn published_releases(releases: &dyn Releases) -> Result<Vec<Release>> {
    explorer_releases(&releases.list()?)
}

/// Sends the step, runs the command it asks for, folds the report back in.
fn step(step: Step, path: &Path, local: Option<PathBuf>) -> Result<ExitCode> {
    let run: Run = document::load(path)?;
    let hdiff: HDiffPatch = HDiffPatch::from_env();
    let io: Io = match local {
        Some(folder) => shell::folder(folder, hdiff),
        None => shell::ci(hdiff),
    };
    let from: &'static str = run.state.name();
    let dropped_before: usize = run.dropped.len();
    let (run, command): (Run, Option<Command>) = update(run, Msg::Step(step));
    let run: Run = match command {
        Some(command) => {
            let report: Report = execute(command, &io);
            for line in report.lines() {
                log(&format!("  {line}"));
            }
            update(run, Msg::Report(report)).0
        }
        None => run,
    };
    for dropped in run.dropped.iter().skip(dropped_before) {
        log(&format!("  {} dropped: {}", dropped.base, dropped.reason));
    }
    log(&format!("{from} --{step}--> {}", run.state));
    document::save(path, &run)?;
    if let State::Failed { at, reason } = &run.state {
        eprintln!("::error::delta run failed at {at}: {reason}");
        return Ok(ExitCode::FAILURE);
    }
    Ok(ExitCode::SUCCESS)
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
            let bases: Vec<Tag> = pick_bases(&published_releases(&GhReleases)?, count, &current);
            println!("{}", serde_json::to_string(&bases)?);
        }
        Operation::ListPrunable { keep, window, live } => {
            for tag in prune_targets(&published_releases(&GhReleases)?, keep, window, live.as_ref()) {
                println!("{tag}");
            }
        }
        Operation::CreateRun {
            document: path,
            tag,
            platform,
            zip,
            releases,
            bases,
            work,
        } => {
            let base_tags: Vec<Tag> = serde_json::from_str(&bases).context("--bases is not a JSON array of tags")?;
            let target: Target = Target {
                tag,
                platform,
                zip,
                releases,
                work,
            };
            document::save(&path, &Run::new(Request::new(target, base_tags)?))?;
            log(&format!("created {}", path.display()));
        }
        Operation::Step {
            step: name,
            document: path,
            local,
        } => return step(name, &path, local),
        Operation::Summary { document: path } => {
            print!("{}", summary(&document::load(&path)?));
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
