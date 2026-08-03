
use anyhow::{Result, anyhow, bail};

fn hex(flag: &str, value: &str) -> Result<u64> {
    u64::from_str_radix(value.trim_start_matches("0x"), 16)
        .map_err(|error| anyhow!("{flag} {value}: {error}"))
}

#[cfg(target_os = "macos")]
mod platform {
    use super::{Result, anyhow, bail, hex};
    use std::ffi::CString;
    use std::path::PathBuf;

    pub struct Arguments {
        pub service: CString,
        pub segment: CString,
        pub cookie: u64,
        pub dylib_dir: PathBuf,
        pub max_in_flight: usize,
    }

    const DEFAULT_MAX_IN_FLIGHT: usize =
        uuav_ipc::protocol::VIDEO_RING_CAPACITY + uuav_ipc::protocol::RETAINED_FRAMES;

    impl Arguments {
        pub fn parse(arguments: impl Iterator<Item = String>) -> Result<Self> {
            let mut service = None;
            let mut segment = None;
            let mut cookie = None;
            let mut dylib_dir = None;
            let mut max_in_flight = DEFAULT_MAX_IN_FLIGHT;

            let mut arguments = arguments;
            while let Some(flag) = arguments.next() {
                let value = arguments
                    .next()
                    .ok_or_else(|| anyhow!("{flag} needs a value"))?;
                match flag.as_str() {
                    "--service" => service = Some(name("--service", &value)?),
                    "--segment" => segment = Some(name("--segment", &value)?),
                    "--cookie" => cookie = Some(hex("--cookie", &value)?),
                    "--dylib-dir" => dylib_dir = Some(PathBuf::from(value)),
                    "--max-in-flight" => {
                        max_in_flight = value
                            .parse::<usize>()
                            .map_err(|error| anyhow!("--max-in-flight {value}: {error}"))?
                            .max(1);
                    }
                    other => bail!("unknown argument {other}"),
                }
            }

            Ok(Self {
                service: service.ok_or_else(|| anyhow!("--service is required"))?,
                segment: segment.ok_or_else(|| anyhow!("--segment is required"))?,
                cookie: cookie.ok_or_else(|| anyhow!("--cookie is required"))?,
                dylib_dir: dylib_dir
                    .or_else(beside_self)
                    .ok_or_else(|| anyhow!("--dylib-dir is required (no default was derivable)"))?,
                max_in_flight,
            })
        }
    }

    fn beside_self() -> Option<PathBuf> {
        Some(std::env::current_exe().ok()?.parent()?.to_path_buf())
    }

    fn name(flag: &str, value: &str) -> Result<CString> {
        CString::new(value).map_err(|_| anyhow!("{flag} contains a NUL byte"))
    }
}

#[cfg(windows)]
mod platform {
    use super::{Result, anyhow, bail, hex};
    use uuav_ipc::win::wire::Mode;

    pub struct Arguments {
        pub pipe: u64,
        pub segment: u64,
        pub cookie: u64,
        pub mode: Mode,
        pub adapter_luid: Option<u64>,
    }

    impl Arguments {
        pub fn parse(arguments: impl Iterator<Item = String>) -> Result<Self> {
            let mut pipe = None;
            let mut segment = None;
            let mut cookie = None;
            let mut mode = None;
            let mut adapter_luid = None;

            let mut arguments = arguments;
            while let Some(flag) = arguments.next() {
                let value = arguments
                    .next()
                    .ok_or_else(|| anyhow!("{flag} needs a value"))?;
                match flag.as_str() {
                    "--pipe" => pipe = Some(hex("--pipe", &value)?),
                    "--segment" => segment = Some(hex("--segment", &value)?),
                    "--cookie" => cookie = Some(hex("--cookie", &value)?),
                    "--mode" => {
                        mode = Some(match value.as_str() {
                            "gpu" => Mode::Gpu,
                            "software" => Mode::Software,
                            other => bail!("--mode {other} is neither gpu nor software"),
                        });
                    }
                    "--adapter" => adapter_luid = Some(hex("--adapter", &value)?),
                    other => bail!("unknown argument {other}"),
                }
            }

            Ok(Self {
                pipe: pipe.ok_or_else(|| anyhow!("--pipe is required"))?,
                segment: segment.ok_or_else(|| anyhow!("--segment is required"))?,
                cookie: cookie.ok_or_else(|| anyhow!("--cookie is required"))?,
                mode: mode.ok_or_else(|| anyhow!("--mode is required"))?,
                adapter_luid,
            })
        }
    }
}

pub use platform::Arguments;
