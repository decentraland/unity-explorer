# Summary: Voice Chat Exploration & Proximity Prototype

**Дата:** 2026-03-02

---

## Что было сделано

### 1. High-level обзор Voice Chat системы

Изучена полная архитектура существующего voice chat:

- **Два типа:** Private (1-на-1 по wallet) и Community (групповой по communityId)
- **Стек:** LiveKit SDK (WebRTC, Rust audio backend) → Unity AudioMixer
- **Оркестрация:** `VoiceChatOrchestrator` -- FSM с ReactiveProperty, координирует типы звонков
- **Комнаты:** `VoiceChatActivatableConnectiveRoom` -- отдельная LiveKit-комната, активируется только при звонке
- **Треки:** `VoiceChatTrackManager` публикует локальный микрофон и подписывается на remote
- **Воспроизведение:** `PlaybackSourcesHub` -- один `LivekitAudioSource` (Unity `AudioSource`) на участника, все в одном `AudioMixerGroup`, Unity микширует нативно
- **Нормализации нет:** только компенсация Windows (+13dB), AGC внутри LiveKit SDK
- **Low-level логики в DCL-коде нет:** всё скрыто в LiveKit SDK

### 2. Анализ Island Room инфраструктуры

- Island Room -- LiveKit-комната от Archipelago сервера, группирует игроков по позиции
- `RoomHub` хранит 4 комнаты: Island, Scene, Chat, VoiceChat
- `EntityParticipantTable` маппит walletId → Entity (позиция аватара)
- Island Room уже имеет `AudioStreams`, `AudioTracks` -- поддержка аудио есть на уровне клиента

### 3. Выбор подхода для Proximity Voice Chat

Рассмотрены три варианта:

| Вариант | Описание | Вердикт |
|---------|----------|---------|
| **A: Publish в Island Room** | Переиспользовать существующую комнату | **Выбран** |
| B: Вторая комната, тот же connstr | Изоляция, но DuplicateIdentity риск | Отвергнут |
| C: Новый BE endpoint | Полноценно, но overkill | Отложен |

### 4. Тест публикации аудио в Island Room

Создан `ProximityVoiceChatTest.cs` -- минимальный тестовый класс:
- Подписывается на Island Room connection
- При подключении пытается создать и опубликовать аудио-трек
- Логирует `TrackSubscribed` от других участников
- Все логи с префиксом `[PROXIMITY_TEST]`

**Результат: УСПЕХ** -- LiveKit-сервер разрешает аудио-треки в Island Room.

### 5. План итеративной реализации

**Итерация 1** (завершена): `ProximityVoiceChatManager` -- автоматическая публикация/приём аудио через Island Room, spatial `LivekitAudioSource` для каждого remote-участника

**Итерация 2** (завершена): 3D Spatial Audio -- ECS-система `ProximityAudioPositionSystem` + `ProximityAudioSourceComponent`, shared dictionary как мост между менеджером и ECS, позиционирование AudioSource по `CharacterTransform` каждый кадр

**Итерация 3** (будущее): Интеграция с Orchestrator, `VoiceChatType.SPATIAL`, координация с Private/Community, UI

---

## Ключевые файлы проекта

### Voice Chat (существующий)

| Файл | Роль |
|------|------|
| `Assets/DCL/VoiceChat/VoiceChatOrchestrator.cs` | FSM-координатор звонков |
| `Assets/DCL/VoiceChat/VoiceChatRoomManager.cs` | Подключение к LiveKit room, events |
| `Assets/DCL/VoiceChat/VoiceChatTrackManager.cs` | Publish/subscribe аудио-треков |
| `Assets/DCL/VoiceChat/PlaybackSourcesHub.cs` | Хранилище remote AudioSources |
| `Assets/DCL/VoiceChat/VoiceChatMicrophoneHandler.cs` | Push-to-talk / toggle |
| `Assets/DCL/VoiceChat/VoiceChatConfiguration.cs` | ScriptableObject с настройками |
| `Assets/DCL/VoiceChat/VoiceChatContainer.cs` | DI-контейнер |
| `Assets/DCL/PluginSystem/Global/VoiceChatPlugin.cs` | Точка инициализации |

### Rooms & Multiplayer

| Файл | Роль |
|------|------|
| `Assets/DCL/Multiplayer/Connections/RoomHubs/RoomHub.cs` | Хаб всех комнат |
| `Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/ArchipelagoIslandRoom.cs` | Island Room |
| `Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/VoiceChatActivatableConnectiveRoom.cs` | Voice Chat Room |
| `Assets/DCL/Multiplayer/Connections/Rooms/Connective/ConnectiveRoom.cs` | Базовый класс комнат |
| `Assets/DCL/Multiplayer/Profiles/Tables/EntityParticipantTable.cs` | walletId → Entity маппинг |
| `Assets/DCL/Infrastructure/Global/Dynamic/DynamicWorldContainer.cs` | Создание комнат и DI |

### Proximity (новое)

| Файл | Роль |
|------|------|
| `Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs` | Менеджер proximity voice chat (publish + spatial audio через Island Room, пишет в shared dictionary) |
| `Assets/DCL/VoiceChat/Proximity/ProximityAudioSourceComponent.cs` | ECS-компонент, хранит Transform аудиосурса |
| `Assets/DCL/VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs` | ECS-система: назначает компонент на entity, синхронизирует позиции, cleanup |

> `ProximityVoiceChatTest.cs` удалён — заменён на `ProximityVoiceChatManager`.
> `VoiceChatPlugin.cs` владеет `ConcurrentDictionary<string, Transform>` и передаёт его менеджеру (запись) и системе (чтение).

---

## Инсайт от коллеги

> Room -- это просто `IRoom`, можно публиковать треки в любую комнату (пока BE разрешает permissions).
> `LivekitAudioSource` создаётся автоматически при подписке на stream. Для 3D audio позиция обновляется через ECS-систему (а не reparent под аватар — аватар может не существовать или пересоздаваться).
> Для нового типа voice chat, если он exclusive -- нужно координировать через Orchestrator. Если параллельный -- можно работать независимо.

---

## Открытые вопросы

1. **Interaction с Private/Community:** что делать со spatial когда активен другой звонок? (mute / disconnect / coexist) → Итерация 3
2. **Нагрузка на Island Room:** сколько одновременных аудио-треков выдержит Island Room без деградации?
3. **Permissions:** могут ли серверные permissions на Island Room измениться?
4. ~~**3D audio настройки:** какие `minDistance`, `maxDistance`, `rolloffMode` оптимальны для Decentraland?~~ → Решено: `minDistance=2`, `maxDistance=50`, `Logarithmic` rolloff (может быть вынесено в `VoiceChatConfiguration`)
