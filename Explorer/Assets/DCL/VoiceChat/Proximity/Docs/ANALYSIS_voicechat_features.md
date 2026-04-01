# Анализ фич VoiceChat для переноса в Proximity VoiceChat

## Контекст

Существующий VoiceChat (Private + Community) содержит ряд фич, которые отсутствуют в Proximity VoiceChat. Этот документ анализирует каждую фичу на предмет применимости к proximity-модели (3D spatial, всегда включён, без выделенного UI).

---

## Что уже реализовано в Proximity

- Публикация/подписка аудио-треков в Island Room
- 3D spatial audio (ILD, ITD, HRTF, distance rolloff, pan calculator)
- Lip sync (amplitude-based, frequency bands, mouth atlas)
- Audio Effect Zones (silence zones, reverb, echo, filter)
- ECS-позиционирование AudioSource на голове аватара
- Debug-виджет с runtime-слайдерами
- Nametag speaking indicator (ProximityNametagsHandler)
- Mute/Unmute + Push-to-Talk (ProximityVoiceChatStateModel + NearbyVoiceWidgetController)
- Смена микрофона в рантайме (VoiceChatSettings.MicrophoneChanged)
- Mute proximity при Private/Community call (Suppress/Resume)
- macOS permissions guard (VoiceChatPermissions.GuardAsync)
- Reconnection retry (ActivateWithRetryAsync)
- Mute persistence (ProximityMuteService + REST API)
- Lazy track publishing (ADR_lazy_track_publishing.md)
- UI: sidebar button + nearby voice widget panel

---

## Фичи для переноса

### 1. Индикатор говорящего в Nametags ✅ РЕАЛИЗОВАНО

**Приоритет:** Высокий  
**Сложность:** Низкая (~50 строк)  
**Зависимости:** нет

**Текущее состояние в VoiceChat:**  
`VoiceChatNametagsHandler` слушает `ActiveSpeakers.Updated` от VoiceChat Room, ставит `VoiceChatNametagComponent` на entity аватара. `NametagPlacementSystem` читает компонент и включает CSS-класс `nametag--voice-chat` — появляется анимированный badge с тремя зелёными столбиками.

**Проблема:**  
`VoiceChatNametagsHandler` слушает **только** `roomHub.VoiceChatRoom().Room()` (Community/Private), а **не** Island Room. Proximity speaking status не попадает в nametags.

При этом в `VoiceChatPlugin` уже есть подписка на `islandRoom.ActiveSpeakers.Updated` (заполняет `proximityConfigHolder.SpeakingParticipants`). Данные есть — не пробрасываются в ECS.

**Реализация:**  
`ProximityNametagsHandler` создан:
- Подписывается на `islandRoom.ActiveSpeakers.Updated`
- Через `entityParticipantTable.TryGet()` находит entity аватара
- Ставит `VoiceChatNametagComponent(isSpeaking, isHushed: isMuted)`
- Поддерживает `ActiveSpeakersDiffTracker` для отслеживания diff
- Suppression при Private/Community call
- Использует `HasPublishedAudioTrack` для определения кому показывать nametag

Компонент `VoiceChatNametagComponent`, `NametagPlacementSystem`, `NametagElement` и CSS-анимация — переиспользованы без изменений.

**Ключевые файлы:**
- `VoiceChat/VoiceChatNametagsHandler.cs` — образец
- `VoiceChat/VoiceChatNametagComponent.cs` — переиспользуем as-is
- `NameTags/Systems/NametagPlacementSystem.cs` — уже читает компонент
- `NameTags/NametagElement.cs` — CSS `nametag--voice-chat`

---

### 2. Управление микрофоном (Mute/Unmute + PTT) ✅ РЕАЛИЗОВАНО

**Приоритет:** Высокий  
**Сложность:** Средняя  
**Зависимости:** нет

**Текущее состояние в VoiceChat:**  
`VoiceChatMicrophoneHandler` реализует:
- **Push-to-talk:** hotkey `DCLInput.Instance.VoiceChat.Talk`, threshold 0.5s — короткое нажатие = toggle, длинное = PTT
- **Toggle:** программное включение/выключение через `ToggleMicrophone()`
- **ReactiveProperty** `IsMicrophoneEnabled` — UI подписывается на состояние

**Проблема:**  
В Proximity микрофон публикуется в `ProximityVoiceChatManager.PublishLocalTrack()` и **всегда активен** — нет mute, нет PTT, нет toggle.

**Реализация:**  
- `ProximityVoiceChatStateModel` с состояниями Hearing/Speaking управляет `rtcAudioSource.Stop()/Start()`
- PTT через `NearbyVoiceWidgetController` + `DCLInput.VoiceChat.Talk` (T key)
- Lazy track publishing: публикация при первом Speaking (см. `ADR_lazy_track_publishing.md`)
- Серверный mute не нужен — локальный `Stop()/Start()` достаточен

---

### 3. Смена микрофона в рантайме ✅ РЕАЛИЗОВАНО

**Приоритет:** Средний  
**Сложность:** Низкая (~15 строк)  
**Зависимости:** нет

**Реализация:**  
В `ProximityVoiceChatManager` подписка на `VoiceChatSettings.MicrophoneChanged` → `rtcAudioSource?.SwitchMicrophone(selection)`.

---

### 4. Mute proximity при Private/Community call ✅ РЕАЛИЗОВАНО

**Приоритет:** Средний  
**Сложность:** Средняя  
**Зависимости:** #2 (Mute/Unmute)

**Реализация:**  
- `ProximityVoiceChatManager` подписан на `callStatus`
- `VOICE_CHAT_IN_CALL` → `stateModel.Suppress()` → Blocked (запоминает pre-blocked state)
- `DISCONNECTED` / `ENDING_CALL` → `stateModel.Resume()` → восстанавливает предыдущее состояние
- `ProximityNametagsHandler` также подавляет nametags при активном звонке

---

### 5. Звуковой фидбек Mute/Unmute — НЕ РЕАЛИЗОВАНО

**Приоритет:** Низкий  
**Сложность:** Минимальная  
**Зависимости:** #2 (Mute/Unmute)

**Текущее состояние:** Не перенесён. `MicrophoneAudioToggleHandler` можно подключить к `ProximityVoiceChatStateModel.State` при необходимости.

---

### 6. macOS Microphone Permissions ✅ РЕАЛИЗОВАНО

**Приоритет:** Средний  
**Сложность:** Низкая  
**Зависимости:** нет

**Реализация:**  
`VoiceChatPermissions.GuardAsync(ct)` вызывается перед `MicrophoneRtcAudioSource.New()` на macOS в `PublishLocalTrackAsync()`.

---

### 7. Reconnection retry ✅ РЕАЛИЗОВАНО

**Приоритет:** Низкий  
**Сложность:** Низкая-средняя  
**Зависимости:** нет

**Реализация:**  
`ActivateWithRetryAsync` с `MaxReconnectionAttempts` (default 3) и `ReconnectionDelayMs` (default 2000ms). Также `STREAM_READY_MAX_ATTEMPTS = 5` с `STREAM_READY_DELAY_MS = 100ms` для ожидания готовности AudioStream.

---

### 8. Мьют заблокированных игроков (social block) — НЕ РЕАЛИЗОВАНО

**Приоритет:** Средний  
**Сложность:** Низкая-средняя  
**Зависимости:** нет

**Проблема:**  
Игроки, заблокированные через Block User в контекстном меню профиля, продолжают быть слышны в proximity voice chat. `ProximityMuteService` обрабатывает только proximity-специфичный мьют — глобальный social block не учитывается.

**Решение:**  
При создании remote AudioSource (или при обновлении block-списка) проверять, заблокирован ли участник. Заблокированные игроки должны быть замьючены аналогично proximity mute.

---

## Фичи, которые НЕ переносим

| Фича | Причина |
|------|---------|
| **VoiceChat Panel UI** (Private/Community) | Намеренно — proximity не требует UI панели |
| **Роли (Speaker/Listener/Moderator)** | Не применимо — все равны в proximity |
| **Request to speak / Promote / Kick** | Модерация Community-формата |
| **RPC-сервисы** (RPCPrivateVoiceChatService и т.д.) | Proximity живёт в Island Room |
| **PlaybackSourcesHub** | Non-spatial playback для Community |
| **Отдельная VoiceChat Room** | Proximity использует Island Room |
| **VoiceChatParticipantsStateService** | UI-ориентирован (списки участников, roles) |
| **VoiceChatCallTypeValidator** | Private/Community специфика |

---

## Сводная таблица

| # | Фича | Приоритет | Сложность | Статус |
|---|------|-----------|-----------|--------|
| 1 | Nametag speaking indicator | Высокий | Низкая | ✅ Реализовано |
| 2 | Mute/Unmute + PTT | Высокий | Средняя | ✅ Реализовано |
| 3 | Смена микрофона в рантайме | Средний | Низкая | ✅ Реализовано |
| 4 | Mute proximity при Community call | Средний | Средняя | ✅ Реализовано |
| 5 | Звуковой фидбек mute/unmute | Низкий | Минимальная | ❌ Не реализовано |
| 6 | macOS permissions guard | Средний | Низкая | ✅ Реализовано |
| 7 | Reconnection retry | Низкий | Низкая-средняя | ✅ Реализовано |
| 8 | Мьют заблокированных (social block) | Средний | Низкая-средняя | ❌ Не реализовано |
