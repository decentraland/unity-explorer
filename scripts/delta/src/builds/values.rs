//! Simple types of a build. Each can only hold a valid value: construction goes through the
//! rule, and so does deserialization.

use nutype::nutype;

/// No empty, `.` or `..` segment; no `\` (a Windows separator), `:` (drive letters, NTFS
/// streams) or control character.
fn is_rel_path(value: &str) -> bool {
    let unsafe_char: bool = value.chars().any(|c: char| c.is_control() || c == '\\' || c == ':');
    let unsafe_segment: bool = value.split('/').any(|s: &str| s.is_empty() || s == "." || s == "..");
    !unsafe_char && !unsafe_segment
}

fn is_sha256(value: &str) -> bool {
    value.len() == 64
        && value
            .chars()
            .all(|c: char| c.is_ascii_digit() || ('a'..='f').contains(&c))
}

fn is_mode(value: &str) -> bool {
    (3..=4).contains(&value.len()) && value.chars().all(|c: char| ('0'..='7').contains(&c))
}

/// A path inside a build, relative to its root, `/`-separated, that cannot leave the root.
#[nutype(
    validate(predicate = is_rel_path),
    derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, AsRef, Display, FromStr, Serialize, Deserialize)
)]
pub struct RelPath(String);

impl RelPath {
    pub fn as_str(&self) -> &str {
        self.as_ref()
    }

    /// The path below `root` on this machine.
    pub fn under(&self, root: &std::path::Path) -> std::path::PathBuf {
        root.join(self.as_str())
    }
}

/// A lowercase hex SHA-256 digest.
#[nutype(
    validate(predicate = is_sha256),
    derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, AsRef, Display, Serialize, Deserialize)
)]
pub struct Sha256(String);

/// Unix permission bits as octal digits, e.g. `755`.
#[nutype(
    validate(predicate = is_mode),
    derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, AsRef, Display, Serialize, Deserialize)
)]
pub struct Mode(String);

impl Mode {
    pub fn bits(&self) -> u32 {
        let text: &str = self.as_ref();
        u32::from_str_radix(text, 8).unwrap_or(0o644)
    }
}

#[cfg(test)]
mod tests {
    use super::{Mode, RelPath, Sha256};
    use rstest::rstest;

    #[rstest]
    #[case("a")]
    #[case("Data/space dir/asset file.bin")]
    #[case("x.app/Contents/Info.plist")]
    #[case(".hidden")]
    fn rel_path_accepts(#[case] path: &str) -> anyhow::Result<()> {
        let parsed: RelPath = RelPath::try_new(path.to_owned())?;
        anyhow::ensure!(parsed.as_str() == path);
        Ok(())
    }

    #[rstest]
    #[case("")]
    #[case("/etc/passwd")]
    #[case("a/../b")]
    #[case("..")]
    #[case("./a")]
    #[case("a//b")]
    #[case("a/")]
    #[case("..\\..\\x.dll")]
    #[case("C:x.dll")]
    #[case("\\\\host\\share\\x")]
    #[case("Decentraland.exe:ads")]
    #[case("a\u{0}b")]
    #[case("a\nb")]
    #[case("a\tb")]
    fn rel_path_refuses(#[case] path: &str) -> anyhow::Result<()> {
        anyhow::ensure!(RelPath::try_new(path.to_owned()).is_err(), "accepted {path:?}");
        Ok(())
    }

    #[test]
    fn rel_path_is_checked_when_read_from_json() -> anyhow::Result<()> {
        let read: Result<Vec<RelPath>, serde_json::Error> = serde_json::from_str(r#"["ok", "C:x"]"#);
        anyhow::ensure!(read.is_err());
        Ok(())
    }

    #[test]
    fn sha256_and_mode_rules() -> anyhow::Result<()> {
        anyhow::ensure!(Sha256::try_new("a".repeat(64)).is_ok());
        anyhow::ensure!(Sha256::try_new("A".repeat(64)).is_err());
        anyhow::ensure!(Sha256::try_new("a".repeat(63)).is_err());
        anyhow::ensure!(Mode::try_new("755".to_owned())?.bits() == 0o755);
        anyhow::ensure!(Mode::try_new("4755".to_owned()).is_ok());
        anyhow::ensure!(Mode::try_new("999".to_owned()).is_err());
        anyhow::ensure!(Mode::try_new("75".to_owned()).is_err());
        Ok(())
    }
}
