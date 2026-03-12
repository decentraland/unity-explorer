# Lip Sync Investigation Summary

> Voice-driven mouth animation for proximity voice chat avatars.

---

## 1. Problem Statement

Avatars in Decentraland proximity voice chat are visually static while players speak. There is no visual feedback that an avatar is talking — no mouth movement, no facial animation. This significantly reduces social presence and non-verbal communication in the virtual world.

**Goal:** Animate avatar mouth sprites in response to voice chat audio, selecting from a 16-pose sprite atlas (1024×1024, 4×4 grid of 256px cells).

---

## 2. Existing Infrastructure

### 2.1 Voice Chat Audio Pipeline

```
LiveKit Server
    ↓ (WebRTC)
FFI → AudioStreamEvent (FrameReceived) → NativeAudioBufferResampleTee
    ↓
LivekitAudioSource.OnAudioFilterRead()
    ↓
AudioStream.ReadAudio(data, channels, sampleRate)   ← raw PCM mono here
    ↓
[Spatialization Pipeline: ITD → ILD → HeadShadow → HRTF]
    ↓
Unity AudioSource output (speakers)
```

**Key file:** `client-sdk-unity/Runtime/Scripts/Rooms/Streaming/Audio/LivekitAudioSource.cs`

The `OnAudioFilterRead` callback runs on the **audio thread** (~46 calls/sec at 48kHz/1024 samples). After `ReadAudio()` the buffer contains clean mono PCM data. Spatialization is applied afterwards and modifies amplitude per-ear — unsuitable for lip sync analysis.

### 2.2 Participant Speaking Status

LiveKit provides binary speaking detection via `IActiveSpeakers`:

```
Room.ActiveSpeakersChanged event
    ↓
DefaultActiveSpeakers.UpdateCurrentActives(IEnumerable<string> identities)
    ↓
VoiceChatParticipantsStateService.OnActiveSpeakersUpdated()
    ↓
participantState.IsSpeaking.Value = true/false
```

**Key finding:** `Participant.AudioLevel` (float) exists in the LiveKit SDK but is **dead code** — the property has `private set` and is never assigned anywhere. The `ActiveSpeakersChanged` event only carries participant identity strings, not audio level values.

**What's available without LiveKit SDK changes:**
- `IActiveSpeakers` — `IReadOnlyCollection<string>` of currently speaking participant identities
- `Participant.Speaking` — bool, also dead code (never set)
- Binary only: who is speaking, not how loud

### 2.3 Entity–Participant Mapping

`ProximityAudioPositionSystem` already establishes the mapping chain:

```
activeAudioSources[identity] → entityParticipantTable.TryGet(identity) → Entity
    → World.Add(entity, ProximityAudioSourceComponent)
    → SyncPositions: AudioSource.position = AvatarBase.HeadAnchorPoint
```

The same pattern can be reused for lip sync: iterate a shared dictionary, resolve identity to entity, update/add a lip sync ECS component.

### 2.4 Avatar Mouth Renderer

Mouth is rendered via a `Renderer` named `"Mask_Mouth"` found in `AvatarShapeComponent.InstantiatedWearables`. The PR #7452 prototype uses `MaterialPropertyBlock` to override the texture array index on this renderer without touching the shared pool material:

```csharp
static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();
s_Mpb.Clear();
s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, phonemeTextureArray);
s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, phonemeIndex);
mouthRenderer.SetPropertyBlock(s_Mpb);
```

Clearing the property block (`SetPropertyBlock(null)`) reverts to the default material texture.

### 2.5 Sprite Atlas Layout

`Mouth_Atlas.png` — 1024×1024, 4×4 grid, 16 poses (256×256 each):

```
Row 0: [0]  Idle/teeth    [1]  O-round      [2]  Closed flat   [3]  Teeth show
Row 1: [4]  Open+tongue   [5]  Small O      [6]  Smile+tongue  [7]  Round O
Row 2: [8]  Oval open     [9]  Oval+tongue   [10] Teeth grid    [11] Small open
Row 3: [12] Wide smile    [13] Teeth+tongue  [14] Round         [15] Side
```

For voice-driven animation, poses can be grouped by "openness":
- **Idle (closed):** index 2
- **Slightly open (quiet speech):** indices 5, 8, 11
- **Medium open (normal speech):** indices 1, 3, 4, 6, 9, 10
- **Wide open (loud speech):** indices 0, 7

The atlas must be sliced into a `Texture2DArray` at load time (same approach as PR #7452: `Graphics.Blit` per cell → `ReadPixels` → `CopyTexture`).

---

## 3. Approaches to Audio Analysis

### 3.1 Amplitude (RMS)

**How it works:** Compute root-mean-square of the PCM buffer. Map the resulting 0..1 value to mouth openness via thresholds.

```csharp
float sum = 0f;
for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
float rms = Mathf.Sqrt(sum / data.Length);
float amplitude = Mathf.Clamp01(rms * sensitivity);
```

| Pros | Cons |
|------|------|
| Trivial to implement (~5 lines) | Cannot distinguish vowels from consonants |
| Zero allocations | "AAAAAA" and "SSSSSS" look the same if same volume |
| Works on any audio | Mouth just "flaps" open/closed |
| Negligible CPU cost | No phoneme/viseme information |

**Performance:** ~0.01ms per buffer (2048 floats). Negligible even for 50+ sources.

**Best suited for:** MVP, quick visual feedback, stylized art where precision doesn't matter.

### 3.2 FFT Frequency Band Analysis

**How it works:** Apply FFT (or `AudioSource.GetSpectrumData`) to decompose audio into frequency bands. Map energy distribution to approximate mouth shapes:
- Low frequencies (200–800 Hz) dominant → open vowels (A, O)
- Mid frequencies (800–2500 Hz) dominant → closed vowels (E, I, U)
- High frequencies (2500–8000 Hz) dominant → sibilants (S, SH, F)

| Pros | Cons |
|------|------|
| Can approximate vowel vs consonant | Requires manual threshold tuning per voice |
| No external dependencies | Results vary significantly between speakers |
| Better than pure amplitude | Formant frequencies overlap between phonemes |
| Moderate implementation effort | Not robust enough for accurate visemes |

**Performance:** FFT on 1024 samples ≈ 0.05–0.1ms. Manageable for 10+ sources but not free.

**Implementation notes:**
- `AudioSource.GetSpectrumData` runs on main thread but reads post-spatialization data (wrong for lip sync)
- Manual FFT in `OnAudioFilterRead` reads pre-spatialization data (correct) but needs a scratch buffer
- Divide spectrum into 3–5 bands, compute energy per band, use ratios to select pose
- Significant tuning effort; quality plateau is lower than OVRLipSync

**Best suited for:** Intermediate step if OVRLipSync cannot be used (licensing, platform constraints). Provides better visual variety than amplitude alone at moderate engineering cost.

### 3.3 Viseme Detection (OVRLipSync)

**How it works:** Feed PCM buffer to Meta's Oculus Lipsync SDK. It returns an array of 15 viseme weights corresponding to standard mouth poses (Sil, PP, FF, TH, DD, KK, CH, SS, NN, RR, AA, E, I, O, U).

```csharp
OVRLipSync.ProcessFrame(context, buffer, frame);
float[] visemes = frame.Visemes; // 15 weights, sum to ~1.0
```

| Pros | Cons |
|------|------|
| Best quality — purpose-built for this task | External native plugin dependency |
| Real-time, optimized DSP (not heavy ML) | One "context" per audio source (~memory) |
| 15 visemes map cleanly to 12–16 sprite poses | Licensing: free but Meta/Oculus SDK |
| Language-independent | Need to pool contexts for 50+ avatars |

**Performance per context:** ~0.1–0.3ms per `ProcessFrame` call.

**Scaling strategy:**
- Pool of N contexts (recommended N=8)
- Assign contexts only to actively speaking avatars
- Silent avatars get viseme Sil (idle) for free
- Typical scenario: 2–5 simultaneous speakers → well within budget

**Best suited for:** Final production quality. Maximum visual fidelity with reasonable CPU cost.

### 3.4 Comparison Matrix

| Criterion | Amplitude | FFT Bands | OVRLipSync |
|-----------|-----------|-----------|------------|
| Implementation effort | Hours | Days | 1–2 days |
| Accuracy | Low | Medium | High |
| CPU per source per frame | ~0.01ms | ~0.05–0.1ms | ~0.1–0.3ms |
| 50 sources simultaneously | Trivial | Manageable | Need pooling |
| External dependencies | None | None | OVRLipSync native plugin |
| Distinguishes A/O/E? | No | Roughly | Yes |
| Distinguishes vowel/consonant? | No | Roughly | Yes |
| Quality ceiling | "Flapping" | "Okay" | "Good to great" |

---

## 4. Data Source Options

### 4.1 IActiveSpeakers (Binary — No LiveKit Changes)

- **What:** List of participant identities currently speaking
- **Granularity:** ~4–5 updates/sec (LiveKit server-side VAD interval)
- **Latency:** Signaling channel, ~200–500ms from actual speech
- **Thread safety:** Already on main thread via `OnActiveSpeakersUpdated`
- **Usable for:** Binary open/close, random animation trigger

### 4.2 RMS in OnAudioFilterRead (Float — Minimal LiveKit Change)

- **What:** Pre-spatialization amplitude as `volatile float` on `LivekitAudioSource`
- **Granularity:** Every audio buffer (~21ms at 48kHz/1024 samples)
- **Latency:** Real-time (audio thread)
- **Thread safety:** `Interlocked.Exchange` for write, `Interlocked.CompareExchange` or `volatile` for read
- **Change scope:** ~5 lines in `LivekitAudioSource.OnAudioFilterRead`, after `ReadAudio`, before spatialization:

```csharp
// Ideal insertion point in OnAudioFilterRead:
resource.Value.ReadAudio(data.AsSpan(), channels, sampleRate);

// >>> LIP SYNC: compute pre-spatialization amplitude
float sum = 0f;
for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
Interlocked.Exchange(ref _amplitude, Mathf.Sqrt(sum / data.Length));

bool spatialized = !bypassSpatialization && ...
```

- **Usable for:** Amplitude-driven animation, weighted random, FFT (if extended)

### 4.3 PCM Buffer for OVRLipSync

- **What:** Same PCM buffer from `OnAudioFilterRead`, passed to `OVRLipSync.ProcessFrame`
- **Additional concern:** OVRLipSync's `ProcessFrame` can be called from any thread, but the context is not thread-safe — one context per source, no sharing
- **Usable for:** Full viseme detection

---

## 5. Threading Model

```
AUDIO THREAD                          MAIN THREAD (ECS)
─────────────                         ─────────────────
OnAudioFilterRead()
  │
  ├─ ReadAudio(data)                  ProximityLipSyncSystem.Update(dt)
  │                                     │
  ├─ Compute RMS                        ├─ Read amplitude from LivekitAudioSource
  │   Interlocked.Exchange(             │   (volatile float, thread-safe)
  │     ref _amplitude, rms)            │
  │                                     ├─ entityParticipantTable.TryGet(identity)
  ├─ [Optional: OVRLipSync              │   → resolve Entity
  │   ProcessFrame(context, data)]      │
  │                                     ├─ Smooth amplitude (Lerp with deltaTime)
  ├─ Spatialization pipeline            │
  │   (ITD → ILD → HeadShadow → HRTF)  ├─ Select sprite index by algorithm
  │                                     │
  └─ Output to speakers                └─ Apply MaterialPropertyBlock
```

**Critical rule:** No ECS structural changes (Add/Remove) from the audio thread. All ECS writes happen in the system on the main thread.

---

## 6. Visual Quality Considerations

### 6.1 Smoothing

Raw amplitude fluctuates rapidly. Without smoothing, the mouth will jitter. Apply exponential smoothing on the main thread:

```csharp
smoothed = Mathf.Lerp(smoothed, target, smoothingFactor * deltaTime * 60f);
```

Recommended `smoothingFactor`: 0.15–0.25 for natural feel.

### 6.2 Hysteresis

Without hysteresis, the mouth flickers between two poses at the threshold boundary. Solution: different thresholds for "opening" and "closing":

```
Open threshold:  0.15  (mouth opens when amplitude rises above this)
Close threshold: 0.08  (mouth closes when amplitude drops below this)
```

### 6.3 Hold Time

Each pose should be held for a minimum duration to prevent sub-frame flickering. PR #7452 uses `PhonemeDuration = 0.1f` (100ms) — a good starting point for 10 fps visual update rate.

### 6.4 Distance Culling

Skip lip sync processing for avatars that are:
- Not visible (`AvatarShapeComponent.IsVisible == false`)
- Beyond a configurable distance (e.g., > 15m — at this distance, mouth detail is invisible)
- Mouth renderer is disabled

---

## 7. Relationship with PR #7452

PR #7452 (`feat/avatar-blink`) by @olavra introduces:
- `AvatarBlinkSystem` — random eye blink animation
- `AvatarMouthAnimationSystem` — **text-based** mouth animation (chat messages → character-to-phoneme mapping)
- `AvatarMouthTalkingComponent` — bridge from `NametagPlacementSystem` (chat bubble) to mouth animation

**Key difference:** PR #7452 animates mouth from **text** (chat messages). Our feature animates mouth from **voice audio**. These are fundamentally different input channels driving the same visual output.

**Conflict potential:** Both systems write `MaterialPropertyBlock` to the `Mask_Mouth` renderer. If both are active simultaneously, the last writer wins. Need to establish priority:
- **Voice lip sync takes priority** (real-time, more responsive)
- Text-based animation only runs when `IActiveSpeakers` does not include the participant
- Alternatively, unify into a single system with multiple input sources

**Reusable from PR #7452:**
- `FindMouthRenderer` pattern (searching `InstantiatedWearables` for `"Mask_Mouth"`)
- `MaterialPropertyBlock` application pattern (static `s_Mpb`, Clear/Set/Apply)
- Atlas slicing into `Texture2DArray` (`CreateMouthPhonemeTextureArrayAsync`)
- Wearable re-instantiation handling (`MouthRenderer == null` → re-find)
- Visibility suppression (`avatarShape.IsVisible` check)

---

## 8. Assembly Dependency Concern

The lip sync system needs access to:
- `AvatarShapeComponent`, `AvatarBase`, `InstantiatedWearables` → `DCL.AvatarRendering` assembly
- `ProximityAudioSourceComponent`, `IActiveSpeakers` access → `DCL.VoiceChat` / `LiveKit` assemblies
- `IReadOnlyEntityParticipantTable` → `DCL.Multiplayer` assembly
- `CharacterTransform` → `DCL.Character` assembly

The system may need to live in an assembly that references both `AvatarRendering` and `VoiceChat`, or use a bridge component pattern (like `AvatarMouthTalkingComponent` in PR #7452) to decouple them.

---

## 9. Key Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Local avatar lip sync | Not needed | Camera perspective doesn't show own mouth |
| Text-based mouth animation | Out of scope | Separate feature (PR #7452), not related to voice |
| Starting data source | `IActiveSpeakers` (binary) | Zero LiveKit changes, immediate visual feedback |
| Iterative progression | Binary → Amplitude → OVR | Each step builds on the previous, can ship at any point |
| OVRLipSync | Last iteration | External dependency, evaluate after simpler approaches work |
| FFT analysis | Optional intermediate | Higher effort than amplitude, lower quality than OVR; keep as fallback if OVR is not viable |
| Feature flag | Required | Toggle lip sync independently from voice chat |
| Sprite atlas | Reuse `Mouth_Atlas.png` (1024×1024, 4×4) | Already created, matches PR #7452 approach |

---

## 10. Open Questions

1. **Assembly placement:** Where should the lip sync ECS system live? Options: extend `ProximityAudioPositionSystem`, new system in VoiceChat assembly (needs AvatarRendering reference), or bridge component approach.
2. **PR #7452 coordination:** Will blink and text-based mouth merge before or after voice lip sync? Need to avoid conflicting `MaterialPropertyBlock` writes.
3. **Atlas pose mapping refinement:** Current grouping (idle/slight/medium/wide) is a first pass. May need adjustment after visual testing.
4. **OVRLipSync licensing:** Confirm Meta SDK license allows distribution in non-Oculus builds of Decentraland.
5. **`Participant.AudioLevel` revival:** Should we fix the dead code in the LiveKit SDK to populate `AudioLevel` from the Rust FFI layer? This would give amplitude without needing to compute RMS in `OnAudioFilterRead`, but requires deeper FFI work.
