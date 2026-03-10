# ADR: Spatial Audio Pipeline для Proximity Voice Chat

**Status:** Implemented (ILD EqualPower/HeadShadow + ITD + Pinna HRTF). Опционально: Short FIR (итерация D)  
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
                                                        2. Extract mono → monoBuffer
                                                        3. Multi-pass pipeline:
                                                           [ITD] → [ILD] → [HeadShadow] → [HRTF]
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

## Архитектура переключателей (композиция)

Независимые переключатели вместо монолитного enum. Каждый эффект включается отдельно, любая комбинация допустима.

### Inspector layout

```csharp
[Header("ILD — Interaural Level Difference")]
public ILDMode ildMode;                     // None | EqualPower | HeadShadow
// + ildStrength

[Header("Head Shadow Filter")]
public ShadowFilterOrder shadowFilterOrder; // OnePole6dB..FourPole24dB | Biquad12dB | MultiBand3
// + shadowCutoffHz, shadowStrength, biquadQ
// + crossoverLowMid, crossoverMidHigh, lowBandDb, midBandDb, highBandDb (MultiBand3)

[Header("ITD — Interaural Time Difference")]
public bool enableITD;
// + headRadius

[Header("HRTF — Pinna / Spectral Cues")]
public bool enableHRTF;
// + elevationInfluence, pinnaNotchFreq, pinnaNotchQ, pinnaNotchDepthDb

[Header("HRTF — Secondary Notch (C2)")]
// + pinnaSecondaryRatio, pinnaSecondaryStrength (0 = only primary notch)
```

### Multi-pass pipeline

Pipeline разбит на **отдельные проходы** (не interleaved) — для корректного профилирования через `ProfilerMarker`:

```
Extract mono → monoBuffer[]
    ↓
[LiveKit.Spatial.ITD]        — delay line, Woodworth, linear interpolation
    ↓
[LiveKit.Spatial.ILD]        — equal-power gains (cos/sin)
    ↓
[LiveKit.Spatial.HeadShadow] — LPF/biquad/multiband на контралатеральном ухе
    ↓
[LiveKit.Spatial.HRTF]       — peaking EQ notch(es), elevation-dependent
    ↓
interleaved data[]
```

Каждая стадия — отдельный цикл по `samplesPerChannel`. При типичных буферах (1024 samples) данные помещаются в L1 cache, overhead от нескольких проходов минимален.

---

## HeadShadow: режимы фильтрации

Реальный head shadow при 90° азимута: <500 Hz: ~0-2 dB, 1 kHz: ~5 dB, 2 kHz: ~10 dB, 4 kHz: ~15 dB, 8 kHz: ~20 dB.

| Режим | Спад | Описание |
|-------|------|----------|
| OnePole6dB | 6 dB/oct | Мягкий, subtle |
| TwoPole12dB | 12 dB/oct | Дефолт, ближе всего к реальному ~8-10 dB/oct |
| ThreePole18dB | 18 dB/oct | Усиленный |
| FourPole24dB | 24 dB/oct | Агрессивный |
| Biquad12dB | 12 dB/oct + Q | С настраиваемым резонансом |
| MultiBand3 | 3-band | Точнее всего: per-band gain в dB по измеренной кривой |

**MultiBand3** — 3-полосный кроссовер (LPF biquad + HPF biquad, mid = complementary). Gains интерполируются по `shadowAmount = |sin(az)| * strength * cos(el)`. Дефолты: low=-2 dB, mid=-10 dB, high=-20 dB — точно по измерениям (Blauert, 1997).

---

## Pinna HRTF: реализация

### Что такое HRTF

**Head-Related Transfer Function** — передаточная функция, описывающая как звук трансформируется на пути от источника до барабанной перепонки с учётом дифракции на голове, плечах и ушной раковине (pinna). HRTF — "аудио-отпечаток" формы ушей.

### Зачем HRTF

ILD и ITD работают в горизонтальной плоскости. Но существует "cone of confusion" — множество точек, дающих одинаковые ILD/ITD (конус вокруг межушной оси). Pinna решает это: её складки создают elevation-зависимые спектральные провалы (notches) на 6-10 kHz. Мозг по ним определяет верх/низ/перед/зад.

### Реализованная параметрика

**Primary notch (C1):** Peaking EQ biquad с отрицательным gain (Bristow-Johnson Audio EQ Cookbook). Частота зависит от elevation: `pinnaNotchFreq × (1 ± 40% × elevationInfluence)`. Дефолт: 7000 Hz, Q=4, depth=-9 dB.

**Secondary notch (C2):** На `primaryFreq × pinnaSecondaryRatio` (дефолт 1.6×). Глубина = `primaryDepth × pinnaSecondaryStrength` (дефолт 0.6 → -5.4 dB). Отключается при `pinnaSecondaryStrength = 0`.

Оба notch применяются к **обоим ушам** (pinna фильтрует обе стороны, elevation одинаков). L/R разница обеспечивается ILD/ITD/HeadShadow.

### Варианты реализации (сравнение)

| Вариант | Качество | CPU (150 src) | Pre-baked | Статус |
|---------|----------|---------------|-----------|--------|
| C1: 1 parametric notch | Базовая вертикаль | ~0.25 ms | Нет | **ЗАВЕРШЁН** |
| C2: 2 parametric notch | Хорошая вертикаль | ~0.5 ms | Нет | **ЗАВЕРШЁН** |
| D: Short FIR 64-tap (HRIR) | Полная 3D | ~9.8 ms | Да (HRIR ~125 KB) | Опционально |
| SOFA HRTF | Полная 3D | ~9.8 ms | Да + парсер | Не планируется |

### Elevation в реализованных эффектах

| Эффект | Как elevation влияет | В коде |
|--------|---------------------|--------|
| ILD EqualPower | Уменьшается (голова не затеняет сверху) | `pan * cos(el)` |
| ITD | Уменьшается (путь до ушей выравнивается) | `effectiveAz * cos(el)` |
| HeadShadow | Уменьшается (нет экранирования сверху) | `shadowAmount * cos(el)` |
| Pinna HRTF | **Основной cue** для вертикальной локализации | notch freq от elevation |

---

## Профилирование

5 `ProfilerMarker` для мониторинга в Unity Profiler:

| Маркер | Что измеряет |
|--------|-------------|
| `LiveKit.Spatial` | Весь pipeline (обёртка) |
| `LiveKit.Spatial.ITD` | Delay line + Woodworth |
| `LiveKit.Spatial.ILD` | Equal-power gains |
| `LiveKit.Spatial.HeadShadow` | LPF/biquad/multiband фильтрация |
| `LiveKit.Spatial.HRTF` | Peaking EQ notch(es) |

Маркеры вызываются 1 раз за audio buffer (не per-sample) благодаря multi-pass архитектуре.

---

## Референции

- Blauert, J. (1997). Spatial Hearing — ILD measurements по частотам
- Woodworth & Schlosberg (1954) — ITD = r(θ + sin θ)/c
- Algazi et al. (2001) — средний радиус головы 0.0875 m (CIPIC database)
- Hebrank & Wright (1974) — pinna spectral cues для вертикальной локализации
- Lopez-Poveda & Meddis (1996) — pinna notch depths 6-15 dB, harmonic ratios
- Van Wanrooij & Van Opstal (2004) — head shadow как доминирующий ILD cue
- Robert Bristow-Johnson — Audio EQ Cookbook (biquad / peaking EQ формулы)

---

## Camera-Relative Panning

Требование: в 3rd person пан относительно камеры. При повороте аватара на 180° при неподвижной камере — звук остаётся "слева в кадре".

**Текущая реализация:** `ProximityPanCalculator` использует `AudioListener.transform`. Если listener на камере, panning уже camera-relative.

**Будущее:** Позиция из головы аватара (distance rolloff), ориентация из камеры (panning).

---

## Завершённые итерации

### Итерация 1: Mono + Zero Pan
`LivekitAudioSource.New(mono: true)` — читает стерео, извлекает L канал, дублирует L=R.

### Итерация 2: Angular Panning (базовый ILD)
`Pan` property (-1..+1) + equal-power pan law. Резкий скачок при проходе сзади.

### Итерация 3: Fix ILD + 3D углы
`sin(azimuth) * cos(elevation)` — плавный переход, поддержка elevation.

### Итерация A: ITD
Delay line 256 сэмплов, формула Woodworth, линейная интерполяция.

### Итерация B: Рефакторинг + HeadShadow
- Композиция: `ILDMode` enum + `enableITD` bool + `enableHRTF` bool
- HeadShadow: 6 режимов фильтрации (OnePole → FourPole, Biquad, MultiBand3)
- MultiBand3: 3-полосный кроссовер с per-band gain по измеренной кривой
- Tooltips с физическими референсами на все параметры

### Итерация C: Pinna HRTF (C1 + C2)
- C1: Primary peaking EQ notch (elevation → freq 4-12 kHz, Q=4, depth=-9 dB)
- C2: Secondary notch (×1.6 частоты, depth × 0.6), отключаемый через `pinnaSecondaryStrength`
- Pipeline реструктурирован в отдельные проходы (multi-pass)
- ProfilerMarkers для каждой стадии (ITD, ILD, HeadShadow, HRTF)
- Кэшированный `monoBuffer` для allocation-free multi-pass

---

## Опциональная итерация D: Short FIR 64-tap (HRIR)

Если параметрических notch недостаточно — convolution с реальными HRIR (MIT KEMAR, public domain). ~125 KB данных, ~65 µs/source. Решение принимается после субъективной оценки C1/C2.

**SOFA HRTF** — отмечен, не планируется (избыточная сложность).

---

## Файлы проекта

### SDK (LiveKit fork)

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | `ILDMode`/`ShadowFilterOrder` enum, `enableITD`/`enableHRTF`, multi-pass pipeline, ProfilerMarkers, delay line, cascade/biquad/multiband HeadShadow, peaking EQ notch (primary + secondary) |

### Unity project

| Файл | Изменения |
|------|-----------|
| `ProximityPanCalculator.cs` | Вычисление azimuth + elevation |
| `ProximityVoiceChatManager.cs` | `New(mono: spatial)` → задать `ildMode` |
| `manifest.json` | `file:` ссылка на локальный SDK (dev) |

---

## Финализация (после всех итераций)

1. Push SDK на GitHub
2. Переключить `manifest.json` на Git URL
3. Оценить перенос `ProximityPanCalculator` в ECS для производительности (50+ участников)
4. Оценить FFI mono оптимизацию (чтение 1 канала из нативного слоя)
