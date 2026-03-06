# Proximity Voice Chat: Spatial Audio — План имплементации

## Выбранный подход

**Вариант 6: Mono в LiveKit SDK fork + Manual Angular Panning в OnAudioFilterRead**

Подробный анализ и обоснование — см. [ADR](ADR_streaming_audioclip.md).

---

## Предусловия

- Локальный форк LiveKit SDK: `c:\DCL\LiveKit\client-sdk-unity`
- Удалённый репозиторий: `https://github.com/decentraland/client-sdk-unity.git`
- Текущая ветка: `chore/rust-audio-for-mac-intel`
- Unity manifest ссылается на: `com.decentraland.livekit-sdk` → Git URL + branch

---

## Итерация 1: Mono + Zero Pan (проверка теории)

Цель: проверить что LiveKit выдаёт чистый моно-сигнал и OnAudioFilterRead без артефактов дублирует его в стерео.

### Шаг 1: Git — создать ветку в SDK fork

```
cd c:\DCL\LiveKit\client-sdk-unity
git fetch origin
git checkout chore/rust-audio-for-mac-intel
git checkout -b feat/mono-spatial-audio
```

### Шаг 2: SDK — добавить mono mode в LivekitAudioSource

**Файл:** `Runtime/Scripts/Rooms/Streaming/Audio/LivekitAudioSource.cs`

Изменения:
1. Добавить `bool _monoMode` поле
2. Расширить `New()` — принимать параметр `mono`:
   ```csharp
   public static LivekitAudioSource New(bool playing = true, bool mono = false)
   ```
3. Добавить `float[] _monoBuffer` — промежуточный буфер для чтения моно данных
4. В `OnAudioFilterRead`:
   - Если `_monoMode = false` → текущая логика без изменений (стерео ReadAudio)
   - Если `_monoMode = true`:
     1. Выделить/переиспользовать `_monoBuffer` размером `data.Length / 2`
     2. `ReadAudio(_monoBuffer, 1, sampleRate)` — запросить 1 канал
     3. Дублировать: `data[i*2] = _monoBuffer[i]; data[i*2+1] = _monoBuffer[i];`

### Шаг 3: SDK — изменить AudioStream для поддержки моно

**Файл:** `Runtime/Scripts/Rooms/Streaming/Audio/AudioStream.cs`

Изменения:
- Убрать хардкод `currentChannels = 2`
- Принимать `channels` через параметр `ReadAudio(data, channels, sampleRate)`
- Нативный слой (FFI) запрашивает `channels` каналов

### Шаг 4: SDK — commit и push

```
git add -A
git commit -m "feat: add mono mode to LivekitAudioSource for spatial audio panning"
git push -u origin feat/mono-spatial-audio
```

### Шаг 5: Unity — обновить manifest.json

**Файл:** `Explorer/Packages/manifest.json`

```json
"com.decentraland.livekit-sdk": "https://github.com/decentraland/client-sdk-unity.git#feat/mono-spatial-audio"
```

Альтернатива для локальной разработки — использовать `file:` протокол:
```json
"com.decentraland.livekit-sdk": "file:../../../LiveKit/client-sdk-unity"
```

### Шаг 6: Unity — ProximityVoiceChatManager

**Файл:** `Explorer/Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs`

Изменения в `CreateSource`:
- Для `spatial = true`: вызвать `LivekitAudioSource.New(mono: true)` вместо `LivekitAudioSource.New(true)`
- Убрать создание `SpatialAudioStreamFeeder` (больше не нужен)
- `source.Construct(stream)` — стандартный путь (моно-режим внутри LivekitAudioSource)

Изменения в `DestroySource`:
- Убрать cleanup `SpatialAudioStreamFeeder`

Private/Community Voice Chat — без изменений (используют `LivekitAudioSource.New(true)` → стерео по умолчанию).

### Шаг 7: Cleanup

- Удалить `SpatialAudioStreamFeeder.cs` (если больше не нужен)
- Проверить что нет других ссылок на него

### Проверка

- [ ] Аудио чистое, без артефактов (микро-паузы, рваность)
- [ ] Distance rolloff работает при spatialBlend=1
- [ ] Private/Community voice chat не сломан (стерео по умолчанию)
- [ ] L=R одинаковый (нулевой pan) — как отправная точка

---

## Итерация 2: Angular Panning (следующая)

После подтверждения что моно работает чисто:

1. Кешировать позицию/ориентацию камеры в `Update()` (main thread → volatile/lock-free переменные для audio thread)
2. В `OnAudioFilterRead` после чтения моно:
   - Рассчитать угол: `angle = SignedAngle(camera.forward, sourcePos - cameraPos, camera.up)`
   - L gain = `cos((angle + 90) * PI / 360)`; R gain = `sin((angle + 90) * PI / 360)`
   - `data[i*2] = _monoBuffer[i] * leftGain; data[i*2+1] = _monoBuffer[i] * rightGain;`
3. Вопрос: реализовать в LivekitAudioSource (SDK) или в отдельном компоненте (Unity project)

---

## Итерация 3: Camera-Relative Panning (позже)

Разделение: позиция AudioListener из головы аватара, ориентация из камеры.
