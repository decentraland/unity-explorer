# ADR: Spatial Audio Pipeline для Proximity Voice Chat

**Status:** Implemented (ILD EqualPower/HeadShadow + ITD). Следующий шаг: Pinna HRTF  
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
                                                        3. Pipeline: ITD → ILD/HeadShadow → HRTF
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

[Header("Head Shadow Filter")]
public ShadowFilterOrder shadowFilterOrder; // OnePole6dB..FourPole24dB | Biquad12dB | MultiBand3
// + shadowCutoffHz, shadowStrength, biquadQ
// + crossoverLowMid, crossoverMidHigh, lowBandDb, midBandDb, highBandDb (MultiBand3)

[Header("ITD — Interaural Time Difference")]
public bool enableITD;
// + headRadius

[Header("HRTF — Pinna / Spectral Cues")]
public bool enableHRTF;
// + elevationInfluence
```

### Pipeline в OnAudioFilterRead

```
mono sample → [ITD delay] → (sampleL, sampleR) → [ILD gains + HeadShadow filter] → [HRTF notch] → data[]
```

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

## HRTF: стратегия итеративной реализации

### Что такое HRTF

**Head-Related Transfer Function** — передаточная функция, описывающая как звук трансформируется на пути от источника до барабанной перепонки с учётом дифракции на голове, плечах и ушной раковине (pinna). HRTF — "аудио-отпечаток" формы ушей.

### Зачем HRTF

ILD и ITD работают в горизонтальной плоскости. Но существует "cone of confusion" — множество точек, дающих одинаковые ILD/ITD (конус вокруг межушной оси). Pinna решает это: её складки создают elevation-зависимые спектральные провалы (notches) на 6-10 kHz. Мозг по ним определяет верх/низ/перед/зад.

### Варианты реализации (без внешних плагинов)

| Вариант | Качество | CPU (150 src) | Pre-baked | Сложность |
|---------|----------|---------------|-----------|-----------|
| C1: 1 parametric notch | Базовая вертикаль | ~0.25 ms | Нет | Простая |
| C2: 2 parametric notch | Хорошая вертикаль | ~0.5 ms | Нет | Простая |
| D: Short FIR 64-tap (HRIR) | Полная 3D | ~9.8 ms | Да (HRIR ~125 KB) | Средняя |
| SOFA HRTF | Полная 3D | ~9.8 ms | Да + парсер | Высокая, overkill |

### Решение: итеративный путь C1 → C2 → (может быть D)

**C1: 1 parametric notch** — biquad notch filter, частота от elevation. Notch ~6-10 kHz.  
**C2: 2 parametric notch** — primary + secondary notch (~1.6× частоты). Более объёмная вертикаль.  
**D: Short FIR** — реальная HRIR (MIT KEMAR, public domain). Возможно, если параметрика недостаточно. При 150 источниках ~9.8 ms (~47% audio budget) — допустимо при 50, на грани при 150.  
**SOFA** — отмечен, но не планируется (избыточная сложность для voice chat).

### Elevation в уже реализованных эффектах

| Эффект | Как elevation влияет | В коде |
|--------|---------------------|--------|
| ILD EqualPower | Уменьшается (голова не затеняет сверху) | `pan * cos(el)` |
| ITD | Уменьшается (путь до ушей выравнивается) | `effectiveAz * cos(el)` |
| HeadShadow | Уменьшается (нет экранирования сверху) | `shadowAmount * cos(el)` |
| Pinna HRTF | **Основной cue** для вертикальной локализации | notch freq от elevation |

### Referенции

- Blauert, J. (1997). Spatial Hearing — ILD measurements по частотам
- Woodworth & Schlosberg (1954) — ITD = r(θ + sin θ)/c
- Algazi et al. (2001) — средний радиус головы 0.0875 m (CIPIC database)
- Hebrank & Wright (1974) — pinna spectral cues для вертикальной локализации
- Van Wanrooij & Van Opstal (2004) — head shadow как доминирующий ILD cue
- Robert Bristow-Johnson — Audio EQ Cookbook (biquad формулы)

---

## Camera-Relative Panning

Требование: в 3rd person пан относительно камеры. При повороте аватара на 180° при неподвижной камере — звук остаётся "слева в кадре".

**Текущая реализация:** `ProximityPanCalculator` использует `AudioListener.transform`. Если listener на камере, panning уже camera-relative.

**Будущее:** Позиция из головы аватара (distance rolloff), ориентация из камеры (panning).

---

## Завершённые итерации

### Итерация 1: Mono + Zero Pan — ЗАВЕРШЕНА
`LivekitAudioSource.New(mono: true)` — читает стерео, извлекает L канал, дублирует L=R.

### Итерация 2: Angular Panning (базовый ILD) — ЗАВЕРШЕНА
`Pan` property (-1..+1) + equal-power pan law. Резкий скачок при проходе сзади.

### Итерация 3: Fix ILD + 3D углы — ЗАВЕРШЕНА
`sin(azimuth) * cos(elevation)` — плавный переход, поддержка elevation.

### Итерация A: ITD — ЗАВЕРШЕНА
Delay line 256 сэмплов, формула Woodworth, линейная интерполяция.

### Итерация B: Рефакторинг + HeadShadow — ЗАВЕРШЕНА
- Композиция: `ILDMode` enum + `enableITD` bool + `enableHRTF` bool
- HeadShadow: 6 режимов фильтрации (OnePole → FourPole, Biquad, MultiBand3)
- MultiBand3: 3-полосный кроссовер с per-band gain по измеренной кривой
- Tooltips с физическими референсами на все параметры
