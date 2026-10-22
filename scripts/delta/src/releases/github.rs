//! Which releases exist and which ones a delta run starts from or removes.

use anyhow::{Context, Result};
use serde::Deserialize;

use crate::releases::tag::Tag;

/// One item of `GET /repos/{repo}/releases` (the wire format; other fields are ignored).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
pub struct ReleaseDto {
    pub tag_name: String,
    pub draft: bool,
    pub prerelease: bool,
    pub published_at: Option<String>,
}

/// A published Explorer release.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Release {
    pub tag: Tag,
    /// GitHub's UTC timestamp (`2026-10-13T09:00:00Z`); these sort chronologically as text.
    pub published_at: String,
}

/// Published `v*` releases: no drafts, no pre-releases (`/releases/latest` never serves them).
/// Other tags (the Avatar Preview Renderer's) are ignored; a malformed `v*` tag is an error.
pub fn explorer_releases(dtos: &[ReleaseDto]) -> Result<Vec<Release>> {
    let mut releases: Vec<Release> = Vec::new();
    for dto in dtos {
        if dto.draft || dto.prerelease || !dto.tag_name.starts_with('v') {
            continue;
        }
        let tag: Tag = dto
            .tag_name
            .parse()
            .with_context(|| format!("malformed release {:?}", dto.tag_name))?;
        let published_at: String = dto
            .published_at
            .clone()
            .with_context(|| format!("release {tag} is published without a publish time"))?;
        releases.push(Release { tag, published_at });
    }
    Ok(releases)
}

/// The `count` most recently published releases other than `current`.
pub fn pick_bases(releases: &[Release], count: usize, current: &Tag) -> Vec<Tag> {
    let mut newest_first: Vec<&Release> = releases.iter().filter(|r: &&Release| &r.tag != current).collect();
    newest_first.sort_by(|a: &&Release, b: &&Release| b.published_at.cmp(&a.published_at));
    newest_first
        .into_iter()
        .take(count)
        .map(|r: &Release| r.tag.clone())
        .collect()
}

/// Releases `keep` .. `keep + window - 1` by descending version, never `live`. Version order,
/// so publishing an older tag late cannot push out a newer release's patches.
pub fn prune_targets(releases: &[Release], keep: usize, window: usize, live: Option<&Tag>) -> Vec<Tag> {
    let mut by_version: Vec<&Tag> = releases.iter().map(|r: &Release| &r.tag).collect();
    by_version.sort_by(|a: &&Tag, b: &&Tag| b.cmp(a));
    by_version
        .into_iter()
        .skip(keep)
        .take(window)
        .filter(|tag: &&Tag| Some(*tag) != live)
        .cloned()
        .collect()
}

/// Reads `gh api repos/<repo>/releases/tags/<tag>`, which serves published releases only: a draft
/// is a 404 like a missing tag. Only a 404 means unpublished; any other failure is an error.
pub fn read_published(succeeded: bool, stdout: &str, stderr: &str) -> Result<bool> {
    if !succeeded {
        anyhow::ensure!(stderr.contains("HTTP 404"), "release lookup failed: {}", stderr.trim());
        return Ok(false);
    }
    let release: ReleaseDto = serde_json::from_str(stdout).context("invalid release lookup response")?;
    Ok(!release.draft)
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{Release, ReleaseDto, explorer_releases, pick_bases, prune_targets, read_published};
    use crate::releases::tag::Tag;

    fn read(json: &str) -> Result<Vec<Release>> {
        let dtos: Vec<ReleaseDto> = serde_json::from_str(json)?;
        explorer_releases(&dtos)
    }

    fn tags(texts: &[&str]) -> Result<Vec<Tag>> {
        let mut parsed: Vec<Tag> = Vec::new();
        for text in texts {
            parsed.push(text.parse()?);
        }
        Ok(parsed)
    }

    // API order (created_at) deliberately differs from published_at order.
    const LISTED: &str = r#"[
      {"tag_name": "v0.188.0-alpha", "draft": true, "prerelease": false, "published_at": null},
      {"tag_name": "v0.186.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-08T14:33:06Z"},
      {"tag_name": "avatar-preview-renderer/v3.4.0", "draft": false, "prerelease": false, "published_at": "2026-10-09T10:00:00Z"},
      {"tag_name": "v0.188.0-rc1", "draft": false, "prerelease": true, "published_at": "2026-10-13T12:00:00Z"},
      {"tag_name": "v0.187.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-13T09:00:00Z", "name": "ignored"},
      {"tag_name": "v0.185.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-06T14:22:59Z"},
      {"tag_name": "v0.184.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-01T19:18:45Z"},
      {"tag_name": "v0.183.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-09-29T15:45:19Z"}
    ]"#;

    // v0.187 was published after v0.188.
    const OUT_OF_ORDER: &str = r#"[
      {"tag_name": "v0.189.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-20T10:00:00Z"},
      {"tag_name": "v0.187.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-16T10:00:00Z"},
      {"tag_name": "v0.188.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-15T10:00:00Z"},
      {"tag_name": "v0.186.10-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-08T10:00:00Z"},
      {"tag_name": "v0.186.9-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-09T10:00:00Z"}
    ]"#;

    #[test]
    fn bases_are_the_newest_published_skipping_drafts_prereleases_and_other_tags() -> Result<()> {
        let releases: Vec<Release> = read(LISTED)?;
        let current: Tag = "v0.188.0-alpha".parse()?;
        ensure!(pick_bases(&releases, 3, &current) == tags(&["v0.187.0-alpha", "v0.186.0-alpha", "v0.185.0-alpha"])?);
        ensure!(pick_bases(&releases, 10, &current).len() == 5);
        ensure!(pick_bases(&releases, 0, &current).is_empty());
        Ok(())
    }

    #[test]
    fn bases_exclude_the_current_tag_and_follow_publish_time() -> Result<()> {
        let releases: Vec<Release> = read(LISTED)?;
        let current: Tag = "v0.187.0-alpha".parse()?;
        ensure!(pick_bases(&releases, 2, &current) == tags(&["v0.186.0-alpha", "v0.185.0-alpha"])?);
        let late: Vec<Release> = read(OUT_OF_ORDER)?;
        let newest: Tag = "v0.189.0-alpha".parse()?;
        ensure!(pick_bases(&late, 2, &newest) == tags(&["v0.187.0-alpha", "v0.188.0-alpha"])?);
        Ok(())
    }

    #[test]
    fn prune_keeps_the_newest_and_takes_the_next_window_by_version() -> Result<()> {
        let releases: Vec<Release> = read(LISTED)?;
        ensure!(prune_targets(&releases, 2, 2, None) == tags(&["v0.185.0-alpha", "v0.184.0-alpha"])?);
        ensure!(prune_targets(&releases, 4, 5, None) == tags(&["v0.183.0-alpha"])?);
        ensure!(prune_targets(&releases, 10, 3, None).is_empty());
        let late: Vec<Release> = read(OUT_OF_ORDER)?;
        ensure!(prune_targets(&late, 2, 5, None) == tags(&["v0.187.0-alpha", "v0.186.10-alpha", "v0.186.9-alpha"])?);
        let live: Tag = "v0.187.0-alpha".parse()?;
        ensure!(prune_targets(&late, 2, 5, Some(&live)) == tags(&["v0.186.10-alpha", "v0.186.9-alpha"])?);
        Ok(())
    }

    #[test]
    fn a_malformed_v_tag_is_an_error_instead_of_being_skipped() -> Result<()> {
        let malformed: Result<Vec<Release>> = read(
            r#"[{"tag_name": "vNext", "draft": false, "prerelease": false, "published_at": "2026-10-20T10:00:00Z"}]"#,
        );
        ensure!(malformed.is_err());
        Ok(())
    }

    #[test]
    fn published_lookup() -> Result<()> {
        ensure!(!read_published(false, "", "gh: Not Found (HTTP 404)")?);
        let draft: &str = r#"{"tag_name": "v1.0.0", "draft": true, "prerelease": false, "published_at": null}"#;
        ensure!(!read_published(true, draft, "")?);
        let published: &str =
            r#"{"tag_name": "v1.0.0", "draft": false, "prerelease": false, "published_at": "2026-10-20T10:00:00Z"}"#;
        ensure!(read_published(true, published, "")?);
        ensure!(read_published(false, "", "gh: Bad Gateway (HTTP 502)").is_err());
        Ok(())
    }
}
