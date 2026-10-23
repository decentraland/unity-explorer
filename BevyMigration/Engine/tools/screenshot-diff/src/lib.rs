//! Screenshot comparison for the rendering gates (BevyMigration/docs/README.md § 7, steps 3 to 5):
//! Unity's frame against Bevy's, same camera, same content, within a per-case threshold.

use std::{fs::File, io::BufWriter, path::Path};

/// An RGB8 image with tightly packed rows.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Rgb {
    pub width: u32,
    pub height: u32,
    pub data: Vec<u8>,
}

/// How two screenshots differ.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Comparison {
    /// Mean absolute channel difference over every pixel, 0 to 255.
    pub mean_abs_diff: f64,
    /// Share of pixels whose largest channel difference exceeds `pixel_tolerance`, 0 to 1.
    pub differing_fraction: f64,
}

/// Acceptance limits of one case.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Threshold {
    /// A pixel counts as different above this channel difference (0 to 255).
    pub pixel_tolerance: u8,
    /// Highest acceptable [`Comparison::differing_fraction`].
    pub max_differing_fraction: f64,
    /// Highest acceptable [`Comparison::mean_abs_diff`].
    pub max_mean_abs_diff: f64,
}

impl Default for Threshold {
    fn default() -> Self {
        Self {
            pixel_tolerance: 24,
            max_differing_fraction: 0.02,
            max_mean_abs_diff: 4.0,
        }
    }
}

impl Comparison {
    pub fn passes(&self, threshold: &Threshold) -> bool {
        self.differing_fraction <= threshold.max_differing_fraction
            && self.mean_abs_diff <= threshold.max_mean_abs_diff
    }
}

pub fn read_png(path: &Path) -> Result<Rgb, String> {
    let file = File::open(path).map_err(|e| format!("{}: {e}", path.display()))?;
    let decoder = png::Decoder::new(file);
    let mut reader = decoder
        .read_info()
        .map_err(|e| format!("{}: {e}", path.display()))?;
    let mut buffer = vec![0; reader.output_buffer_size()];
    let info = reader
        .next_frame(&mut buffer)
        .map_err(|e| format!("{}: {e}", path.display()))?;
    let bytes = buffer
        .get(..info.buffer_size())
        .ok_or_else(|| format!("{}: short frame", path.display()))?;

    let channels = match (info.color_type, info.bit_depth) {
        (png::ColorType::Rgb, png::BitDepth::Eight) => 3,
        (png::ColorType::Rgba, png::BitDepth::Eight) => 4,
        (png::ColorType::Grayscale, png::BitDepth::Eight) => 1,
        (color, depth) => {
            return Err(format!(
                "{}: unsupported PNG layout {color:?} {depth:?}",
                path.display()
            ));
        }
    };

    let mut data = Vec::with_capacity((info.width * info.height * 3) as usize);

    for pixel in bytes.chunks_exact(channels) {
        match channels {
            1 => data.extend_from_slice(&[pixel[0], pixel[0], pixel[0]]),
            _ => data.extend_from_slice(&pixel[..3]),
        }
    }

    Ok(Rgb {
        width: info.width,
        height: info.height,
        data,
    })
}

pub fn write_png(path: &Path, image: &Rgb) -> Result<(), String> {
    let file = File::create(path).map_err(|e| format!("{}: {e}", path.display()))?;
    let mut encoder = png::Encoder::new(BufWriter::new(file), image.width, image.height);
    encoder.set_color(png::ColorType::Rgb);
    encoder.set_depth(png::BitDepth::Eight);
    let mut writer = encoder
        .write_header()
        .map_err(|e| format!("{}: {e}", path.display()))?;
    writer
        .write_image_data(&image.data)
        .map_err(|e| format!("{}: {e}", path.display()))
}

/// Compares two same-sized images; the returned diff image is white where the pixel differs beyond
/// the tolerance and darkened otherwise.
pub fn compare(
    expected: &Rgb,
    actual: &Rgb,
    pixel_tolerance: u8,
) -> Result<(Comparison, Rgb), String> {
    if expected.width != actual.width || expected.height != actual.height {
        return Err(format!(
            "size mismatch: expected {}x{}, actual {}x{}",
            expected.width, expected.height, actual.width, actual.height
        ));
    }

    let pixel_count = (expected.width as usize) * (expected.height as usize);

    if pixel_count == 0 {
        return Err("empty image".to_owned());
    }

    let mut diff = Vec::with_capacity(expected.data.len());
    let mut sum_abs: u64 = 0;
    let mut differing: usize = 0;

    for (a, b) in expected
        .data
        .chunks_exact(3)
        .zip(actual.data.chunks_exact(3))
    {
        let mut largest = 0u8;

        for (&x, &y) in a.iter().zip(b) {
            let delta = x.abs_diff(y);
            sum_abs += u64::from(delta);
            largest = largest.max(delta);
        }

        if largest > pixel_tolerance {
            differing += 1;
            diff.extend_from_slice(&[255, 255, 255]);
        } else {
            diff.extend_from_slice(&[a[0] / 4, a[1] / 4, a[2] / 4]);
        }
    }

    // Ratios through fixed-point integers so no cast can lose precision.
    let total = u64::try_from(pixel_count).map_err(|e| e.to_string())?;
    let mean_milli = sum_abs
        .checked_mul(1000)
        .and_then(|s| s.checked_div(total.checked_mul(3)?))
        .ok_or("overflow")?;
    let fraction_micro = u64::try_from(differing)
        .map_err(|e| e.to_string())?
        .checked_mul(1_000_000)
        .and_then(|d| d.checked_div(total))
        .ok_or("overflow")?;
    let mean_milli = u32::try_from(mean_milli).map_err(|e| e.to_string())?;
    let fraction_micro = u32::try_from(fraction_micro).map_err(|e| e.to_string())?;

    let comparison = Comparison {
        mean_abs_diff: f64::from(mean_milli) / 1000.0,
        differing_fraction: f64::from(fraction_micro) / 1_000_000.0,
    };

    Ok((
        comparison,
        Rgb {
            width: expected.width,
            height: expected.height,
            data: diff,
        },
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn solid(width: u32, height: u32, rgb: [u8; 3]) -> Rgb {
        Rgb {
            width,
            height,
            data: (0..width * height).flat_map(|_| rgb).collect(),
        }
    }

    #[test]
    fn identical_images_pass_and_differing_ones_fail() {
        let a = solid(4, 4, [10, 20, 30]);
        let same = compare(&a, &a, 0);
        assert!(same.as_ref().is_ok_and(
            |(c, _)| c.differing_fraction < f64::EPSILON && c.mean_abs_diff < f64::EPSILON
        ));

        let mut b = a.clone();
        b.data[0] = 255;
        let result = compare(&a, &b, 24);
        assert!(result.is_ok());
        let Ok((comparison, diff)) = result else {
            return;
        };
        assert!((comparison.differing_fraction - 1.0 / 16.0).abs() < 1e-6);
        assert_eq!(&diff.data[..3], &[255, 255, 255]);
        assert!(!comparison.passes(&Threshold {
            pixel_tolerance: 24,
            max_differing_fraction: 0.01,
            max_mean_abs_diff: 4.0
        }));
        assert!(comparison.passes(&Threshold {
            pixel_tolerance: 24,
            max_differing_fraction: 0.1,
            max_mean_abs_diff: 10.0
        }));
    }

    #[test]
    fn rejects_size_mismatch() {
        assert!(compare(&solid(2, 2, [0; 3]), &solid(3, 2, [0; 3]), 0).is_err());
    }
}
