
use std::path::Path;
use std::sync::Arc;

use anyhow::{Result, bail};
use windows::Win32::Graphics::Direct3D11::{ID3D11Device, ID3D11Texture2D};
use windows::Win32::Graphics::Dxgi::IDXGIKeyedMutex;
use windows::core::Interface as _;
use windows_sys::Win32::System::Memory::FILE_MAP_READ;

use crate::protocol::{
    self, Fault, HelperEvent, LogEntry, SURFACE_SLOT_COUNT, SharedSegment, SurfaceGeometry,
    TransportRead, kind,
};
use crate::win::gpu;
use crate::win::pipe::{self, Channel};
use crate::win::shm::{Mapping, PlaneSection};
use crate::win::spawn::{ExitStatus, Helper, Integrity, Launch};
use crate::win::wire::{Confinement, Message, Mode};

pub const HELLO_TIMEOUT_MS: u32 = 8_000;

pub const COMMAND_TIMEOUT_MS: u32 = 100;

pub const SHUTDOWN_GRACE_MS: u32 = 250;

const MAX_MESSAGES_PER_PUMP: u32 = 64;

const MAX_LOGS_PER_PUMP: u32 = 64;

const MAX_PLANE_SECTION_BYTES: usize = 512 << 20;

pub use crate::win::spawn::Integrity as HelperIntegrity;

pub mod uuav_state {
    pub const CLOSED: u32 = 0;
    pub const OPENING: u32 = 1;
    pub const READY: u32 = 2;
    pub const ENDED: u32 = 5;
    pub const ERROR: u32 = 6;
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Phase {
    Closed,
    Opening,
    Open,
    Ended,
    Failed,
}

pub struct SurfaceSlot {
    pub content: SlotContent,
    pub generation: u64,
}

pub enum SlotContent {
    Shared {
        texture: ID3D11Texture2D,
        mutex: IDXGIKeyedMutex,
        geometry: SurfaceGeometry,
    },
    Planes { section: PlaneSection },
}

impl SurfaceSlot {
    pub const fn geometry(&self) -> Option<&SurfaceGeometry> {
        match &self.content {
            SlotContent::Shared { geometry, .. } => Some(geometry),
            SlotContent::Planes { .. } => None,
        }
    }
}

#[derive(Debug, Default)]
pub struct Pumped {
    pub surfaces_imported: u32,
    pub opened: bool,
    pub ended: bool,
    pub goodbye: bool,
    pub failed: Option<u64>,
    pub helper_exited: Option<ExitStatus>,
    pub fetch_doorbells: u32,
    pub logs: Vec<LogEntry>,
}

pub struct Rendezvous {
    cookie: u64,
    mapping: Arc<Mapping>,
    channel: Channel,
    client: std::os::windows::io::OwnedHandle,
}

impl Rendezvous {
    pub fn open() -> Result<Self> {
        let cookie = protocol::nonce();
        let host_pid = std::process::id();
        let mapping = Mapping::create()?;
        mapping.segment().initialise(host_pid, cookie);
        let (channel, client) = Channel::create_pair(host_pid)?;
        Ok(Self {
            cookie,
            mapping: Arc::new(mapping),
            channel,
            client,
        })
    }

    pub fn segment(&self) -> &SharedSegment {
        self.mapping.segment()
    }

    pub const fn cookie(&self) -> u64 {
        self.cookie
    }

    pub fn spawn(
        self,
        exe: &Path,
        plan: Confinement,
        integrity: Integrity,
        device: Option<ID3D11Device>,
        adapter_luid: Option<u64>,
        timeout_ms: u32,
    ) -> Result<Session> {
        let helper = Helper::spawn(&Launch {
            exe,
            plan,
            integrity,
            pipe: &self.client,
            segment: self.mapping.section(),
            cookie: self.cookie,
            adapter_luid,
        })?;
        self.accept(helper, plan.mode, device, timeout_ms)
    }

    fn accept(
        self,
        mut helper: Helper,
        mode: Mode,
        device: Option<ID3D11Device>,
        timeout_ms: u32,
    ) -> Result<Session> {
        let Self {
            cookie,
            mapping,
            mut channel,
            client,
        } = self;
        drop(client);

        let received = match channel.receive(timeout_ms) {
            Ok(received) => received,
            Err(error) => return Err(helper.died_before_hello(&format!("{error:#}"))),
        };
        let Some(hello) = received else {
            return Err(helper.died_before_hello(&format!("nothing arrived within {timeout_ms} ms")));
        };
        protocol::check_incoming(hello.kind, hello.carries_handle())?;
        if hello.kind != kind::HELLO {
            return Err(Fault::Message {
                kind: hello.kind,
                what: "first message must be HELLO",
            }
            .into());
        }
        if hello.payload != cookie {
            return Err(Fault::Message {
                kind: hello.kind,
                what: "HELLO cookie does not match",
            }
            .into());
        }
        let claimed = mapping.segment().helper_pid();
        if claimed != helper.pid() {
            return Err(Fault::Attach {
                what: "the process that attached the segment is not the one we spawned",
            }
            .into());
        }
        let _ignored = helper.is_alive();

        Ok(Session {
            mapping,
            channel,
            helper,
            device,
            mode,
            surfaces: std::array::from_fn(|_ignored| None),
            phase: Phase::Closed,
            open_generation: 0,
            fault: None,
            imported_total: 0,
        })
    }
}

pub struct Session {
    mapping: Arc<Mapping>,
    channel: Channel,
    helper: Helper,
    device: Option<ID3D11Device>,
    mode: Mode,
    surfaces: [Option<SurfaceSlot>; SURFACE_SLOT_COUNT],
    phase: Phase,
    open_generation: u64,
    fault: Option<Fault>,
    imported_total: u64,
}

impl Session {
    pub fn start(
        exe: &Path,
        device: Option<&ID3D11Device>,
        protocol_whitelist: &str,
        integrity: Integrity,
    ) -> Result<Self> {
        let rendezvous = Rendezvous::open()?;
        if !rendezvous
            .segment()
            .protocol_whitelist
            .publish(protocol_whitelist)
        {
            bail!(
                "protocol whitelist {protocol_whitelist:?} is empty or longer than {} bytes",
                protocol::PROTOCOL_WHITELIST_MAX_BYTES
            );
        }

        let luid = match device {
            Some(device) => gpu::adapter_luid(device).ok().filter(|luid| *luid != 0),
            None => None,
        };
        rendezvous.segment().adapter.publish(luid.unwrap_or(0));

        let (plan, adapter, device) = match luid {
            Some(luid) => (Confinement::gpu(), Some(luid), device.cloned()),
            None => (Confinement::software(), None, None),
        };
        rendezvous.spawn(exe, plan, integrity, device, adapter, HELLO_TIMEOUT_MS)
    }

    pub fn segment(&self) -> &SharedSegment {
        self.mapping.segment()
    }

    pub const fn mapping(&self) -> &Arc<Mapping> {
        &self.mapping
    }

    pub const fn phase(&self) -> Phase {
        self.phase
    }

    pub const fn fault(&self) -> Option<Fault> {
        self.fault
    }

    pub const fn mode(&self) -> Mode {
        self.mode
    }

    pub const fn imported_total(&self) -> u64 {
        self.imported_total
    }

    pub fn surface(&self, slot: usize) -> Option<&SurfaceSlot> {
        self.surfaces.get(slot).and_then(Option::as_ref)
    }

    pub fn pump(&mut self) -> Result<Pumped> {
        if let Some(fault) = self.fault {
            return Err(fault.into());
        }
        let mut pumped = Pumped::default();

        for _ in 0..MAX_MESSAGES_PER_PUMP {
            let received = match self.channel.receive(0) {
                Ok(received) => received,
                Err(error) => {
                    if let Some(fault) = error.downcast_ref::<Fault>().copied() {
                        self.fail(fault);
                        return Err(fault.into());
                    }
                    self.phase = Phase::Failed;
                    if Channel::peer_is_gone(&error) {
                        break;
                    }
                    self.kill_helper();
                    return Err(error);
                }
            };
            let Some(message) = received else { break };
            if let Err(fault) = self.handle(message, &mut pumped) {
                self.fail(fault);
                return Err(fault.into());
            }
        }

        if let Err(fault) = self.drain_logs(&mut pumped) {
            self.fail(fault);
            return Err(fault.into());
        }

        if self.phase == Phase::Open
            && let TransportRead::Corrupt(fault) = self.segment().transport.read()
        {
            self.fail(fault);
            return Err(fault.into());
        }

        if !self.helper.is_alive() {
            pumped.helper_exited = self.helper.exit_status();
            if !matches!(self.phase, Phase::Ended | Phase::Failed) {
                self.phase = Phase::Failed;
            }
        }

        Ok(pumped)
    }

    pub fn open(&mut self, url: &str) -> Result<u64> {
        let Some(generation) = self.mapping.segment().open.publish(url) else {
            bail!("media URL is longer than {} bytes", protocol::URL_MAX_BYTES);
        };
        self.mapping.segment().cancel.clear();
        self.open_generation = generation;
        self.phase = Phase::Opening;
        self.command(kind::OPEN)?;
        Ok(generation)
    }

    pub fn play(&mut self) -> Result<()> {
        self.command(kind::PLAY)
    }

    pub fn pause(&mut self) -> Result<()> {
        self.command(kind::PAUSE)
    }

    pub fn close(&mut self) -> Result<()> {
        self.command(kind::CLOSE)?;
        if self.phase != Phase::Failed {
            self.phase = Phase::Closed;
        }
        Ok(())
    }

    pub fn command(&mut self, message_kind: u32) -> Result<()> {
        let generation = self.open_generation;
        self.command_with(message_kind, generation)
    }

    pub fn command_with(&mut self, message_kind: u32, payload: u64) -> Result<()> {
        if !protocol::is_host_to_helper(message_kind) {
            return Err(Fault::Message {
                kind: message_kind,
                what: "not a host-to-helper kind",
            }
            .into());
        }
        self.channel.send(
            &Message::scalar(message_kind, 0, payload),
            COMMAND_TIMEOUT_MS,
        )
    }

    pub fn set_log_level(&mut self, level: i32) -> Result<()> {
        self.command_with(kind::SET_LOG_LEVEL, i64::from(level).cast_unsigned())
    }

    pub fn helper_alive(&mut self) -> bool {
        self.helper.is_alive()
    }

    pub fn kill_helper(&mut self) {
        self.mapping.segment().cancel.set();
        self.helper.kill();
    }

    pub fn shutdown(&mut self) {
        self.mapping.segment().cancel.set();
        let _ignored = self.command(kind::SHUTDOWN);
        if self.helper.wait_for_exit(SHUTDOWN_GRACE_MS).is_none() {
            self.helper.kill();
        }
        if self.phase != Phase::Failed {
            self.phase = Phase::Closed;
        }
    }

    fn handle(&mut self, message: Message, pumped: &mut Pumped) -> Result<(), Fault> {
        protocol::check_incoming(message.kind, message.carries_handle())?;

        match protocol::classify_helper_message(message.kind)? {
            HelperEvent::Surface => {
                self.import_surface(message.index, message.payload, message.handle)?;
                pumped.surfaces_imported = pumped.surfaces_imported.saturating_add(1);
                Ok(())
            }
            HelperEvent::Opened => {
                self.phase = Phase::Open;
                pumped.opened = true;
                Ok(())
            }
            HelperEvent::Failed => {
                self.phase = Phase::Failed;
                pumped.failed = Some(message.payload);
                Ok(())
            }
            HelperEvent::Ended => {
                self.phase = Phase::Ended;
                pumped.ended = true;
                Ok(())
            }
            HelperEvent::Goodbye => {
                pumped.goodbye = true;
                Ok(())
            }
            HelperEvent::FetchDoorbell => {
                pumped.fetch_doorbells = pumped.fetch_doorbells.saturating_add(1);
                Ok(())
            }
            HelperEvent::Audio => Err(Fault::Message {
                kind: kind::AUDIO,
                what: "audio packets do not ride the Windows control pipe",
            }),
        }
    }

    fn import_surface(&mut self, index: u32, payload: u64, value: u64) -> Result<(), Fault> {
        let (generation, section_bytes) = match self.mode {
            Mode::Gpu => (payload, 0usize),
            Mode::Software => (payload >> 32, (payload & 0xFFFF_FFFF) as usize),
        };
        let slot = admit_slot(self.surface(index as usize), index, generation)?;

        let access = match self.mode {
            Mode::Gpu => pipe::Access::Same,
            Mode::Software => pipe::Access::Mask(FILE_MAP_READ),
        };
        let pulled = unsafe {
            pipe::duplicate_from_child(self.helper.process_handle(), kind::SURFACE, value, access)
        };
        let Ok(handle) = pulled else {
            return Err(Fault::Message {
                kind: kind::SURFACE,
                what: "the surface handle could not be duplicated out of the helper",
            });
        };

        let content = match self.mode {
            Mode::Gpu => {
                let Some(device) = self.device.as_ref() else {
                    return Err(Fault::Message {
                        kind: kind::SURFACE,
                        what: "a shared surface arrived but the host has no D3D11 device",
                    });
                };
                let Ok(texture) = gpu::open_shared(device, &handle) else {
                    return Err(Fault::Message {
                        kind: kind::SURFACE,
                        what: "the handle does not name a shareable D3D11 resource",
                    });
                };
                let Ok(mutex) = texture.cast::<IDXGIKeyedMutex>() else {
                    return Err(Fault::Message {
                        kind: kind::SURFACE,
                        what: "the shared surface has no keyed mutex",
                    });
                };
                let geometry = gpu::measure(&texture)?;
                SlotContent::Shared {
                    texture,
                    mutex,
                    geometry,
                }
            }
            Mode::Software => {
                if section_bytes == 0 || section_bytes > MAX_PLANE_SECTION_BYTES {
                    return Err(Fault::Message {
                        kind: kind::SURFACE,
                        what: "plane section length is out of range",
                    });
                }
                let Ok(section) = PlaneSection::open_read_only(handle, section_bytes) else {
                    return Err(Fault::Message {
                        kind: kind::SURFACE,
                        what: "the handle does not name a section of the claimed length",
                    });
                };
                SlotContent::Planes { section }
            }
        };

        let Some(entry) = self.surfaces.get_mut(slot) else {
            return Err(Fault::Message {
                kind: kind::SURFACE,
                what: "surface slot out of range",
            });
        };
        *entry = Some(SurfaceSlot {
            content,
            generation,
        });
        self.imported_total = self.imported_total.saturating_add(1);
        Ok(())
    }

    fn drain_logs(&self, pumped: &mut Pumped) -> Result<(), Fault> {
        for _ in 0..MAX_LOGS_PER_PUMP {
            let Some(entry) = self.mapping.segment().log.take()? else {
                break;
            };
            pumped.logs.push(entry);
        }
        Ok(())
    }

    fn fail(&mut self, fault: Fault) {
        if self.fault.is_none() {
            self.fault = Some(fault);
        }
        self.phase = Phase::Failed;
        self.kill_helper();
    }
}

impl Drop for Session {
    fn drop(&mut self) {
        self.mapping.segment().cancel.set();
        self.helper.kill();
    }
}

const fn admit_slot(
    existing: Option<&SurfaceSlot>,
    index: u32,
    generation: u64,
) -> Result<usize, Fault> {
    let slot = index as usize;
    if slot >= SURFACE_SLOT_COUNT {
        return Err(Fault::Message {
            kind: kind::SURFACE,
            what: "surface slot out of range",
        });
    }
    if generation == 0 {
        return Err(Fault::Message {
            kind: kind::SURFACE,
            what: "surface generation must be non-zero",
        });
    }
    let repeated = match existing {
        Some(existing) => generation <= existing.generation,
        None => false,
    };
    if repeated {
        return Err(Fault::Message {
            kind: kind::SURFACE,
            what: "surface generation does not advance",
        });
    }
    Ok(slot)
}
