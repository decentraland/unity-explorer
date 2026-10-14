//! The production I/O: GitHub through `gh`, the bucket through `aws` and `@dcl/cdn-uploader`,
//! and code signatures through `osslsigncode` and `codesign`. Settings are read from the
//! environment when a call runs, so they never reach the run document.

use std::env;
use std::path::Path;
use std::process::Output;

use anyhow::{Context, Result};

use crate::io::{Bucket, Caching, Releases, Signer};
use crate::publishing::values::ETag;
use crate::releases::github::{ReleaseDto, read_published};
use crate::releases::tag::{Platform, Tag};
use crate::shell::process::{capture, path_text, redacted, run, stdout_text};

fn variable(name: &str) -> Result<String> {
    let value: String = env::var(name).with_context(|| format!("{name} is not set"))?;
    anyhow::ensure!(!value.is_empty(), "{name} is empty");
    Ok(value)
}

/// GitHub releases of `$GITHUB_REPOSITORY`, through `gh` (authenticated by `GH_TOKEN`).
#[derive(Debug, Default)]
pub struct GhReleases;

impl GhReleases {
    /// Every release of the repository, as the API lists them.
    pub fn list(&self) -> Result<Vec<ReleaseDto>> {
        let repository: String = variable("GITHUB_REPOSITORY")?;
        let output: Output = run("gh", ["api", &format!("repos/{repository}/releases?per_page=100")])?;
        serde_json::from_slice(&output.stdout).context("invalid releases list")
    }
}

impl Releases for GhReleases {
    fn is_published(&self, tag: &Tag) -> Result<bool> {
        let repository: String = variable("GITHUB_REPOSITORY")?;
        let output: Output = capture("gh", ["api", &format!("repos/{repository}/releases/tags/{tag}")])?;
        read_published(
            output.status.success(),
            &String::from_utf8_lossy(&output.stdout),
            &redacted(String::from_utf8_lossy(&output.stderr).into_owned()),
        )
    }
}

/// The bucket `$EXPLORER_TEAM_S3_BUCKET`: reads and removals through the `aws` CLI, uploads
/// through the program named by `$CDN_UPLOADER`, `@dcl/cdn-uploader` installed by CI from the
/// committed lockfile in `scripts/delta/uploader`; it runs with the bucket's write credentials,
/// so an install resolved on the fly is never an option.
#[derive(Debug, Default)]
pub struct S3Bucket;

impl Bucket for S3Bucket {
    fn remove_prefix(&self, prefix: &str) -> Result<()> {
        let bucket: String = variable("EXPLORER_TEAM_S3_BUCKET")?;
        run(
            "aws",
            [
                "s3",
                "rm",
                "--recursive",
                "--only-show-errors",
                &format!("s3://{bucket}/{prefix}"),
            ],
        )?;
        Ok(())
    }

    /// A missing key is `Ok(None)`; any other failure (credentials, throttling, network) is an
    /// error.
    fn download(&self, key: &str, to: &Path) -> Result<Option<ETag>> {
        let bucket: String = variable("EXPLORER_TEAM_S3_BUCKET")?;
        let output: Output = capture(
            "aws",
            [
                "s3api",
                "get-object",
                "--bucket",
                &bucket,
                "--key",
                key,
                path_text(to)?,
                "--query",
                "ETag",
                "--output",
                "text",
            ],
        )?;
        if !output.status.success() {
            let stderr: String = String::from_utf8_lossy(&output.stderr).into_owned();
            if stderr.contains("NoSuchKey") || stderr.contains("(404)") {
                return Ok(None);
            }
            anyhow::bail!("aws failed ({}):\n{}", output.status, redacted(stderr));
        }
        Ok(Some(
            ETag::try_new(stdout_text(&output)).context("aws returned no ETag")?,
        ))
    }

    fn etag(&self, key: &str) -> Result<ETag> {
        let bucket: String = variable("EXPLORER_TEAM_S3_BUCKET")?;
        let output: Output = run(
            "aws",
            [
                "s3api",
                "head-object",
                "--bucket",
                &bucket,
                "--key",
                key,
                "--query",
                "ETag",
                "--output",
                "text",
            ],
        )?;
        ETag::try_new(stdout_text(&output)).context("aws returned no ETag")
    }

    fn upload(&self, folder: &Path, prefix: &str, caching: Caching) -> Result<()> {
        let bucket: String = variable("EXPLORER_TEAM_S3_BUCKET")?;
        let program: String = variable("CDN_UPLOADER")?;
        let mut args: Vec<&str> = Vec::new();
        args.extend([
            "--bucket",
            &bucket,
            "--local-folder",
            path_text(folder)?,
            "--bucket-folder",
            prefix,
            "--variant",
            "uncompressed",
        ]);
        if caching == Caching::Immutable {
            args.push("--immutable");
        }
        run(&program, args)?;
        Ok(())
    }
}

/// `osslsigncode verify` (Authenticode) for windows64, `codesign --verify --deep --strict` for macos.
#[derive(Debug, Default)]
pub struct SystemSigner;

impl Signer for SystemSigner {
    fn verifies(&self, platform: Platform, artifact: &Path) -> Result<bool> {
        let artifact_text: &str = path_text(artifact)?;
        let output: Output = match platform {
            Platform::Windows64 => capture("osslsigncode", ["verify", "-in", artifact_text])?,
            Platform::Macos => capture("codesign", ["--verify", "--deep", "--strict", artifact_text])?,
        };
        Ok(output.status.success())
    }
}
