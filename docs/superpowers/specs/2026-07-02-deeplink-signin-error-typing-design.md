# Deep-link sign-in: типизация ошибок retrieval + retry на минт requestId

**Дата:** 2026-07-02
**Ветка:** `chore/authentication/deep-link-login`
**Статус:** дизайн утверждён пользователем (скоуп (a) — только диагностика, без правок UI; retry вариант (2) — enforced на POST)

## Контекст

Gap-анализ deep-link sign-in против ADR-288 / auth#218,412,413 / creator-hub#1338 выявил два пробела:

1. **Нет типизации ошибок `GET /identities/{identityId}`.** Семантика кодов (подтверждена по ADR-288 и creator-hub):
   - `404` — identity не найдена **или уже была получена** (single-use);
   - `410` — identity протухла (TTL ≤ 15 мин);
   - `403` — IP mismatch: signed-fetch при сохранении identity пришёл с одного IP, GET — с другого. Реальный кейс у пользователей с VPN/private relay (creator-hub показывает спец-подсказку).
   У нас любой не-2xx → generic `UnityWebRequestException` → в Sentry/логах неразличимо.
2. **ADR SHOULD «retry with exponential backoff» на сетевые ошибки.** `GET /identities` уже покрыт `RetryPolicy.DEFAULT` (2 ретрая, backoff ×3, min 1s — для транзиентных сетевых/DNS ошибок идемпотентных запросов и 429/503 с `Retry-After`). `POST /requests` (минт `requestId`) не идемпотентен → дефолтная политика его не ретраит, хотя повтор безопасен: в худшем случае минтится второй `requestId`, первый протухает неиспользованным.

## Решения

- **Скоуп (a):** типизированные исключения + различимые сообщения в `ReportHub`/Sentry/span-телеметрии. Пользователь видит прежний generic-попап (`ErrorType.CONNECTION_ERROR`). Тексты попапа — вне скоупа (нужен дизайнер).
- **Один класс исключения с enum-причиной**, не три класса (аналог `SignInError.reason` в creator-hub; три типа ради диагностики — лишняя структура).
- **Enforced retry на `POST /requests`** через существующий `RetryPolicy.Enforce()`.

## Дизайн (4 точки касания)

### 1. Новое исключение

Файл: `Explorer/Assets/DCL/Web3/Authenticators/Exceptions/DeeplinkSigninRetrievalException.cs` (рядом с `SignatureExpiredException`).

```csharp
public class DeeplinkSigninRetrievalException : Web3Exception
{
    public enum ErrorReason
    {
        NotFound,   // 404: not found or already retrieved (single-use)
        Expired,    // 410: expired (TTL <= 15 min)
        IpMismatch, // 403: retrieval IP differs from the signed-fetch IP (VPN/private relay)
    }

    public ErrorReason Reason { get; }
}
```

- Конструктор принимает `ErrorReason reason` и `string identityId`; сообщение формируется внутри по reason и включает `identityId` (для `IpMismatch` — упоминание VPN/private relay как вероятной причины).
- Наследование от `Web3Exception`: существующий `catch (Web3Exception)` в `IdentityVerificationDappDeepLinkAuthState.AuthenticateAsync` продолжает вести в `ErrorType.CONNECTION_ERROR` — роутинг UI не меняется.
- Сборка `DCL.Web3` с включённым nullable — аннотировать соответственно.

### 2. Маппинг кодов в `FetchIdentityByIdAsync`

Файл: `Explorer/Assets/DCL/Web3/Authenticators/Implementations/Dapp/DappDeepLinkAuthenticator.cs`.

К существующему запросу добавляется готовый хелпер `WebRequestUtils.WithCustomExceptionAsync`:

```csharp
IdentityAuthResponseDto json = await webRequestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
                                                         .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>()
                                                         .WithCustomExceptionAsync(e => e.ResponseCode switch
                                                          {
                                                              404 => new DeeplinkSigninRetrievalException(ErrorReason.NotFound, identityId),
                                                              410 => new DeeplinkSigninRetrievalException(ErrorReason.Expired, identityId),
                                                              403 => new DeeplinkSigninRetrievalException(ErrorReason.IpMismatch, identityId),
                                                              _ => e, // прочие коды — поведение без изменений
                                                          });
```

- Лямбда захватывает `identityId` — не `static`; это одноразовый логин-путь, аллокация допустима.
- Для незамапленных кодов factory возвращает исходное исключение (`throw e` в хелпере сбрасывает stack trace — приемлемо, тип и сообщение сохраняются).

### 3. Телеметрия в auth-состоянии

Файл: `Explorer/Assets/DCL/AuthenticationScreenFlow/States/IdentityVerificationDappDeepLinkAuthState.cs`, метод `Exit()`.

В switch для `spanErrorInfo` добавляется один case **выше** `Web3Exception` (иначе перехватит базовый):

```csharp
DeeplinkSigninRetrievalException ex => new SpanErrorInfo($"Signin identity retrieval failed: {ex.Reason}", ex),
```

Catch-блоки `AuthenticateAsync` не меняются (типизированное исключение ловится существующим `catch (Web3Exception)`).

### 4. Retry на минт requestId

Файл: `DappDeepLinkAuthenticator.cs`, метод `CreateSigninRequestAsync`:

```csharp
var commonArguments = new CommonArguments(urlBuilder.Build(), RetryPolicy.Enforce());
```

2 повтора с backoff ×3 на транзиентных сетевых/DNS сбоях. `GET /identities` остаётся на `RetryPolicy.DEFAULT` — уже соответствует ADR SHOULD, код не меняется.

## Тесты

У сборки `DCL.Web3` нет тестового asmdef. Маппинг — чистый switch по трём кодам; новая тестовая сборка ради него в скоуп не входит. Если позже понадобится — маппинг выносится в статический метод и покрывается в отдельно заведённой тестовой сборке.

## Вне скоупа

- Пользовательские тексты попапа по причинам ошибок (ждёт дизайнера) — скоуп (b).
- Лимит попыток логина (аналог `MAX_SIGNIN_ATTEMPTS = 3` из creator-hub).
- WebSocket-fallback на code-флоу (ADR SHOULD, отдельное продуктовое решение).
- `dclenv` в deep link — отклонено: env неявно зашит в эндпоинты клиента, кросс-env гонка двух инстансов вне скоупа.
