# Meta-Transactions для ThirdWebAuthenticator

## Цель

Реализовать **gasless транзакции** для ThirdWeb-пользователей (email + OTP login) через Decentraland relay-сервер. Пользователи с InAppWallet имеют баланс 0 MATIC и не могут платить за газ напрямую.

## Текущий статус

**❌ НЕ РАБОТАЕТ** — ошибка `SIGNER_AND_SIGNATURE_DO_NOT_MATCH`

**ВАЖНО:** Domain separator не совпадает с контрактом. Мы используем неправильный формат EIP712Domain.

---

## Важный контекст: что уже работает

### ✅ Сценарий 1: Unity + DappWeb3Authenticator + Magic Social Login

Gifting работает корректно когда пользователь:
1. Открывает **Unity клиент**
2. Авторизуется через **DappWeb3Authenticator** (dApp flow)
3. Использует **Magic social login** (Google, Discord и т.д.)

В этом сценарии:
- Формирование `transferFrom` data корректное
- Encoding `executeMetaTransaction` корректный
- POST запрос к `transactions-api.decentraland.org` корректный
- Relay-сервер принимает и выполняет транзакции
- **Подпись формируется Magic SDK и принимается контрактом**

Код в `Web3GiftTransferService.cs` и `ManualTxEncoder.cs` проверен и работает.

### ✅ Сценарий 2: Веб-браузер + ThirdWeb (без Unity)

Когда пользователь работает **чисто в браузере** (Unity не участвует):
1. Заходит на **Decentraland Marketplace** (web)
2. Авторизуется через **ThirdWeb** (email OTP)
3. Покупает wearables — это **meta-транзакция**

В этом сценарии всё работает. Веб-версия использует:
- `decentraland-transactions` библиотеку
- `eth_signTypedData_v4` через EIP-1193 провайдер от ThirdWeb
- Подпись принимается relay-сервером

Ссылка: https://github.com/decentraland/decentraland-connect/blob/master/src/connectors/ThirdwebConnector.ts

### ❌ Сценарий 3: Unity + ThirdWebAuthenticator (текущая проблема)

Проблема **только** когда:
1. Пользователь открывает **Unity клиент**
2. Авторизуется через **ThirdWebAuthenticator** (email OTP)
3. Пытается сделать gifting

Контракт отклоняет подпись: `SIGNER_AND_SIGNATURE_DO_NOT_MATCH`.

### Выводы из работающих сценариев

Поскольку **оба** работающих сценария (Magic в Unity и ThirdWeb в браузере) используют одинаковый relay и контракты:
- ❌ Проблема НЕ в формате `transferFrom` data
- ❌ Проблема НЕ в encoding `executeMetaTransaction`
- ❌ Проблема НЕ в relay-сервере
- ✅ **Проблема в том как ThirdWeb Unity SDK хеширует/подписывает EIP-712 typed data**

---

Контракт не может верифицировать подпись от ThirdWeb InAppWallet.

**Найденная проблема:** Domain separator не совпадает с контрактом!

```
★ Contract domain separator: 0x64579df6ce1ca075cf235d9c8ac11ded8d771670b46dacce90c162a98b96cd77
★ Our computed (salt format, v='1'): 0xc04cf5c0e8c51e510e38872d31e9e526dbc12154543e7c4257076b7ade59ccc4
★ Our computed (salt format, v='2'): 0x81ca2272a83f3ff3616a8f0fe15d9a49ea3cebdb824ec29fb42c695c62c8c926
★ Our computed (standard format): НЕ СОВПАДАЕТ
★ Our computed (minimal format): НЕ СОВПАДАЕТ
```

Ни один из стандартных форматов EIP712Domain не даёт совпадение.

**Исходный код контракта найден:**
- https://github.com/decentraland/wearables-contracts/blob/master/contracts/commons/EIP712Base.sol
- https://github.com/decentraland/wearables-contracts/blob/master/contracts/commons/NativeMetaTransaction.sol

Контракт использует формат:
```solidity
"EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)"
domainSeparator = keccak256(abi.encode(
    typeHash, 
    keccak256(bytes(name)), 
    keccak256(bytes(version)), 
    address(this), 
    bytes32(getChainId())
));
```

**Наш код использует тот же формат!** Значит проблема в параметрах (name, version, или chainId).

### Что пробовали

| # | Попытка | Результат |
|---|---------|-----------|
| 1 | nonce как число `(long)nonce` | Не помогло |
| 2 | Версия "1" для collection контрактов | Не помогло |
| 3 | Contract name из `name()` | Не помогло |
| 4 | ВСЕ адреса lowercase | Получили `high_congestion` (газ дорогой) - казалось что работает! |
| 5 | Повторный тест с нормальным газом | Снова `SIGNER_AND_SIGNATURE_DO_NOT_MATCH` |
| 6 | Адреса как есть (без toLowerCase) - как в JS | Ещё не протестировано |

### Важное наблюдение

**Подпись НЕ МЕНЯЕТСЯ** между запусками даже когда мы меняем параметры (lowercase/checksum)!

```
Signature: 0x1a7e0ca259ddbef0e0ec75c091c3e37a74b01b2c5f10182e12cd25ab727fa46e246c4e2b2153c481da22caea5aa43de324d4316b9532681219445ce07ebcca171b
Hash: 0xee23d56dfefba1bc3b71127f151bec03aacda076840caba7e076e55fcc3ae0c6
```

Возможные причины:
1. **ThirdWeb SDK кеширует подпись** для "эквивалентных" typed data
2. **Nethereum нормализует адреса** при хешировании (address type → bytes)
3. **Контракт хеширует иначе** чем Nethereum/ThirdWeb

---

## Ключевые находки из JS библиотеки

Изучили исходники `decentraland-transactions`:
- https://github.com/decentraland/decentraland-transactions/blob/master/src/sendMetaTransaction.ts
- https://github.com/decentraland/decentraland-transactions/blob/master/src/utils.ts

### JS НЕ делает toLowerCase!

```javascript
// sendMetaTransaction.ts - getDataToSign()
message: {
  nonce: parseInt(nonce, 16),  // hex → number
  from: account,               // БЕЗ toLowerCase!
  functionSignature            // БЕЗ toLowerCase!
}

// utils.ts - getExecuteMetaTransactionData()
to32Bytes(account)  // просто padStart, БЕЗ toLowerCase!
```

### Текущая реализация (после фиксов)

Теперь используем адреса **как есть** (как JS библиотека):
- `CreateMetaTxTypedData` — без toLowerCase
- `EncodeExecuteMetaTransaction` — без toLowerCase  
- `PostToTransactionsServerAsync` — без toLowerCase

---

## Архитектура решения

### Поток мета-транзакции

1. Получить nonce пользователя из контракта на **Polygon**: `getNonce(address)`
2. Получить имя контракта: `name()` 
3. Создать EIP-712 typed data с domain (chainId=137 для Polygon) и message
4. Подписать через `SignTypedDataV4(typedDataJson)` — подпись происходит **локально** в кошельке
5. Закодировать `executeMetaTransaction(userAddress, functionSignature, r, s, v)`
6. POST на `https://transactions-api.decentraland.org/v1/transactions`
7. **Relay-сервер** проверяет подпись и выполняет транзакцию на **Polygon**, оплачивая газ

**Важно:** Контракты wearables находятся на **Polygon (chainId=137)**. EIP-712 domain должен содержать этот chainId (или salt=0x89).

### EIP-712 Typed Data структура

```json
{
  "types": {
    "EIP712Domain": [
      { "name": "name", "type": "string" },
      { "name": "version", "type": "string" },
      { "name": "verifyingContract", "type": "address" },
      { "name": "salt", "type": "bytes32" }
    ],
    "MetaTransaction": [
      { "name": "nonce", "type": "uint256" },
      { "name": "from", "type": "address" },
      { "name": "functionSignature", "type": "bytes" }
    ]
  },
  "primaryType": "MetaTransaction",
  "domain": {
    "name": "Contract Name from name()",
    "version": "1",
    "verifyingContract": "0xContractAddress",
    "salt": "0x0000...0089"
  },
  "message": {
    "nonce": 0,
    "from": "0xUserAddress",
    "functionSignature": "0x23b872dd..."
  }
}
```

### Важно: salt vs chainId

DCL контракты используют **нестандартный** EIP712Domain:

```solidity
// Стандартный EIP-712
EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)

// DCL / Matic Meta-Tx (NativeMetaTransaction.sol)  
EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)
```

**salt = chainId padded to bytes32**, например для Polygon (137):
```
salt = 0x0000000000000000000000000000000000000000000000000000000000000089
```

---

## Ключевые файлы

- `ThirdWebAuthenticator.cs` — основная реализация мета-транзакций
- `ThirdWebManager.cs` — создание ThirdwebClient с rpcOverrides
- `TestOnChainWeb3.cs` — тесты:
  - `TestGiftingViaRelay` — основной тест meta-tx
  - `CheckPolygonGasPrice` — проверка газа (лимит relay: 800 gwei)
  - `CheckNftOwner` — проверка владельца NFT
  - `CheckDomainSeparator` — проверка domain separator контракта
- `Web3GiftTransferService.cs` — использует `Web3RequestSource.Internal`

## Как запустить тест

1. Залогиниться через ThirdWeb OTP
2. В Inspector на `TestOnChainWeb3`:
   - Выбрать `PolygonMainnet`
   - Можно изменить `testTokenId`, `testContractAddress`, `testRecipientAddress`
3. Context Menu → `CheckPolygonGasPrice` (убедиться что газ < 800 gwei)
4. Context Menu → `CheckNftOwner` (убедиться что NFT принадлежит вам)
5. Context Menu → `TestGiftingViaRelay`
6. Смотреть логи `[ThirdWeb]` и `[EIP712-Manual]`

---

## Тест на кэширование подписи

Для проверки кэширует ли ThirdWeb SDK подписи, используй 3 тестовых метода:

### Подготовка

1. Сделай gift 3 разных wearables через UI (или используй логи из обычного gifting)
2. Скопируй из логов `[GiftService]` данные для каждого wearable:
   - `Contract Address`
   - `Token ID`
3. Заполни в Inspector секции:
   - `Signature Caching Test Data - Wearable 1`
   - `Signature Caching Test Data - Wearable 2`
   - `Signature Caching Test Data - Wearable 3`

### Запуск тестов

1. Context Menu → `CacheTest 1 - First Wearable`
2. Context Menu → `CacheTest 2 - Second Wearable`
3. Context Menu → `CacheTest 3 - Third Wearable`
4. Context Menu → `CacheTest - Print Summary`

### Анализ результатов

Найди в логах строки `[ThirdWeb] Full signature:` для каждого теста.

**Если все 3 подписи РАЗНЫЕ** → Кэширования нет, проблема в другом.
**Если хотя бы 2 подписи ОДИНАКОВЫЕ** → ThirdWeb SDK кэширует подписи!

Также сравни `[EIP712-Hash] ★ Final digest:` — они должны быть разными для разных wearables.

---

## Тестовые данные

```
senderAddress = 0xb1a7fC4bbD9856bFA1F70F6B111444Cd9d351592
recipientAddress = 0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1
contractAddress = 0x167d6b63511a7b5062d1f7b07722fccbbffb5105
contractName = "Decentraland Tutorial Wearables"
tokenId = 210624583337114373395836055367340864637790190801098222508621978860
chainId = 137 (Polygon Mainnet)
```

---

## Отладочный режим: USE_MANUAL_EIP712_SIGNING

В `ThirdWebAuthenticator.cs` есть режим `USE_MANUAL_EIP712_SIGNING = true` который:

1. Вычисляет EIP-712 hash через Nethereum `Eip712TypedDataSigner.EncodeTypedData()`
2. Вычисляет hash вручную для сравнения (double-check)
3. Подписывает через ThirdWeb `SignTypedDataV4()`
4. Восстанавливает адрес из подписи через `RecoverFromSignatureV4()`
5. Сравнивает адреса — если не совпадают, ThirdWeb хеширует по-другому

### Логи для анализа

```
[EIP712-Manual] Nethereum computed HASH: 0x...
[EIP712-Manual] Manual computed HASH: 0x...
[EIP712-Manual] Recovered address from signature: 0x...
[EIP712-Manual] Expected signer address: 0x...
[EIP712-Manual] ✅ ADDRESSES MATCH! или ❌ ADDRESSES DO NOT MATCH!
[ThirdWeb] Signature components: r=..., s=..., v=...
```

---

## Следующие шаги для отладки

### 1. **Проверить domain separator** (КРИТИЧНО!)

Запустить `CheckDomainSeparator` в Inspector. Это сравнит:
- Domain separator из контракта (`domainSeparator()`)
- Наш вычисленный domain separator

**Если они НЕ совпадают** — проблема в параметрах domain (name, version, salt).

### 2. **Проанализировать логи хеширования**

При запуске `TestGiftingViaRelay` смотреть логи `[EIP712-Hash]`:
```
[EIP712-Hash] Domain type hash: 0x...
[EIP712-Hash] Name hash ('Decentraland Tutorial Wearables'): 0x...
[EIP712-Hash] Version hash ('1'): 0x...
[EIP712-Hash] Salt (chainId=137): 0x...89
[EIP712-Hash] ★ Our computed DOMAIN SEPARATOR: 0x...
[EIP712-Hash] ★ Final digest: 0x...
```

### 3. **Сравнить JSON typed data**

Новый код логирует полный JSON перед подписанием:
```
[ThirdWeb] Created typed data JSON (length=XXX):
{"types":{"EIP712Domain":[...],...}
```

Сравнить с тем что генерирует JS библиотека. Ключевые моменты:
- Порядок ключей в types
- Формат nonce (число, не строка)
- Формат salt (0x + 64 hex символа)

### 4. **Проверить через веб**

Попробовать gifting через Marketplace/in-world с этим же аккаунтом.
Если работает там — проблема точно в нашей реализации.

### 5. **Попробовать raw hash signing**

Изменить `USE_RAW_HASH_SIGNING = true` в ThirdWebAuthenticator.cs.
Это обойдёт `SignTypedDataV4` и подпишет hash напрямую.

**ВАЖНО:** PersonalSign добавляет prefix, поэтому это НЕ будет работать с контрактом напрямую.
Но поможет понять подписывает ли ThirdWeb SDK правильный hash.

### 6. **Связаться с ThirdWeb**

Спросить как InAppWallet хеширует EIP-712 typed data внутри.

---

## Гипотезы о причине проблемы

### Гипотеза 1: Разница в JSON сериализации

ThirdWeb SDK может парсить JSON и хешировать иначе чем мы ожидаем.
Например, порядок ключей или whitespace могут влиять.

**Проверка:** Сравнить JSON побайтово с JS библиотекой.

### Гипотеза 2: Кеширование подписей в ThirdWeb SDK

Если подпись не меняется при изменении параметров — SDK может кешировать.

**Проверка:** Добавить timestamp или random в JSON и проверить меняется ли подпись.

### Гипотеза 3: Разница в типах EIP-712

DCL использует нестандартный EIP712Domain с `salt` вместо `chainId`.
ThirdWeb SDK может не поддерживать этот формат корректно.

**Проверка:** Сравнить domain separator с контрактом.

### Гипотеза 4: Проблема с encoding bytes в typed data

`functionSignature` имеет тип `bytes`. SDK может кодировать hex string иначе.

**Проверка:** Проверить struct hash отдельно.

---

## Ссылки

- **decentraland-transactions** (JS): https://github.com/decentraland/decentraland-transactions
- **sendMetaTransaction.ts**: https://github.com/decentraland/decentraland-transactions/blob/master/src/sendMetaTransaction.ts
- **utils.ts** (encoding): https://github.com/decentraland/decentraland-transactions/blob/master/src/utils.ts
- **RequestPage.tsx** (веб): https://github.com/decentraland/auth/blob/main/src/components/Pages/RequestPage/RequestPage.tsx
- **EIP712Base.sol**: https://github.com/ProjectOpenSea/opensea-creatures/blob/master/contracts/common/meta-transactions/EIP712Base.sol
- **NativeMetaTransaction.sol**: https://github.com/ProjectOpenSea/opensea-creatures/blob/master/contracts/common/meta-transactions/NativeMetaTransaction.sol

---

## Известные контракты (hardcoded в KnownMetaTxContracts)

| Address | Name | Version |
|---------|------|---------|
| 0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4 | Decentraland MANA | 1 |
| 0x480a0f4e360e8964e68858dd231c2922f1df45ef | Decentraland Marketplace | 2 |
| 0xb96697fa4a3361ba35b774a42c58daccaad1b8e1 | Decentraland Bid | 2 |
| 0x9d32aac179153a991e832550d9f96f6d1e05d4b4 | CollectionManager | 2 |

**Для unknown контрактов**: получаем имя из `name()`, версия "1" (стандарт EIP712Base).

---

## Вопросы для ThirdWeb/DCL команд

### Для ThirdWeb:
```
We're using InAppWallet.SignTypedDataV4(jsonString) to sign EIP-712 
meta-transactions for Decentraland relay, but getting signature mismatch.

Nethereum's RecoverFromSignatureV4 shows the signature is valid for our hash,
but the on-chain contract rejects it with SIGNER_AND_SIGNATURE_DO_NOT_MATCH.

Questions:
1. How does InAppWallet internally hash EIP-712 typed data?
2. Does it use ethers.js _TypedDataEncoder or custom implementation?
3. Does the SDK cache signatures for "equivalent" typed data?
4. Does JSON key order or whitespace affect the hash?
```

### Для DCL:
```
We're implementing meta-transactions for ThirdWeb InAppWallet users in Unity,
using transactions-api.decentraland.org, but getting SIGNER_AND_SIGNATURE_DO_NOT_MATCH.

The same transaction works in two scenarios:
1. Unity client + DappWeb3Authenticator + Magic social login
2. Web browser + ThirdWeb (Marketplace purchases)

But fails only with: Unity client + ThirdWebAuthenticator (email OTP).

Our computed domain separator doesn't match the contract's domainSeparator().
Contract: 0x64579df6ce1ca075cf235d9c8ac11ded8d771670b46dacce90c162a98b96cd77
We tried multiple EIP712Domain formats but none matched.

Questions:
1. What exact EIP712Domain format do DCL collection contracts use?
2. Can you share the EIP712Base.sol source for these contracts?
3. Is there a test endpoint to validate our EIP-712 typed data?
```
