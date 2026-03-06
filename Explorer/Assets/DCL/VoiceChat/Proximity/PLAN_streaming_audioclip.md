# Streaming AudioClip для Proximity Voice Chat — План реализации

## Контекст

`OnAudioFilterRead` в `LivekitAudioSource` обходит часть Unity audio pipeline (panStereo, Audio Effects до фильтра). Заменяем доставку аудио для spatial-источников на streaming `AudioClip` с `PCMReaderCallback`. См. [ADR](ADR_streaming_audioclip.md) (в этой же папке).

---

## Вариант A: Без модификации LiveKit SDK (текущий)

Дополнительный компонент `SpatialAudioStreamFeeder` рядом с `LivekitAudioSource`.

### Шаг 1: Создать SpatialAudioStreamFeeder

**Файл:** `Assets/DCL/VoiceChat/Proximity/SpatialAudioStreamFeeder.cs`

Компонент MonoBehaviour:
- `Initialize(Weak<AudioStream>, AudioSource)` — принимает стрим и целевой AudioSource
- Создаёт `AudioClip.Create("SpatialLivekitStream", sampleRate, 1, sampleRate, true, OnPCMRead)` — моно, streaming
- Назначает клип на AudioSource, `loop = true`
- `OnPCMRead(float[] data)` — вызывается Unity на audio thread; читает `AudioStream.ReadAudio(data, 1, sampleRate)`
- `Free()` — обнуляет ссылку на стрим
- Обработка `AudioSettings.OnAudioConfigurationChanged` — пересоздаёт клип при смене sample rate

### Шаг 2: Изменить ProximityVoiceChatManager.CreateSource

**Файл:** `Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs`

В методе `CreateSource(StreamKey key, Weak<AudioStream> stream, bool spatial)`:

```
ЕСЛИ spatial:
    - НЕ вызывать source.Construct(stream)
    - Применить spatial настройки к AudioSource
    - Добавить SpatialAudioStreamFeeder на GO
    - Вызвать feeder.Initialize(stream, audioSource)
ИНАЧЕ:
    - Вызвать source.Construct(stream) как раньше
```

`LivekitAudioSource.OnAudioFilterRead` станет no-op для spatial-источников: `stream.Resource.Has` = false → данные не перезаписываются → клип проходит через весь пайплайн.

### Шаг 3: Обновить DestroySource

**Файл:** `Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs`

В методе `DestroySource`:
- Перед `gameObject.SelfDestroy()` найти `SpatialAudioStreamFeeder` на GO
- Вызвать `feeder.Free()` для освобождения ссылки на стрим

### Файлы — сводка

| Действие | Файл |
|----------|------|
| Создать | `Assets/DCL/VoiceChat/Proximity/SpatialAudioStreamFeeder.cs` |
| Изменить | `Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs` |

### Проверка

1. **panStereo:** `spatialBlend = 0`, pan полностью влево → звук только в левом канале
2. **3D Spatial:** `spatialBlend = 1`, два участника на разных позициях → звук идёт с корректных направлений
3. **Distance Rolloff:** удалить аватар → громкость падает по rolloff-кривой
4. **Audio Effects:** добавить AudioLowPassFilter на GO → эффект применяется к голосу
5. **Loopback:** `EnableLocalTrackPlayback = true` → loopback работает как раньше (через OnAudioFilterRead, не через streaming clip)
6. **Смена устройства:** переключить audio output → клип пересоздаётся, звук продолжается

---

## Вариант B: С модификацией LiveKit SDK (будущее)

Тот же подход (streaming AudioClip + PCMReaderCallback), но код живёт внутри `LivekitAudioSource` вместо отдельного компонента. Требует форк [decentraland/client-sdk-unity](https://github.com/decentraland/client-sdk-unity).

### Шаг 1: Добавить spatial-режим в LivekitAudioSource

**Файл (в форке):** `Runtime/Scripts/Rooms/Streaming/Audio/LivekitAudioSource.cs`

- Добавить параметр `bool spatialAudio = false` в `LivekitAudioSource.New()`
- Когда `spatialAudio = true`:
  - В `Construct()` или `OnEnable()` создать streaming моно AudioClip
  - PCMReaderCallback читает из `stream` через `ReadAudio(data, 1, sampleRate)`
  - `OnAudioFilterRead` — skip (`if (useSpatialClip) return;`)
- Когда `spatialAudio = false`: текущее поведение без изменений
- Обработка `AudioSettings.OnAudioConfigurationChanged` — пересоздание клипа

### Шаг 2: Обновить ProximityVoiceChatManager

**Файл:** `Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs`

```
LivekitAudioSource source = LivekitAudioSource.New(explicitName: true, spatialAudio: spatial);
source.Construct(stream);  // теперь всегда вызываем — внутри SDK решает способ доставки
```

### Шаг 3: Удалить SpatialAudioStreamFeeder

Компонент больше не нужен — логика внутри SDK.

### Преимущества перед Вариантом A

- Один компонент вместо двух на GO
- Единая точка ответственности за доставку аудио (LiveKit SDK)
- Проще поддержка WAV-записи (один стрим, одна точка tee)
- Чище API: `LivekitAudioSource.New(spatialAudio: true)` — явный контракт

### Когда переходить

- При первой необходимости форка SDK для других целей
- Или когда SpatialAudioStreamFeeder покажет ограничения (WAV-запись, сложные сценарии)
