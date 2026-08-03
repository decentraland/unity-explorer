
#![warn(clippy::all, clippy::pedantic, clippy::nursery)]
#![deny(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::todo,
    clippy::dbg_macro
)]
#![allow(
    clippy::uninlined_format_args,
    clippy::missing_errors_doc,
    clippy::option_if_let_else,
    clippy::single_match_else,
    clippy::must_use_candidate,
    clippy::future_not_send,
    clippy::enum_glob_use
)]
#![allow(
    clippy::borrow_as_ptr,
    clippy::redundant_pub_crate,
    clippy::cast_precision_loss,
    clippy::cast_sign_loss,
    clippy::cast_possible_truncation,
    clippy::cast_ptr_alignment,
    clippy::missing_safety_doc,
    clippy::doc_markdown
)]

mod abi_guard;
mod argv;
mod audio;
mod core;
mod driver;
mod fetch;
mod gpu;
mod probe;

#[cfg(not(any(target_os = "macos", windows)))]
compile_error!(
    "uuav-adapter targets macOS and Windows only: it links the frozen media core, which has no \
     Linux configuration and no Linux FFmpeg prefix"
);

use anyhow::{Result, anyhow};
use std::ffi::CStr;
use std::process::ExitCode;
use uuav_ipc::protocol::{LogLevel, kind};

use crate::core::LogBridge;
use crate::driver::{Channel as _, Driver};

const SEND_TIMEOUT_MS: u32 = 1_000;

fn main() -> ExitCode {
    match run() {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("uuav-adapter: {error:#}");
            ExitCode::FAILURE
        }
    }
}

fn check_abi_version() -> Result<()> {
    let linked = unsafe { CStr::from_ptr(uuav::uuav_abi_version()) };
    if linked == uuav_abi::ABI_VERSION {
        return Ok(());
    }
    Err(anyhow!(
        "linked media core reports ABI version {:?} but this adapter was built against {:?}: the \
         client and the adapter come from different builds",
        linked.to_string_lossy(),
        uuav_abi::ABI_VERSION.to_string_lossy(),
    ))
}

#[cfg(target_os = "macos")]
mod mach_channel {
    use super::{Result, SEND_TIMEOUT_MS, kind};
    use crate::driver::{Channel, Incoming};
    use crate::gpu::SurfaceHandle;
    use uuav_ipc::mach_ipc::{self, ReceiveRight, SendRight};

    const RECEIVE_TIMED_OUT: &str = "0x10004003";

    pub struct MachChannel {
        pub host: SendRight,
        control: ReceiveRight,
        parent: i32,
    }

    impl MachChannel {
        pub const fn new(host: SendRight, control: ReceiveRight, parent: i32) -> Self {
            Self {
                host,
                control,
                parent,
            }
        }
    }

    impl Channel for MachChannel {
        fn receive(&mut self, timeout_ms: u32) -> Result<Option<Incoming>> {
            let incoming = match mach_ipc::receive(&self.control, timeout_ms) {
                Ok(incoming) => incoming,
                Err(error) => {
                    if error.to_string().contains(RECEIVE_TIMED_OUT) {
                        return Ok(None);
                    }
                    return Err(error);
                }
            };
            drop(incoming.right);
            drop(incoming.reply);
            Ok(Some(Incoming {
                kind: incoming.kind,
                payload: incoming.payload,
            }))
        }

        fn send(&mut self, kind: u32, index: u32, payload: u64) -> Result<()> {
            mach_ipc::send(&self.host, kind, index, payload, None, SEND_TIMEOUT_MS)
        }

        fn send_surface(&mut self, index: u32, generation: u64, surface: SurfaceHandle) -> Result<()> {
            mach_ipc::send(
                &self.host,
                kind::SURFACE,
                index,
                generation,
                Some(surface),
                SEND_TIMEOUT_MS,
            )
        }

        fn host_is_gone(&mut self) -> bool {
            unsafe { libc::getppid() != self.parent }
        }
    }
}

#[cfg(target_os = "macos")]
fn run() -> Result<()> {
    use uuav_ipc::mach_ipc::{self, ReceiveRight};
    use uuav_ipc::shm;

    use crate::mach_channel::MachChannel;

    let arguments = argv::Arguments::parse(std::env::args().skip(1))?;

    let bootstrap = arguments
        .service
        .to_str()
        .map_err(|_| anyhow!("--service is not valid UTF-8"))?;
    uuav_ipc::sandbox::enter(bootstrap, &arguments.dylib_dir)
        .map_err(|error| anyhow!("entering the seatbelt: {error:#}"))?;

    let mapping = shm::Mapping::attach(&arguments.segment)?;
    let segment = mapping.segment();

    check_abi_version()?;

    segment.attach(std::process::id(), arguments.cookie)?;

    let host = mach_ipc::look_up(&arguments.service)?;
    let control = ReceiveRight::allocate()?;
    mach_ipc::send_handout(
        &host,
        kind::HELLO,
        0,
        arguments.cookie,
        &control,
        SEND_TIMEOUT_MS,
    )?;

    let _bridge = LogBridge::install(segment);
    let _fetch_bridge = crate::fetch::FetchBridge::install(segment);
    segment.log.emit(
        LogLevel::Info,
        &format!(
            "uuav-adapter attached: max_in_flight {}, dylib dir {}",
            arguments.max_in_flight,
            arguments.dylib_dir.display(),
        ),
    );
    let parent = unsafe { libc::getppid() };
    let mut channel = MachChannel::new(host, control, parent);
    let _audio = audio::Pump::start(segment, &arguments.service);
    let outcome =
        Driver::start(segment, &mut channel, None).and_then(|mut driver| driver.run());
    let _ = channel.send(kind::GOODBYE, 0, 0);
    outcome
}

#[cfg(windows)]
fn own_process_handle() -> Result<std::os::windows::io::OwnedHandle> {
    use std::os::windows::io::FromRawHandle as _;
    use windows::Win32::Foundation::{DUPLICATE_SAME_ACCESS, DuplicateHandle, HANDLE};

    let me = HANDLE(-1isize as *mut ::core::ffi::c_void);
    let mut duplicated = HANDLE::default();
    unsafe {
        DuplicateHandle(
            me,
            me,
            me,
            &raw mut duplicated,
            0,
            false,
            DUPLICATE_SAME_ACCESS,
        )
    }?;
    Ok(unsafe { std::os::windows::io::OwnedHandle::from_raw_handle(duplicated.0.cast()) })
}

#[cfg(windows)]
mod pipe_channel {
    use super::{Result, SEND_TIMEOUT_MS, kind};
    use crate::driver::{Channel, Incoming};
    use crate::gpu::SurfaceHandle;
    use uuav_ipc::win::pipe;
    use uuav_ipc::win::wire::Message;

    pub struct PipeChannel {
        channel: pipe::Channel,
        gone: bool,
    }

    impl PipeChannel {
        pub const fn new(channel: pipe::Channel) -> Self {
            Self {
                channel,
                gone: false,
            }
        }
    }

    impl Channel for PipeChannel {
        fn receive(&mut self, timeout_ms: u32) -> Result<Option<Incoming>> {
            match self.channel.receive(timeout_ms) {
                Ok(Some(message)) => Ok(Some(Incoming {
                    kind: message.kind,
                    payload: message.payload,
                })),
                Ok(None) => Ok(None),
                Err(error) if pipe::Channel::peer_is_gone(&error) => {
                    self.gone = true;
                    Ok(None)
                }
                Err(error) => Err(error),
            }
        }

        fn send(&mut self, kind: u32, index: u32, payload: u64) -> Result<()> {
            self.channel
                .send(&Message::scalar(kind, index, payload), SEND_TIMEOUT_MS)
        }

        fn send_surface(&mut self, index: u32, generation: u64, surface: SurfaceHandle) -> Result<()> {
            self.channel.send(
                &Message::with_handle(kind::SURFACE, index, generation, surface),
                SEND_TIMEOUT_MS,
            )
        }

        fn host_is_gone(&mut self) -> bool {
            self.gone
        }
    }
}

#[cfg(windows)]
fn run() -> Result<()> {
    use std::os::windows::io::AsRawHandle as _;

    use uuav_ipc::win::wire::Message;
    use uuav_ipc::win::{pipe, shm};

    use crate::pipe_channel::PipeChannel;

    let arguments = argv::Arguments::parse(std::env::args().skip(1))?;

    let mapping = shm::Mapping::from_inherited(arguments.segment)?;
    let segment = mapping.segment();

    check_abi_version()?;
    segment.attach(std::process::id(), arguments.cookie)?;

    let mut channel = pipe::Channel::from_inherited(arguments.pipe)?;
    let own = crate::own_process_handle()?;
    channel.send(
        &Message::with_handle(
            kind::HELLO,
            0,
            arguments.cookie,
            own.as_raw_handle() as usize as u64,
        ),
        SEND_TIMEOUT_MS,
    )?;

    let _bridge = LogBridge::install(segment);
    let _fetch_bridge = crate::fetch::FetchBridge::install(segment);
    segment.log.emit(
        LogLevel::Info,
        &format!(
            "uuav-adapter attached: mode {:?}, adapter luid {:?}",
            arguments.mode, arguments.adapter_luid,
        ),
    );
    let mut channel = PipeChannel::new(channel);
    let _audio = audio::Pump::start(segment);
    let outcome = Driver::start(segment, &mut channel, arguments.adapter_luid)
        .and_then(|mut driver| driver.run());
    let _ = channel.send(kind::GOODBYE, 0, 0);
    outcome
}
