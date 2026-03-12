# ADR: Voice-Driven Lip Sync for Proximity Chat Avatars

> **Status:** Proposed  
> **Date:** 2026-03-12  
> **Authors:** Investigation session (human + AI)  
> **Related:** PR #7452 (feat/avatar-blink), `ProximityVoiceChatManager`, `LivekitAudioSource`

---

## Context

Decentraland proximity voice chat is functional: players hear each other with 3D spatial audio via LiveKit. However, avatars remain visually static during speech — there is no mouth animation or facial feedback. This reduces social presence, especially given the stylized 2D facial features on avatars.

Product vision (from @olavra):
> "My idea is to use the same system to have a texture sheet for mouth gestures (phonemes + expressions) and eyebrows. We can fit 16 mouth poses in a 1024×1024 sprite atlas. We will need to recognize frequencies or just randomize the mouth sequence."

PR #7452 implements a prototype for text-based mouth animation (chat messages drive phonemes) and eye blink. This ADR covers the **voice-driven** counterpart: animating mouths in response to actual audio from the voice chat.

---

## Decision Drivers

1. **Social presence:** Mouth animation is the single highest-impact visual cue for "this person is talking."
2. **Performance:** 50+ avatars in a scene; lip sync processing must be lightweight.
3. **Iterative delivery:** Ship value early with simple approaches, improve quality incrementally.
4. **Minimal coupling:** Avoid tight dependency between the LiveKit audio pipeline and avatar rendering.
5. **Existing infrastructure:** Reuse `ProximityAudioPositionSystem` patterns, `MaterialPropertyBlock` approach from PR #7452, and the mouth sprite atlas.

---

## Decision 1: Audio Analysis Approach — Iterative Progression

### Considered Options

| # | Approach | Effort | Quality | CPU/source | Dependencies |
|---|----------|--------|---------|------------|--------------|
| A | Binary (IActiveSpeakers) + random animation | Hours | Decent (anime-style) | ~0 | None |
| B | Amplitude (RMS) driven | Hours | Better responsiveness | ~0.01ms | 5 lines in LiveKit |
| C | FFT frequency bands | Days | Moderate (rough vowel/consonant) | ~0.05–0.1ms | Manual FFT impl |
| D | OVRLipSync visemes | 1–2 days | Best (15 visemes) | ~0.1–0.3ms | Native plugin |

### Chosen: Iterative A → B → (C optional) → D

**Step 1 — A2+P1: Random animation when speaking.**  
Use `IActiveSpeakers` from the Island Room. When a participant identity appears in the active speakers list, animate their avatar's mouth by cycling through random phoneme sprites at ~10 fps. When they stop speaking, return to idle. Zero LiveKit SDK changes. Immediate visual result.

**Step 2 — A4+P2: Amplitude-weighted random.**  
Add ~5 lines to `LivekitAudioSource.OnAudioFilterRead` to compute pre-spatialization RMS and expose as `volatile float`. ECS system reads amplitude per source, selects random sprite from a subset weighted by loudness (quiet → slightly open poses, loud → wide open poses). Exponential smoothing + hysteresis.

**Step 3 (optional) — FFT frequency bands.**  
Extend RMS computation to include 3–5 frequency band energy analysis in `OnAudioFilterRead`. Map band ratios to approximate mouth shapes (vowels vs consonants). Higher effort, moderate quality gain. **Skip this step if going directly to OVRLipSync.**

**Step 4 — A5+P3: OVRLipSync visemes.**  
Feed PCM buffer from `OnAudioFilterRead` to `OVRLipSync.ProcessFrame`. Map 15 viseme weights to 12–16 sprite poses. Pool of 8 contexts, assigned to actively speaking avatars only.

### Rationale

- Each step is independently shippable and improves on the previous.
- Binary + random (Step 1) already looks surprisingly good for stylized 2D art — anime and many games use this technique.
- Amplitude (Step 2) adds responsiveness with minimal LiveKit SDK change.
- FFT (Step 3) is an intermediate option if OVRLipSync licensing or platform support is problematic.
- OVRLipSync (Step 4) is the quality ceiling for 2D sprite-based lip sync.

---

## Decision 2: Data Source for Audio Analysis

### Considered Options

| Source | What it provides | Latency | Granularity | LiveKit changes |
|--------|-----------------|---------|-------------|-----------------|
| `Participant.AudioLevel` | float 0..1 | Signaling (~200–500ms) | ~4–5/sec | None, but **dead code** — never set |
| `IActiveSpeakers` | bool (speaking/not) | Signaling (~200–500ms) | ~4–5/sec | None |
| `OnAudioFilterRead` RMS | float 0..1 | Real-time | ~46/sec (48kHz/1024) | ~5 lines |
| `AudioSource.GetOutputData` | float[] post-spatialization | Main thread read | Per frame | None |

### Chosen: Start with IActiveSpeakers, progress to OnAudioFilterRead RMS

**`Participant.AudioLevel` is dead code** — declared with `private set` in `Participant.cs` but never assigned anywhere in the C# layer. The `ActiveSpeakersChanged` FFI event only carries `ParticipantIdentities` (list of strings), not audio levels. Unusable without FFI-level work to wire it up from the Rust SDK.

**`AudioSource.GetOutputData`** reads post-spatialization data from the main thread. Amplitude would depend on listener position relative to speaker — incorrect for lip sync (a distant speaker would appear to have a closed mouth). Rejected.

**`IActiveSpeakers`** provides binary speaking status with zero LiveKit changes. Sufficient for Step 1 (random animation).

**`OnAudioFilterRead` RMS** provides real-time pre-spatialization amplitude with minimal change (~5 lines). Required for Step 2+.

### Rationale

Start with the zero-change option to validate the full pipeline (ECS component → renderer → MaterialPropertyBlock). Move to OnAudioFilterRead when quality needs to improve.

---

## Decision 3: ECS Architecture

### Considered Options

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| Extend `ProximityAudioPositionSystem` | Add lip sync logic to existing system | Single system, shared dependencies | Violates single responsibility, grows large |
| New `ProximityLipSyncSystem` | Separate system, same group | Clean separation, independent iteration | Needs same dependencies (entityParticipantTable, etc.) |
| Bridge component (like PR #7452) | Component written by VoiceChat, read by AvatarRendering system | Decouples assemblies | Extra indirection, two systems involved |

### Chosen: New `ProximityLipSyncSystem` with shared dictionary pattern

A new ECS system in the same `PresentationSystemGroup`, `UpdateAfter(ProximityAudioPositionSystem)`. Follows the same pattern: iterates a shared `ConcurrentDictionary`, resolves entities via `entityParticipantTable`, adds/updates a lip sync component.

### Component Design

```csharp
public struct ProximityLipSyncComponent
{
    public Renderer MouthRenderer;
    public float SmoothedAmplitude;
    public int CurrentPoseIndex;
    public float PoseHoldTimer;
    public float RandomSeed;   // per-avatar random offset for animation variety
}
```

### Data Flow

```
IActiveSpeakers (or LivekitAudioSource._amplitude)
    ↓
ProximityLipSyncSystem.Update()
    ↓
foreach participant in data source:
    entityParticipantTable.TryGet(identity) → Entity
    if no ProximityLipSyncComponent: find Mask_Mouth renderer, World.Add
    update SmoothedAmplitude, select pose, apply MaterialPropertyBlock
    ↓
Cleanup: remove component when participant leaves or renderer is null
```

### Rationale

- Mirrors proven `ProximityAudioPositionSystem` architecture.
- `ConcurrentDictionary` bridging avoids race conditions between LiveKit events and ECS.
- Retry-on-every-frame pattern handles cases where Entity doesn't exist yet at subscribe time.
- Separate system allows independent feature flag control and development velocity.

---

## Decision 4: Sprite Atlas and MaterialPropertyBlock

### Chosen: Reuse PR #7452 pattern

- Mouth atlas: `Mouth_Atlas.png` (1024×1024, 4×4 grid of 256px cells, 16 poses)
- Located at: `Explorer/Assets/DCL/VoiceChat/Proximity/Mouth_Atlas.png`
- Sliced into `Texture2DArray` at initialization time using `Graphics.Blit` + `ReadPixels` + `CopyTexture` (same as `AvatarPlugin.CreateMouthPhonemeTextureArrayAsync` in PR #7452)
- Applied per-renderer via static `MaterialPropertyBlock` (reused, no per-frame allocation)
- Clearing the property block (`SetPropertyBlock(null)`) reverts to default material

### Rationale

Proven in PR #7452 prototype. Does not modify the shared pool material, preventing texture corruption on other facial feature renderers (eyes, eyebrows).

---

## Decision 5: No Local Avatar Lip Sync

The local player's own avatar does not need lip sync because:
- Camera is first-person or third-person behind — own mouth is not visible
- Avoids complexity of accessing `MicrophoneRtcAudioSource` data separately
- Can be added later if camera perspective changes

---

## Decision 6: Feature Flag

Lip sync will be gated behind a feature flag in `FeaturesRegistry`. This allows:
- A/B testing between animation approaches
- Disabling the feature if performance issues arise on low-end hardware
- Independent rollout from other voice chat features

---

## Consequences

### Positive
- Immediate social presence improvement with Step 1 (hours of work)
- Each iteration is independently shippable
- Zero LiveKit SDK changes for MVP
- Reuses proven patterns from existing codebase

### Negative
- Step 1 (binary + random) looks "approximate" — not accurate lip sync
- Steps 2–4 require LiveKit SDK changes (though minimal)
- OVRLipSync (Step 4) introduces external native dependency
- Need to coordinate with PR #7452 to avoid MaterialPropertyBlock conflicts on Mask_Mouth

### Risks
- `IActiveSpeakers` update frequency (~4–5/sec) may feel laggy for Step 1 — mitigated by hold-time on poses and random animation continuation
- OVRLipSync licensing may restrict distribution — mitigated by keeping FFT as fallback option
- Assembly dependency between VoiceChat and AvatarRendering may require architectural compromise

---

## References

- PR #7452: https://github.com/decentraland/unity-explorer/pull/7452
- `LivekitAudioSource.cs`: `client-sdk-unity/Runtime/Scripts/Rooms/Streaming/Audio/LivekitAudioSource.cs`
- `ProximityVoiceChatManager.cs`: `Explorer/Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs`
- `ProximityAudioPositionSystem.cs`: `Explorer/Assets/DCL/VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs`
- `Participant.cs`: `client-sdk-unity/Runtime/Scripts/Rooms/Participants/Participant.cs`
- `DefaultActiveSpeakers.cs`: `client-sdk-unity/Runtime/Scripts/Rooms/ActiveSpeakers/DefaultActiveSpeakers.cs`
- `VoiceChatParticipantsStateService.cs`: `Explorer/Assets/DCL/VoiceChat/VoiceChatParticipantsStateService.cs`
- OVRLipSync SDK: https://developer.oculus.com/documentation/unity/audio-ovrlipsync-unity/
