# Cast — LiveKit Media Streaming

This document explains how live video and audio streaming works in the explorer via LiveKit rooms.

## Overview

The cast feature allows scenes to display live video/audio streams from LiveKit rooms. A scene places a `PBVideoPlayer` or `PBAudioStream` SDK component on an entity with a `livekit-video://` URL, and the explorer connects to the room and routes media to the in-world screen.

Two player backends exist side by side:

| Backend | URL scheme | Use case |
|---------|-----------|----------|
| **AvProPlayer** | `http://`, `https://` | Pre-recorded or HLS video |
| **LivekitPlayer** | `livekit-video://` | Real-time room streams |

The `MultiMediaPlayer` REnum wraps both behind a unified interface so the ECS systems don't care which backend is active.

---

## Address Types — `LivekitAddress`

`LivekitAddress` is an REnum (discriminated union) with two variants:

### CurrentStream

```
livekit-video://current-stream
```

Picks the highest-priority available video track in the room — and then **follows the active speaker** (see [Active Speaker Tracking](#active-speaker-tracking-video-follows-voice) below). The priority order is **presentation bot → screen share → active-speaker camera**. This is the default mode for streaming theatre screens.

### UserStream

```
livekit-video://{identity}/{sid}
```

Pins to a specific participant's track by identity and stream ID. No automatic switching occurs.

The `{identity}` value is the participant's wallet address (Ethereum address), set by the Archipelago adapter when it mints the LiveKit JWT.

Defined in `LivekitAddress.cs`. Helper extensions in `LiveKitMediaExtensions.cs` handle parsing.

---

## Video Routing

### How the first video track is selected

When `OpenMedia()` is called:

- **CurrentStream** → `BestInitialVideoKey()` selects in priority order: a **presentation bot** track (identity starts with `presentation-bot:`), then a **screen share** track (`TrackSource.SourceScreenshare`), then the first available video track (`FirstAvailableTrackSid()`). The selected `(identity, sid)` is stored as the current stream key.
- **UserStream** → Directly opens the stream for the specified `(identity, sid)`.

Legacy selection never picks a track named `presentation-video` (`PRESENTATION_VIDEO_TRACK_NAME`): `FindVideoTrackForParticipant()`, `FirstScreenShareVideoKey()` and `FirstAvailableTrackSid()` all skip it. That track carries only the raw video region of a cast v2 presentation, which is drawn by [presentation composition](#presentation-composition-cast-v2) instead.

### Active Speaker Tracking (video-follows-voice)

In `CurrentStream` mode, the video automatically switches to whoever is speaking. This is driven by `TryFollowVideoStreamToActiveSpeaker()`, which runs every frame inside `EnsureVideoIsPlaying()` and picks the best source via `BestFollowCandidate()`.

**How it works:**

1. `room.ActiveSpeakers` (provided by the LiveKit SDK) is an ordered collection of participant identities currently speaking — first element = highest audio level.
2. Each frame, `BestFollowCandidate()` resolves the best source in priority order: **presentation bot** (`PresentationBotVideoKey()`), then **screen share** (`FirstScreenShareVideoKey()`), then the **dominant active speaker** with a video track (`FindVideoTrackForParticipant()`).
3. The video switches only when the chosen `(identity, sid)` differs from the current one. While a presentation bot or screen share is available, it holds the stream and the active-speaker tier is skipped. Active-speaker switches are also debounced (see below).

**Debounce:** A minimum hold time of **1.5 seconds** (`MIN_SPEAKER_HOLD_SECONDS`) prevents flickering during rapid speaker changes. It applies only to the active-speaker tier.

**Presentation bot:** Any participant whose identity starts with `presentation-bot:` (`PRESENTATION_BOT_IDENTITY_PREFIX`) is treated as the authoritative video source. While a presentation bot is present, it holds the stream regardless of active speakers.

**Screen share:** Any video track published with `TrackSource.SourceScreenshare` ranks above active-speaker cameras and below the presentation bot. While a screen share is live, it holds the stream; the player falls back to active-speaker tracking once it ends **or is paused (the track is muted)**, and switches back when it resumes.

**Fallback rules:**

| Scenario | Behavior |
|----------|----------|
| Presentation bot present | Hold on bot (highest priority) |
| Screen share present (no bot) | Hold on screen share |
| Active speaker has no video track | Keep current video |
| No one is speaking | Keep current video |
| Rapid speaker changes (<1.5s) | Debounced — stays on current |
| UserStream mode | No auto-switching (early return) |

**Camera-off (muted) state:** Muting a camera doesn't unpublish the track, so the last decoded frame would otherwise stay frozen on screen. `LivekitPlayer` detects this internally from the current camera track's `TrackPublication.Muted` flag, and `LivekitPlayer.LastTexture()` returns a **camera-off placeholder** (an avatar silhouette plus the streamer's name, like a video call) instead of the frozen frame. `AvatarPlaceHolderTextureSource` builds that placeholder by compositing the static `CameraOffPlaceholder` texture (configured on `MediaPlayerPluginSettings`) with the streamer name through an offscreen camera. It renders into a single reusable render texture and re-renders only when the name changes. When no name is known it shows just the background, and when no placeholder texture is configured it shows a plain dark screen with the name. `UpdateMediaPlayerSystem` blits whatever `LastTexture()` returns, so it needs no special muted-state path. Audio is unaffected, so muted participants are still heard.

**Key methods in `LivekitPlayer.cs`:**

- `BestInitialVideoKey()` — Initial selection: presentation bot → screen share → first available
- `BestFollowCandidate()` — Per-frame source selection in the same priority order
- `TryFollowVideoStreamToActiveSpeaker()` — Applies `BestFollowCandidate()`, switching only when the source changed
- `FirstScreenShareVideoKey()` — Finds the first screen-share video track in the room
- `PresentationBotVideoKey()` / `PresentationBotIdentity()` — Find the presentation bot's video track / identity
- `FindVideoTrackForParticipant(identity)` — Looks up a participant's video track by identity
- `FirstAvailableTrackSid(kind)` — Returns the first available track of a kind (final fallback)
- `LastTexture()` — Returns the live frame, the camera-off placeholder when the current camera track is muted, or the presentation composite while composing

---

## Presentation composition (cast v2)

When the presentation bot runs with client composition, the explorer draws the presentation itself instead of showing a pre-composited bot track. Scenes change nothing: the composite reaches the screen through the same `LastTexture()` path as any other frame.

**Trigger.** `LivekitPlayer` composes while all of these hold:

- the compositor material (`CompositorMaterial` on `MediaPlayerPluginSettings`) and the app-wide `SlideTextureCache` exist;
- the metadata of the composing bot parses (`PresentationLayout.Parse`) with a `slide`;
- the address is `current-stream`, or a `UserStream` pinned to any sid of a presentation bot (including `presentation-video`).

The composing bot is the pinned identity when the requested address is a `UserStream` pinned to a `presentation-bot:` identity, and the first `presentation-bot:` participant found for `current-stream`. The player keeps the requested address apart from the playing one, so a pinned screen never composes another bot, even after the legacy recovery has switched it to `current-stream`. A pin to any other identity never reads participant metadata.

The bot's metadata is read from `LKParticipant.Metadata` on the main thread, only while the room is connected, and re-parsed only when the raw string changes. A reconnect clears each participant's info on the FFI thread, so `OpenMedia` leaves the read to the first `EnsureVideoIsPlaying()` after the room connects. The FFI-thread handlers only raise `pendingPresentationRefresh` on connect, reconnect, participant connect/disconnect and metadata changes. Unparseable or out-of-bounds metadata is warned about once per player and falls back to the legacy path.

**Layers.** `PresentationCompositor` blits one upright `RenderTexture` the size of the slide, scaled down with its aspect preserved to at most 2048 px per side (`MAX_COMPOSITE_SIZE`). It blits again only when an input changed. The composite has three layers:

| Layer | Source | Shown |
|-------|--------|-------|
| Slide | `slide.url`, loaded by `SlideTextureCache` | Always; black while loading, failed or disallowed |
| Video rect | the bot's `presentation-video` track | While `playingVideoIndex` is set; black until a frame newer than the one seen when the index or slide changed arrives, so a new video never shows the previous video's last frame. A newly bound `presentation-video` stream also starts black, so a late joiner during a pause sees black until playback resumes |
| Camera circle | the `presenterIdentity` participant's CAMERA track | While that track is published and not muted |

While composing, the player holds only the `presentation-video` and presenter camera streams, and never opens the legacy track. `presentation-video` is decoded every frame to drain frames that arrive between videos. `LastTexture()` caches the composite per frame, so a second call in the same frame (for example from `GatherMediaStreamDebugSystem`) does not blit again. `CurrentTextureScale` is `Vector2.one` and `IsVideoOpened` is true while composing.

**Slide origin.** `SlideTextureCache` fetches only contract-shaped slide URLs: `https` on the default port, an ASCII host equal to `cast-presenter-service.` plus a Decentraland domain (`IDecentralandUrlsSource.ALL_DOMAINS`), no userinfo, query, fragment, or backslash, and a path ending in `/presentations/{uuid}/slides/{16 lowercase hex}.png`. In the Editor, loopback `http` URLs with the same path are allowed on any port. Slide fetches don't follow redirects. The cache starts at most one fetch every 0.25 s, tracks at most 32 rejected and 32 failed URLs, and logs at most 32 reports per session. It rejects slides decoded larger than 4096 px on either side, and drops the CPU copy of every slide it keeps. It holds up to four slides and retries a failed URL after a cooldown.

**Fallback.** The moment the bot's metadata has no `slide`, or the bot leaves, the player drops both presentation streams, releases the composite render texture, and recovers the legacy track (see [Stream Recovery](#stream-recovery-self-healing)). A reconnect releases both presentation streams from the room's cache, like the legacy stream, and the next `EnsureVideoIsPlaying()` reopens them.

---

## Audio Routing

Audio is handled independently from video.

### All tracks play simultaneously

`OpenAllAudioStreams()` iterates **every remote participant** in the room and opens **every audio track** it finds. Each track gets its own pooled `LivekitAudioSource` from a `ThreadSafeObjectPool`. This means:

- All participants' microphones are heard at once (like a conference call).
- Audio is **not** tied to the currently displayed video — you always hear everyone.
- Volume and spatial positioning are applied uniformly to all sources.
- Discovery is **additive** — new participants joining mid-session are picked up on the next rescan without disrupting existing audio sources.

### Spatial audio

When the SDK component has `spatial = true`, audio sources are positioned in 3D space via `PlaceAudioAt(position)`. Min/max distance is configured through the SDK component fields.

### Paired audio (reserved)

`FindPairedAudio()` maps a video track to its companion audio track (camera → microphone, screenshare → screenshare audio). This exists for future use but is not currently active — all audio plays regardless.

---

## Stream Recovery (Self-Healing)

Both video and audio streams can die at any time (participant disconnects, network issues). The system self-heals via two methods called every frame from `UpdateMediaPlayerSystem`:

### `EnsureVideoIsPlaying()`

```
Composing → Keep the presentation streams resolved (see Presentation composition)
Video dead + UserStream mode → Fallback to CurrentStream (first available track)
Video dead + CurrentStream mode → Re-open CurrentStream
Video alive + CurrentStream mode → TryFollowActiveSpeaker()
```

The LiveKit room events arrive on the FFI thread. Their handlers only set `volatile` flags (`pendingVideoRediscovery`, `pendingAudioRediscovery`, `pendingVideoReset`, `pendingPresentationRefresh`), which `EnsureVideoIsPlaying()` and `EnsureAudioIsPlaying()` consume on the main thread. A rediscovery flag is consumed even while the stream is healthy, and never re-opens an established stream: re-allocating the stream while a subscription is in flight can replace the in-flight `Weak<IVideoStream>` and stall playback (observed on Windows). The room owns every stream: the player only drops its handles, and calls `VideoStreams.Release()` solely to evict streams held across a reconnect.

While the room is tearing down (`canOpenStreams` is false), `EnsureVideoIsPlaying()` opens no stream, because opening one with invalid FFI handles poisons the room's reusable stream cache. The pending flags stay set for the reconnect. It only calls `EnsureAudioIsPlaying()`, which releases the audio sources whose streams died with the room.

`EnsureVideoIsPlaying()` also drives audio discovery. `UpdateMediaPlayerSystem` drives audio from a separate `UpdateAudioStream` query for `PBAudioStream`, and entities with only `PBVideoPlayer` never enter it, so without this a LiveKit room on a video-only screen would stay silent.

### `EnsureAudioIsPlaying()`

```
Any audio source dead → Release dead source, rescan all participants immediately
No dead sources + rescan interval elapsed (2s) → Additive rescan for new participants
No dead sources + within interval → No action
```

**Rescan throttling:** When no tracks have died, `OpenAllAudioStreams()` is called at most once every **2 seconds** (`AUDIO_RESCAN_INTERVAL_SECONDS`). If a dead track is detected, the rescan happens immediately. This prevents unnecessary iteration every frame while still picking up late-joining participants promptly.

---

## Resolution Capping

LiveKit video textures are capped at **2048x2048** (`MAX_LIVEKIT_VIDEO_WIDTH` / `MAX_LIVEKIT_VIDEO_HEIGHT` in `UpdateMediaPlayerSystem`). If a video frame exceeds these dimensions, it's scaled down via `Graphics.Blit()` before being copied to the render target. This prevents GPU stalls from unexpectedly large incoming video. `PresentationCompositor` caps the presentation composite itself at the same 2048 px per side.

When no LiveKit stream is open, the system renders a black texture. When the current camera track is muted (camera off), `LivekitPlayer.LastTexture()` returns the camera-off placeholder (avatar silhouette plus the streamer name), which the system blits like any other frame.

---

## System Architecture

### ECS Systems

| System | Group | Responsibility |
|--------|-------|---------------|
| `CreateMediaPlayerSystem` | ComponentInstantiation | Detects new `PBVideoPlayer`/`PBAudioStream` components, creates `MediaPlayerComponent` with appropriate backend |
| `UpdateMediaPlayerSystem` | SyncedPresentation | Drives playback each frame — calls `EnsureVideoIsPlaying()`, `EnsureAudioIsPlaying()`, handles volume crossfading, and blits the texture from `LastTexture()` (live frame or camera-off placeholder) to the render target |
| `CleanUpMediaPlayerSystem` | CleanUp | Disposes players when entities/components are removed |

### Factory

`MediaFactory` (built by `MediaFactoryBuilder` per scene) decides which backend to create based on the URL scheme. It holds a reference to the scene's `IRoom` from `IRoomHub`. It creates a fresh player per call, because a shared `MediaPlayer` caused a use-after-destroy crash (UNITY-EXPLORER-MV2).

### Component

`MediaPlayerComponent` wraps a `MultiMediaPlayer` (which is either `AvProPlayer` or `LivekitPlayer`). It also tracks frozen-stream detection and audio visualization buffers.

---

## SDK Integration

### How a scene triggers streaming

1. Scene SDK sends a `PBVideoPlayer` component with `src = "livekit-video://current-stream"` (or a specific user address).
2. `CreateMediaPlayerSystem` picks it up, calls `MediaAddress.New()` which detects the `livekit-video://` prefix.
3. `MediaFactory` creates a `LivekitPlayer` backed by the scene's LiveKit room.
4. `UpdateMediaPlayerSystem` drives it every frame.

### `getActiveVideoStreams` API

Scenes can query available streams via `CommsApiWrap.GetActiveVideoStreams()`. The response includes:

```json
{
  "streams": [
    {
      "identity": "participant-id",
      "trackSid": "livekit-video://identity/sid",
      "sourceType": "VTST_CAMERA",
      "name": "Display Name",
      "speaking": true,
      "trackName": "video",
      "width": 1920,
      "height": 1080
    }
  ]
}
```

A synthetic `current-stream` entry is always included, pointing to the first available participant.

### Data messaging API

Scenes can exchange messages with other participants in the LiveKit room through `CommsApiWrap`. The following methods are exposed:

- `PublishData(topic, data)` — Sends a message to a topic. Rate-limited to **10 messages per second** per topic (`MAX_MESSAGES_PER_SECOND`), with a maximum payload of **16 KB** (`MAX_MESSAGE_SIZE_BYTES`).
- `SubscribeToTopic(topic)` — Subscribes to a topic so incoming messages are buffered.
- `ConsumeMessages(topic)` — Returns all buffered messages for a topic and clears the buffer.

Messages are buffered per topic in memory and consumed by the scene on demand. Rate limiting uses a sliding window that resets every second.

### CastV2 — Display Name Resolution

Participants joining via castV2 (unauthenticated web viewers) may not have a `Name` field. Display name is resolved with this fallback chain:

```
Participant.Metadata.displayName → Participant.Name → Participant.Identity
```

Metadata is a JSON string parsed at query time.

---

## Key Files

| File | Role |
|------|------|
| `SDKComponents/MediaStream/LivekitPlayer.cs` | Core player — video/audio routing, speaker tracking, recovery |
| `SDKComponents/MediaStream/LivekitAddress.cs` | `CurrentStream` / `UserStream` address REnum |
| `SDKComponents/MediaStream/MultiMediaPlayer.cs` | Unified wrapper over AvPro and Livekit backends |
| `SDKComponents/MediaStream/MediaPlayerComponent.cs` | ECS component holding the player |
| `SDKComponents/MediaStream/Systems/UpdateMediaPlayerSystem.cs` | Per-frame system driving playback |
| `SDKComponents/MediaStream/Systems/AvatarPlaceHolderTextureSource.cs` | Builds the camera-off placeholder texture (avatar + streamer name) shown when a camera track is muted |
| `SDKComponents/MediaStream/Systems/CreateMediaPlayerSystem.cs` | System creating players from SDK components |
| `SDKComponents/MediaStream/Systems/CleanUpMediaPlayerSystem.cs` | Disposal system |
| `SDKComponents/MediaStream/MediaFactory.cs` | Factory choosing backend by URL |
| `PluginSystem/World/MediaPlayerPlugin.cs` | Plugin settings — `FlipMaterial`, `CompositorMaterial`, `CameraOffPlaceholder` |
| `SDKComponents/MediaStream/PresentationLayout.cs` | Parses the bot's v2 metadata and computes the video rect and camera circle |
| `SDKComponents/MediaStream/SlideTextureCache.cs` | App-wide slide texture cache with the host allowlist |
| `SDKComponents/MediaStream/PresentationCompositor.cs` | Blits slide, video rect and camera circle into one render texture |
| `Rendering/Composition/PresentationCompositorShader.shader` | `DCL/PresentationCompositor` single-pass compositor shader |
| `SDKComponents/MediaStream/LiveKitMediaExtensions.cs` | URL parsing helpers |
| `Infrastructure/.../CommsApi/CommsApiWrap.cs` | `getActiveVideoStreams` API |
| `Infrastructure/.../CommsApi/GetActiveVideoStreamsResponse.cs` | Response builder with display name resolution |
| `Multiplayer/Connections/Rooms/ParticipantExtensions.cs` | Address construction from participants |
