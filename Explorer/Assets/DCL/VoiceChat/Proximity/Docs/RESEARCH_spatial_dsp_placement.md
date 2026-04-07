# Research: Spatial DSP — Audio Thread vs ECS

## Контекст

Вопрос: где вычислять gainL/gainR и коэффициенты фильтров для spatial audio panning?

- **Вариант A:** Оставить в `LivekitAudioSource` / `SpatialAudioDSP` на Audio Thread (текущий подход)
- **Вариант B:** Вынести вычисление коэффициентов в ECS (`ProximityAudioPositionSystem`), потенциально с Burst/Jobs

Аргументы за вариант B:
1. Cache locality (ECS, SoA layout)
2. Много игроков (50-200)
3. Логика может усложниться (Head Shadow с ellipsoidal/snowman model)

---

## Текущий data flow

```
ECS (main thread)                    Audio Thread (OnAudioFilterRead)
+--------------------------+         +------------------------------+
| ProximityAudioPosition   |         | LivekitAudioSource           |
| System                   |         |                              |
|                          | azimuth | 1. ReadAudio(data)           |
| CalculateSpatialAngles() +-------->| 2. Compute gainL/gainR       |
| -> azimuth, elevation    |elevation| 3. Per-sample: data *= gain  |
+--------------------------+         +------------------------------+
```

---

## Что НЕЛЬЗЯ вынести из Audio Thread

Stateful IIR фильтры в `SpatialAudioDSP` (ветка `feat/mono-spatial-audio`) содержат memory state (`z^-1`, `z^-2`), обновляемый sample-by-sample последовательно:

```csharp
// Biquad -- каждый sample зависит от предыдущего
float y = b0 * input + z1;
z1 = b1 * input - a1 * y + z2;  // зависимость от y
z2 = b2 * input - a2 * y;       // зависимость от y
```

Это касается: HeadShadow (cascade LPF, biquad, MultiBand3, DualShelf), HRTF (peaking EQ), ITD (delay line).

**Per-sample application обязана остаться на Audio Thread, где живут данные.**

---

## Что МОЖНО вынести в ECS

Вычисление коэффициентов — чистая математика без состояния, вызывается один раз на буфер (~21ms при 48kHz/1024):

| Что | Текущее место | FLOPs/source |
|-----|---------------|-------------|
| ILD: `gainL`, `gainR` | `ProcessILD` (audio thread) | ~8 (sin, cos, mul) |
| ITD: `delayL`, `delayR` | `ProcessITD` (audio thread) | ~15 (Woodworth model) |
| HeadShadow: biquad/shelf коэффициенты | `ProcessHeadShadow` (audio thread) | ~50-100 (bilinear transform) |
| HRTF: peaking EQ коэффициенты | `ProcessHRTF` (audio thread) | ~30 (ComputePeakingEQ) |
| **Итого коэффициенты** | | **~100-150 FLOPs/source** |

vs. per-sample application: ~10-25 FLOPs x 1024 samples = **10K-25K FLOPs/source**

---

## Числа для 200 игроков

| Операция | На Audio Thread (сейчас) | Burst/ECS (если вынести) |
|----------|--------------------------|--------------------------|
| Коэффициенты (200 src) | 200 x 150 = **30K FLOPs** | ~4-8K (SIMD vectorization) |
| Per-sample (200 src) | 200 x 25K = **5M FLOPs** | **5M FLOPs** (не вынести) |
| **Доля коэффициентов** | **0.6%** общей работы | |

---

## Анализ трех аргументов

### 1. Cache locality (ECS)

Частично верно. Для коэффициентов это ~0.6% работы, выигрыш минимален.
Позиции аватаров (`CharacterTransform`, `AvatarBase.HeadAnchorPoint`) уже в ECS и итерируются батчем — это уже используется правильно.

### 2. 50-200 игроков

Самый сильный аргумент, но не там где ожидается. Проблема не в вычислении коэффициентов, а в том что **200 отдельных `OnAudioFilterRead` callbacks** вызываются Unity на Audio Thread последовательно. Каждый callback = virtual dispatch + managed-to-native transition. Вынос коэффициентов это не решает.

### 3. Head Shadow с ellipsoidal/snowman model

Реальный выигрыш. Ellipsoidal model меняет `shadowAmount` и ITD delay — mapping из game-space в акустические параметры. Вместо `abs(sin(azimuth))` будет ray-ellipsoid intersection (~50-100 FLOPs). С Burst для 200 источников это заметно.

Эти вычисления уже логически относятся к ECS (зависят от позиции головы, не от audio data).

---

## Рекомендация: Hybrid подход

```
ECS (main thread, Burst)              Audio Thread
+--------------------------+          +--------------------------+
| ProximityAudioPosition   |          | SpatialAudioDSP          |
| System                   |          |                          |
|                          | struct   | ApplyILD(gainL, gainR)   |
| 1. azimuth, elevation    +--------->| ApplyITD(delayL, delayR) |
| 2. Head model (snowman)  | Spatial  | ApplyHeadShadow(coeffs)  |
| 3. Compute ALL coeffs:   | Coeffs   | ApplyHRTF(coeffs)        |
|    gainL/R, delays,      |          |                          |
|    biquad b0/b1/a1...    |          | = только per-sample      |
|    HRTF notch coeffs     |          |   filter application     |
+--------------------------+          +--------------------------+
```

### Interface

Заменяет `SetSpatialAngles(float, float)`:

```csharp
struct SpatialCoefficients  // blittable, thread-safe copy
{
    // ILD
    public float GainL, GainR;

    // ITD
    public float DelayL, DelayR;

    // HeadShadow (DualShelf -- 11 FLOP/sample, лучший вариант)
    public float DsS1B0, DsS1B1, DsS1A1;
    public float DsS2B0, DsS2B1, DsS2A1;
    public float DsBaseGain;
    public bool ShadowOnLeft;

    // HRTF
    public float HrtfB0, HrtfB1, HrtfB2, HrtfA1, HrtfA2;
    public float Hrtf2B0, Hrtf2B1, Hrtf2B2, Hrtf2A1, Hrtf2A2;
    public bool HasSecondaryNotch;
}
```

---

## Фазы имплементации

| Фаза | Что делать |
|------|-----------|
| **PR1 (сейчас)** | Оставить как есть. ECS считает azimuth/elevation, audio thread считает gain. Просто и работает. |
| **Переход на `feat/mono-spatial-audio`** | Вынести вычисление коэффициентов в ECS. `SpatialAudioDSP.Process()` принимает `SpatialCoefficients` вместо `LivekitAudioSource src`. Коэффициенты вычисляет `ProximityAudioPositionSystem` (или новая `ProximityAudioDSPCoefficientSystem`). |
| **Ellipsoidal/snowman model** | Burst/Jobs для batch вычисления shadowAmount и ITD delay по новой модели. The only part worth Burst-optimizing. |

---

## Главный bottleneck

При 200 игроках — не коэффициенты, а 200 x `OnAudioFilterRead` с per-sample loops.

Если это станет проблемой, решение — batch audio processing (один буфер, все источники), но это потребует переписать audio pipeline в LiveKit SDK.

---

## Файлы для справки

- Текущий простой panning: `LivekitAudioSource.cs` (ветка `feat/spatial-audio-simple-panning`) — `ApplySpatialPanning()`, ~30 строк DSP
- Экспериментальный полный DSP: `SpatialAudioDSP.cs` (ветка `feat/mono-spatial-audio`) — 469 строк, ITD/ILD/HeadShadow/HRTF pipeline
- ECS система позиционирования: `Assets/DCL/VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs`
- PR с экспериментами: `decentraland/client-sdk-unity#59`
