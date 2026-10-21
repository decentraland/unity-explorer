//! Release tags and platforms.

use std::fmt;
use std::str::FromStr;

use anyhow::{Context, Result, anyhow, ensure};
use serde::{Deserialize, Deserializer, Serialize, Serializer};

/// An Explorer release tag: `v` + a semver version without build metadata. Ordered by version,
/// so `v0.186.10` follows `v0.186.9`.
#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct Tag(semver::Version);

impl FromStr for Tag {
    type Err = anyhow::Error;

    fn from_str(text: &str) -> Result<Tag> {
        let version_text: &str = text
            .strip_prefix('v')
            .ok_or_else(|| anyhow!("not a release tag: {text:?}"))?;
        let version: semver::Version =
            semver::Version::parse(version_text).with_context(|| format!("not a release tag: {text:?}"))?;
        ensure!(version.build.is_empty(), "not a release tag: {text:?}");
        Ok(Tag(version))
    }
}

impl fmt::Display for Tag {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "v{}", self.0)
    }
}

impl Serialize for Tag {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.collect_str(self)
    }
}

impl<'de> Deserialize<'de> for Tag {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Tag, D::Error> {
        let text: String = String::deserialize(deserializer)?;
        text.parse().map_err(serde::de::Error::custom)
    }
}

/// A release platform with delta updates. Epic builds are updated by the Epic store.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize, clap::ValueEnum)]
#[serde(rename_all = "lowercase")]
pub enum Platform {
    Windows64,
    Macos,
}

impl fmt::Display for Platform {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let name: &str = match self {
            Platform::Windows64 => "windows64",
            Platform::Macos => "macos",
        };
        f.write_str(name)
    }
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};
    use rstest::rstest;

    use super::Tag;

    #[rstest]
    #[case("v1.0.0")]
    #[case("v0.187.0-alpha")]
    #[case("v1.2.3-rc.1")]
    fn accepts(#[case] text: &str) -> Result<()> {
        let tag: Tag = text.parse()?;
        ensure!(tag.to_string() == text);
        Ok(())
    }

    #[rstest]
    #[case("")]
    #[case("avatar-preview-renderer/v3.4.0")]
    #[case("v1.0.0-../x")]
    #[case("v9.9.9?x")]
    #[case("vNext")]
    #[case("v1.0")]
    #[case("v1.0.0\n")]
    #[case("1.0.0")]
    #[case("v1.0.0+build")]
    fn refuses(#[case] text: &str) -> Result<()> {
        ensure!(text.parse::<Tag>().is_err(), "accepted {text:?}");
        Ok(())
    }

    #[test]
    fn orders_by_version() -> Result<()> {
        let newer: Tag = "v0.186.10-alpha".parse()?;
        let older: Tag = "v0.186.9-alpha".parse()?;
        ensure!(newer > older);
        Ok(())
    }
}
