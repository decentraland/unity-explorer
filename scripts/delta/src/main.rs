//! `delta`: per-file `HDiffPatch` delta updates for Explorer releases. Design: docs/delta-updates.md.

use std::path::{Path, PathBuf};
use std::process::ExitCode;

use anyhow::{Context, Result};
use clap::{Parser, Subcommand};

use delta::capabilities::Releases;
use delta::publishing::values::ZipName;
use delta::releases::github::{Release, explorer_releases, pick_bases, prune_targets};
use delta::releases::tag::{Platform, Tag};
use delta::run::stage::{Requested, Run, Stage, Target};
use delta::run::summary::summary;
use delta::run::workflows::start;
use delta::run::{Advanced, Capabilities, Workflow, advance, document};
use delta::shell::hdiff::{HDiffPatch, fetch_tools};
use delta::shell::local::{FixedReleases, FixedSigner, FolderCdn, FolderStorage};
use delta::shell::remote::{AwsStorage, CdnUploader, GhReleases, SystemSigner};

#[derive(Debug, Parser)]
#[command(name = "delta", about = "Per-file HDiffPatch delta updates for Explorer releases")]
struct Cli {
    #[command(subcommand)]
    command: Command,
}

#[derive(Debug, Subcommand)]
enum Command {
    /// Install the pinned `HDiffPatch`; prints the folder to add to PATH.
    FetchTools { tools: PathBuf },
    /// Exit 0 only when <tag> has no published release (`GITHUB_REPOSITORY`, `GH_TOKEN`).
    CheckUnpublished { tag: Tag },
    /// JSON array of the <count> most recently published releases other than <current>.
    Bases { count: usize, current: Tag },
    /// Releases whose delta/ folder is removed, one per line.
    Prune {
        keep: usize,
        window: usize,
        /// Never pruned.
        #[arg(long)]
        live: Option<Tag>,
    },
    /// Write a new run document in the Requested stage.
    Start {
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
    /// Run one workflow on the run document.
    Step {
        workflow: Workflow,
        document: PathBuf,
        /// Use <folder> as the bucket and CDN, with nothing published and every signature valid.
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

fn step(workflow: Workflow, path: &Path, local: Option<PathBuf>) -> Result<ExitCode> {
    let run: Run = document::load(path)?;
    let from: &'static str = run.stage.name();
    let differ: HDiffPatch = HDiffPatch::from_env();
    let advanced: Advanced = if let Some(folder) = local {
        let storage: FolderStorage = FolderStorage { root: folder.clone() };
        let cdn: FolderCdn = FolderCdn { root: folder };
        let caps: Capabilities<'_> = Capabilities {
            releases: &FixedReleases::default(),
            storage: &storage,
            cdn: &cdn,
            differ: &differ,
            signer: &FixedSigner::default(),
        };
        advance(run, workflow, &caps)
    } else {
        let caps: Capabilities<'_> = Capabilities {
            releases: &GhReleases,
            storage: &AwsStorage,
            cdn: &CdnUploader,
            differ: &differ,
            signer: &SystemSigner,
        };
        advance(run, workflow, &caps)
    };
    log(&format!("{from} --{workflow}--> {}", advanced.run.stage.name()));
    for event in &advanced.events {
        log(&format!("  {event}"));
    }
    document::save(path, &advanced.run)?;
    if let Stage::Failed { at, reason } = &advanced.run.stage {
        eprintln!("::error::delta run failed at {at}: {reason}");
        return Ok(ExitCode::FAILURE);
    }
    Ok(ExitCode::SUCCESS)
}

fn execute(command: Command) -> Result<ExitCode> {
    match command {
        Command::FetchTools { tools } => {
            println!("{}", fetch_tools(&tools)?.display());
        }
        Command::CheckUnpublished { tag } => {
            if GhReleases.is_published(&tag)? {
                eprintln!(
                    "::error::{tag} is published; its delta patches are immutable and are not (re)generated or uploaded."
                );
                return Ok(ExitCode::FAILURE);
            }
            log(&format!("{tag} has no published release"));
        }
        Command::Bases { count, current } => {
            let bases: Vec<Tag> = pick_bases(&published_releases(&GhReleases)?, count, &current);
            println!("{}", serde_json::to_string(&bases)?);
        }
        Command::Prune { keep, window, live } => {
            for tag in prune_targets(&published_releases(&GhReleases)?, keep, window, live.as_ref()) {
                println!("{tag}");
            }
        }
        Command::Start {
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
            let requested: Requested = start(target, base_tags)?;
            document::save(&path, &Run::start(requested))?;
            log(&format!("started {}", path.display()));
        }
        Command::Step {
            workflow,
            document: path,
            local,
        } => return step(workflow, &path, local),
        Command::Summary { document: path } => {
            print!("{}", summary(&document::load(&path)?));
        }
    }
    Ok(ExitCode::SUCCESS)
}

fn main() -> ExitCode {
    let cli: Cli = Cli::parse();
    match execute(cli.command) {
        Ok(code) => code,
        Err(error) => {
            eprintln!("::error::{error:#}");
            ExitCode::FAILURE
        }
    }
}
