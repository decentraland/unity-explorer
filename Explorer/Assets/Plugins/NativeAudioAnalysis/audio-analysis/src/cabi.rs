use fundsp::fft;

const BANDS_COUNT: usize = 8;

#[repr(C)]
pub struct AudioAnalysis {
    pub amplitude: f32,
    pub bands: [f32; BANDS_COUNT],
    pub spectral_centroid: f32,
    pub spectral_flux: f32,
    pub onset: bool,
    pub bpm: f32,
}

#[unsafe(no_mangle)]
pub extern "C" fn analyze_audio_buffer_fundsp(
    buffer: *const f32,
    len: usize,
    sample_rate: f32,

    onset_treshold: f32, // assumed to be 2.5 by default
) -> AudioAnalysis {
    if buffer.is_null() || len < 2 || sample_rate <= 0.0 {
        return AudioAnalysis {
            amplitude: 0.0,
            bands: [0.0; BANDS_COUNT],
            spectral_centroid: 0.0,
            spectral_flux: 0.0,
            onset: false,
            bpm: 0.0,
        };
    }

    let samples_full = unsafe { std::slice::from_raw_parts(buffer, len) };

    // FunDSP's real_fft requires power-of-two length between 2 and 32768.
    // Use largest power-of-two <= len, clamped to 32768.
    let mut n = largest_power_of_two(len.min(32768));
    if n < 2 {
        n = 2;
    }
    let samples = &samples_full[..n];

    // Amplitude (RMS)
    let amplitude = rms(samples);

    // FFT via FunDSP (fundsp::fft::real_fft)
    // Output length = n/2 + 1 Complex32 bins
    let mut spec = vec![Default::default(); n / 2 + 1];
    fft::real_fft(samples, &mut spec);

    // Convert Complex32 to magnitude spectrum
    let spectrum: Vec<f32> = spec
        .iter()
        .map(|c| (c.re * c.re + c.im * c.im).sqrt())
        .collect();

    // 8 frequency bands
    let bands = compute_bands(&spectrum, sample_rate);

    // Spectral centroid
    let spectral_centroid = compute_centroid(&spectrum, sample_rate);

    // Spectral flux (approx: positive diffs between neighboring bins)
    let spectral_flux = compute_flux(&spectrum);

    // Onset: simple threshold on flux
    let onset = spectral_flux > onset_treshold;

    // Crude BPM via autocorrelation of energy envelope
    let bpm = estimate_bpm(samples, sample_rate);

    AudioAnalysis {
        amplitude,
        bands,
        spectral_centroid,
        spectral_flux,
        onset,
        bpm,
    }
}

fn largest_power_of_two(n: usize) -> usize {
    // clear lowest set bit until only highest remains
    if n == 0 {
        return 0;
    }
    1 << (usize::BITS - 1 - n.leading_zeros())
}

fn rms(samples: &[f32]) -> f32 {
    let sum: f32 = samples.iter().map(|x| x * x).sum();
    (sum / samples.len() as f32).sqrt()
}

fn compute_bands(spectrum: &[f32], sample_rate: f32) -> [f32; 8] {
    let mut bands = [0.0f32; BANDS_COUNT];

    let nyquist = sample_rate * 0.5;
    let edges = [60.0, 120.0, 250.0, 500.0, 1000.0, 2000.0, 4000.0, nyquist];

    for (i, edge) in edges.iter().enumerate() {
        let start_freq = if i == 0 { 0.0 } else { edges[i - 1] };
        let start_bin = freq_to_bin(start_freq, sample_rate, spectrum.len());
        let end_bin = freq_to_bin(*edge, sample_rate, spectrum.len());

        let mut sum = 0.0;
        let mut count = 0usize;
        for bin in start_bin..end_bin {
            if bin < spectrum.len() {
                sum += spectrum[bin];
                count += 1;
            }
        }
        bands[i] = if count > 0 { sum / (count as f32) } else { 0.0 };
    }

    bands
}

fn freq_to_bin(freq: f32, sample_rate: f32, bins: usize) -> usize {
    let nyquist = sample_rate * 0.5;
    if nyquist <= 0.0 || bins < 2 {
        return 0;
    }
    // bins cover 0..=Nyquist
    let bin_f = freq / nyquist * (bins as f32 - 1.0);
    bin_f.round().clamp(0.0, (bins as f32 - 1.0)) as usize
}

fn compute_centroid(spectrum: &[f32], sample_rate: f32) -> f32 {
    if spectrum.is_empty() || sample_rate <= 0.0 {
        return 0.0;
    }

    let nyquist = sample_rate * 0.5;
    let bin_hz = nyquist / (spectrum.len() as f32 - 1.0);

    let mut weighted_sum = 0.0;
    let mut total = 0.0;

    for (i, mag) in spectrum.iter().enumerate() {
        let freq = i as f32 * bin_hz;
        weighted_sum += freq * *mag;
        total += *mag;
    }

    if total <= 1e-6 {
        0.0
    } else {
        weighted_sum / total
    }
}

fn compute_flux(spectrum: &[f32]) -> f32 {
    // Approximation: flux across bins instead of frames.
    // for more detailed presice flux, we should keep previous frame in state.
    // But let's assume the current flux is enough and implement state management if needed
    let mut flux = 0.0;
    for i in 1..spectrum.len() {
        let diff = spectrum[i] - spectrum[i - 1];
        if diff > 0.0 {
            flux += diff;
        }
    }
    flux
}

fn estimate_bpm(samples: &[f32], sample_rate: f32) -> f32 {
    if samples.len() < 4 || sample_rate <= 0.0 {
        return 0.0;
    }

    // Energy envelope
    let energy: Vec<f32> = samples.iter().map(|x| x * x).collect();
    let ac = autocorrelate(&energy);

    // Search lags corresponding to ~40ms .. 1s (60–1500 BPM)
    let min_lag = (sample_rate * 0.04) as usize;
    let max_lag = (sample_rate * 1.0) as usize;

    let mut best_lag = 0usize;
    let mut best_val = 0.0;

    for lag in min_lag..ac.len().min(max_lag) {
        let v = ac[lag];
        if v > best_val {
            best_val = v;
            best_lag = lag;
        }
    }

    if best_lag == 0 {
        0.0
    } else {
        let seconds = best_lag as f32 / sample_rate;
        60.0 / seconds
    }
}

fn autocorrelate(x: &[f32]) -> Vec<f32> {
    let n = x.len();
    let mut out = vec![0.0f32; n];
    for lag in 0..n {
        let mut sum = 0.0;
        let max_i = n - lag;
        for i in 0..max_i {
            sum += x[i] * x[i + lag];
        }
        out[lag] = sum;
    }
    out
}
