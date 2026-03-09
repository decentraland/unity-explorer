# ADR: Spatial Audio Pipeline для Proximity Voice Chat

**Status:** Implemented (ILD базовый). Итеративное развитие: ILD fix → +ITD → ITD+ILD → Parametric HRTF  
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
  - InverseTransformDirection                         .spatializationMode = enum
  - azimuth = Atan2(local.x, local.z)
  - elevation = Atan2(local.y, horizontalDist)        OnAudioFilterRead:
  - livekitAudioSource.SetSpatialAngles(az, el)         1. ReadAudio(data, channels=2)
                                                        2. Extract mono (left channel)
                                                        3. Apply selected algorithm
```

### Разделение ответственности

- **SDK** — алгоритмы спатиализации, переключаемые enum в Inspector. Project-agnostic: принимает углы, не знает про камеру/listener
- **Unity project** — расчёт углов из пространственного контекста (AudioListener → source)

### API: углы вместо pan

Передаём `azimuth` и `elevation` (радианы), а не `pan` (-1..+1), потому что:
- Игра 3D — игроки на разных высотах, elevation нужен
- ITD и HRTF требуют угол, а не pan value
- Каждый алгоритм сам решает как интерпретировать угол

### Ограничение: AudioStream.ReadAudio с channels=1

`ReadAudio(data, 1, sampleRate)` на audio thread вызывает crash (AudioStream пересоздание → `persistentDataPath`, main-thread-only). Читаем стерео и извлекаем моно клиентски.

---

## Стратегия итеративного развития алгоритмов

Инкрементальный путь с полной преемственностью. Каждая итерация добавляет эффект поверх предыдущих. Переключение через enum в Inspector для A/B сравнения.

### 1. ILD — Interaural Level Difference (текущий, требует fix)

Разница громкости между ушами. Equal-power pan law (cos/sin).

**Текущая проблема:** `Atan2(local.x, local.z) / (PI*0.5)` + clamp — резкий скачок pan с +1 на -1 при переходе источника через "сзади".

**Fix:** Использовать `sin(azimuth)` вместо clamp:
- front-right (45°): sin = 0.707 → правее
- right (90°): sin = 1.0 → полностью вправо
- back-right (135°): sin = 0.707 → правее (уже тише)
- back (180°): sin = 0.0 → центр
- Плавный переход, нет скачков

**3D:** `pan = sin(azimuth) * cos(elevation)` — при источнике сверху/снизу ILD уменьшается (оба уха одинаково).

**Параметры Inspector:** `ildStrength` (0..1)

### 2. + ITD — Interaural Time Difference

Задержка звука в дальнем ухе. Мозг использует ITD для локализации на низких частотах (<1.5kHz).

**Физика:** max ITD = headRadius / speedOfSound ≈ 0.0875m / 343m/s ≈ 0.255ms ≈ ~12 сэмплов при 48kHz.

**Реализация:** Кольцевой буфер (delay line) в OnAudioFilterRead. Задержка = `headRadius * (azimuth + sin(azimuth)) / (2 * speedOfSound)` (формула Вудворта для сферической головы).

**Параметры Inspector:** `headRadius` (0.05..0.15, default 0.0875)

**Buffer underrun?** Нет — delay line внутри OnAudioFilterRead, не PCMReaderCallback. Данные уже в буфере.

### 3. ITD + ILD (комбинация)

Оба эффекта вместе. ITD на низких частотах + ILD на высоких — дополняют друг друга.

**Параметры Inspector:** headRadius + ildStrength

### 4. + Parametric HRTF

Упрощённый HRTF без таблиц. Моделирует затенение головой (head shadow) через low-pass фильтр на дальнем ухе.

**Реализация:** One-pole low-pass filter на contralateral ear. Частота среза зависит от угла — чем больше угол, тем ниже cutoff (голова экранирует ВЧ).

**Параметры Inspector:**
- `shadowCutoffHz` (500..4000, default 1500) — минимальный cutoff при 90°
- `shadowStrength` (0..1) — сила эффекта
- `elevationInfluence` (0..1) — влияние elevation на фильтрацию

**Преемственность:** ITD (delay) + ILD (level) + head shadow (filter) = три канала пространственного восприятия. Не полный HRTF с PRTF-таблицами пинны, но значительно лучше чистого ILD.

### Сводная таблица

| Алгоритм | Что моделирует | CPU cost | Качество | Преемственность |
|---|---|---|---|---|
| ILD | Разница громкости L/R | Минимальный | Базовый pan | Основа для всех |
| ITD | Задержка дальнего уха | +delay line ~12 samples | Улучшенная локализация | Добавляется к ILD |
| ITD+ILD | Оба | = ITD + ILD | Хорошая локализация | Комбинация |
| Parametric HRTF | +Head shadow (ВЧ фильтр) | +one-pole filter | Заметно лучше | Добавляется к ITD+ILD |

### Enum для переключения

```csharp
public enum SpatializationMode
{
    None,            // Стерео passthrough (обратная совместимость)
    ILD,             // Equal-power pan (sin(azimuth) * cos(elevation))
    ITD,             // Только задержка (без разницы громкости — для теста)
    ITD_ILD,         // Задержка + громкость
    ParametricHRTF   // Задержка + громкость + head shadow filter
}
```

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

`Pan` property (-1..+1) + equal-power pan law. `ProximityPanCalculator` вычисляет pan через `Atan2` + clamp. Работает, но резкий скачок при проходе источника сзади.
