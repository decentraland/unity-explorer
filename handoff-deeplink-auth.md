# Handoff — Deep-link авторизация (Unity Explorer)

**Дата:** 2026-06-30
**Ветка:** `chore/authentication/auth-login-jwt-improvement-2`
**Режим работы:** РАЗРАБОТКА — следующая сессия пишет/правит C# код. Предыдущая
сессия была разбором механизма (артефакты — в разделе «Справочные материалы»);
понимание уже зафиксировано, теперь реализация.

> Перед любыми правками C# — следовать скиллу **code-standards** (naming, member
> ordering, formatting, nullable, async-паттерны). Линт-хук `Stop` гоняет ReSharper
> по изменённым `.cs` — закрывать его находки до завершения.

---

## Контекст за одну минуту

Decentraland переходит со старого логина (verification-code + WebSocket) на
**deep-link авторизацию** (ADR-288). Виталий реализует desktop-сторону приёма в Unity
Explorer (C#). Механизм уже разобран сквозь PR/ADR и привязан к коду (см. маппинг
ниже). Дальше — кодовая работа на этой ветке.

⚠️ **Точная задача правок ещё не зафиксирована** — уточнить у Виталия в начале
сессии, что именно делаем (доработка `LoginViaDeeplinkAsync` / обработка edge-кейсов
/ зачистка dead-code / регистрация схемы для теста). Кандидаты — в «Следующих шагах».

---

## ⭐ Две главные находки (приоритет для следующей сессии)

### 1. Сокет для deep-link флоу не нужен
Новый флоу = два обычных HTTP-запроса, оба инициируются из Unity:
`POST /requests` (получить `requestId`) и `GET /identities/{identityId}` (забрать
AuthIdentity). Push-канал (socket.io / WebSocket) не требуется.
Сокет жив **только** в старом verification-code флоу (`LoginAsync2` + событие
`"outcome"`), который сейчас — **dead code**: в `LoginSelectionAuthState.cs` строка
со старым `IdentityVerificationDappAuthState` закомментирована (≈177), активна
строка с `IdentityVerificationDappDeepLinkAuthState` (≈178). Нет app-флага
переключения. ⇒ Потенциальная зачистка сокет-кода (`LoginAsync2`,
`IdentityVerificationDappAuthState`) как мёртвого.

### 2. Тестирование нешипнутого билда — регистрация схемы
В Editor / PR-билде `decentraland://` не зарегистрирована в ОС, `deeplink-bridge.json`
пишет только лаунчер `DecentralandLauncherLight` → deep link не возвращается →
`WaitForSigninAsync` виснет на `DEEPLINK_TIMEOUT_SECONDS = 300` → `TimeoutException`
→ `SignatureExpiredException`.
**Решение для тестирования билда:** зарегистрировать `decentraland://` на dev-машине
за конкретным билдом — по аналогии с creator-hub, который делает это через Electron
`app.setAsDefaultProtocolClient(DEEPLINK_PROTOCOL, process.execPath, [entryArg])` в
ветке `process.defaultApp` (dev-режим). Для Unity-билда — ручная регистрация схемы
(Windows: реестр `HKCU\Software\Classes\decentraland`; macOS: `CFBundleURLTypes` в
Info.plist) с указанием пути к exe билда.
**Альтернатива для Editor (быстрее):** `_Scratch/DeeplinkBackendTester.cs`
(`#if UNITY_EDITOR`) — контекст-меню «1. Connect + Request» → вставить `identityId`
в Inspector → «2. Dispatch identityId» (имитирует приход deep link через
`dispatcher.Dispatch(id)`) → «3. Fetch identity by id».

---

## Суть механизма (краткий ввод)

- **Проблема:** старый флоу → UX-трение (ручная сверка кода) + session fixation
  (канал привязан к `requestId` из URL, известному атакующему).
- **Решение:** обратную доставку `identityId` делает **ОС**, маршрутизируя
  `decentraland://open?signin={identityId}` в локальное приложение. Не по сети →
  привязка к машине пользователя.
- **Главный инсайт:** в deep link едет только одноразовый opaque **UUID v4**
  (`identityId`) — «билет». Сам AuthIdentity идёт отдельно по HTTPS. Вариант «весь
  identity в URL» отвергнут (объём, утечка в логи, нет серверной валидации).
- **Безопасность:** signed-fetch на `POST /identities`; TTL ≤ 15 мин (`410`);
  single-use (`404` после забора); OS-routing + HTTPS.

**Сквозной флоу (7 шагов):** Client `POST /requests`→`requestId` → открыть браузер
`…/auth/requests/{requestId}?flow=deeplink` → wallet → браузер `POST /identities`
→ `{identityId, expiration}` → браузер открывает `decentraland://open?signin={id}`
→ ОС → Client → `GET /identities/{id}`.

---

## Маппинг на C# (точки для правок)

| Слой | Файл | Роль |
|---|---|---|
| Приём (argv cold-start / `deeplink-bridge.json` running) | `Assets/DCL/RuntimeDeepLink/DeepLinkSentinel.cs` | Поллит bridge каждые 200мс (`CHECK_IN_PERIOD`), `DeepLink.FromJson()` |
| Роутинг | `Assets/DCL/RuntimeDeepLink/DeepLinkHandleImplementation.cs` | `ValueOf(AppArgsFlags.SIGNIN)` → `dispatcher.Dispatch` |
| Буфер | `Assets/DCL/RuntimeDeepLink/DeeplinkSigninDispatcher.cs` | Буферит `identityId`, отдаёт подписчику |
| Логин | `Assets/DCL/Web3/Authenticators/Implementations/Dapp/DappWeb3Authenticator.Deeplink.cs` | `LoginViaDeeplinkAsync`→`WaitForSigninAsync` (300с)→`FetchIdentityByIdAsync` |
| Флаг | `Assets/DCL/Infrastructure/Global/AppArgs/AppArgsFlags.cs` | `public const string SIGNIN = "signin";` |
| Старый сокет (dead) | `DappWeb3Authenticator.cs` `LoginAsync2`; `IdentityVerificationDappAuthState.cs` | Не вызывается |

---

## Ссылки (обязательно сохранить)

- **ADR-288** «Identity-Based Deep Link Authentication»: https://github.com/decentraland/adr/pull/312/files
- **auth#218** (web, foundational): https://github.com/decentraland/auth/pull/218
- **auth#412** (фикс impersonation — только `dcl_personal_sign` подписывает identity): https://github.com/decentraland/auth/pull/412
- **auth#413** (схема `dcl-creator-hub://`, параметризация deepLink): https://github.com/decentraland/auth/pull/413
- **creator-hub#1338** (эталон Electron-приёма: `setAsDefaultProtocolClient`, `open-url`, argv/`second-instance` на Windows): https://github.com/decentraland/creator-hub/pull/1338

---

## Справочные материалы (для онбординга, читать по необходимости)

Разбор механизма из прошлой сессии — полезен как контекст, **не предмет работы**:
`C:/Users/popuz/.claude/projects/C--DCL-unity-explorer/teach/`
- `reference/sequence.html` — URL-форматы, HTTP-эндпоинты, коды ошибок (404/410/403), тайминги. **Самое полезное при кодинге.**
- `reference/glossary.html` — термины (identityId, AuthIdentity, signed-fetch, OS-routing…).
- `learning-records/0002-socket-vs-deeplink-and-editor-testing.md` — детали по находкам №1 и №2.
- `lessons/0001-deeplink-auth-essence.html` — обзорный урок (для контекста).

Память: `~/.claude/projects/C--DCL-unity-explorer/memory/project_auth_deeplink_flow.md` (стоит обновить деталями ADR-288).

---

## Следующие шаги (кандидаты на реализацию — подтвердить у Виталия)

1. **Edge-кейсы приёма deep link** в `LoginViaDeeplinkAsync` / `WaitForSigninAsync`:
   двойной запуск, late subscriber (гонка буфера в `DeeplinkSigninDispatcher`),
   протухший/повторный билет (`GET /identities/{id}` → 404/410/403), поведение по
   таймауту 300с (`DEEPLINK_TIMEOUT_SECONDS`). Корректная отмена через CT и
   обработка исключений (`SuppressToResultAsync`, не глотать всё подряд).
2. **Зачистка dead-code сокет-флоу** (находка №1): `LoginAsync2` в
   `DappWeb3Authenticator.cs`, состояние `IdentityVerificationDappAuthState`,
   закомментированная ветка в `LoginSelectionAuthState.cs`. Сначала убедиться, что
   нигде не вызывается; затем удалить + проверить asmdef/InternalsVisibleTo/сборку.
3. **Регистрация `decentraland://` для теста билда** (находка №2): рецепт по аналогии
   с creator-hub (`setAsDefaultProtocolClient` в ветке `process.defaultApp`) —
   Windows-реестр `HKCU\Software\Classes\decentraland` / macOS `CFBundleURLTypes`.
   Для Editor — использовать существующий `_Scratch/DeeplinkBackendTester.cs`.
4. **Тесты** на новую логику (см. testing-infrastructure / UnitySystemTestBase, NSubstitute).

---

## Suggested skills (для разработки)

- **code-standards** — ОБЯЗАТЕЛЬНО перед любыми правками C#: naming, member ordering,
  formatting, nullable, GC/память, паттерны тестов. Линт-хук гоняет ReSharper по diff.
- **async-programming** — основной для этой задачи: `LoginViaDeeplinkAsync`,
  `WaitForSigninAsync` (UniTask, `CancellationToken`, таймауты, `SuppressToResultAsync`,
  обработка `OperationCanceledException` vs прочих через `ReportHub`).
- **testing-infrastructure** — при добавлении тестов на флоу/диспатчер.
- **plugin-architecture** — если правки затрагивают DI/контейнеры/wiring
  (`DynamicWorldContainer`, регистрация аутентификатора).
- **consolidate-assembly-definitions** — если зачистка dead-code затрагивает asmdef.
- (контекст) Учебные артефакты прошлой сессии — в разделе «Справочные материалы».
