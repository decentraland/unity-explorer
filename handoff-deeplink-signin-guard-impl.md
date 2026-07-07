# Handoff — Реализация «Guard на signin» (дизайн УТВЕРЖДЁН)

**Дата:** 2026-07-01
**Ветка:** `chore/authentication/deep-link-login`
**Режим:** РЕАЛИЗАЦИЯ. Grilling завершён, все ветки дизайна разрешены. Следующая сессия
пишет C# код и тесты по плану ниже. Другой агент.

> Перед C#-правками — скилл **code-standards** (naming, member ordering, nullable, GC) и
> **async-programming** (UniTask, CT, `SuppressToResultAsync`). Линт-хук `Stop` гоняет
> ReSharper по изменённым `.cs` — закрывать находки до завершения.

---

## Контекст (НЕ переоткрывать — ссылки)

Корень бага, механизм deep-link, карта кода и кросс-репо контракт с Launcher уже
разобраны — **читать `C:/DCL/unity-explorer/handoff-deeplink-signin-guard.md`**
(разделы «Что уже разобрано», «Точная карта кода», «Кросс-репо контракт»). Там же LR-артефакты
(`~/.claude/projects/C--DCL-unity-explorer/teach/learning-records/0002..0004`).

**Суть в одну строку:** залогиненный in-world инстанс поллит bridge весь lifetime, выигрывает
200-мс гонку, **удаляет** signin-файл и диспатчит в диспатчер без подписчика → значение теряется,
а инстанс, который реально логинится, файл уже не видит → висит 300с.

**Требование пользователя:** signin-файл НЕ консьюмить, если никто не ждёт. Файл может быть
переписан Launcher'ом сверху — это ОК, гонку двух логинящихся инстансов НЕ решаем.[deeplink-bridge.json](../../Users/popuz/AppData/Local/DecentralandLauncherLight/deeplink-bridge.json)

---

## Утверждённые решения (это была секция «Открытые вопросы» старого handoff)

1. **Определение «логин ждёт signin» = `current != null`** (live-подписчик). Файл на диске
   становится буфером. → **`bufferedIdentityId` удаляется полностью.** Узкое окно
   OpenUrl→Subscribe игнорируем (недостижимо, browser round-trip — секунды).
2. **Контракт `HandleDeepLink` → `bool consumed`** (`true` = удалить файл). НЕ enum, НЕ Result.
3. **Спам от defer-ре-лупа:** убрать все `[DLDBG]` `Debug.Log`; defer-путь **молчит** (это
   нормальный «не мне» исход, не ошибка). Дедуп по контенту НЕ добавляем.
4. **IOException-скоуп: только `try/catch` вокруг `File.Delete`** (лог + continue). Чтение
   оставить как есть (`SuppressToResultAsync`). `FileShare` НЕ трогаем.

---

## План правок (порядок: dispatcher → handle/interface → sentinel → тесты)

Все пути от `C:/DCL/unity-explorer/Explorer/`.

### `Assets/DCL/RuntimeDeepLink/IDeeplinkSigninDispatcher.cs` + `DeeplinkSigninDispatcher.cs`
- Добавить в интерфейс + реализацию: `bool HasSubscriber => current != null;`
- Удалить поле `bufferedIdentityId`. `Dispatch` → только `current?.Handler(identityId);`.
  `Subscribe` больше НЕ реплеит буфер. `Remove` чистит только `current`.
- Убрать `[DLDBG]` `Debug.Log`. Параметры `expectedRequestId`/`sourceRequestId` (Stage 2,
  forward-compat) НЕ трогать.

### `Assets/DCL/RuntimeDeepLink/DeepLinkHandle.cs` (интерфейс + `Null`) + `DeepLinkHandleImplementation.cs`
- Сигнатура: `Result HandleDeepLink(DeepLink)` → `bool HandleDeepLink(DeepLink)`
  (`true` = consumed/удалить). Обновить xml-doc «exception free».
- `Null.HandleDeepLink` → `true`.
- Guard в реальной реализации:
  - `signin` непустой **и** `!deeplinkSigninDispatcher.HasSubscriber` → `return false`
    (defer, молча, файл остаётся);
  - есть подписчик → `Dispatch(signin)` + `return true`.
- Navigation / community / no-match → `return true` (никогда не оставлять → нет ре-лупа).
- Логирование success/error success переезжает **внутрь** метода (`ReportHub`, не Sentinel).
  Убрать `[DLDBG]`.

### `Assets/DCL/RuntimeDeepLink/DeepLinkSentinel.cs`
- **Реордер:** `DeepLink.FromJson` вызывать **до** `File.Delete`.
- Матрица файла:
  - read fail (`contentResult.Success == false`) → `continue` (как сейчас, транзиентный IO);
  - parse fail → `ReportHub.LogError` + `File.Delete`;
  - `HandleDeepLink` == `true` → `File.Delete`;
  - `HandleDeepLink` == `false` → оставить (defer).
- `File.Delete` обернуть в `try/catch` (лог + continue), защита UniTaskVoid-лупа от смерти.
- Убрать все `[DLDBG]` `Debug.Log`.

### Blast radius (проверено Grep)
Единственный вызывающий `HandleDeepLink` вне реализаций — Sentinel.
`RuntimeDeepLinkPlayground.cs` использует только `Null` — не трогать.
`BootstrapContainer.cs` / `DappWeb3Authenticator*.cs` — потребители диспатчера,
сигнатуру `HasSubscriber` не ломают (только добавление).

---

## Тесты (скилл **testing-infrastructure**, NUnit + NSubstitute)

- `DeeplinkSigninDispatcher`: `Dispatch` без подписчика НЕ зовёт handler и `HasSubscriber == false`;
  с подписчиком — зовёт handler.
- `DeepLinkHandle`: signin без ждущего → `false` (не диспатчит); signin с ждущим → `true` +
  `Dispatch`; navigation → `true`.
- Ручной репро гонки: см. LR-0004; Editor-хелпер `_Scratch/DeeplinkBackendTester.cs`.

## Осознанно вне скоупа
Гонка двух одновременно логинящихся инстансов; машинно-глобальный путь файла (полный фикс —
суффикс `requestId`/PID, кросс-репо с `launcher-rust`, отдельная задача); `FileShare` на чтении.

---

## Suggested skills
- **code-standards** — ОБЯЗАТЕЛЬНО перед любым C#.
- **async-programming** — Sentinel-цикл, CT, `SuppressToResultAsync`, `OperationCanceledException`.
- **testing-infrastructure** — тесты на dispatcher/handle.
- (справка) `handoff-deeplink-signin-guard.md` — фон/корень бага/карта кода; LR в
  `~/.claude/projects/C--DCL-unity-explorer/teach/`.
