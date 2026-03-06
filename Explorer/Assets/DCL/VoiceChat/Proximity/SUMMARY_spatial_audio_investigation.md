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

Первоначально предположили что весь пайплайн Unity (pan, spatial, volume) применяется ДО `OnAudioFilterRead`, и поэтому `OnAudioFilterRead` перезаписывает уже обработанные данные. Предложили streaming AudioClip как решение — подача данных через `PCMReaderCallback` ДО всего пайплайна.

Был создан компонент `SpatialAudioStreamFeeder` и интегрирован в `ProximityVoiceChatManager`.

### Этап 3: Эмпирическая проверка

Тестирование показало:
- `spatialBlend = 1` (3D) + rolloff — **работает** ✓
- `panStereo` при `spatialBlend = 0` — **не работает** ✗

Это опровергло гипотезу что "весь пайплайн до OnAudioFilterRead". Часть обработки (distance rolloff) явно после фильтров.

### Этап 4: Streaming AudioClip — проблема buffer underrun

Тестирование `SpatialAudioStreamFeeder` выявило артефакты: микро-паузы, обрывки, рваный звук. Причина — `PCMReaderCallback` вызывается асинхронно относительно LiveKit буфера. Когда callback запрашивает данные, а буфер пуст → нули → артефакты.

`OnAudioFilterRead` не имеет этой проблемы — он синхронен с DSP output.

### Этап 5: Анализ исходного кода Unity

Изучены исходники Unity Audio (`AudioSource.cpp`, `SoundChannel.cpp`, `AudioCustomFilter.cpp`). Установлен **точный** порядок пайплайна:

```
1.  AudioClip PCM / PCMReaderCallback
2.  Pitch (FMOD Channel: setFrequency)
3.  FMOD Head DSP: panStereo (setPan)                        ← PRE-DSP
4.  [Опц.] Spatializer Plugin (pre-effects) на m_dryGroup
5.  Built-in Effects + OnAudioFilterRead на m_wetGroup        ← LiveKit пишет сюда
6.  [Опц.] Spatializer Plugin (post-effects)
7.  Volume (setVolume = CachedRolloff × ...)                  ← POST-DSP
8.  3D Angular Panning (set3DPanLevel × set3DAttributes)      ← POST-DSP
9.  Parent Group (AudioMixer) + Reverb Zones (parallel SEND)
```

**Ключевое открытие:**
- `setPan` (panStereo) = Head DSP = PRE-DSP → применяется к тишине → не работает
- `setVolume` (distance rolloff) = POST-DSP → применяется к данным LiveKit → работает
- `set3DAttributes` (3D angular panning) = POST-DSP → но зависит от **формата канала**

### Этап 6: Уточнение — angular panning тоже не работает

Vitaly подтвердил: при `spatialBlend = 1` работает **только** distance rolloff, а angular panning (left/right) — нет. Только затухание громкости с расстоянием, но не направление звука.

Причина: FMOD's 3D mix matrix на этапе 8 ведёт себя по-разному для моно и стерео:
- **Моно** → распределяет 1 канал по выходам на основе 3D позиции → angular panning
- **Стерео** → сохраняет стерео-образ, применяет только distance attenuation → нет angular panning

`OnAudioFilterRead` на m_wetGroup всегда выдаёт стерео (channels=2) → FMOD видит стерео → нет angular panning.

---

## Текущее состояние

### Что работает с OnAudioFilterRead (без изменений)

- Distance rolloff ✓
- Audio Effects (после LivekitAudioSource в компонентах) ✓
- Reverb Zones ✓
- Amplification/Silence zones ✓

### Что не работает

- Angular panning (L/R в 3D) ✗ — стерео формат канала
- panStereo (2D) ✗ — Head DSP к тишине (не нужен для proximity)

### Варианты решения angular panning (в обсуждении)

1. **Manual Angular Panning в OnAudioFilterRead** — ручной L/R balance по углу. Быстро, производительно, без buffer underrun. Не нативный FMOD panning.
2. **Streaming Mono AudioClip + Ring Buffer** — нативный FMOD panning, но +20-40мс latency и сложность ring buffer.
3. **То же в форке LiveKit SDK** — чище архитектура, те же минусы.
4. **Mono Silent Clip + OnAudioFilterRead** — не проверено, может не сработать (m_wetGroup может форсить стерео).
5. **Native Spatializer Plugin** — overkill.

### Camera-Relative Panning

Дополнительное требование: в 3rd person пан должен быть относительно камеры. Решается через AudioListener (rotation = camera.rotation) или ручной расчёт угла относительно камеры (при Варианте 1).

---

## Артефакты кода (требуют cleanup)

- `SpatialAudioStreamFeeder.cs` — создан для streaming AudioClip подхода. Содержит debug toggle для сравнения режимов. Решение о судьбе зависит от выбранного варианта.
- Изменения в `ProximityVoiceChatManager.CreateSource` и `DestroySource` — добавлена поддержка SpatialAudioStreamFeeder. Может потребоваться откат.

---

## Документы

| Документ | Содержание |
|----------|-----------|
| `ADR_streaming_audioclip.md` | Точный DSP-пайплайн, все варианты angular panning с плюсами/минусами |
| `PLAN_streaming_audioclip.md` | Шаги для каждого варианта, рекомендуемый порядок действий |
| `SUMMARY_spatial_audio_investigation.md` | Этот документ |
