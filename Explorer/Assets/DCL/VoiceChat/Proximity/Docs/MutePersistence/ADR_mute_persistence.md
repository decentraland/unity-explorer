# ADR: Persistent Mute for Proximity Voice Chat

**Status:** Implemented (Iteration 1)
**Date:** 2026-03-24
**Authors:** Voice Chat team

---

## Context

Proximity Voice Chat уже поддерживает mute/unmute отдельных игроков через `ProximityMuteService`. Изначальная реализация — in-memory `HashSet<string>`, без persistence. При перезапуске приложения все mute'ы сбрасывались.

### Требования

1. Mute-состояние должно сохраняться между сессиями (рестарт приложения)
2. Новая опция "Mute / Unmute" в контекстном меню профиля (уже реализована локально)
3. Игрок не должен получать voice-стримы от заблокированных и замьюченных пользователей
4. Реализация должна следовать паттернам, принятым в проекте

### Новый Backend API

Backend-команда развернула HTTP REST API на Social Service для управления мьютами:

| Метод | Endpoint | Описание |
|-------|----------|----------|
| `GET` | `/v1/mutes` | Пагинированный список замьюченных. Поддерживает фильтрацию по address |
| `POST` | `/v1/mutes` | Замьютить указанный address |
| `DELETE` | `/v1/mutes` | Снять мьют с указанного address |

Запросы требуют подписи (signed fetch). Хранение на стороне сервера — "the same way we do with blocked players".

**Формат ответа GET:**
```json
{
  "data": {
    "results": [{ "address": "string", "muted_at": "date-time" }],
    "total": 0, "limit": 0, "offset": 0, "page": 0, "pages": 0
  }
}
```

**Тело POST / DELETE:** `{ "muted_address": "string" }`, ответ — 204 No Content.

---

## Существующие паттерны в проекте

### Blocked Players (RPC-based)

Наиболее близкий аналог. Архитектура:

```mermaid
flowchart TD
    RPCFriends["RPCFriendsService<br/>(gRPC/WebSocket)"] -->|BlockUser/UnblockUser<br/>GetBlockedUsers| SocialServiceBE["Social Service BE"]
    RPCFriends -->|events| EventBus["IFriendsEventBus"]
    EventBus -->|OnYouBlockedProfile<br/>OnYouUnblockedProfile| Cache["UserBlockingCache"]
    Cache -->|UserBlocked/UserUnblocked events| UI["Context Menu, Chat, etc."]
    SocialServiceBE -->|SubscribeToBlockUpdates<br/>(server push stream)| RPCFriends
```

**Ключевые особенности:**
- Коммуникация через **RPC** (gRPC over WebSocket), не REST
- **Real-time sync** через server push stream (`SubscribeToBlockUpdates`)
- **In-memory cache** (`UserBlockingCache`) с событиями
- Загрузка при старте через `BlocklistCheckStartupOperation`
- Двунаправленная блокировка (ты блокируешь / тебя блокируют)

### Communities (HTTP REST-based)

Пример CRUD через REST с signed fetch:

```csharp
// GET
await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
    .CreateFromJson<TResponse>(WRJsonParser.Newtonsoft);

// POST с JSON body
await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateJson(body), string.Empty, ct)
    .CreateFromJson<TResponse>(WRJsonParser.Newtonsoft);

// DELETE (без body)
await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
    .CreateFromJson<TResponse>(WRJsonParser.Newtonsoft);
```

Использует `IWebRequestController` + `IWeb3IdentityCache` + `IDecentralandUrlsSource`.

---

## Island Room и частота реконнектов

Proximity Voice Chat работает через Island Room. Анализ жизненного цикла:

| Сценарий | Reconnect? | Частота |
|----------|-----------|---------|
| Перемещение между парселями (тот же realm) | Нет | — |
| Смена realm (Genesis → World) | Да, полный (Stop → Restart) | Редко, только по телепорту |
| Сервер переназначил island (архипелаг) | Прозрачный room switch | Когда игрок уходит далеко от текущей группы |
| Сбой сети | Авто-reconnect через 5 сек | Редко |

**Механизм:** каждую ~1 сек `ArchipelagoIslandRoom` отправляет heartbeat с позицией. Если сервер назначает новый island — присылает `IslandChangedMessage` с новым connection string. `ConnectiveRoom` переключается прозрачно.

**Вывод для мьютов:** mute привязан к wallet address, а не к room/island. Список мьютов остаётся валидным при любых переключениях комнат. Reconnect'ы Island Room редки — кэш, загруженный при старте, актуален весь сеанс.

---

## Рассмотренные варианты

### Вариант 1: Cache-First (загрузка при старте) ✅ ВЫБРАН

Загрузка полного списка мьютов при старте приложения. In-memory кэш, синхронизация при локальных изменениях.

**Плюсы:**
- Консистентность с паттерном `UserBlockingCache`
- Мгновенные lookup'ы при подключении участников
- Бэкенд-инженер рекомендует: "if you request the muted users on the startup, the API allows an easy way to go through the paginated list without any issues"
- Работает корректно даже при временной недоступности API (кэш уже загружен)
- Минимальная сложность

**Минусы:**
- Не обновляется если мьют установлен с другого устройства (не в приоритете)
- Пагинация при старте если много мьютов (маловероятно для большинства пользователей)

### Вариант 2: On-Demand (запрос при подключении участника)

Запрос статуса мьюта при появлении каждого участника в Island Room.

**Плюсы:**
- Свежие данные для каждого нового участника
- Минимальный startup cost

**Минусы:**
- Сетевая задержка — участник слышен до получения ответа
- Бэкенд-инженер предупредил: "we might need to change it" если запрашивать по участникам
- Больше запросов при частой смене участников
- Не работает при недоступности API

### Вариант 3: Hybrid (Cache + Background Refresh)

Cache-First + фоновое обновление при смене realm/island.

**Плюсы:**
- Лучшая актуальность данных
- Graceful degradation

**Минусы:**
- Избыточная сложность на текущем этапе
- Multi-device sync не в приоритете

---

## Decision

**Выбран Вариант 1 (Cache-First)** по следующим причинам:

1. **Консистентность с проектом** — `UserBlockingCache` использует аналогичный паттерн
2. **Рекомендация бэкенда** — API оптимизирован для загрузки полного списка при старте
3. **Island Room стабилен** — reconnect'ы редки, кэш валиден весь сеанс
4. **Multi-device не в приоритете** — достаточно persistence между рестартами
5. **Расширяемость** — можно перейти к Варианту 3, добавив refresh при realm change, без ломки существующей архитектуры

### Отличие от Blocked Players

| Аспект | Blocked Players | Mute |
|--------|----------------|------|
| Протокол | gRPC/WebSocket (RPC) | HTTP REST (signed fetch) |
| Real-time sync | Server push stream | Нет (только локальные изменения) |
| Направленность | Двусторонняя (блок/блокнут) | Односторонняя (только мьют) |
| Scope | Глобальный (чат, профили, voice) | Только Proximity Voice Chat |

### Инфраструктурные изменения

DELETE-эндпоинт мьютов требует тело запроса (`{ "muted_address": "..." }`). Существующий `SignedFetchDeleteAsync` не поддерживал body — добавлен новый overload в `SignedWebRequestControllerExtensions.cs`, принимающий `GenericPostArguments`. Это возможно, т.к. `GenericDeleteRequest` внутренне использует `GenericPostRequest.CreateWebRequest` (с upload handler) и просто меняет HTTP-метод на DELETE.

---

## Consequences

- `ProximityMuteService` рефакторнут в фасад над `IProximityMuteCache` + `IProximityMuteRepository`
- Добавлен `IProximityMuteCache` с событиями (аналог `IUserBlockingCache`)
- При старте приложения выполняется HTTP-запрос для загрузки мьютов (в `VoiceChatPlugin.InitializeAsync`)
- Mute/Unmute из контекстного меню — async (запрос к API, при успехе обновляется кэш, при ошибке — кэш не меняется)
- Путь расширения к Варианту 3: добавить refresh в `OnConnectionUpdated` или при realm change

### Рассмотренные, но отложенные улучшения

**Локальный persist кэша (PlayerPrefs/файл):**
При старте загружать последнее известное состояние из локального хранилища, а затем обновлять из API. Это даёт мгновенный старт с мьютами даже при недоступности BE. Отложено, потому что добавляет проблему синхронизации локального состояния с сервером, а multi-device не в приоритете.

**Фоновая загрузка (не блокировать InitializeAsync):**
Запустить `LoadAsync` как fire-and-forget вместо `await`. Это ускорит инициализацию плагина. Отложено, потому что создаёт окно (несколько секунд), когда мьюты ещё не загружены и пользователь слышит замьюченных. Один HTTP-запрос параллельно с другими startup-операциями — задержка минимальна, а гарантия "мьюты готовы до первого аудио" ценнее.

**Текущее поведение при недоступности BE:**
`LoadAsync` ловит все исключения и логирует warning — кэш остаётся пустым, proximity chat работает как будто никто не замьючен. `SetMutedAsync` при ошибке API не обновляет кэш — мьют не применяется, что логично: нет смысла мьютить локально, если нельзя сохранить на сервере (при рестарте сбросится).
