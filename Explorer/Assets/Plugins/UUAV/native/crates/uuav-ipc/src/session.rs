
use std::ffi::CStr;
use std::path::Path;
use std::sync::Arc;

use anyhow::{Result, bail};
use mach2::message::{MACH_RCV_INTERRUPTED, MACH_RCV_TIMED_OUT, MACH_SEND_INVALID_DEST};
use objc2_core_foundation::CFRetained;
use objc2_io_surface::IOSurfaceRef;

use crate::audio::JitterRing;
use crate::mach_ipc::{self, Incoming, ReceiveRight, SendRight};
use crate::protocol::{
    self, Fault, HelperEvent, LogEntry, SURFACE_SLOT_COUNT, SharedSegment, SurfaceGeometry,
    TransportRead, kind,
};
use crate::shm::{Mapping, NameGuard};
use crate::spawn::{ExitStatus, Helper};

pub const HELLO_TIMEOUT_MS: u32 = 5_000;

pub const COMMAND_TIMEOUT_MS: u32 = 100;

pub const SHUTDOWN_GRACE_MS: u32 = 250;

const MAX_MESSAGES_PER_PUMP: u32 = 64;

const MAX_LOGS_PER_PUMP: u32 = 64;

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
    pub surface: CFRetained<IOSurfaceRef>,
    pub geometry: SurfaceGeometry,
    pub generation: u64,
    pub id: u32,
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
    service: std::ffi::CString,
    segment_name: NameGuard,
    mapping: Arc<Mapping>,
    inbox: ReceiveRight,
}

impl Rendezvous {
    pub fn open() -> Result<Self> {
        let cookie = protocol::nonce();
        let host_pid = std::process::id();
        let service = protocol::service_name(host_pid, cookie)?;
        let inbox = mach_ipc::check_in(&service)?;

        let segment_name = NameGuard::new(protocol::segment_name(host_pid, cookie)?);
        let mapping = Mapping::create(segment_name.name())?;
        mapping.segment().initialise(host_pid, cookie);

        Ok(Self {
            cookie,
            service,
            segment_name,
            mapping: Arc::new(mapping),
            inbox,
        })
    }

    pub fn service_name(&self) -> &CStr {
        self.service.as_c_str()
    }

    pub fn segment_name(&self) -> &CStr {
        self.segment_name.name()
    }

    pub const fn cookie(&self) -> u64 {
        self.cookie
    }

    pub fn segment(&self) -> &SharedSegment {
        self.mapping.segment()
    }

    pub fn spawn(self, exe: &Path, timeout_ms: u32) -> Result<Session> {
        let helper = Helper::spawn(exe, self.service_name(), self.segment_name(), self.cookie)?;
        self.accept(Some(helper), timeout_ms)
    }

    pub fn accept(mut self, helper: Option<Helper>, timeout_ms: u32) -> Result<Session> {
        let hello = mach_ipc::receive(&self.inbox, timeout_ms)?;
        let Incoming {
            kind: message_kind,
            payload,
            right,
            reply,
            ..
        } = hello;
        drop(reply);

        protocol::check_incoming(message_kind, right.is_some())?;
        if message_kind != kind::HELLO {
            return Err(Fault::Message {
                kind: message_kind,
                what: "first message must be HELLO",
            }
            .into());
        }
        if payload != self.cookie {
            return Err(Fault::Message {
                kind: message_kind,
                what: "HELLO cookie does not match",
            }
            .into());
        }
        let Some(control) = right else {
            return Err(Fault::Message {
                kind: message_kind,
                what: "HELLO carried no control port",
            }
            .into());
        };

        if let Some(helper) = helper.as_ref() {
            let claimed = self.mapping.segment().helper_pid();
            let spawned = u32::try_from(helper.pid()).unwrap_or(0);
            if claimed != spawned {
                return Err(Fault::Attach {
                    what: "the process that attached the segment is not the one we spawned",
                }
                .into());
            }
        }

        self.segment_name.unlink_now();

        Ok(Session {
            mapping: self.mapping,
            inbox: self.inbox,
            control,
            helper,
            surfaces: std::array::from_fn(|_ignored| None),
            segment_name: self.segment_name,
            phase: Phase::Closed,
            open_generation: 0,
            fault: None,
            imported_total: 0,
            audio: None,
            audio_scratch: Box::new([0.0; protocol::AUDIO_PACKET_SAMPLES]),
        })
    }
}

pub struct Session {
    mapping: Arc<Mapping>,
    inbox: ReceiveRight,
    control: SendRight,
    helper: Option<Helper>,
    surfaces: [Option<SurfaceSlot>; SURFACE_SLOT_COUNT],
    segment_name: NameGuard,
    phase: Phase,
    open_generation: u64,
    fault: Option<Fault>,
    imported_total: u64,
    audio: Option<&'static JitterRing>,
    audio_scratch: Box<[f32; protocol::AUDIO_PACKET_SAMPLES]>,
}

impl Session {
    pub fn start(exe: &Path) -> Result<Self> {
        Self::start_with(exe, "file", HELLO_TIMEOUT_MS)
    }

    pub fn start_with(exe: &Path, protocol_whitelist: &str, hello_timeout_ms: u32) -> Result<Self> {
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
        rendezvous.spawn(exe, hello_timeout_ms)
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

    pub const fn imported_total(&self) -> u64 {
        self.imported_total
    }

    pub fn surface(&self, slot: usize) -> Option<&SurfaceSlot> {
        self.surfaces.get(slot).and_then(Option::as_ref)
    }

    pub fn uuav_state(&self) -> u32 {
        match self.phase {
            Phase::Failed => uuav_state::ERROR,
            Phase::Closed => uuav_state::CLOSED,
            Phase::Opening => uuav_state::OPENING,
            Phase::Ended => uuav_state::ENDED,
            Phase::Open => match self.segment().transport.read() {
                TransportRead::Fresh(snapshot) => snapshot.state.to_wire(),
                TransportRead::Contended => uuav_state::READY,
                TransportRead::Corrupt(_ignored) => uuav_state::ERROR,
            },
        }
    }

    pub fn pump(&mut self) -> Result<Pumped> {
        if let Some(fault) = self.fault {
            return Err(fault.into());
        }
        let mut pumped = Pumped::default();

        for _ in 0..MAX_MESSAGES_PER_PUMP {
            let received = match self.try_receive() {
                Ok(received) => received,
                Err(error) => {
                    self.phase = Phase::Failed;
                    return Err(error);
                }
            };
            let Some(incoming) = received else {
                break;
            };
            if let Err(fault) = self.handle(incoming, &mut pumped) {
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

        if let Some(helper) = self.helper.as_mut()
            && !helper.is_alive()
        {
            pumped.helper_exited = helper.exit_status();
            if !matches!(self.phase, Phase::Ended | Phase::Failed) {
                self.phase = Phase::Failed;
            }
        }

        Ok(pumped)
    }

    pub fn open(&mut self, url: &str) -> Result<u64> {
        let segment = self.mapping.segment();
        let Some(generation) = segment.open.publish(url) else {
            bail!("media URL is longer than {} bytes", protocol::URL_MAX_BYTES);
        };
        segment.cancel.clear();
        self.open_generation = generation;
        self.phase = Phase::Opening;
        self.command(kind::OPEN)?;
        Ok(generation)
    }

    pub fn play(&self) -> Result<()> {
        self.command(kind::PLAY)
    }

    pub fn pause(&self) -> Result<()> {
        self.command(kind::PAUSE)
    }

    pub fn close(&mut self) -> Result<()> {
        self.command(kind::CLOSE)?;
        if self.phase != Phase::Failed {
            self.phase = Phase::Closed;
        }
        Ok(())
    }

    pub fn command(&self, message_kind: u32) -> Result<()> {
        self.command_with(message_kind, self.open_generation)
    }

    pub fn command_with(&self, message_kind: u32, payload: u64) -> Result<()> {
        if !protocol::is_host_to_helper(message_kind) {
            return Err(Fault::Message {
                kind: message_kind,
                what: "not a host-to-helper kind",
            }
            .into());
        }
        mach_ipc::send(
            &self.control,
            message_kind,
            0,
            payload,
            None,
            COMMAND_TIMEOUT_MS,
        )
    }

    pub fn set_log_level(&self, level: i32) -> Result<()> {
        self.command_with(kind::SET_LOG_LEVEL, i64::from(level).cast_unsigned())
    }

    pub fn helper_alive(&mut self) -> bool {
        self.helper.as_mut().is_some_and(Helper::is_alive)
    }

    pub fn kill_helper(&mut self) {
        self.mapping.segment().cancel.set();
        if let Some(helper) = self.helper.as_mut() {
            helper.kill();
        }
    }

    pub fn shutdown(&mut self) {
        self.mapping.segment().cancel.set();
        let _ = self.command(kind::SHUTDOWN);
        if let Some(helper) = self.helper.as_mut()
            && helper.wait_for_exit(SHUTDOWN_GRACE_MS).is_none()
        {
            helper.kill();
        }
        if self.phase != Phase::Failed {
            self.phase = Phase::Closed;
        }
    }

    pub const fn set_audio_ring(&mut self, ring: &'static JitterRing) {
        self.audio = Some(ring);
    }

    fn try_receive(&mut self) -> Result<Option<Incoming>> {
        match mach_ipc::receive_into(&self.inbox, 0, self.audio_scratch.as_mut_slice()) {
            Ok(incoming) => Ok(Some(incoming)),
            Err(error) if is_empty_inbox(&error) => Ok(None),
            Err(error) => Err(error),
        }
    }

    fn handle(&mut self, incoming: Incoming, pumped: &mut Pumped) -> Result<(), Fault> {
        let Incoming {
            kind: message_kind,
            index,
            payload,
            right,
            reply,
            samples,
        } = incoming;
        drop(reply);

        protocol::check_incoming(message_kind, right.is_some())?;

        match protocol::classify_helper_message(message_kind)? {
            HelperEvent::Audio => {
                if let Some(ring) = self.audio {
                    let pts = f64::from_bits(payload);
                    if let Some(taken) = self.audio_scratch.get(..samples as usize)
                        && pts.is_finite()
                    {
                        ring.push_packet(pts, taken, index != 0);
                    }
                }
                Ok(())
            }
            HelperEvent::Surface => {
                let Some(right) = right else {
                    return Err(Fault::Message {
                        kind: message_kind,
                        what: "SURFACE carried no port right",
                    });
                };
                self.import_surface(index, payload, right)?;
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
                pumped.failed = Some(payload);
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
        }
    }

    fn import_surface(&mut self, index: u32, generation: u64, right: SendRight) -> Result<(), Fault> {
        let slot = admit_slot(self.surface(index as usize), index, generation)?;

        let imported = IOSurfaceRef::lookup_from_mach_port(right.as_raw());
        drop(right);
        let Some(surface) = imported else {
            return Err(Fault::Message {
                kind: kind::SURFACE,
                what: "port right does not name an IOSurface",
            });
        };

        let geometry = measure(&surface)?;
        let id = surface.id();

        let Some(entry) = self.surfaces.get_mut(slot) else {
            return Err(Fault::Message {
                kind: kind::SURFACE,
                what: "surface slot out of range",
            });
        };
        *entry = Some(SurfaceSlot {
            surface,
            geometry,
            generation,
            id,
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
        if let Some(helper) = self.helper.as_mut() {
            helper.kill();
        }
        self.segment_name.unlink_now();
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

fn measure(surface: &IOSurfaceRef) -> Result<SurfaceGeometry, Fault> {
    if surface.plane_count() != 2 {
        return Err(Fault::Message {
            kind: kind::SURFACE,
            what: "surface is not a two-plane NV12/P010 surface",
        });
    }
    let luma = (
        plane_dimension(surface.width_of_plane(0))?,
        plane_dimension(surface.height_of_plane(0))?,
    );
    let chroma = (
        plane_dimension(surface.width_of_plane(1))?,
        plane_dimension(surface.height_of_plane(1))?,
    );
    Ok(SurfaceGeometry {
        plane_width: [luma.0, chroma.0],
        plane_height: [luma.1, chroma.1],
        plane_count: 2,
    })
}

fn plane_dimension(value: usize) -> Result<u32, Fault> {
    let narrowed = u32::try_from(value).unwrap_or(u32::MAX);
    if narrowed == 0 || narrowed > protocol::MAX_PLANE_DIMENSION {
        return Err(Fault::Message {
            kind: kind::SURFACE,
            what: "surface plane dimension out of range",
        });
    }
    Ok(narrowed)
}

fn mach_error_is(error: &anyhow::Error, code: mach2::message::mach_msg_return_t) -> bool {
    error.to_string().ends_with(&format!("{code:#010x}"))
}

fn is_empty_inbox(error: &anyhow::Error) -> bool {
    mach_error_is(error, MACH_RCV_TIMED_OUT) || mach_error_is(error, MACH_RCV_INTERRUPTED)
}

pub fn peer_is_gone(error: &anyhow::Error) -> bool {
    mach_error_is(error, MACH_SEND_INVALID_DEST)
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::too_many_lines,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;
    use crate::protocol;

    #[test]
    fn a_slot_index_past_the_table_is_a_fault() {
        let fault = admit_slot(None, SURFACE_SLOT_COUNT as u32, 1).expect_err("must be rejected");
        assert_eq!(
            fault,
            Fault::Message {
                kind: kind::SURFACE,
                what: "surface slot out of range"
            }
        );
        assert!(admit_slot(None, u32::MAX, 1).is_err());
    }

    #[test]
    fn a_zero_generation_is_a_fault() {
        assert!(admit_slot(None, 0, 0).is_err());
    }

    #[test]
    fn the_command_gate_refuses_helper_to_host_kinds() {
        assert!(!protocol::is_host_to_helper(kind::SURFACE));
        assert!(protocol::is_host_to_helper(kind::PLAY));
    }
}
