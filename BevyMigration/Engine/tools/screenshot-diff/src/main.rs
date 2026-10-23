//! `screenshot-diff <expected.png> <actual.png> [--out diff.png] [--tolerance 24] [--max-fraction 0.02] [--max-mean 4.0]`
//! Exit code 0 when the comparison is within the threshold, 1 otherwise, 2 on usage or I/O errors.

use screenshot_diff::{Threshold, compare, read_png, write_png};
use std::{path::PathBuf, process::ExitCode};

fn main() -> ExitCode {
    match run(std::env::args().skip(1).collect()) {
        Ok(passed) => {
            if passed {
                ExitCode::SUCCESS
            } else {
                ExitCode::from(1)
            }
        }
        Err(error) => {
            eprintln!("screenshot-diff: {error}");
            ExitCode::from(2)
        }
    }
}

fn run(args: Vec<String>) -> Result<bool, String> {
    let mut positional = Vec::new();
    let mut out = None;
    let mut threshold = Threshold::default();
    let mut i = 0;

    while i < args.len() {
        let arg = &args[i];
        let value = args.get(i + 1);

        match arg.as_str() {
            "--out" => {
                out = Some(PathBuf::from(value.ok_or("--out needs a path")?));
                i += 2;
            }
            "--tolerance" => {
                threshold.pixel_tolerance = value
                    .and_then(|v| v.parse().ok())
                    .ok_or("--tolerance needs 0..255")?;
                i += 2;
            }
            "--max-fraction" => {
                threshold.max_differing_fraction = value
                    .and_then(|v| v.parse().ok())
                    .ok_or("--max-fraction needs a number")?;
                i += 2;
            }
            "--max-mean" => {
                threshold.max_mean_abs_diff = value
                    .and_then(|v| v.parse().ok())
                    .ok_or("--max-mean needs a number")?;
                i += 2;
            }
            _ => {
                positional.push(PathBuf::from(arg));
                i += 1;
            }
        }
    }

    let (Some(expected), Some(actual)) = (positional.first(), positional.get(1)) else {
        return Err("usage: screenshot-diff <expected.png> <actual.png> [--out diff.png] [--tolerance N] [--max-fraction F] [--max-mean M]".to_owned());
    };

    let (comparison, diff) = compare(
        &read_png(expected)?,
        &read_png(actual)?,
        threshold.pixel_tolerance,
    )?;

    if let Some(out) = out {
        write_png(&out, &diff)?;
    }

    let passed = comparison.passes(&threshold);
    println!(
        "mean abs diff {:.3} (max {:.3}), differing pixels {:.3}% (max {:.3}%): {}",
        comparison.mean_abs_diff,
        threshold.max_mean_abs_diff,
        comparison.differing_fraction * 100.0,
        threshold.max_differing_fraction * 100.0,
        if passed { "PASS" } else { "FAIL" }
    );
    Ok(passed)
}
