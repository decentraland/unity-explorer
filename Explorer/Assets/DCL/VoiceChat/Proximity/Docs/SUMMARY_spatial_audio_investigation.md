# Summary: Исследование Spatial Audio Pipeline для Proximity Voice Chat

**Дата:** 2026-03-06  
**Участники:** Vitaly Popuzin, AI Assistant  
**Контекст:** Итерация 2 Proximity Voice Chat — проблемы с пространственным аудио

---

## Исходная проблема

При тестировании proximity voice chat обнаружено: `panStereo` не работает. При `spatialBlend = 0` и полном смещении pan влево/вправо — звук одинаков в обоих каналах. Обычный AudioSource с клипом работает корректно.

---

## Ход исследования

### Этап 1: Анализ LivekitAudioSource

`LivekitAudioSource` использует `OnAudioFilterRead` без клипа — полностью перезаписывает буфер данными из LiveKit. `AudioStream` захардкожен на 2 канала, моно-голос дублируется в L=R.

### Этап 2: Первоначальная гипотеза (ошибочная)

Предположили что весь Unity audio pipeline до `OnAudioFilterRead`. Создали `SpatialAudioStreamFeeder` (streaming AudioClip). Тестирование опровергло: distance rolloff работает (POST-DSP), а pan нет (PRE-DSP).

### Этап 3: Streaming AudioClip — buffer underrun

`PCMReaderCallback` асинхронен → артефакты (микро-паузы, рваный звук). `OnAudioFilterRead` синхронен и не имеет этой проблемы.

### Этап 4: Анализ исходного кода Unity

Изучены `AudioSource.cpp`, `SoundChannel.cpp`, `AudioCustomFilter.cpp`. Точный пайплайн:

- `panStereo` = Head DSP = PRE-DSP → к тишине → не работает
- `setVolume` (distance rolloff) = POST-DSP, скалярный → работает
- `3D Angular Panning` = POST-DSP, но FMOD не панорамирует стерео (только моно)

**Root cause:** `OnAudioFilterRead` на m_wetGroup всегда стерео. FMOD видит стерео → нет angular panning.

### Этап 5: Выбор подхода — ручная спатиализация в SDK

Рассмотрено 6 вариантов. Выбран: Mono Mode в `LivekitAudioSource` + ручное panning в `OnAudioFilterRead`. SDK project-agnostic: получает данные от вызывающего кода, применяет алгоритм.

### Этап 6: Архитектура — что передавать в SDK

Варианты: (A) pan, (B) angle, (C) позиции, (D) gains. Выбран (A) pan для начала. Позже пересмотрено в пользу углов (azimuth + elevation) — для 3D и продвинутых алгоритмов.

### Этап 7: Имплементация Итерации 1 (Mono + Zero Pan)

`LivekitAudioSource.New(mono: true)`. Crash при `ReadAudio(data, 1, sampleRate)` на audio thread — `persistentDataPath` main-thread-only. Fix: читаем стерео, извлекаем моно клиентски.

Результат: аудио чистое, без артефактов.

### Этап 8: Имплементация Итерации 2 (Базовый ILD)

`Pan` property (-1..+1) + equal-power cos/sin. `ProximityPanCalculator` — `InverseTransformDirection` + `Atan2` + clamp.

Результат: панорамирование работает, но **резкий скачок** pan при проходе источника сзади (clamp с +1 на -1).

### Этап 9: Обсуждение алгоритмов спатиализации

Рассмотренные алгоритмы по качеству:

1. **ILD EqualPower** — разница громкости L/R. Простой, но грубый.
2. **ILD HeadShadow** — частотно-зависимая ILD. Low-pass на дальнем ухе (голова экранирует ВЧ).
3. **ITD** — задержка дальнего уха. Для локализации на НЧ (<1.5kHz).
4. **Pinna HRTF** — notch-фильтры ушной раковины. Единственный cue для вертикальной локализации.
5. **Full HRTF** (Steam Audio) — полные таблицы. Overkill для voice chat.

### Этап 10: Итерация 3 — Fix ILD + 3D углы

`sin(azimuth) * cos(elevation)` вместо clamp. `SetSpatialAngles(azimuth, elevation)` API. `SpatializationMode` enum. Плавный переход при проходе сзади.

### Этап 11: Итерация A — ITD

Delay line (кольцевой буфер 256 сэмплов). Формула Woodworth для сферической головы. Линейная интерполяция для дробных задержек. ITD и ITD+ILD комбинации.

### Этап 12: Архитектурное решение — композиция вместо enum

Вместо `SpatializationMode` (один enum, комбинаторный взрыв) — **независимые переключатели**:

```csharp
public enum ILDMode { None, EqualPower, HeadShadow }
public bool enableITD;
public bool enableHRTF;
```

Любая комбинация допустима. Pipeline: ITD → ILD → HRTF.

### Этап 13: Итерация B — HeadShadow (Frequency-Dependent ILD)

Реализован Head Shadow с 6 режимами фильтрации через `ShadowFilterOrder` enum:

- **OnePole6dB** — 6 dB/oct, subtle
- **TwoPole12dB** — 12 dB/oct, дефолт (ближе к реальным ~8-10 dB/oct)
- **ThreePole18dB** — 18 dB/oct, усиленный
- **FourPole24dB** — 24 dB/oct, агрессивный
- **Biquad12dB** — 12 dB/oct с настраиваемым Q
- **MultiBand3** — 3-полосный кроссовер с per-band gain по измеренной кривой

MultiBand3 реализован через комплементарные LPF/HPF biquad (Butterworth Q=0.707). Gains интерполируются по `shadowAmount = |sin(az)| * strength * cos(el)`: от 0 dB (фронт) до сконфигурированных значений (90°). Дефолты: low=-2 dB, mid=-10 dB, high=-20 dB — точно по измерениям Blauert (1997).

Все параметры снабжены `[Tooltip]` с физическими объяснениями, формулами и академическими референсами. Реалистичные дефолты и диапазоны.

### Этап 14: Обсуждение HRTF

Разобрали HRTF (Head-Related Transfer Function) — передаточную функцию, описывающую трансформацию звука дифракцией на голове и ушной раковине. Ключевая роль: решение "cone of confusion", вертикальная и перед/зад локализация.

Варианты: C1 (1 notch), C2 (2 notch), D (Short FIR), SOFA (не планируется). Решено: C1 → C2 → (опционально D).

### Этап 15: Итерация C — Pinna HRTF (C1 + C2)

Реализованы оба шага за одну итерацию:

**C1 — Primary notch:** Peaking EQ biquad (Bristow-Johnson) с отрицательным gain. Частота зависит от elevation: `pinnaNotchFreq × (1 ± 40% × elevationInfluence)`. Дефолты: 7000 Hz, Q=4, depth=-9 dB.

**C2 — Secondary notch:** На `primaryFreq × 1.6` (дефолт). Глубина = `primaryDepth × 0.6`. Отключается через `pinnaSecondaryStrength = 0` для A/B сравнения C1 vs C2.

Оба notch применяются к обоим ушам. Хелперы: `ComputePeakingEQ()` (static, формулы Bristow-Johnson), `ApplyBiquad()` (generic Direct Form II Transposed).

### Этап 16: ProfilerMarkers + multi-pass рефакторинг

Pipeline реструктурирован из одного interleaved цикла в **отдельные проходы** (multi-pass). Каждая стадия — свой цикл по `samplesPerChannel` с `ProfilerMarker.Auto()`:

| Маркер | Стадия |
|--------|--------|
| `LiveKit.Spatial` | Весь pipeline |
| `LiveKit.Spatial.ITD` | Delay line |
| `LiveKit.Spatial.ILD` | Equal-power gains |
| `LiveKit.Spatial.HeadShadow` | LPF/multiband |
| `LiveKit.Spatial.HRTF` | Peaking EQ notch(es) |

Добавлен кэшированный `monoBuffer[]` для allocation-free multi-pass.

---

## Текущее состояние

### Что реализовано

- **ILD EqualPower:** `sin(azimuth) * cos(elevation)` → equal-power pan law
- **ILD HeadShadow:** 6 режимов фильтрации (OnePole → FourPole, Biquad, MultiBand3)
- **MultiBand3:** 3-полосный кроссовер, per-band gain по измеренной кривой head shadow
- **ITD:** Delay line + Woodworth + linear interpolation
- **HRTF C1:** Primary pinna notch (peaking EQ, elevation-dependent)
- **HRTF C2:** Secondary pinna notch (×1.6 freq, ×0.6 depth)
- **ProfilerMarkers:** 5 маркеров, multi-pass архитектура
- **Любая комбинация:** ILD + ITD + HRTF работает, переключение в Inspector без артефактов
- Distance rolloff (нативный Unity, POST-DSP)
- Audio Effects, Reverb Zones
- Обратная совместимость (Private/Community chat — стерео passthrough)
- Tooltips с физическими описаниями и референсами на всех параметрах

### Опциональные следующие шаги

1. **Итерация D (опционально):** Short FIR 64-tap (HRIR из MIT KEMAR, если параметрика недостаточно)
2. **Субъективная оценка:** A/B тестирование всех комбинаций для выбора оптимального пресета
3. **ECS миграция:** Перенос `ProximityPanCalculator` в ECS при 50+ участниках

### Файлы

**SDK (LiveKit fork), ветка `feat/mono-spatial-audio`:**
- `LivekitAudioSource.cs` — ILDMode, ShadowFilterOrder, enableITD, enableHRTF, multi-pass pipeline, ProfilerMarkers, delay line, cascade/biquad/multiband LPF, peaking EQ notch (C1+C2), ComputePeakingEQ, ApplyBiquad

**Unity project:**
- `ProximityPanCalculator.cs` — azimuth + elevation из AudioListener
- `ProximityVoiceChatManager.cs` — New(mono: spatial), AddComponent ProximityPanCalculator
- `manifest.json` — file: ссылка на локальный SDK
- `SpatialAudioStreamFeeder.cs` — удалён

---

## Документы

| Документ | Содержание |
|----------|-----------|
| `ADR_streaming_audioclip.md` | DSP-пайплайн, архитектура, все алгоритмы, HRTF, ProfilerMarkers, референсы |
| `PLAN_streaming_audioclip.md` | Пошаговый план всех итераций (1-3, A-C завершены, D опционально) |
| `SUMMARY_spatial_audio_investigation.md` | Этот документ — полная хронология |
