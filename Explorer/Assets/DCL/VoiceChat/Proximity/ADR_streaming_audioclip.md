# ADR: Spatial Audio Pipeline для Proximity Voice Chat

**Status:** Implemented (ILD EqualPower + ITD). Итеративное развитие: Mono → ILD → ILD fix → ITD → HeadShadow → Pinna HRTF  
**Date:** 2026-03-06  
**Authors:** Voice Chat team  
**Related:** Итерация 2 из ADR_proximity_voice_chat

---

## Context

В итерации 2 Proximity Voice Chat реализован 3D spatial audio через `LivekitAudioSource` из LiveKit SDK (`com.decentraland.livekit-sdk`, форк `decentraland/client-sdk-unity`). Компонент использует `OnAudioFilterRead` для инъекции аудио в Unity audio pipeline.

**Обнаруженные проблемы:**
1. `panStereo` (2D pan) не работает — звук одинаков в обоих каналах при любом значении pan
2. **Angular panning (left/right в 3D)** не работает — при `spatialBlend = 1` звук не смещается влево/вправо при перемещении источника вокруг слушателя
3. Distance rolloff (затухание с расстоянием) — **работает** корректно

---

## Analysis

### Точный DSP-пайплайн Unity (из исходного кода)

На основе анализа `AudioSource.cpp`, `SoundChannel.cpp`, `AudioCustomFilter.cpp` из исходного кода Unity (FMOD-based):

```
1.  AudioClip PCM / PCMReaderCallback
         ↓
2.  Pitch (FMOD Channel: setFrequency = pitch × dopplerPitch × baseFreq)
         ↓
3.  FMOD Head DSP: panStereo (setPan → matrix DSP в начале цепочки)     ← PRE-DSP
         ↓
    ═══ m_dryGroup ═══
4.  [Опц.] Spatializer Plugin (pre-effects, если spatializePostEffects=false)
         ↓
    ═══ m_wetGroup ═══
5.  Built-in Effects + OnAudioFilterRead                                  ← в порядке компонентов
         ↓
6.  [Опц.] Spatializer Plugin (post-effects, если spatializePostEffects=true)
         ↓
    ═══ Channel Output Mixing (FMOD) ═══
7.  Volume (setVolume = CachedRolloff × channelVol × ambientVol)          ← POST-DSP
8.  3D Angular Panning (set3DPanLevel × set3DAttributes × set3DSpread)    ← POST-DSP
         ↓
9.  Parent Group (AudioMixer / AudioManager + Listener Effects)
         +
    Reverb Zones (параллельный SEND, не insert)
```

### Почему panStereo и angular panning не работают

- `panStereo` = FMOD Head DSP (этап 3, PRE-DSP) → применяется к тишине (нет клипа), `OnAudioFilterRead` перезаписывает
- `Angular panning` = FMOD 3D mix matrix (этап 8, POST-DSP) → не панорамирует стерео источники (только моно)
- `OnAudioFilterRead` всегда работает в стерео (channels=2), LiveKit дублирует моно в L=R → FMOD видит стерео

### Что работает

- Distance rolloff (`setVolume`, POST-DSP, скалярный)
- Audio Effects (на m_wetGroup)
- Reverb Zones (параллельный SEND)

---

## Решение: Ручная спатиализация в OnAudioFilterRead

Поскольку FMOD не может панорамировать стерео-буфер из `OnAudioFilterRead`, реализуем спатиализацию вручную в `LivekitAudioSource` (LiveKit SDK fork). SDK получает 3D-углы от вызывающего кода и применяет алгоритм в `OnAudioFilterRead`.

### Архитектура

```
[Unity Project]                                    [LiveKit SDK fork]
ProximityPanCalculator (MonoBeh)          →        LivekitAudioSource
  - AudioListener transform                          .SetSpatialAngles(azimuth, elevation)
  - InverseTransformDirection                         .ildMode = EqualPower | HeadShadow
  - azimuth = Atan2(local.x, local.z)                .enableITD = true/false
  - elevation = Atan2(local.y, horizontalDist)        .enableHRTF = true/false
  - livekitAudioSource.SetSpatialAngles(az, el)
                                                      OnAudioFilterRead:
                                                        1. ReadAudio(data, channels=2)
                                                        2. Extract mono (left channel)
                                                        3. Pipeline: ITD → ILD → HRTF
```

### Разделение ответственности

- **SDK** — алгоритмы спатиализации, композиция через независимые переключатели. Project-agnostic: принимает углы, не знает про камеру/listener
- **Unity project** — расчёт углов из пространственного контекста (AudioListener → source)

### API: углы вместо pan

Передаём `azimuth` и `elevation` (радианы), а не `pan` (-1..+1), потому что:
- Игра 3D — игроки на разных высотах, elevation нужен
- ITD и HRTF требуют угол, а не pan value
- Каждый алгоритм сам решает как интерпретировать угол

### Ограничение: AudioStream.ReadAudio с channels=1

`ReadAudio(data, 1, sampleRate)` на audio thread вызывает crash (AudioStream пересоздание → `persistentDataPath`, main-thread-only). Читаем стерео и извлекаем моно клиентски.

---

## Архитектура переключателей (композиция вместо enum)

Вместо одного `SpatializationMode` enum с комбинаторным взрывом — **независимые переключатели**. Каждый эффект включается/выключается отдельно, любая комбинация допустима.

### Inspector layout

```csharp
[Header("ILD — Interaural Level Difference")]
public ILDMode ildMode = ILDMode.None;       // None | EqualPower | HeadShadow
[Range(0f, 1f)] public float ildStrength = 1f;
[Range(500f, 4000f)] public float shadowCutoffHz = 1500f;  // только HeadShadow
[Range(0f, 1f)] public float shadowStrength = 0.7f;        // только HeadShadow

[Header("ITD — Interaural Time Difference")]
public bool enableITD = false;
[Range(0.05f, 0.15f)] public float headRadius = 0.0875f;

[Header("HRTF — Pinna / Spectral Cues")]
public bool enableHRTF = false;
[Range(0f, 1f)] public float elevationInfluence = 0.5f;
```

### ILDMode enum

```csharp
public enum ILDMode
{
    None,         // Стерео passthrough (обратная совместимость)
    EqualPower,   // sin(azimuth) * cos(elevation) → equal-power pan law
    HeadShadow    // EqualPower + one-pole low-pass на дальнем ухе (Frequency-Dependent ILD)
}
```

### Преимущества композиции

- Любая комбинация без дублирования: `EqualPower`, `EqualPower + ITD`, `HeadShadow + ITD + HRTF`
- Нет комбинаторного взрыва в enum
- В Inspector сразу видно какие слои включены
- Итеративное добавление не ломает предыдущие переключатели

### Pipeline в OnAudioFilterRead

Эффекты применяются как пайплайн, каждый в своей функции:

```
mono sample → [ITD delay] → (sampleL, sampleR) → [ILD gains/shadow] → [HRTF notch] → interleaved data[]
```

---

## Стратегия итеративного развития алгоритмов

### 1. ILD EqualPower — ЗАВЕРШЁН

Разница громкости между ушами. `pan = sin(azimuth) * cos(elevation)`, equal-power pan law (cos/sin gains).

**Elevation:** `cos(elevation)` уменьшает ILD — источник сверху → оба уха одинаково (голова не затеняет).

### 2. ITD — ЗАВЕРШЁН

Задержка звука в дальнем ухе. Кольцевой буфер 256 сэмплов. Формула Woodworth для сферической головы.

**Elevation:** `effectiveAz *= cos(elevation)` — путь до обоих ушей выравнивается при источнике сверху.

### 3. ILD HeadShadow (Frequency-Dependent ILD) — СЛЕДУЮЩИЙ

Голова экранирует высокие частоты сильнее, чем низкие. Дальнее ухо получает one-pole low-pass filter.

**Физика:** `cutoff = lerp(20000, shadowCutoffHz, |sin(az)| * shadowStrength)`  
**Elevation:** `cutoff` увеличивается при elevation (голова меньше экранирует сверху).

### 4. Pinna HRTF (Spectral Cues) — БУДУЩИЙ

Ушная раковина создаёт notch-фильтры, зависящие от elevation. Это единственный cue для различения верх/низ и перед/зад (cone of confusion).

**Реализация:** Parametric biquad notch filter. Notch frequency зависит от elevation (~6-8 kHz при 0°, сдвигается с углом).

### Психоакустика и elevation

| Эффект | Что даёт elevation | В коде |
|--------|-------------------|--------|
| ILD | Уменьшается (голова не затеняет сверху) | `* cos(el)` |
| ITD | Уменьшается (путь до ушей выравнивается) | `effectiveAz * cos(el)` |
| Head Shadow | Уменьшается (нет экранирования сверху) | `cutoff` растёт |
| Pinna HRTF | **Основной cue** для вертикальной локализации | notch freq от elevation |

### Сводная таблица

| Эффект | Что моделирует | CPU cost | Inspector |
|--------|---------------|----------|-----------|
| ILD EqualPower | Разница громкости L/R | Минимальный | `ildMode`, `ildStrength` |
| ILD HeadShadow | + ВЧ фильтрация дальнего уха | +one-pole filter | + `shadowCutoffHz`, `shadowStrength` |
| ITD | Задержка дальнего уха | +delay line | `enableITD`, `headRadius` |
| Pinna HRTF | Notch от ушной раковины | +biquad filter | `enableHRTF`, `elevationInfluence` |

---

## Camera-Relative Panning

Требование: в 3rd person пан относительно камеры. При повороте аватара на 180° при неподвижной камере — звук остаётся "слева в кадре".

**Текущая реализация:** `ProximityPanCalculator` использует `AudioListener.transform`. Если listener на камере, panning уже camera-relative.

**Будущее:** Позиция из головы аватара (distance rolloff), ориентация из камеры (panning).

---

## Завершённые итерации

### Итерация 1: Mono + Zero Pan — ЗАВЕРШЕНА

`LivekitAudioSource.New(mono: true)` — читает стерео, извлекает L канал, дублирует L=R. Аудио чистое.

### Итерация 2: Angular Panning (базовый ILD) — ЗАВЕРШЕНА

`Pan` property (-1..+1) + equal-power pan law. Работает, но резкий скачок при проходе источника сзади.

### Итерация 3: Fix ILD + 3D углы — ЗАВЕРШЕНА

`sin(azimuth) * cos(elevation)` — плавный переход сзади, поддержка elevation.

### Итерация A: ITD — ЗАВЕРШЕНА

Delay line (256 сэмплов), формула Woodworth, линейная интерполяция. ITD и ITD+ILD режимы.
