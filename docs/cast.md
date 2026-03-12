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

Picks the first available video track in the room — and then **follows the active speaker** (see [Active Speaker Tracking](#active-speaker-tracking-video-follows-voice) below). This is the default mode for streaming theatre screens.

### UserStream

```
livekit-video://{identity}/{sid}
```

Pins to a specific participant's track by identity and stream ID. No automatic switching occurs.

Defined in `LivekitAddress.cs`. Helper extensions in `LiveKitMediaExtensions.cs` handle parsing.

---

## Video Routing

### How the first video track is selected

When `OpenMedia()` is called:

- **CurrentStream** → `FirstVideoTrackingIdentity()` iterates all remote participants (under lock) and returns the first video track found. The participant's identity is stored in `currentVideoIdentity`.
- **UserStream** → Directly opens the stream for the specified `(identity, sid)`.

### Active Speaker Tracking (video-follows-voice)

In `CurrentStream` mode, the video automatically switches to whoever is speaking. This is driven by `TryFollowActiveSpeaker()`, which runs every frame inside `EnsureVideoIsPlaying()`.

**How it works:**

1. `room.ActiveSpeakers` (provided by the LiveKit SDK) is an ordered collection of participant identities currently speaking — first element = highest audio level.
2. Each frame, `TryFollowActiveSpeaker()` reads the dominant speaker.
3. If the dominant speaker differs from the current video identity **and** enough time has passed since the last switch, the video stream is swapped.

**Debounce:** A minimum hold time of **1.5 seconds** (`MIN_SPEAKER_HOLD_SECONDS`) prevents flickering during rapid speaker changes.

**Fallback rules:**

| Scenario | Behavior |
|----------|----------|
| Active speaker has no video track | Keep current video |
| No one is speaking | Keep current video |
| Rapid speaker changes (<1.5s) | Debounced — stays on current |
| UserStream mode | No auto-switching (early return) |

**Key methods in `LivekitPlayer.cs`:**

- `FirstVideoTrackingIdentity()` — Selects first video track and records identity
- `TryFollowActiveSpeaker()` — Core speaker-tracking logic with debounce
- `FindVideoTrackForParticipant(identity)` — Looks up a participant's video track by identity

---

## Audio Routing

Audio is handled independently from video.

### All tracks play simultaneously

`OpenAllAudioStreams()` iterates **every remote participant** in the room and opens **every audio track** it finds. Each track gets its own pooled `LivekitAudioSource` from a `ThreadSafeObjectPool`. This means:

- All participants' microphones are heard at once (like a conference call).
- Audio is **not** tied to the currently displayed video — you always hear everyone.
- Volume and spatial positioning are applied uniformly to all sources.

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
Any audio source dead → Release all, re-collect all audio tracks
All audio alive → No action
```

This means if a participant leaves and rejoins, or a new participant joins, the audio will automatically pick them up on the next recovery cycle.

---

## System Architecture

### ECS Systems

| System | Group | Responsibility |
|--------|-------|---------------|
| `CreateMediaPlayerSystem` | ComponentInstantiation | Detects new `PBVideoPlayer`/`PBAudioStream` components, creates `MediaPlayerComponent` with appropriate backend |
| `UpdateMediaPlayerSystem` | SyncedPresentation | Drives playback each frame — calls `EnsureVideoIsPlaying()`, `EnsureAudioIsPlaying()`, handles volume crossfading, texture updates |
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
| `SDKComponents/MediaStream/Systems/CreateMediaPlayerSystem.cs` | System creating players from SDK components |
| `SDKComponents/MediaStream/Systems/CleanUpMediaPlayerSystem.cs` | Disposal system |
| `SDKComponents/MediaStream/MediaFactory.cs` | Factory choosing backend by URL |
| `SDKComponents/MediaStream/LiveKitMediaExtensions.cs` | URL parsing helpers |
| `Infrastructure/.../CommsApi/CommsApiWrap.cs` | `getActiveVideoStreams` API |
| `Infrastructure/.../CommsApi/GetActiveVideoStreamsResponse.cs` | Response builder with display name resolution |
| `Multiplayer/Connections/Rooms/ParticipantExtensions.cs` | Address construction from participants |
