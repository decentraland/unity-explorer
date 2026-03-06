# Summary: Исследование Spatial Audio Pipeline для Proximity Voice Chat

**Дата:** 2026-03-06  
**Участники:** Vitaly Popuzin, AI Assistant  
**Контекст:** Итерация 2 Proximity Voice Chat — проблемы с пространственным аудио

---

## Исходная проблема

При тестировании proximity voice chat обнаружено: `panStereo` не работает. При `spatialBlend = 0` и полном смещении pan влево/вправо — звук одинаков в обоих каналах. Обычный AudioSource с клипом работает корректно. Подозрение пало на `LivekitAudioSource` из LiveKit SDK.

---

## Ход исследования

### Этап 1: Анализ LivekitAudioSource

Изучен `LivekitAudioSource.cs` из пакета `com.decentraland.livekit-sdk`. Компонент использует `OnAudioFilterRead` для инъекции аудио из LiveKit `AudioStream` в Unity audio pipeline. AudioSource создаётся **без клипа** — `OnAudioFilterRead` полностью перезаписывает буфер данными из LiveKit.

`AudioStream` захардкожен на 2 канала (`currentChannels = 2`), моно-голос дублируется в оба канала (L=R).

Пакет приходит из форка: `https://github.com/decentraland/client-sdk-unity.git#chore/rust-audio-for-mac-intel`

### Этап 2: Первоначальная гипотеза (ошибочная)

Первоначально предположили что весь пайплайн Unity (pan, spatial, volume) применяется ДО `OnAudioFilterRead`, и поэтому `OnAudioFilterRead` перезаписывает уже обработанные данные. Предложили streaming AudioClip как решение.

Был создан компонент `SpatialAudioStreamFeeder` и интегрирован в `ProximityVoiceChatManager`.

### Этап 3: Эмпирическая проверка

Тестирование показало:
- `spatialBlend = 1` (3D) + distance rolloff — **работает**
- `panStereo` при `spatialBlend = 0` — **не работает**
- Angular panning (L/R в 3D) — **не работает**

Distance rolloff работает, а pan нет — это опровергло начальную гипотезу.

### Этап 4: Streaming AudioClip — buffer underrun

Тестирование `SpatialAudioStreamFeeder` выявило артефакты: микро-паузы, обрывки, рваный звук. Причина — `PCMReaderCallback` вызывается асинхронно относительно LiveKit буфера. `OnAudioFilterRead` синхронен с DSP output и не имеет этой проблемы.

### Этап 5: Анализ исходного кода Unity

Изучены `AudioSource.cpp`, `SoundChannel.cpp`, `AudioCustomFilter.cpp`. Установлен точный пайплайн:

```
1.  AudioClip PCM / PCMReaderCallback
2.  Pitch (setFrequency)
3.  FMOD Head DSP: panStereo (setPan)                        ← PRE-DSP
4.  [Опц.] Spatializer Plugin (pre-effects) на m_dryGroup
5.  Built-in Effects + OnAudioFilterRead на m_wetGroup        ← LiveKit пишет сюда
6.  [Опц.] Spatializer Plugin (post-effects)
7.  Volume (setVolume = CachedRolloff × ...)                  ← POST-DSP
8.  3D Angular Panning (set3DPanLevel × set3DAttributes)      ← POST-DSP
9.  Parent Group (AudioMixer) + Reverb Zones (parallel SEND)
```

**Ключевые выводы:**
- `panStereo` = Head DSP = PRE-DSP → применяется к тишине → не работает
- `setVolume` (distance rolloff) = POST-DSP, скалярный → работает
- `3D Angular Panning` = POST-DSP, но FMOD 3D mix matrix не панорамирует **стерео** источники (только моно)

### Этап 6: Root cause — стерео формат

`OnAudioFilterRead` на m_wetGroup всегда работает в стерео (channels=2). LiveKit запрашивает 2 канала, дублируя моно в L=R. FMOD видит стерео → angular panning пропускается.

### Этап 7: Обзор вариантов решения

Рассмотрено 6 вариантов (подробно в ADR):
1. Manual Angular Panning (стерео) — работает, но лишняя конверсия моно→стерео в нативном слое
2. Streaming Mono AudioClip — buffer underrun (подтверждён)
3. Streaming Mono в SDK fork — те же проблемы
4. Mono Silent Clip + OnAudioFilterRead — не проверено, вероятно m_wetGroup форсит стерео
5. Native Spatializer Plugin — overkill
6. **Mono в LiveKit SDK + Manual Angular Panning** — выбранный подход

### Этап 8: Принятие решения

**Выбран Вариант 6:**
- В форке LiveKit SDK: `LivekitAudioSource.New(mono: true)` запрашивает моно из нативного слоя
- В `OnAudioFilterRead` читает моно, распределяет по L/R стерео-буфера
- Итерация 1: нулевой pan (L=R дубликат) — проверка чистоты аудио
- Итерация 2: angular panning по углу camera→source
- Итерация 3: camera-relative panning (позиция от аватара, ориентация от камеры)

**Обратная совместимость:** `mono = false` по умолчанию → Private/Community Voice Chat без изменений.

---

## Текущее состояние

### Что работает с OnAudioFilterRead (без изменений)

- Distance rolloff
- Audio Effects (после LivekitAudioSource в порядке компонентов)
- Reverb Zones
- Amplification/Silence zones

### Что не работает (будет решено)

- Angular panning (L/R в 3D) — стерео формат канала → решается моно режимом
- panStereo (2D) — Head DSP к тишине (не нужен для proximity)

### Артефакты кода (требуют cleanup)

- `SpatialAudioStreamFeeder.cs` — создан для streaming AudioClip подхода, будет удалён
- Изменения в `ProximityVoiceChatManager.CreateSource`/`DestroySource` — поддержка SpatialAudioStreamFeeder, будет заменена на `mono: true`

---

## Camera-Relative Panning

Требование: в 3rd person пан относительно камеры, не аватара. При повороте аватара на 180° при неподвижной камере — звук остаётся "слева в кадре".

Решение (итеративно):
- **Итерация 1:** Позиция камеры для расчёта угла
- **Итерация 2+:** Позиция из головы аватара, ориентация из камеры

---

## Документы

| Документ | Содержание |
|----------|-----------|
| `ADR_streaming_audioclip.md` | DSP-пайплайн, все варианты, обоснование выбора Варианта 6 |
| `PLAN_streaming_audioclip.md` | Пошаговый план имплементации (итерации 1-3) |
| `SUMMARY_spatial_audio_investigation.md` | Этот документ — хронология исследования |
