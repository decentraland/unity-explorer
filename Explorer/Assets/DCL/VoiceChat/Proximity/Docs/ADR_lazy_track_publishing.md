# ADR: Lazy Track Publishing — Publish on First Speaking, Auto-Unpublish on Idle

**Status:** Accepted
**Date:** 2026-03-31 (updated 2026-04-01)
**Authors:** Voice Chat team

---

## Context

В текущей реализации Proximity Voice Chat локальный аудио-трек публикуется при входе в состояние `Hearing` (т.е. при подключении к Island Room), даже если пользователь ни разу не включал микрофон. Микрофон при этом немедленно останавливается (`rtcAudioSource.Stop()`), но трек остаётся опубликованным в LiveKit.

### Текущий flow

```
Connect to Island Room
  → State: Hearing
    → ActivateWithRetryAsync()
      → PublishLocalTrackAsync()   ← публикуем трек
        → rtcAudioSource.Start()   ← включаем микрофон
      → SubscribeToExistingRemoteTracks()
      → rtcAudioSource.Stop()      ← сразу выключаем микрофон (не Speaking)

  → State: Speaking (user presses mic)
    → rtcAudioSource.Start()       ← просто включаем микрофон, трек уже опубликован
```

### Проблемы

1. **Лишний сигнальный трафик.** Каждый подключённый участник создаёт `TrackPublication` в LiveKit, даже если никогда не будет говорить. Это генерирует сигнальный трафик (метаданные трека, SDP negotiation) без пользы.

2. **`HasPublishedAudioTrack` не является надёжным сигналом.** Наличие опубликованного аудио-трека у участника не означает, что он использует микрофон. Это делает невозможным использование `TrackPublication` как индикатора "этот аватар использует микрофон" для UI-элементов (nametag иконки, lip sync hints и т.д.).

3. **Избыточная инициализация.** `MicrophoneRtcAudioSource` создаётся и запускается при входе в Hearing, затем немедленно останавливается. Это лишний цикл init/start/stop.

---

## Decision

Принят **вариант A: Lazy Publish** — публикация локального трека откладывается до первого перехода в состояние `Speaking`.

### Новый flow

```
Connect to Island Room
  → State: Hearing
    → SubscribeToExistingRemoteTracks()    ← только подписка на чужие треки
    → (трек НЕ публикуется, микрофон НЕ создаётся)

  → State: Speaking (first time)
    → PublishLocalTrackAsync()             ← публикуем трек
      → rtcAudioSource.Start()            ← включаем микрофон
    → published = true
    → idleTimer.Reset()

  → State: Hearing (stop speaking)
    → rtcAudioSource.Stop()               ← останавливаем микрофон
    → (трек остаётся опубликованным)
    → idleTimer starts counting

  → State: Speaking (subsequent, before idle timeout)
    → rtcAudioSource.Start()              ← просто включаем микрофон
    → idleTimer.Reset()

  → Idle timeout expires (user hasn't spoken for N minutes)
    → UnpublishLocalTrack()               ← убираем трек
    → published = false
    → (nametag icon disappears)

  → State: Speaking (after idle unpublish)
    → PublishLocalTrackAsync()             ← повторная публикация
    → idleTimer.Reset()
```

### Ключевые изменения

1. **`ActivateWithRetryAsync` разделяется на две части:**
   - `SubscribeToRemoteTracksAsync()` — вызывается при входе в `Hearing` (подписка на remote-треки)
   - `PublishLocalTrackAsync()` — вызывается при первом входе в `Speaking` (публикация + старт микрофона)

2. **`HasPublishedAudioTrack` становится надёжным сигналом:** если у remote-участника есть опубликованный аудио-трек, значит он хотя бы раз включал микрофон. Это можно использовать для UI (nametag иконки: dots/wave для не-мьютнутых, hushed для мьютнутых).

3. **`published` flag** сохраняет текущую семантику — предотвращает повторную публикацию при последующих переходах в `Speaking`.

4. **Auto-unpublish по idle таймауту:** если пользователь не переходит в `Speaking` в течение настраиваемого периода (например, 5 минут) после последнего speaking, трек автоматически unpublish-ится. Это освобождает сигнальные ресурсы для долго молчащих участников. При следующем нажатии на микрофон трек публикуется заново (с одноразовой задержкой).

---

## Alternatives Considered

### B. Full Dynamic — publish/unpublish при каждом Speaking/StopSpeaking

- Трек существует только пока микрофон включён
- **Отклонён:** высокая сложность, publish/unpublish churn при частых переключениях, задержка на каждое включение микрофона, потенциальные race conditions, сложнее дебажить

### C. Keep current + track Speaking state separately

- Публикация при Hearing остаётся как есть
- Для nametag иконок заводится отдельный `HashSet<string>` "кто хотя бы раз говорил"
- **Отклонён:** сигнальный трафик остаётся, дублирование логики между ProximityVoiceChatManager и ProximityNametagsHandler, `TrackPublication` теряет смысл как сигнал

### D. Использовать Participant Metadata/Attributes для передачи speaking-флага

LiveKit предоставляет два механизма для кастомных данных участника:

- **`Participant.Metadata`** (string, обычно JSON) — обновляется через `room.UpdateLocalMetadata(json)`, все участники получают `UpdateFromParticipant.MetadataChanged`. На Island Room уже используется для `IslandMetadata` (координаты парсела, lambdas endpoint). На VoiceChat Room — для `ParticipantCallMetadata` (name, muted, role и т.д.).

- **`Participant.Attributes`** (`IReadOnlyDictionary<string, string>`) — key-value пары, не используются в проекте. Потенциально можно добавить `{"proximity_mic": "true"}` без конфликтов с существующими metadata.

- **Отложено:** с вариантом A (lazy publish) + idle unpublish наличие `TrackPublication` с `KindAudio` уже является надёжным сигналом "этот участник активно использует микрофон". Добавление кастомного флага в metadata/attributes было бы дублированием. Может быть полезно в будущем для передачи дополнительной информации (например, preferred spatial audio radius, voice effects и т.д.), но на текущем этапе не оправдано.

---

## Consequences

### Positive

- Нет лишнего сигнального трафика для non-speakers
- `TrackPublication` = "активно использует микрофон" — чистый сигнал для UI без дополнительных metadata/attributes
- Микрофон не инициализируется до реальной необходимости
- Idle unpublish освобождает ресурсы для долго молчащих участников
- Не нужен кастомный speaking-флаг в Participant.Metadata/Attributes — TrackPublication покрывает потребность

### Negative

- Одноразовая задержка при нажатии на микрофон после idle unpublish (publish + SDP negotiation)
- Нужно разделить `ActivateWithRetryAsync` на два метода
- Idle timer добавляет дополнительное состояние для отслеживания

### Neutral

- Для remote-участников ничего не меняется — подписка на их треки происходит по-прежнему при входе в Hearing
- Existing remote tracks подхватываются сразу при подключении

---

## Implementation Notes

### Файлы, требующие изменений

- `ProximityVoiceChatManager.cs` — разделение `ActivateWithRetryAsync`, перенос `PublishLocalTrackAsync` из `Hearing` в `Speaking`, idle timer логика
- `ProximityVoiceChatStateModel.cs` — уже исправлен: `Suppress()` теперь запоминает реальное состояние (включая `Speaking`)
- `ProximityNametagsHandler.cs` — уже исправлен: `ApplyNametagStateToConnectedParticipants()` использует `HasPublishedAudioTrack` для определения кому показывать nametag
- `VoiceChatConfiguration` (или `ProximityAudioSettings`) — добавить настройку idle timeout (default: 5 min, TBD)

### Idle Timer

- Таймер стартует при переходе `Speaking → Hearing` (микрофон выключен, трек опубликован)
- Таймер сбрасывается при каждом переходе в `Speaking`
- Таймер отменяется при `Suppress` / `Disconnect`
- По истечении таймера: `UnpublishLocalTrack()`, `published = false`
- При следующем `Speaking`: повторная публикация через `PublishLocalTrackAsync()`

### Порядок состояний при вызове (Private/Community call)

```
Speaking → Suppress() → Blocked (preBlockedState = Speaking)
  → idleTimer cancelled
  → Call ends → Resume() → Speaking (restored)
    → rtcAudioSource.Start() (трек уже опубликован)
    → idleTimer.Reset()
```

```
Hearing → Suppress() → Blocked (preBlockedState = Hearing)
  → idleTimer cancelled
  → Call ends → Resume() → Hearing (restored)
    → (ничего не публикуется, микрофон не трогается)
    → idleTimer resumes (if was published)
```

### Metadata / Attributes — Future Considerations

На текущем этапе `TrackPublication` покрывает потребность в сигнале "использует микрофон". Если в будущем потребуется передавать дополнительную информацию (spatial audio radius, voice effects preferences, speaking intent), можно использовать:

- `Participant.Attributes` — предпочтительный вариант, key-value формат, не конфликтует с существующими `Metadata` (которые заняты `IslandMetadata` на Island Room)
- `Participant.Metadata` — требует расширения `IslandMetadata` struct, рискует сломать парсинг у других клиентов
