# Meta-Transactions для Donations (Tips)

## Цель

Реализовать **мета-транзакции** для donations/tips через ThirdWeb InAppWallet. Пользователи с email+OTP login имеют баланс 0 MATIC на Polygon и не могут платить за газ напрямую.

## Текущий статус

**✅ РАБОТАЕТ!**

Meta-транзакции для donations/tips через ThirdWeb InAppWallet полностью функциональны на обеих сетях (Amoy testnet и Polygon mainnet).

---

## Решение

Была найдена и исправлена **ключевая проблема**:

### EIP-712 Domain Name — Разные имена для разных сетей!

**Проблема:** Мы использовали `"Decentraland MANA"` для всех сетей, но EIP-712 domain name **отличается** для каждой сети.

**Решение:** Правильные имена из `decentraland-transactions` npm пакета:

| Сеть | ChainId | EIP-712 Domain Name |
|------|---------|---------------------|
| Polygon Mainnet | 137 | `"(PoS) Decentraland MANA"` |
| Polygon Amoy | 80002 | `"Decentraland MANA(PoS)"` |

**Важно:** Обрати внимание на разницу — на Mainnet `(PoS)` в начале, на Amoy `(PoS)` в конце без пробела!

Источник: https://github.com/decentraland/decentraland-transactions/blob/master/src/contracts/manaToken.ts

```csharp
private static readonly Dictionary<string, ContractMetaTxInfo> KnownMetaTxContracts = new ()
{
    // MANA on Polygon Mainnet - "(PoS) Decentraland MANA"
    { "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", new ContractMetaTxInfo("(PoS) Decentraland MANA", "1", 137) },

    // MANA on Polygon Amoy - "Decentraland MANA(PoS)" (no space!)
    { "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", new ContractMetaTxInfo("Decentraland MANA(PoS)", "1", 80002) },
};
```

---

## Архитектура: Два Flow

### 1. DappWeb3Auth Flow (браузер, работает в production)

```
Unity Client → eth_sendTransaction → Auth API → Browser dApp → sendMetaTransaction() → Relay Server → Polygon
```

- Пользователь подключён к **Ethereum Mainnet** (или Sepolia для .zone)
- Баланс проверяется на **Polygon** (или Amoy для .zone) через `readonlyNetwork`
- В браузере `decentraland-transactions` библиотека вызывает `sendMetaTransaction()`
- Relay server (`transactions-api.decentraland.org`) оплачивает газ и выполняет транзакцию на Polygon

### 2. ThirdWebAuth Flow (наша реализация)

```
Unity Client → SendMetaTransactionAsync() → EIP-712 Sign → Relay Server → Polygon
```

- Пользователь подключён к **Ethereum Mainnet** (кошелёк НЕ переключается!)
- Баланс проверяется на **Polygon** через `readonlyNetwork` параметр
- Unity подписывает EIP-712 сообщение
- Relay server выполняет транзакцию на Polygon

---

## Ключевые файлы

### Unity (Explorer)

- `ThirdWebAuthenticator.cs` — основная реализация мета-транзакций
  - `SendMetaTransactionAsync()` — точка входа для мета-транзакций
  - `GetContractMetaTxInfoAsync()` — возвращает EIP-712 domain info для контракта
  - `KnownMetaTxContracts` — словарь известных контрактов с их EIP-712 параметрами
  - `GetChainIdFromReadonlyNetwork()` — маппинг network name → chainId
  - `SendWithoutConfirmationAsync()` — обрабатывает readonly запросы с учётом `readonlyNetwork`
  - `GetRelayServerUrl()` — выбирает relay server по chainId (mainnet → .org, testnet → .zone)

- `DonationsService.cs` — сервис donations
  - `GetCurrentBalanceAsync()` — проверка баланса MANA на Polygon/Amoy
  - `SendDonationAsync()` — отправка donation через `Web3RequestSource.Internal`
  - `MATIC_CONTRACT_ADDRESS` = `0xA1c57f48F0Deb89f569dFbE6E2B7f46D33606fD4` (MANA на Polygon)
  - `AMOY_NET_CONTRACT_ADDRESS` = `0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0` (MANA на Amoy)

- `ManualTxEncoder.cs` — кодирование транзакций
  - `EncodeSendDonation()` — кодирует `transfer(address,uint256)` (selector `0xa9059cbb`)
  - `EncodeGetBalance()` — кодирует `balanceOf(address)` (selector `0x70a08231`)

- `TestOnChainWeb3.cs` — ручное тестирование
  - `CheckManaAmoyDomainSeparator()` — проверка domain separator MANA на Amoy

---

## Контракты и сети

### MANA Token

| Сеть | ChainId | Контракт | EIP-712 Domain Name | Version | Meta-Tx |
|------|---------|----------|---------------------|---------|---------|
| Polygon Mainnet | 137 | `0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4` | `"(PoS) Decentraland MANA"` | 1 | ✅ |
| Polygon Amoy | 80002 | `0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0` | `"Decentraland MANA(PoS)"` | 1 | ✅ |

### Relay Servers

| Сеть | ChainId | URL |
|------|---------|-----|
| Polygon Mainnet | 137 | `https://transactions-api.decentraland.org/v1/transactions` |
| Polygon Amoy | 80002 | `https://transactions-api.decentraland.zone/v1/transactions` |

---

## Изменения сделанные для Donations

### 1. Поддержка `readonlyNetwork` в ThirdWebAuthenticator

**Проблема:** Баланс MANA показывал 0, т.к. запрос шёл на Ethereum вместо Polygon.

**Решение:** Добавлен маппинг `readonlyNetwork` → `chainId`:

```csharp
private static int? GetChainIdFromReadonlyNetwork(string? networkName)
{
    return networkName?.ToLowerInvariant() switch
    {
        "polygon" => 137,        // Polygon Mainnet
        "amoy" => 80002,         // Polygon Amoy Testnet
        "ethereum" => 1,         // Ethereum Mainnet
        "sepolia" => 11155111,   // Ethereum Sepolia Testnet
        _ => null,
    };
}
```

### 2. MANA добавлен в KnownMetaTxContracts с правильными именами

**ВАЖНО:** EIP-712 domain name отличается для разных сетей!

```csharp
private static readonly Dictionary<string, ContractMetaTxInfo> KnownMetaTxContracts = new ()
{
    // MANA on Polygon Mainnet - name starts with "(PoS)"
    { "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", new ContractMetaTxInfo("(PoS) Decentraland MANA", "1", 137) },

    // MANA on Polygon Amoy Testnet - name ends with "(PoS)" WITHOUT space!
    { "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", new ContractMetaTxInfo("Decentraland MANA(PoS)", "1", 80002) },
    
    // ... other contracts
};
```

### 3. Выбор relay сервера по chainId

```csharp
private static string GetRelayServerUrl(int chainId)
{
    bool isMainnet = chainId == 137;
    return isMainnet 
        ? "https://transactions-api.decentraland.org/v1/transactions"
        : "https://transactions-api.decentraland.zone/v1/transactions";
}
```

---

## Тестирование

### Запуск для тестирования на Amoy

```
--dclenv zone --debug --donations-ui
```

### Ручной тест domain separator

1. В Unity найти объект с `TestOnChainWeb3`
2. Context Menu → `CheckManaAmoyDomainSeparator`
3. Смотреть логи — покажет какие параметры совпадают с контрактом

### Ожидаемые логи при успехе

```
[ThirdWeb] Sending meta-transaction via Decentraland relay
[ThirdWeb] From: 0x..., Contract: 0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0
[ThirdWeb] ★ FOUND in KnownMetaTxContracts: Name='Decentraland MANA(PoS)', Version='1', ChainId=80002
[ThirdWeb] Contract info - Name: Decentraland MANA(PoS), Version: 1, ChainId: 80002
[ThirdWeb] Using relay server for chainId=80002: https://transactions-api.decentraland.zone/v1/transactions
[ThirdWeb] Meta-tx nonce: 0
[EIP712-Manual] ✅ ADDRESSES MATCH! Signature is valid for our computed hash.
✅ Donation relay transaction sent!
Transaction Hash: 0x...
```

---

## Troubleshooting

### Ошибка: `high_congestion`

```
Current network gas price exceeds max gas price allowed
```

Газ на Polygon слишком дорогой. Relay лимит — 800 gwei. Подождите пока газ упадёт.

Мониторинг: https://polygonscan.com/gastracker

### Ошибка: `SIGNER_AND_SIGNATURE_DO_NOT_MATCH`

Проверьте:
1. Правильное ли EIP-712 domain name используется?
   - Mainnet: `"(PoS) Decentraland MANA"`
   - Amoy: `"Decentraland MANA(PoS)"` (без пробела!)
2. Version = `"1"`?
3. Правильный chainId (137 для mainnet, 80002 для Amoy)?

---

## Ссылки

- **MetaTxReadme.md** — документация по мета-транзакциям для Gifting (работает!)
- **decentraland-transactions** npm: https://www.npmjs.com/package/decentraland-transactions
- **manaToken.ts** (источник EIP-712 имён): https://github.com/decentraland/decentraland-transactions/blob/master/src/contracts/manaToken.ts
- **MANA Contract (Polygon)**: https://polygonscan.com/address/0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4
- **MANA Contract (Amoy)**: https://amoy.polygonscan.com/address/0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0
- **Transactions API (mainnet)**: https://transactions-api.decentraland.org
- **Transactions API (testnet)**: https://transactions-api.decentraland.zone
