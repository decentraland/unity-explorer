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

Вопрос: "Есть более точные алгоритмы, учитывающие психо-акустику?"

Рассмотренные алгоритмы по качеству:

1. **ILD (Interaural Level Difference)** — разница громкости L/R. Текущий (equal-power pan). Простой, но грубый. Проблема со скачком сзади.

2. **ITD (Interaural Time Difference)** — задержка дальнего уха. Мозг использует для низких частот (<1.5kHz). Delay line ~12 сэмплов при 48kHz. Не требует буфера (delay line внутри OnAudioFilterRead).

3. **ITD + ILD** — комбинация. ITD для низких частот + ILD для высоких — как в реальности.

4. **Parametric HRTF** — упрощённый HRTF без таблиц. One-pole low-pass filter на дальнем ухе (head shadow). Моделирует затенение головой. Значительно лучше чем ILD, но без полного HRTF (нет pinna filtering).

5. **Full HRTF** (Steam Audio, Resonance Audio) — полные PRTF-таблицы. Overkill для voice chat.

### Этап 10: Решение — итеративный путь

**Преемственность:** каждый алгоритм добавляет эффект поверх предыдущих, не ломая их. Полная совместимость назад.

**Стратегия:** Fix ILD (sin вместо clamp, 3D) → + ITD → ITD+ILD → + Parametric HRTF

**API change:** `Pan` (-1..+1) → `SetSpatialAngles(azimuth, elevation)` (радианы). Углы нужны для ITD и HRTF, pan недостаточен.

**Inspector:** Enum `SpatializationMode` для переключения алгоритмов на лету (A/B сравнение). Параметры с `[Header]` группировкой.

**ILD fix:** `sin(azimuth)` вместо `Atan2/clamp`. Плавный переход:
- right (90°) → sin=1 → полностью вправо
- back (180°) → sin=0 → центр
- Нет скачка

**3D:** `pan = sin(azimuth) * cos(elevation)`. Elevation уменьшает ILD при источнике сверху/снизу.

---

## Текущее состояние

### Что работает

- Distance rolloff (нативный Unity, POST-DSP)
- Angular panning ILD (ручной equal-power pan, есть скачок сзади)
- Audio Effects, Reverb Zones
- Обратная совместимость (Private/Community chat — стерео)

### Что нужно сделать (по порядку)

1. **Fix ILD:** sin(azimuth) * cos(elevation), enum + Inspector параметры
2. **+ ITD:** delay line, headRadius параметр
3. **ITD+ILD:** комбинация
4. **+ Parametric HRTF:** one-pole low-pass на дальнем ухе

### Файлы

**SDK (LiveKit fork), ветка `feat/mono-spatial-audio`:**
- `LivekitAudioSource.cs` — monoMode, Pan, equal-power panning (будет расширен: enum, angles, ITD, HRTF)

**Unity project:**
- `ProximityPanCalculator.cs` — расчёт pan (будет: azimuth + elevation)
- `ProximityVoiceChatManager.cs` — mono: spatial, AddComponent ProximityPanCalculator
- `manifest.json` — file: ссылка на локальный SDK
- `SpatialAudioStreamFeeder.cs` — удалён

---

## Документы

| Документ | Содержание |
|----------|-----------|
| `ADR_streaming_audioclip.md` | DSP-пайплайн, алгоритмы ILD/ITD/HRTF, enum, стратегия |
| `PLAN_streaming_audioclip.md` | Пошаговый план итераций 3/A/B, Inspector параметры |
| `SUMMARY_spatial_audio_investigation.md` | Этот документ — полная хронология |
