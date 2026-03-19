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

---

## Фичи для переноса

### 1. Индикатор говорящего в Nametags

**Приоритет:** Высокий  
**Сложность:** Низкая (~50 строк)  
**Зависимости:** нет

**Текущее состояние в VoiceChat:**  
`VoiceChatNametagsHandler` слушает `ActiveSpeakers.Updated` от VoiceChat Room, ставит `VoiceChatNametagComponent` на entity аватара. `NametagPlacementSystem` читает компонент и включает CSS-класс `nametag--voice-chat` — появляется анимированный badge с тремя зелёными столбиками.

**Проблема:**  
`VoiceChatNametagsHandler` слушает **только** `roomHub.VoiceChatRoom().Room()` (Community/Private), а **не** Island Room. Proximity speaking status не попадает в nametags.

При этом в `VoiceChatPlugin` уже есть подписка на `islandRoom.ActiveSpeakers.Updated` (заполняет `proximityConfigHolder.SpeakingParticipants`). Данные есть — не пробрасываются в ECS.

**Решение:**  
Создать `ProximityNametagsHandler`, который:
- Подписывается на `islandRoom.ActiveSpeakers.Updated`
- Через `entityParticipantTable.TryGet()` находит entity аватара
- Ставит `VoiceChatNametagComponent(isSpeaking: true/false)`

Компонент `VoiceChatNametagComponent`, `NametagPlacementSystem`, `NametagElement` и CSS-анимация — полностью переиспользуемы, ноль изменений в UI.

**Нюанс приоритета:** если одновременно активен Community VoiceChat и Proximity, оба пишут в один компонент. Варианты:
- Proximity пишет только когда Community call не активен
- OR-логика: говорит хоть где-то — показываем

**Ключевые файлы:**
- `VoiceChat/VoiceChatNametagsHandler.cs` — образец
- `VoiceChat/VoiceChatNametagComponent.cs` — переиспользуем as-is
- `NameTags/Systems/NametagPlacementSystem.cs` — уже читает компонент
- `NameTags/NametagElement.cs` — CSS `nametag--voice-chat`

---

### 2. Управление микрофоном (Mute/Unmute + PTT)

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

**Решение:**  
Добавить mute/unmute в `ProximityVoiceChatManager`:
- Хранить ссылку на `MicrophoneRtcAudioSource`
- `MuteMicrophone()` → `rtcAudioSource.Stop()`
- `UnmuteMicrophone()` → `rtcAudioSource.Start()`
- Экспонировать `ReactiveProperty<bool> IsMicrophoneEnabled`

Для PTT — переиспользовать логику из `VoiceChatMicrophoneHandler` (hotkey binding + hold threshold). Вынести в общий класс или адаптировать.

**Нюанс:** `VoiceChatMicrophoneHandler` привязан к `ICommunityCallOrchestrator` (метод `NotifyMicrophoneStateChange` шлёт mute на сервер Community). Для proximity серверный mute не нужен — достаточно локального `Stop()/Start()`.

---

### 3. Смена микрофона в рантайме

**Приоритет:** Средний  
**Сложность:** Низкая (~15 строк)  
**Зависимости:** нет

**Текущее состояние в VoiceChat:**  
`VoiceChatMicrophoneHandler` подписывается на `VoiceChatSettings.MicrophoneChanged` и вызывает `MicrophoneRtcAudioSource.SwitchMicrophone(newSelection)`.

**Проблема:**  
Proximity создаёт микрофон один раз в `PublishLocalTrack()`. При смене устройства в настройках — proximity продолжает использовать старый микрофон.

**Решение:**  
В `ProximityVoiceChatManager`:
```csharp
VoiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;

private void OnMicrophoneChanged(MicrophoneSelection selection)
{
    rtcAudioSource?.SwitchMicrophone(selection);
}
```

---

### 4. Mute proximity при Private/Community call

**Приоритет:** Средний  
**Сложность:** Средняя  
**Зависимости:** #2 (Mute/Unmute)

**Текущее состояние в VoiceChat:**  
`VoiceChatOrchestrator` координирует Private и Community — нельзя быть в двух звонках одновременно.

**Проблема:**  
Proximity всегда активен независимо от Community/Private. Если пользователь в Community call — proximity аудио продолжает транслироваться.

**Решение:**  
Подписаться на `voiceChatOrchestrator.CurrentCallStatus`:
- `VOICE_CHAT_IN_CALL` → suppress proximity (mute микрофон + опционально mute playback)
- `DISCONNECTED` / `ENDING_CALL` → resume proximity

Вариант реализации:
```csharp
voiceChatOrchestratorState.CurrentCallStatus.Subscribe(status =>
{
    switch (status)
    {
        case VoiceChatStatus.VOICE_CHAT_IN_CALL:
            SuppressProximity();
            break;
        case VoiceChatStatus.DISCONNECTED:
        case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
            ResumeProximity();
            break;
    }
});
```

---

### 5. Звуковой фидбек Mute/Unmute

**Приоритет:** Низкий  
**Сложность:** Минимальная  
**Зависимости:** #2 (Mute/Unmute)

**Текущее состояние в VoiceChat:**  
`MicrophoneAudioToggleHandler` проигрывает звуковые клипы при переключении микрофона — пользователь слышит тональный сигнал.

**Решение:**  
Переиспользовать `MicrophoneAudioToggleHandler` — он подписывается на `IsMicrophoneEnabled` ReactiveProperty. Когда у proximity появится ReactiveProperty из пункта #2, подключить тот же handler.

---

### 6. macOS Microphone Permissions

**Приоритет:** Средний  
**Сложность:** Низкая  
**Зависимости:** нет

**Текущее состояние в VoiceChat:**  
`VoiceChatPermissions` — P/Invoke для `RequestMicrophonePermission()` / `CurrentMicrophonePermission()`. `GuardAsync` ждёт разрешения перед публикацией.

**Проблема:**  
`PublishLocalTrack()` создаёт `MicrophoneRtcAudioSource` без проверки permissions. На macOS — сбой или молчащий микрофон.

**Решение:**  
Добавить guard перед `MicrophoneRtcAudioSource.New()`:
```csharp
if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
{
    await VoiceChatPermissions.GuardAsync(ct);
}
```

---

### 7. Reconnection retry

**Приоритет:** Низкий  
**Сложность:** Низкая-средняя  
**Зависимости:** нет

**Текущее состояние в VoiceChat:**  
`VoiceChatReconnectionManager` — автоматические retry с `MaxReconnectionAttempts` и `ReconnectionDelayMs`.

**Проблема:**  
При `ConnectionUpdate.Disconnected` proximity вызывает `Deactivate()`. Reconnection зависит от Island Room — но на уровне proximity нет retry для publish.

**Решение:**  
- Retry публикации микрофона при ошибке (с delay и max attempts)
- Логирование причин отключения через `VoiceChatDisconnectReasonHelper`

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

| # | Фича | Приоритет | Сложность | Зависимости | Итерация |
|---|------|-----------|-----------|-------------|----------|
| 1 | Nametag speaking indicator | Высокий | Низкая | — | 3 |
| 2 | Mute/Unmute + PTT | Высокий | Средняя | — | 3 |
| 3 | Смена микрофона в рантайме | Средний | Низкая | — | 3 |
| 4 | Mute proximity при Community call | Средний | Средняя | #2 | 3 |
| 5 | Звуковой фидбек mute/unmute | Низкий | Минимальная | #2 | 3 |
| 6 | macOS permissions guard | Средний | Низкая | — | 3 |
| 7 | Reconnection retry | Низкий | Низкая-средняя | — | 3 |
