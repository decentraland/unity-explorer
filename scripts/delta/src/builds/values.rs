//! Simple types of a build. Each can only hold a valid value: construction goes through the
//! rule, and so does deserialization.

use nutype::nutype;

/// A control character, `\` (a Windows separator), `:` (drive letters, NTFS streams) or one of
/// `< > " | ? *`, which Win32 refuses in a name.
pub fn has_unsafe_char(text: &str) -> bool {
    text.chars()
        .any(|c: char| c.is_control() || matches!(c, '\\' | ':' | '<' | '>' | '"' | '|' | '?' | '*'))
}

/// `CON`, `PRN`, `AUX`, `NUL`, and `COM` or `LPT` followed by `0` to `9`, `¹`, `²` or `³`, with
/// or without an extension: Win32 opens the device instead of a file of that name.
fn is_windows_device(segment: &str) -> bool {
    let stem: String = segment.split('.').next().unwrap_or_default().to_ascii_uppercase();
    matches!(stem.as_str(), "CON" | "PRN" | "AUX" | "NUL")
        || stem
            .strip_prefix("COM")
            .or_else(|| stem.strip_prefix("LPT"))
            .is_some_and(|port: &str| {
                matches!(
                    port,
                    "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" | "¹" | "²" | "³"
                )
            })
}

/// No empty, `.` or `..` segment, no segment ending in `.` or a space (NTFS drops them, so two
/// names on a Unix runner are one on Windows), no Windows device name, and no unsafe character.
fn is_rel_path(value: &str) -> bool {
    let unsafe_segment: bool = value
        .split('/')
        .any(|s: &str| s.is_empty() || s == "." || s == ".." || s.ends_with(['.', ' ']) || is_windows_device(s));
    !has_unsafe_char(value) && !unsafe_segment
}

/// Lowercase hexadecimal digits only.
pub fn is_lower_hex(text: &str) -> bool {
    text.chars()
        .all(|c: char| c.is_ascii_digit() || ('a'..='f').contains(&c))
}

fn is_sha256(value: &str) -> bool {
    value.len() == 64 && is_lower_hex(value)
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
    #[case("COM10")]
    #[case("CONFIG")]
    #[case("console.log")]
    #[case("lpt")]
    #[case("a.b.c")]
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
    #[case("Decentraland.exe.")]
    #[case("Foo.dll ")]
    #[case("sub/trailing. /x")]
    #[case("CON")]
    #[case("con.txt")]
    #[case("Data/aux")]
    #[case("COM1")]
    #[case("lpt9.log")]
    #[case("COM0")]
    #[case("lpt³.txt")]
    #[case("Data/COM¹.dll")]
    #[case("a<b")]
    #[case("a>b")]
    #[case("a\"b")]
    #[case("a|b")]
    #[case("a?b")]
    #[case("a*b")]
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
