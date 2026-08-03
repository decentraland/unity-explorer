use anyhow::{Result, anyhow};
use arc_swap::ArcSwap;
use std::sync::Arc;
use std::thread::{self};

use crossbeam_channel::{Receiver, Sender, TrySendError, bounded};

use crate::ffutil::StreamingProtocol;
use crate::frame_info::FrameInfo;
use crate::hw_device::HwDevice;
use crate::playback::{
    CancelToken, ControlPush, DEFAULT_PLAYBACK_RATE, PlaybackUnit, ReadOnlyCancelToken,
    UnitControls, fill_silence,
};
use crate::video_output::VideoTextureView;
use crate::{AudioOptionsView, ControlsState, ErrorCallback, MediaInfo, UUAVState, VideoSize};

enum Playback {
    Closed,
    Opening,
    Failed,
    Active(Arc<PlaybackUnit>),
}

type Url = String;

type OpenIntent = (Url, ReadOnlyCancelToken);

pub(crate) struct UUAVPlayer {
    audio_out: AudioOptionsView,
    playback: Arc<ArcSwap<Playback>>,
    play_control: ControlPush<bool>,
    looping_control: ControlPush<bool>,
    rate_control: ControlPush<f64>,
    sender_open_intent: Sender<OpenIntent>,
    receiver_open_intent: Receiver<OpenIntent>,
    last_cancel_token: Option<CancelToken>,
}

impl Drop for UUAVPlayer {
    fn drop(&mut self) {
        self.close_media();
    }
}

impl UUAVPlayer {
    pub(crate) fn new(
        id: crate::PlayerId,
        device: HwDevice,
        audio_out: AudioOptionsView,
        error_callback: ErrorCallback,
        protocol_whitelist: Arc<StreamingProtocol>,
    ) -> Result<Self> {
        let (sender_open_intent, receiver_open_intent) = bounded::<OpenIntent>(1);
        let playback = Arc::new(ArcSwap::from_pointee(Playback::Closed));
        let play_control = ControlPush::new(false);
        let looping_control = ControlPush::new(false);
        let rate_control = ControlPush::new(DEFAULT_PLAYBACK_RATE);

        let spawned = thread::Builder::new()
            .name(format!("uuav-player-{id}"))
            .spawn({
                let audio_out = audio_out.clone();
                let playback = playback.clone();
                let receiver = receiver_open_intent.clone();
                let play_or_pause = play_control.consumer();
                let looping = looping_control.consumer();
                let rate = rate_control.consumer();

                move || {
                    while let Ok((url, cancel_token)) = receiver.recv() {
                        playback.store(Arc::new(Playback::Opening));

                        match PlaybackUnit::open(
                            url,
                            device.clone(),
                            audio_out.clone(),
                            cancel_token.clone(),
                            error_callback.clone(),
                            &protocol_whitelist,
                            UnitControls {
                                play_or_pause: play_or_pause.clone(),
                                looping: looping.clone(),
                                rate: rate.clone(),
                            },
                        ) {
                            Ok((unit, pipeline)) => {
                                let unit = Arc::new(unit);
                                playback.store(Arc::new(Playback::Active(Arc::clone(&unit))));
                                if let Err(e) = unit.run_blocking(pipeline) {
                                    error_callback.report(e.to_string());
                                    playback.store(Arc::new(Playback::Failed));
                                } else {
                                    playback.store(Arc::new(Playback::Closed));
                                }
                            }
                            Err(e) => {
                                if cancel_token.is_cancelled() {
                                    playback.store(Arc::new(Playback::Closed));
                                } else {
                                    error_callback.report(e.to_string());
                                    playback.store(Arc::new(Playback::Failed));
                                }
                            }
                        }
                    }
                }
            });

        match spawned {
            Ok(_handle) => Ok(Self {
                audio_out,
                playback,
                play_control,
                looping_control,
                rate_control,
                sender_open_intent,
                receiver_open_intent,
                last_cancel_token: None,
            }),
            Err(e) => Err(anyhow!("failed to spawn playback thread: {e}")),
        }
    }

    pub(crate) fn open_media_intent(&mut self, url: String) -> Result<()> {
        const ATTEMPTS: u32 = 100;

        self.close_media();

        let cancel = CancelToken::new();
        self.last_cancel_token = Some(cancel.clone());

        let mut payload: OpenIntent = (url, cancel.into());

        for _ in 0..ATTEMPTS {
            match self.sender_open_intent.try_send(payload) {
                Ok(()) => return Ok(()),
                Err(TrySendError::Full(recovery)) => {
                    payload = recovery;
                    let _ = self.receiver_open_intent.try_recv();
                }
                Err(TrySendError::Disconnected(_)) => {
                    return Err(anyhow!(
                        "Instance is disposed, should never happen but no hard fall with unreachable"
                    ));
                }
            }
        }

        Err(anyhow!("Ran out of attempts: {ATTEMPTS}"))
    }

    pub(crate) fn close_media(&mut self) {
        self.play_control.push(false);

        if let Some(cancel_token) = self.last_cancel_token.take() {
            cancel_token.cancel();
            self.playback.rcu(|cur| {
                if matches!(cur.as_ref(), Playback::Failed) {
                    Arc::new(Playback::Closed)
                } else {
                    cur.clone()
                }
            });
        }
    }

    fn unit(&self) -> Option<Arc<PlaybackUnit>> {
        match self.playback.load().as_ref() {
            Playback::Active(unit) => Some(Arc::clone(unit)),
            Playback::Closed | Playback::Opening | Playback::Failed => None,
        }
    }

    fn active_unit(&self) -> Result<Arc<PlaybackUnit>> {
        self.unit()
            .ok_or_else(|| anyhow!("no media open: {:?}", self.state()))
    }

    pub(crate) fn play(&self) -> Result<()> {
        self.play_control.push(true);
        Ok(())
    }

    pub(crate) fn pause(&self) -> Result<()> {
        self.play_control.push(false);
        Ok(())
    }

    pub(crate) fn set_looping(&self, looping: bool) {
        self.looping_control.push(looping);
    }

    pub(crate) fn looping(&self) -> bool {
        self.looping_control.latest()
    }

    pub(crate) fn controls_state(&self) -> ControlsState {
        let (play, play_pending) = self.play_control.snapshot();
        let (looping, looping_pending) = self.looping_control.snapshot();
        let (rate, rate_pending) = self.rate_control.snapshot();
        ControlsState {
            rate,
            play: u8::from(play),
            play_pending: u8::from(play_pending),
            looping: u8::from(looping),
            looping_pending: u8::from(looping_pending),
            rate_pending: u8::from(rate_pending),
        }
    }

    pub(crate) fn set_rate(&self, rate: f64) {
        self.rate_control.push(rate);
    }

    pub(crate) fn rate(&self) -> f64 {
        self.rate_control.latest()
    }

    pub(crate) fn seek_intent(&self, time: f64) -> Result<()> {
        self.active_unit()?.seek_intent(time);
        Ok(())
    }

    pub(crate) fn state(&self) -> UUAVState {
        match self.playback.load().as_ref() {
            Playback::Closed => UUAVState::UUAV_CLOSED,
            Playback::Opening => UUAVState::UUAV_OPENING,
            Playback::Failed => UUAVState::UUAV_ERROR,
            Playback::Active(unit) => unit.state(),
        }
    }

    pub(crate) fn duration(&self) -> Option<f64> {
        self.unit()?.duration()
    }

    pub(crate) fn current_time(&self) -> Option<f64> {
        Some(self.unit()?.current_time())
    }

    pub(crate) fn assign_master_clock(&self, current_time: f64) -> Result<()> {
        self.active_unit()?.assign_master_clock(current_time);
        Ok(())
    }

    pub(crate) fn video_size(&self) -> Option<VideoSize> {
        self.unit()?.video_size()
    }

    pub(crate) fn media_info(&self) -> Option<MediaInfo> {
        Some(self.unit()?.media_info())
    }

    pub(crate) fn video_texture(
        &self,
        #[cfg(target_os = "macos")] plane: i32,
    ) -> Option<VideoTextureView> {
        self.unit()?.video_texture(
            #[cfg(target_os = "macos")]
            plane,
        )
    }

    pub(crate) fn frame_info(&self) -> Option<FrameInfo> {
        self.unit()?.frame_info()
    }

    pub(crate) fn on_render_event(&self) {
        if let Some(unit) = self.unit() {
            unit.on_render_event();
        }
    }

    pub(crate) fn read_audio(&self, dst: *mut f32, nb_frames: i32) -> i32 {
        let Ok(frames) = usize::try_from(nb_frames) else {
            return 0;
        };
        match self.unit() {
            Some(unit) => unit.read_audio(dst, frames),
            None => {
                fill_silence(dst, frames, self.audio_out.current().channels_usize());
                0
            }
        }
    }
}
