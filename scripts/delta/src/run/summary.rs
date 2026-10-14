//! The Markdown job summary of a run.

use crate::publishing::manifest::Manifest;
use crate::run::stage::{Run, Stage};

const MEBIBYTE: u64 = 1_048_576;

/// `12.3 MB`, rounded down to a tenth.
fn megabytes(bytes: u64) -> String {
    let tenths: u64 = bytes.saturating_mul(10).checked_div(MEBIBYTE).unwrap_or(0);
    let whole: u64 = tenths.checked_div(10).unwrap_or(0);
    let tenth: u64 = tenths.checked_rem(10).unwrap_or(0);
    format!("{whole}.{tenth} MB")
}

/// Percent of the full zip a launcher does not download.
fn saving(download: u64, zip: u64) -> u64 {
    let percent: u64 = download.saturating_mul(100).checked_div(zip).unwrap_or(100);
    100_u64.saturating_sub(percent)
}

fn table(manifest: &Manifest) -> Vec<String> {
    let mut lines: Vec<String> = vec![
        format!("### Delta patches: {} -> {}", manifest.platform(), manifest.target()),
        String::new(),
        "| From | Patched | Whole | Deleted | Download | Full zip | Saving |".to_owned(),
        "|---|---|---|---|---|---|---|".to_owned(),
    ];
    for (base, delta) in manifest.deltas() {
        let download: u64 = delta.download_size();
        lines.push(format!(
            "| {base} | {} | {} | {} | {} | {} | {}% |",
            delta.patches().len(),
            delta.whole().len(),
            delta.deleted().len(),
            megabytes(download),
            megabytes(manifest.zip().size),
            saving(download, manifest.zip().size),
        ));
    }
    lines
}

pub fn summary(run: &Run) -> String {
    let mut lines: Vec<String> = match &run.stage {
        Stage::Published(published) => table(&published.manifest),
        Stage::Failed { at, reason } => {
            vec![
                format!("### Delta patches: failed at `{at}`"),
                String::new(),
                "```".to_owned(),
                reason.clone(),
                "```".to_owned(),
            ]
        }
        other => vec![format!("### Delta patches: stopped at {}", other.name())],
    };
    if !run.drops.is_empty() {
        lines.push(String::new());
        lines.push("Left out:".to_owned());
        for dropped in &run.drops {
            lines.push(format!("- {}: {}", dropped.base, dropped.reason));
        }
    }
    lines.push(String::new());
    lines.join("\n")
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{megabytes, saving};

    #[test]
    fn sizes_and_savings() -> Result<()> {
        ensure!(megabytes(0) == "0.0 MB");
        ensure!(megabytes(1_572_864) == "1.5 MB");
        ensure!(saving(30, 460) == 94);
        ensure!(saving(10, 0) == 0);
        Ok(())
    }
}
