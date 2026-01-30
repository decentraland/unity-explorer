# Meta-Transactions для ThirdWebAuthenticator

## Цель

Реализовать **gasless транзакции** для ThirdWeb-пользователей (email + OTP login) через Decentraland relay-сервер. Пользователи с InAppWallet имеют баланс 0 MATIC и не могут платить за газ напрямую.

## Текущий статус

**✅ РАБОТАЕТ!**

Meta-транзакции для gifting wearables/emotes через ThirdWeb InAppWallet полностью функциональны.

---

## Решение

Были найдены и исправлены **три ключевые проблемы**:

### 1. EIP-712 Domain Name — Hardcoded "Decentraland Collection"

**Проблема:** Мы использовали `name()` контракта (например "Decentraland Tutorial Wearables"), но EIP-712 domain инициализируется с **hardcoded** именем.

**Решение:** Все DCL collection контракты используют:
```
name = "Decentraland Collection"
version = "2"
```

Это hardcoded в контракте `ERC721BaseCollectionV2.sol`:
```solidity
function initialize(...) {
    _initializeEIP712('Decentraland Collection', '2');  // ← Hardcoded!
    _initERC721(_name, _symbol);  // ← Token name отличается
}
```

### 2. ChainId для EIP-712 Salt — Polygon (137)

**Проблема:** Использовали chainId кошелька (Ethereum = 1), но контракты на Polygon.

**Решение:** Salt в EIP-712 domain всегда должен быть chainId **контракта** (Polygon = 137):
```
salt = 0x0000000000000000000000000000000000000000000000000000000000000089
```

### 3. Не переключать сеть кошелька для meta-tx

**Проблема:** Переключали кошелёк на Polygon перед подписью, что могло влиять на поведение ThirdWeb SDK.

**Решение:** Для Internal requests (meta-tx) кошелёк остаётся на текущей сети (Ethereum). Только RPC запросы (nonce) и EIP-712 salt используют Polygon chainId.

Это соответствует поведению [decentraland-connect ThirdwebConnector](https://github.com/decentraland/decentraland-connect/blob/master/src/connectors/ThirdwebConnector.ts).

---

## Архитектура решения

### Поток мета-транзакции

```
┌─────────────────────────────────────────────────────────────────┐
│  Unity Client (ThirdWebAuthenticator)                           │
├─────────────────────────────────────────────────────────────────┤
│  1. Получить nonce из контракта (Polygon RPC)                   │
│  2. Создать EIP-712 typed data:                                 │
│     - domain.name = "Decentraland Collection"                   │
│     - domain.version = "2"                                      │
│     - domain.salt = 0x...0089 (chainId=137)                     │
│  3. Подписать через SignTypedDataV4 (кошелёк НЕ переключается!) │
│  4. Закодировать executeMetaTransaction(user, sig, data)        │
│  5. POST на transactions-api.decentraland.org                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Decentraland Relay Server                                      │
├─────────────────────────────────────────────────────────────────┤
│  - Проверяет подпись                                            │
│  - Оплачивает газ                                               │
│  - Выполняет транзакцию на Polygon                              │
│  - Лимит газа: 800 gwei                                         │
└─────────────────────────────────────────────────────────────────┘
```

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
    "name": "Decentraland Collection",
    "version": "2",
    "verifyingContract": "0xContractAddress",
    "salt": "0x0000000000000000000000000000000000000000000000000000000000000089"
  },
  "message": {
    "nonce": 0,
    "from": "0xUserAddress",
    "functionSignature": "0x23b872dd..."
  }
}
```

---

## Ключевые файлы

- `ThirdWebAuthenticator.cs` — основная реализация мета-транзакций
  - `SendMetaTransactionAsync()` — точка входа
  - `GetContractMetaTxInfoAsync()` — возвращает "Decentraland Collection" + "2"
  - `CreateMetaTxTypedData()` — формирует EIP-712 JSON
  - `POLYGON_CHAIN_ID = 137` — hardcoded для relay
- `TestOnChainWeb3.cs` — тесты:
  - `CheckDomainSeparator` / `CheckDomainSeparator2` — проверка domain separator
  - `TestGiftingViaRelay` / `TestGiftingViaRelay2` — тесты gifting
  - `CheckPolygonGasPrice` — проверка газа (лимит relay: 800 gwei)
- `Web3GiftTransferService.cs` — использует `Web3RequestSource.Internal`

---

## Как запустить тест

1. Залогиниться через ThirdWeb OTP
2. В Inspector на `TestOnChainWeb3`:
   - Можно изменить `testTokenId`, `testContractAddress`, `testRecipientAddress`
3. Context Menu → `CheckPolygonGasPrice` (убедиться что газ < 800 gwei)
4. Context Menu → `CheckDomainSeparator` (должен показать ✅ MATCH)
5. Context Menu → `TestGiftingViaRelay`
6. Смотреть логи `[ThirdWeb]` и `[EIP712-Manual]`

### Ожидаемые логи при успехе

```
[ThirdWeb] Internal request - NOT switching wallet network
[ThirdWeb] Using Polygon chainId=137 for meta-transaction
[ThirdWeb] Using DCL collection EIP-712 domain: name='Decentraland Collection', version='2'
[EIP712-Hash] Salt (chainId=137): 0x...0089
[EIP712-Manual] ✅ ADDRESSES MATCH! Signature is valid for our computed hash.
✅ Gifting relay transaction sent!
Transaction Hash: 0x...
```

---

## Тестовые данные

### Test Case 1 (Decentraland Tutorial Wearables)
```
contractAddress = 0x167d6b63511a7b5062d1f7b07722fccbbffb5105
tokenId = 210624583337114373395836055367340864637790190801098222508621978860
recipientAddress = 0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1
domainSeparator = 0x64579df6ce1ca075cf235d9c8ac11ded8d771670b46dacce90c162a98b96cd77
```

### Test Case 2 (Another Collection)
```
contractAddress = 0x66871d01e15af85ea6c172b7c4821b0f9bb71880
tokenId = 674
recipientAddress = 0xda2d974646fa7ee9f75f288db2050aae09c3ba1f
domainSeparator = 0xb97975c9e92af845d5c283d5051a2c8f3e47926bff230b265b655d5651ed917d
```

---

## Известные контракты (hardcoded в KnownMetaTxContracts)

| Address | Name | Version |
|---------|------|---------|
| 0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4 | Decentraland MANA | 1 |
| 0x480a0f4e360e8964e68858dd231c2922f1df45ef | Decentraland Marketplace | 2 |
| 0xb96697fa4a3361ba35b774a42c58daccaad1b8e1 | Decentraland Bid | 2 |
| 0x9d32aac179153a991e832550d9f96f6d1e05d4b4 | CollectionManager | 2 |

**Для collection контрактов (wearables/emotes):** всегда `"Decentraland Collection"` + `"2"`.

---

## Важно: salt vs chainId

DCL контракты используют **нестандартный** EIP712Domain:

```solidity
// Стандартный EIP-712
EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)

// DCL / Matic Meta-Tx (NativeMetaTransaction.sol)  
EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)
```

**salt = chainId padded to bytes32**, для Polygon (137):
```
salt = 0x0000000000000000000000000000000000000000000000000000000000000089
```

---

## Troubleshooting

### Ошибка: `high_congestion`

```
Current network gas price 1165002786032 exceeds max gas price allowed 800000000000
```

Газ на Polygon слишком дорогой. Relay лимит — 800 gwei. Подождите пока газ упадёт.

Мониторинг: https://polygonscan.com/gastracker

### Ошибка: `SIGNER_AND_SIGNATURE_DO_NOT_MATCH`

Проверьте:
1. `CheckDomainSeparator` показывает ✅ MATCH?
2. Используется `"Decentraland Collection"` + версия `"2"`?
3. Salt = `0x...0089` (chainId=137)?
4. Кошелёк НЕ переключается на Polygon?

---

## Ссылки

- **ERC721BaseCollectionV2.sol**: https://github.com/decentraland/wearables-contracts/blob/master/contracts/collections/v2/ERC721BaseCollectionV2.sol
- **EIP712Base.sol**: https://github.com/decentraland/wearables-contracts/blob/master/contracts/commons/EIP712Base.sol
- **decentraland-transactions** (JS): https://github.com/decentraland/decentraland-transactions
- **ThirdwebConnector.ts**: https://github.com/decentraland/decentraland-connect/blob/master/src/connectors/ThirdwebConnector.ts
