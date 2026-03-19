# Proximity Voice Chat -- План реализации

## Контекст

Нужен Spatial Nearby Voice Chat, который включён по дефолту и использует участников Island Room. Реализация идёт итеративно.

---

## Итерация 1: Базовый прототип (без 3D audio) ✓

**Статус:** Завершена

### Выбранный подход: Вариант A -- публикация аудио в Island Room

Island Room -- уже подключённая LiveKit-комната, в которой находятся все участники по близости. `IRoom` поддерживает `AudioStreams`, `AudioTracks`, `PublishTrack`. Не нужна новая комната, новый connection string, никакой координации с BE.

**Проверено:** LiveKit-сервер разрешает публикацию аудио-треков в Island Room (тест `ProximityVoiceChatTest` пройден успешно).

### Что сделано

Создан `ProximityVoiceChatManager`, подключён в `VoiceChatPlugin`:

1. Слушает `ConnectionUpdated` на Island Room
2. При подключении -- создаёт `MicrophoneRtcAudioSource`, публикует аудио-трек в Island Room
3. Подписывается на `TrackSubscribed`/`TrackUnsubscribed` на Island Room
4. Создаёт spatial `LivekitAudioSource` для каждого remote-участника
5. При отключении Island -- cleanup

Работает полностью параллельно существующему voice chat, не трогает Orchestrator.

---

## Итерация 2: 3D Spatial Audio ✓

**Статус:** Завершена

### Что сделано

Реализовано 3D-позиционирование аудио через ECS-систему. Менеджер управляет жизненным циклом аудио (LiveKit), система управляет привязкой к entity и синхронизацией позиций.

### Архитектура

```mermaid
flowchart TD
    IslandRoom["Island Room (IRoom)"] -->|TrackSubscribed| PVM["ProximityVoiceChatManager"]
    PVM -->|"activeAudioSources[walletId] = transform"| Dict["ConcurrentDictionary&lt;string, Transform&gt;<br/>(shared, owned by VoiceChatPlugin)"]
    Dict -->|читает каждый кадр| System["ProximityAudioPositionSystem"]
    System -->|"entityParticipantTable.TryGet(walletId)"| EPT["EntityParticipantTable"]
    EPT -->|Entity найден| System
    System -->|"world.Add(entity, ProximityAudioSourceComponent)"| ECS["ECS World"]
    System -->|"SyncPositions: AudioSource.position = CharacterTransform.Position"| ECS
    IslandRoom -->|TrackUnsubscribed| PVM
    PVM -->|"activeAudioSources.TryRemove(walletId)"| Dict
    System -->|"null Transform → world.Remove&lt;ProximityAudioSourceComponent&gt;"| ECS
```

### Файлы

| Действие | Файл |
|----------|------|
| Создать | `Assets/DCL/VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs` |
| Создать | `Assets/DCL/VoiceChat/Proximity/ProximityAudioSourceComponent.cs` |
| Изменить | `Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs` |
| Изменить | `Assets/DCL/PluginSystem/Global/VoiceChatPlugin.cs` |

### Ключевые решения

**Shared Dictionary как мост между Manager и ECS:**
- `VoiceChatPlugin` владеет `ConcurrentDictionary<string, Transform>`
- Менеджер пишет (walletId → AudioSource Transform) при subscribe/unsubscribe
- Система читает словарь каждый кадр и назначает/снимает компоненты

**Почему не прямой `world.Add` в менеджере:**
- Entity может ещё не существовать на момент `OnTrackSubscribed` (LiveKit-событие приходит раньше, чем `MultiplayerProfilesSystem` создаёт entity)
- Система каждый кадр пробует `entityParticipantTable.TryGet()` — привяжет как только entity появится

**Cleanup через null-detection:**
- При `RemoveRemoteSource` менеджер убирает из словаря и вызывает `DestroySource`
- Система в `SyncPositions` детектит null Transform → собирает в cleanup-список → `world.Remove` после query

### Spatial Audio настройки

```csharp
spatialBlend = 1.0f       // полный 3D
dopplerLevel = 0f         // без doppler-эффекта
rolloffMode = Logarithmic
minDistance = 2f
maxDistance = 50f
spread = 0f
```

## Итерация 3: Полноценная интеграция (планируется)

> Детальный анализ каждой фичи — см. `ANALYSIS_voicechat_features.md`

### 3.1 Nametag speaking indicator

**Приоритет:** Высокий | **Сложность:** Низкая

Создать `ProximityNametagsHandler` — аналог `VoiceChatNametagsHandler`, но для Island Room:
- Подписка на `islandRoom.ActiveSpeakers.Updated`
- `entityParticipantTable.TryGet()` → `world.AddOrSet(entity, VoiceChatNametagComponent)`
- Переиспользует существующий компонент, `NametagPlacementSystem` и CSS-анимацию

### 3.2 Mute/Unmute + Push-to-Talk

**Приоритет:** Высокий | **Сложность:** Средняя

- Добавить mute/unmute в `ProximityVoiceChatManager` (`rtcAudioSource.Stop()/Start()`)
- Экспонировать `ReactiveProperty<bool> IsMicrophoneEnabled`
- Переиспользовать PTT-логику из `VoiceChatMicrophoneHandler` (hotkey + hold threshold)

### 3.3 Смена микрофона в рантайме

**Приоритет:** Средний | **Сложность:** Низкая

- Подписка на `VoiceChatSettings.MicrophoneChanged`
- Вызов `rtcAudioSource.SwitchMicrophone(newSelection)`

### 3.4 Mute proximity при Private/Community call

**Приоритет:** Средний | **Сложность:** Средняя | **Зависит от:** 3.2

- Подписка на `voiceChatOrchestrator.CurrentCallStatus`
- `VOICE_CHAT_IN_CALL` → suppress proximity (mute mic + optional mute playback)
- `DISCONNECTED` / `ENDING_CALL` → resume

### 3.5 Звуковой фидбек mute/unmute

**Приоритет:** Низкий | **Сложность:** Минимальная | **Зависит от:** 3.2

- Переиспользовать `MicrophoneAudioToggleHandler` с ReactiveProperty из 3.2

### 3.6 macOS permissions guard

**Приоритет:** Средний | **Сложность:** Низкая

- Добавить `VoiceChatPermissions.GuardAsync()` перед `MicrophoneRtcAudioSource.New()` на macOS

### 3.7 Reconnection retry

**Приоритет:** Низкий | **Сложность:** Низкая-средняя

- Retry публикации микрофона при ошибке (delay + max attempts)
- Логирование причин отключения

---

## Отвергнутые варианты

### Вариант B: Отдельная комната с тем же connection string

- Изолирует аудио от data-трафика
- Но: тот же токен вызовет `DuplicateIdentity` disconnect
- Значительно сложнее

### Вариант C: Новый серверный endpoint

- Аналог community voice chat но с auto-join
- Требует BE разработки
- Overkill для прототипа
