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

**Camera-off (muted) state:** `IsCurrentVideoMuted` reads the current video track's `TrackPublication.Muted` flag. Muting a camera does not unpublish the track, so the last decoded frame would otherwise stay frozen on screen. When the current track is muted, `UpdateMediaPlayerSystem` shows a **camera-off placeholder** (an avatar silhouette plus the streamer's name, like a video call) instead of the frozen frame. `CameraOffScreenComposer` composites the static `CameraOffPlaceholder` texture (configured on `MediaPlayerPluginSettings`) with the streamer name (`LivekitPlayer.CurrentStreamerName`) into a per-name cached texture via an offscreen camera; it falls back to the plain placeholder when no name is known, and to black when no placeholder texture is set. Audio is unaffected, so muted participants are still heard.

**Key methods in `LivekitPlayer.cs`:**

- `BestInitialVideoKey()` — Initial selection: presentation bot → screen share → first available
- `BestFollowCandidate()` — Per-frame source selection in the same priority order
- `TryFollowVideoStreamToActiveSpeaker()` — Applies `BestFollowCandidate()`, switching only when the source changed
- `FirstScreenShareVideoKey()` — Finds the first screen-share video track in the room
- `PresentationBotVideoKey()` / `PresentationBotIdentity()` — Find the presentation bot's video track / identity
- `FindVideoTrackForParticipant(identity)` — Looks up a participant's video track by identity
- `FirstAvailableTrackSid(kind)` — Returns the first available track of a kind (final fallback)
- `IsCurrentVideoMuted` — Reads the current track's muted state from its publication

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
Video dead + UserStream mode → Fallback to CurrentStream (first available track)
Video dead + CurrentStream mode → Re-open CurrentStream
Video alive + CurrentStream mode → TryFollowActiveSpeaker()
```

### `EnsureAudioIsPlaying()`

```
Any audio source dead → Release dead source, rescan all participants immediately
No dead sources + rescan interval elapsed (2s) → Additive rescan for new participants
No dead sources + within interval → No action
```

**Rescan throttling:** When no tracks have died, `OpenAllAudioStreams()` is called at most once every **2 seconds** (`AUDIO_RESCAN_INTERVAL_SECONDS`). If a dead track is detected, the rescan happens immediately. This prevents unnecessary iteration every frame while still picking up late-joining participants promptly.

---

## Resolution Capping

LiveKit video textures are capped at **1920x1080** (`MAX_LIVEKIT_VIDEO_WIDTH` / `MAX_LIVEKIT_VIDEO_HEIGHT` in `UpdateMediaPlayerSystem`). If a video frame exceeds these dimensions, it's scaled down via `Graphics.Blit()` before being copied to the render target. This prevents GPU stalls from unexpectedly large incoming video.

When no texture is available (no stream is open), the system renders a black texture. When the current LiveKit track is muted (camera off), it renders the camera-off placeholder (avatar silhouette plus the streamer name) instead.

---

## System Architecture

### ECS Systems

| System | Group | Responsibility |
|--------|-------|---------------|
| `CreateMediaPlayerSystem` | ComponentInstantiation | Detects new `PBVideoPlayer`/`PBAudioStream` components, creates `MediaPlayerComponent` with appropriate backend |
| `UpdateMediaPlayerSystem` | SyncedPresentation | Drives playback each frame — calls `EnsureVideoIsPlaying()`, `EnsureAudioIsPlaying()`, handles volume crossfading, texture updates, and renders the camera-off placeholder when the current track is muted |
| `CleanUpMediaPlayerSystem` | CleanUp | Disposes players when entities/components are removed |

### Factory

`MediaFactory` (built by `MediaFactoryBuilder` per scene) decides which backend to create based on the URL scheme. It holds a reference to the scene's `IRoom` from `IRoomHub`.

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
| `SDKComponents/MediaStream/Systems/CameraOffScreenComposer.cs` | Composites the avatar placeholder + streamer name shown when a track is muted |
| `SDKComponents/MediaStream/Systems/CreateMediaPlayerSystem.cs` | System creating players from SDK components |
| `SDKComponents/MediaStream/Systems/CleanUpMediaPlayerSystem.cs` | Disposal system |
| `SDKComponents/MediaStream/MediaFactory.cs` | Factory choosing backend by URL |
| `PluginSystem/World/MediaPlayerPlugin.cs` | Plugin settings — `FlipMaterial`, `CameraOffPlaceholder` |
| `SDKComponents/MediaStream/LiveKitMediaExtensions.cs` | URL parsing helpers |
| `Infrastructure/.../CommsApi/CommsApiWrap.cs` | `getActiveVideoStreams` API |
| `Infrastructure/.../CommsApi/GetActiveVideoStreamsResponse.cs` | Response builder with display name resolution |
| `Multiplayer/Connections/Rooms/ParticipantExtensions.cs` | Address construction from participants |
