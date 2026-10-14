//! The production capabilities: GitHub through `gh`, the bucket through `aws`, the CDN through
//! `@dcl/cdn-uploader`, and code signatures through `osslsigncode` and `codesign`. Settings are
//! read from the environment when a call runs, so they never reach the run document.

use std::env;
use std::path::Path;
use std::process::Output;

use anyhow::{Context, Result};

use crate::capabilities::{Caching, Cdn, Releases, Signer, Storage};
use crate::publishing::values::ETag;
use crate::releases::github::{ReleaseDto, read_published};
use crate::releases::tag::{Platform, Tag};
use crate::shell::process::{capture, run, stdout_text};

fn variable(name: &str) -> Result<String> {
    let value: String = env::var(name).with_context(|| format!("{name} is not set"))?;
    anyhow::ensure!(!value.is_empty(), "{name} is empty");
    Ok(value)
}

fn path_text(path: &Path) -> Result<&str> {
    path.to_str()
        .with_context(|| format!("non-UTF-8 path {}", path.display()))
}

/// GitHub releases of `$GITHUB_REPOSITORY`, through `gh` (authenticated by `GH_TOKEN`).
#[derive(Debug, Default)]
pub struct GhReleases;

impl Releases for GhReleases {
    fn is_published(&self, tag: &Tag) -> Result<bool> {
        let repository: String = variable("GITHUB_REPOSITORY")?;
        let output: Output = capture("gh", ["api", &format!("repos/{repository}/releases/tags/{tag}")])?;
        read_published(
            output.status.success(),
            &String::from_utf8_lossy(&output.stdout),
            &String::from_utf8_lossy(&output.stderr),
        )
    }

    fn list(&self) -> Result<Vec<ReleaseDto>> {
        let repository: String = variable("GITHUB_REPOSITORY")?;
        let output: Output = run("gh", ["api", &format!("repos/{repository}/releases?per_page=100")])?;
        serde_json::from_slice(&output.stdout).context("invalid releases list")
    }
}

/// The bucket `$EXPLORER_TEAM_S3_BUCKET`, through the `aws` CLI.
#[derive(Debug, Default)]
pub struct AwsStorage;

impl Storage for AwsStorage {
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

    fn download(&self, key: &str, to: &Path) -> Result<ETag> {
        let bucket: String = variable("EXPLORER_TEAM_S3_BUCKET")?;
        let output: Output = run(
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
        ETag::try_new(stdout_text(&output)).context("aws returned no ETag")
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
}

/// Uploads with `npx @dcl/cdn-uploader@next` into `$EXPLORER_TEAM_S3_BUCKET`.
#[derive(Debug, Default)]
pub struct CdnUploader;

impl Cdn for CdnUploader {
    fn upload(&self, folder: &Path, prefix: &str, caching: Caching) -> Result<()> {
        let bucket: String = variable("EXPLORER_TEAM_S3_BUCKET")?;
        let mut args: Vec<&str> = vec![
            "@dcl/cdn-uploader@next",
            "--bucket",
            &bucket,
            "--local-folder",
            path_text(folder)?,
            "--bucket-folder",
            prefix,
            "--variant",
            "uncompressed",
        ];
        if caching == Caching::Immutable {
            args.push("--immutable");
        }
        run("npx", args)?;
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
