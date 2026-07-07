# Handoff — Реализация «Guard на signin» (не потреблять чужой bridge-файл)

**Дата:** 2026-07-01
**Ветка:** `chore/authentication/auth-login-jwt-improvement-2`
**Режим:** РАЗРАБОТКА. Задача выбрана и обсуждена; следующая сессия проектирует детали
и пишет C# код. На момент паузы шёл **brainstorming** (стадия уточняющих вопросов) —
дизайн ещё НЕ утверждён и НЕ записан в spec.

> Перед C#-правками — скилл **code-standards** (naming, member ordering, nullable, GC) и
> **async-programming** (UniTask, CT, `SuppressToResultAsync`). Линт-хук `Stop` гоняет
> ReSharper по изменённым `.cs` — закрывать находки до завершения.

---

## Что уже разобрано (НЕ переоткрывать — ссылки)

Механизм deep-link и корень бага полностью разобраны в прошлой сессии:
- `~/.claude/projects/C--DCL-unity-explorer/teach/learning-records/0004-bridge-race-two-processes.md`
  — **корневой диагноз** гонки (два процесса на один машинно-глобальный файл).
- `.../0003-launcher-single-instance-bridge.md` — протокол ACK: **Explorer** читает и
  удаляет bridge, Launcher лишь ждёт исчезновения файла (таймаут ~3с, `remove_file`
  только при отмене/таймауте → `E3001`).
- `.../0002-socket-vs-deeplink-and-editor-testing.md` — почему в Editor линк не приходит,
  тест через `_Scratch/DeeplinkBackendTester.cs`.
- Прошлый handoff: `C:/DCL/unity-explorer/handoff-deeplink-auth.md` (общий флоу, маппинг на код, ссылки на ADR-288 / auth#218,#412,#413 / creator-hub#1338).

**Суть бага в одну строку:** залогиненный in-world инстанс поллит bridge весь lifetime
(нужно для navigation deep links), поэтому выигрывает 200-мс гонку, **удаляет** файл и
**Dispatch'ит signin в диспатчер без подписчика** → значение буферизуется и теряется, а
инстанс, который реально логинится (напр. Editor), файл уже не видит → висит 300с.

---

## Выбранное решение: Guard на signin

Не потреблять `signin`-deep-link, если **никакой логин его не ждёт**: не удалять файл,
не буферить, не диспатчить. Оставить файл — его заберёт тот инстанс, что реально логинится
(или Launcher уберёт по 3-с таймауту). **Navigation (position/realm/community) не трогаем** —
их потребляет любой инстанс как сейчас.

Осознанно **НЕ решается** (частичное решение, принято): гонка между двумя одновременно
логинящимися инстансами; машинно-глобальный путь файла. Полный фикс — адресация файла на
инстанс (суффикс `requestId`/PID) — кросс-репо с `launcher-rust`, отдельная задача.

---

## Точная карта кода (проверено чтением файлов)

Все пути от `C:/DCL/unity-explorer/Explorer/`.

### `Assets/DCL/RuntimeDeepLink/DeepLinkSentinel.cs`
Статический `StartListenForDeepLinksAsync` — цикл `while (!token.IsCancellationRequested)`,
поллинг `CHECK_IN_PERIOD = 200ms`. **Текущий порядок (проблемный):**
`File.Exists` → `File.ReadAllTextAsync` → **`File.Delete` (стр. 57, ДО парсинга)** →
`DeepLink.FromJson` → `handle.HandleDeepLink`.
`File.Delete` без try/catch; чтение без `FileShare` → источник `IOException` при гонке.

### `Assets/DCL/RuntimeDeepLink/DeepLinkHandleImplementation.cs` (`DeepLinkHandle`)
`HandleDeepLink(DeepLink)`: извлекает `AppArgsFlags.SIGNIN`; если есть →
`deeplinkSigninDispatcher.Dispatch(signin)` → `return Result.SuccessResult()`. Иначе
роутит position/realm/community. Возвращает `Result` (Success/Error) — **сейчас не
различает «потреблено» vs «оставить файл»**.

### `Assets/DCL/RuntimeDeepLink/DeeplinkSigninDispatcher.cs`
Поля `current` (Subscription?) и `bufferedIdentityId`. `Dispatch` **всегда** буферит
(`bufferedIdentityId = id`) + `current?.Handler(id)`. `Subscribe` ставит `current` и
реплеит буфер. `Remove` (при Dispose подписки) чистит и `current`, и буфер.
**Нет способа спросить «есть ли активный подписчик».**

### `Assets/DCL/Web3/Authenticators/Implementations/Dapp/DappWeb3Authenticator.Deeplink.cs`
`LoginViaDeeplinkAsync`: `OpenUrl(url)` (стр. 81) → `WaitForSigninAsync` (стр. 85) →
`FetchIdentityByIdAsync`. `WaitForSigninAsync` **подписывается** (стр. 122) уже ПОСЛЕ
`OpenUrl`; таймаут `DEEPLINK_TIMEOUT_SECONDS = 300` (Realtime). Есть узкое окно
OpenUrl→Subscribe, где `current == null`, но реальный линк так рано не придёт
(браузерный round-trip — секунды).

---

## Требуемые изменения (черновик дизайна — уточнить/утвердить)

1. **Реордер в Sentinel:** парсить ДО удаления; удалять условно по результату обработки.
2. **`HandleDeepLink` возвращает outcome** «потреблено / оставить» (enum или bool `consumed`),
   а не просто `Result`. Sentinel удаляет файл только если «потреблено».
3. **Guard:** signin считается потреблённым только если диспатчер кого-то ждёт. Добавить в
   `IDeeplinkSigninDispatcher` признак `HasSubscriber` / `IsAwaitingSignin`.
4. **Матрица удаления файла:**
   - parse fail → **delete** (сбросить битый файл, иначе бесконечный re-loop при реордере);
   - signin + нет ждущего → **leave** (не delete/buffer/dispatch);
   - signin + есть ждущий → **dispatch + delete**;
   - navigation → **handle + delete** (как сейчас).
5. **Лог-спам:** idle-инстанс будет перечитывать оставленный файл каждые 200мс до ~3с
   (пока Launcher не уберёт). Понизить уровень лога / дедуп по контенту, чтобы не спамить.
6. **(Скоуп — решить)** Хардненинг `IOException`: `FileShare.Read` на чтении + try/catch на
   `File.Delete`. Технически отдельно от guard, но трогаем тот же метод.

---

## Открытые вопросы для старта сессии (были заданы, ответа нет)

- **A. Определение «логин ждёт signin»:**
  (1) `current != null` — просто, без нового состояния; узкое окно OpenUrl→Subscribe
  сигнал проигнорирует (практически недостижимо из-за round-trip). **Рекоменд.**
  (2) Явный флаг «awaiting», выставляемый в начале deep-link-ожидания до `OpenUrl` и
  снимаемый в конце — покрывает окно полностью, чуть больше состояния.
  → Влияет на семантику буфера: при (1) буфер фактически становится мёртвым (можно
  упростить), при (2) буфер осмыслен для узкой гонки.
- **B. Скоуп:** только guard, или guard + хардненинг IOException (п.6)?

---

## Тестирование

- Юнит: `DeeplinkSigninDispatcher` — dispatch без подписчика НЕ буферит/не зовёт handler;
  `HandleDeepLink` — signin без ждущего возвращает «оставить», navigation → «потреблено».
- Ручной репро гонки: см. LR-0004 — запустить шипнутый (залогиниться, стоять in-world) +
  Editor-флоу; до фикса Editor иногда висит 300с. После — шипнутый оставляет файл, Editor
  забирает. Editor-хелпер: `_Scratch/DeeplinkBackendTester.cs`.
- Скилл **testing-infrastructure** (UnitySystemTestBase / NSubstitute).

## Кросс-репо контракт (для справки, менять не требуется)

`launcher-rust` (`C:/DCL/launcher-rust/core/src/deeplink_bridge.rs`) пишет
`{ "deeplink": "decentraland://..." }`, поллит исчезновение файла (~3с,
`OPEN_DEEPLINK_TIMEOUT`), `remove_file` только при cancel/timeout. Если Explorer оставит
файл (guard-skip) и никто больше не логинится — Launcher корректно отвалится `E3001`.

---

## Suggested skills

- **code-standards** — ОБЯЗАТЕЛЬНО перед любым C#.
- **async-programming** — цикл Sentinel, CT, `SuppressToResultAsync`, обработка
  `OperationCanceledException` vs прочих через `ReportHub`.
- **superpowers:brainstorming** — продолжить с утверждения дизайна (вопросы A/B выше),
  затем записать spec; далее **superpowers:writing-plans** для плана реализации.
- **testing-infrastructure** — тесты на диспатчер/handle.
- (контекст) Учебные артефакты и LR: `~/.claude/projects/C--DCL-unity-explorer/teach/`.
