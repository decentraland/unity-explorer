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

Delay line (кольцевой буфер 256 сэмплов). Формула Woodworth для сферической головы. Линейная интерполяция для дробных задержек. Режимы ITD и ITD+ILD.

### Этап 12: Архитектурное решение — композиция вместо enum

Вместо `SpatializationMode` (один enum, комбинаторный взрыв) — **независимые переключатели**:

```csharp
public enum ILDMode { None, EqualPower, HeadShadow }
public bool enableITD;
public bool enableHRTF;
```

Любая комбинация допустима. Пайплайн: ITD → ILD → HRTF.

---

## Текущее состояние

### Что реализовано

- **ILD EqualPower:** `sin(azimuth) * cos(elevation)` → equal-power pan law
- **ITD:** Delay line + Woodworth + linear interpolation
- **ITD+ILD:** Комбинация задержки и gains
- Distance rolloff (нативный Unity, POST-DSP)
- Audio Effects, Reverb Zones
- Обратная совместимость (Private/Community chat — стерео passthrough)

### Что нужно сделать (по порядку)

1. **Рефакторинг:** `SpatializationMode` → `ILDMode` enum + `enableITD` bool + `enableHRTF` bool
2. **ILD HeadShadow:** One-pole low-pass на дальнем ухе (Frequency-Dependent ILD)
3. **Pinna HRTF:** Notch-фильтры от elevation (вертикальная локализация)

### Файлы

**SDK (LiveKit fork), ветка `feat/mono-spatial-audio`:**
- `LivekitAudioSource.cs` — ILDMode, enableITD, enableHRTF, pipeline, delay line, LPF (будет: notch)

**Unity project:**
- `ProximityPanCalculator.cs` — azimuth + elevation из AudioListener
- `ProximityVoiceChatManager.cs` — New(mono: spatial), AddComponent ProximityPanCalculator
- `manifest.json` — file: ссылка на локальный SDK
- `SpatialAudioStreamFeeder.cs` — удалён

---

## Документы

| Документ | Содержание |
|----------|-----------|
| `ADR_streaming_audioclip.md` | DSP-пайплайн, архитектура композиции, алгоритмы, стратегия |
| `PLAN_streaming_audioclip.md` | Пошаговый план итераций B (HeadShadow), C (Pinna) |
| `SUMMARY_spatial_audio_investigation.md` | Этот документ — полная хронология |
