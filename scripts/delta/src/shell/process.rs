//! Running external tools.

use std::ffi::OsStr;
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

/// Like `capture`, and fails with the tool's output unless it exits with 0.
pub fn run<I, S>(program: &str, args: I) -> Result<Output>
where
    I: IntoIterator<Item = S>,
    S: AsRef<OsStr>,
{
    let output: Output = capture(program, args)?;
    ensure!(
        output.status.success(),
        "{program} failed ({}):\n{}{}",
        output.status,
        String::from_utf8_lossy(&output.stdout),
        String::from_utf8_lossy(&output.stderr)
    );
    Ok(output)
}

pub fn stdout_text(output: &Output) -> String {
    String::from_utf8_lossy(&output.stdout).trim().to_owned()
}
