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

**Camera-off (muted) state:** Muting a camera doesn't unpublish the track, so the last decoded frame would otherwise stay frozen on screen. `LivekitPlayer` detects this internally from the current camera track's `TrackPublication.Muted` flag, and `LivekitPlayer.LastTexture()` returns a **camera-off placeholder** (an avatar silhouette plus the streamer's name, like a video call) instead of the frozen frame. `AvatarPlaceHolderTextureSource` builds that placeholder by compositing the static `CameraOffPlaceholder` texture (configured on `MediaPlayerPluginSettings`) with the streamer name through an offscreen camera. It renders into a single reusable render texture and re-renders only when the name changes. When no name is known it shows just the background, and when no placeholder texture is configured it shows a plain dark screen with the name. `UpdateMediaPlayerSystem` blits whatever `LastTexture()` returns, so it needs no special muted-state path. A muted screen share isn't a camera, so it keeps its last frame and never shows the placeholder. Live LiveKit frames arrive vertically flipped and the placeholder is upright, so `CurrentTextureScale` is `Vector2.one` for the placeholder and `(1, -1)` for a live frame. Audio is unaffected, so muted participants are still heard.

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

**Trigger.** `LivekitPlayer` composes while both of these hold:

- the metadata of the composing bot parses (`PresentationLayout.Parse`) with a `slide`;
- the address is `current-stream`, or a `UserStream` pinned to any sid of a presentation bot (including `presentation-video`).

The composing bot is the pinned identity when the requested address is a `UserStream` pinned to a `presentation-bot:` identity, and the first `presentation-bot:` participant found for `current-stream`. The player keeps the requested address apart from the playing one, so a pinned screen never composes another bot, even after the legacy recovery has switched it to `current-stream`. A pin to any other identity never reads participant metadata.

The bot's metadata is read from `LKParticipant.Metadata` on the main thread, only while the room is connected, and re-parsed only when the raw string or the composing bot changes. The FFI-thread handlers only raise `pendingPresentationRefresh` on connect, reconnect, participant connect/disconnect and metadata changes. Unparseable or out-of-bounds metadata is warned about once per player and falls back to the legacy path. While the room can't open streams (see [Stream Recovery](#stream-recovery-self-healing)), the player drops the presentation, so `IsVideoOpened` is false and the screen renders black; once the room is connected again, the player re-reads the metadata and composes again.

**Layers.** `PresentationCompositor` blits one upright `RenderTexture` the size of the slide, which parsing already bounds to 2048 px per side (`PresentationLayout.MAX_SLIDE_SIZE`, the same `LiveKitMediaExtensions.MAX_LIVEKIT_TEXTURE_SIZE` that caps LiveKit video). It blits on every compose while a video or camera frame is drawn; with only the slide shown, it blits again only when the slide or a rect changed, or right after a video or camera frame stops being drawn, so the circle or rect never keeps a stale frame. `LivekitPlayer` composes at most once per frame: another `LastTexture()` call in the same frame, such as the one from `GatherMediaStreamDebugSystem`, returns the same composite. A video or camera frame whose texture holds sRGB data in a linear texture (`isDataSRGB` is false) is converted to linear in the shader, as the legacy path's flip blit does in `UpdateMediaPlayerSystem`. The composite has three layers:

| Layer | Source | Shown |
|-------|--------|-------|
| Slide | `slide.url`, loaded by `SlideTextureCache` | Always; the previous slide stays while the next one loads, fails, or is disallowed, and the slide is black until the first one loads |
| Video rect | the bot's `presentation-video` track | While `playingVideoIndex` points at a `slideVideos` entry with valid geometry, whatever `videoState` is; black until the stream has decoded its first frame. The SDK gives no new-frame signal, so when a new video starts the rect can briefly show the previous video's last frame until the new video's first sample arrives |
| Camera circle | the `presenterIdentity` participant's CAMERA track | While that track is published and not muted |

While composing, the player holds only the `presentation-video` and presenter camera streams, and never opens the legacy track. `presentation-video` is decoded only on a compose that shows the video rect. `CurrentTextureScale` is `Vector2.one` and `IsVideoOpened` is true while composing.

**Slide origin.** `SlideTextureCache` fetches only contract-shaped slide URLs: `https` on the default port, the host of `DecentralandUrl.CastPresenterService` (`cast-presenter-service.{BaseDomain}`, so it follows a [custom base domain](custom-base-domain.md)), no userinfo, query, fragment, or backslash, and a path made of an optional prefix of `[A-Za-z0-9_-]` segments followed by `/presentations/{uuid}/slides/{16 lowercase hex}.png`. In the Editor, loopback `http` URLs with the same path are allowed on any port. A URL must already be canonical, that is, equal to its own `Uri.AbsoluteUri`. The server's `PUBLIC_BASE_URL` must therefore be lowercase, with no explicit port and only `[A-Za-z0-9_-]` path segments, or every slide fails. Slide fetches don't follow redirects. The cache starts at most one fetch every 0.25 s per bot, tracks at most 32 bots, 32 rejected URLs, and 32 failed URLs, forgetting the least recently tracked one first, and logs at most 8 rejections and 32 failures per session. Before decoding a body, it reads the PNG header and rejects a body that isn't a PNG (a KTX2 body included), is larger than 32 MB (`MAX_SLIDE_BYTES`), or is larger than 2048 px on either side (`PresentationLayout.MAX_SLIDE_SIZE`, which also bounds the metadata's `slide.width` and `slide.height`). It doesn't fetch a rejected URL again until the rejected set forgets it, and counts each rejection against the rejection log budget. The byte limit applies to the downloaded body, so it bounds the decode but doesn't stop the download early. Slides decode without a CPU copy. The cache holds up to four slides, evicting the least recently used one, and retries a failed URL after a cooldown.

**Slide memory.** `SlideTextureCache` counts the players composing from it and destroys every cached slide when the last one stops composing. A fetch that completes after that is destroyed instead of cached. It's registered with `CacheCleaner`, which evicts the least recently used slides under memory pressure but keeps one most recently used slide per composing player, so the slides on screen survive. Slide requests time out after 15 s (`REQUEST_TIMEOUT_SECONDS`) and then retry after the failure cooldown.

**Fallback.** The moment the bot's metadata has no `slide`, or the bot leaves, the player drops both presentation streams, releases the composite render texture, and recovers the legacy track (see [Stream Recovery](#stream-recovery-self-healing)). A pinned screen reopens its pinned stream, and falls back to `current-stream` only once that stream is dead. A reconnect releases both presentation streams from the room's cache, like the legacy stream, and the next `EnsureVideoIsPlaying()` reopens them.

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
No video held → Open the playing address (deferred open, composition ended, reconnect)
Video dead + UserStream mode → Fallback to CurrentStream (first available track)
Video dead + CurrentStream mode → Re-open CurrentStream
Video alive + CurrentStream mode → TryFollowActiveSpeaker()
```

The LiveKit room events arrive on the FFI thread. Their handlers only set `volatile` flags (`pendingVideoRediscovery`, `pendingAudioRediscovery`, `pendingVideoReset`, `pendingPresentationRefresh`), which `EnsureVideoIsPlaying()` and `EnsureAudioIsPlaying()` consume on the main thread. A rediscovery flag is consumed even while the stream is healthy, and never re-opens an established stream: re-allocating the stream while a subscription is in flight can replace the in-flight `Weak<IVideoStream>` and stall playback (observed on Windows). The room owns every stream: the player only drops its handles, and calls `VideoStreams.Release()` solely to evict streams held across a reconnect, because they belong to the torn-down connection and the reopen must get a fresh stream instead of the room's cached one. `LivekitPlayer` is otherwise main-thread only.

While the room is tearing down (`canOpenStreams` is false), `EnsureVideoIsPlaying()` opens no stream, because opening one with invalid FFI handles poisons the room's reusable stream cache. The pending flags stay set for the reconnect. It drops a presentation being composed, and calls `EnsureAudioIsPlaying()`, which releases the audio sources whose streams died with the room.

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

LiveKit video textures are capped at **2048x2048** (`LiveKitMediaExtensions.MAX_LIVEKIT_TEXTURE_SIZE`, used by `UpdateMediaPlayerSystem`). If a video frame exceeds these dimensions, it's scaled down via `Graphics.Blit()` before being copied to the render target. This prevents GPU stalls from unexpectedly large incoming video. Presentation slides, and so the composite, are bounded to the same 2048 px per side when the bot's metadata is parsed.

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

`MediaFactory` (built by `MediaFactoryBuilder` per scene) decides which backend to create based on the URL scheme. It holds a reference to the scene's `IRoom` from `IRoomHub`. It creates a fresh player per call, because a shared `MediaPlayer` caused a use-after-destroy crash (UNITY-EXPLORER-MV2). `MediaFactoryBuilder` receives the app-wide `SlideTextureCache` and the compositor material (`CompositorMaterial` on `MediaPlayerContainer.Settings`) once, and every `LivekitPlayer` builds its `PresentationCompositor` from that material when it's created.

On Linux (`UNITY_EDITOR_LINUX`, `UNITY_STANDALONE_LINUX`), `MediaPlayerPluginWrapper.InjectToWorld` compiles the media systems out, so its fields look unused to InspectCode, which runs with those defines. `MediaPlayerPlugin` creates the camera-off placeholder only where the systems are compiled in, because the placeholder builds an offscreen camera and a render texture as soon as it's created.

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
| `PluginSystem/World/MediaPlayerPlugin.cs` | Plugin settings — `FlipMaterial`, `CameraOffPlaceholder` |
| `SDKComponents/MediaStream/Systems/MediaPlayerContainer.cs` | Container settings — `MediaPlayerPrefab`, `CompositorMaterial`; owns the `SlideTextureCache` |
| `SDKComponents/MediaStream/PresentationLayout.cs` | Parses the bot's v2 metadata and computes the video rect and camera circle |
| `SDKComponents/MediaStream/SlideTextureCache.cs` | App-wide slide texture cache with the host allowlist |
| `SDKComponents/MediaStream/PresentationCompositor.cs` | Blits slide, video rect and camera circle into one render texture |
| `Rendering/Composition/PresentationCompositorShader.shader` | `DCL/PresentationCompositor` single-pass compositor shader |
| `SDKComponents/MediaStream/LiveKitMediaExtensions.cs` | URL parsing helpers |
| `Infrastructure/.../CommsApi/CommsApiWrap.cs` | `getActiveVideoStreams` API |
| `Infrastructure/.../CommsApi/GetActiveVideoStreamsResponse.cs` | Response builder with display name resolution |
| `Multiplayer/Connections/Rooms/ParticipantExtensions.cs` | Address construction from participants |
