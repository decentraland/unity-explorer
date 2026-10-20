//! Running external tools.

use std::ffi::OsStr;
use std::path::Path;
use std::process::{Command, Output, Stdio};

use anyhow::{Context, Result, ensure};

/// Runs `program` with `args`, no stdin, capturing its output.
pub fn capture<I, S>(program: &str, args: I) -> Result<Output>
where
    I: IntoIterator<Item = S>,
    S: AsRef<OsStr>,
{
    Command::new(program)
        .args(args)
        .stdin(Stdio::null())
        .output()
        .with_context(|| format!("cannot run {program}"))
}

/// The values that must never reach an error message, which ends up in the run document and
/// the job summary: the bucket name and every credential the tools run with.
const SECRET_VARIABLES: [&str; 5] = [
    "EXPLORER_TEAM_S3_BUCKET",
    "AWS_ACCESS_KEY_ID",
    "AWS_SECRET_ACCESS_KEY",
    "AWS_SESSION_TOKEN",
    "GH_TOKEN",
];

/// `text` with every one of `values` replaced by `***`.
fn redact(text: String, values: &[String]) -> String {
    values
        .iter()
        .filter(|value: &&String| !value.is_empty())
        .fold(text, |text: String, value: &String| text.replace(value, "***"))
}

/// `text` with every secret value of the environment, and every AWS ARN (it names the account
/// and the IAM principal), replaced by `***`.
pub fn redacted(text: String) -> String {
    let values: Vec<String> = SECRET_VARIABLES
        .iter()
        .filter_map(|name: &&str| std::env::var(name).ok())
        .collect();
    redact_arns(&redact(text, &values))
}

/// `text` with every `arn:aws...` token, up to whitespace or a quote, replaced by `arn:***`.
fn redact_arns(text: &str) -> String {
    let mut out: String = String::with_capacity(text.len());
    let mut rest: &str = text;
    while let Some((before, after)) = rest.split_once("arn:aws") {
        out.push_str(before);
        out.push_str("arn:***");
        let end: usize = after
            .find(|c: char| c.is_whitespace() || c == '"' || c == '\'')
            .unwrap_or(after.len());
        rest = after.get(end..).unwrap_or_default();
    }
    out.push_str(rest);
    out
}

/// Like `capture`, and fails with the tool's output (secrets redacted) unless it exits with 0.
pub fn run<I, S>(program: &str, args: I) -> Result<Output>
where
    I: IntoIterator<Item = S>,
    S: AsRef<OsStr>,
{
    let output: Output = capture(program, args)?;
    ensure!(
        output.status.success(),
        "{program} failed ({}):\n{}",
        output.status,
        redacted(format!(
            "{}{}",
            String::from_utf8_lossy(&output.stdout),
            String::from_utf8_lossy(&output.stderr)
        ))
    );
    Ok(output)
}

pub fn stdout_text(output: &Output) -> String {
    String::from_utf8_lossy(&output.stdout).trim().to_owned()
}

pub fn path_text(path: &Path) -> Result<&str> {
    path.to_str()
        .with_context(|| format!("non-UTF-8 path {}", path.display()))
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{redact, redact_arns};

    #[test]
    fn secret_values_are_replaced() -> Result<()> {
        let values: Vec<String> = vec!["bucket-name-test".to_owned(), String::new()];
        let text: String = redact("upload failed: s3://bucket-name-test/releases/x".to_owned(), &values);
        ensure!(text == "upload failed: s3://***/releases/x", "{text}");
        Ok(())
    }

    #[test]
    fn arns_are_replaced() -> Result<()> {
        let text: String = redact_arns(
            "User: arn:aws:iam::000000000000:user/example is not authorized to perform: s3:PutObject on \
             resource: \"arn:aws:s3:::bucket-name-test/x\" because 'arn:aws:iam::000000000000:role/r' ends",
        );
        ensure!(
            text == "User: arn:*** is not authorized to perform: s3:PutObject on resource: \"arn:***\" because \
                    'arn:***' ends",
            "{text}"
        );
        ensure!(redact_arns("no arn here, arn:aws") == "no arn here, arn:***");
        Ok(())
    }
}
