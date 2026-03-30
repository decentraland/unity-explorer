# Web3 & Authentication

## dApp Authentication Process

Allows the user authentication in the application via web3 wallet signing.

The class: `DappWeb3Authenticator : IWeb3VerifiedAuthenticator`.

The `LoginAsync` process consists in:
1. Connects via web socket ([Socket.IO lib](https://github.com/doghappy/socket.io-client-csharp)) to the [auth-server](https://github.com/decentraland/auth-server?tab=readme-ov-file) `https://auth-api.decentraland.zone`/`https://auth-api.decentraland.org`
2. Request a signature for an ephemeral login message
3. The server responds with a `requestId`
4. The client opens a new browser tab through [explorer-website](https://github.com/decentraland/explorer-website) to let the user sign through a custom installed wallet using the `requestId`
5. After the user signs the payload, the server sends a message to the client with the resultant auth-chain information

Identities expire after 1 week.


## Identity Cache Management & Local Storage

After the user successfully authenticates, the identity is stored in two sources, via `ProxyWeb3Authenticator`:
- PlayerPrefs `PlayerPrefsIdentityProvider`, so the identity can be used in the following sessions without the need of the login process
- Memory `MemoryWeb3IdentityCache`, so it can be accessed anywhere without performance implications


## How to Get the User Address

It is required that the user is already authenticated, through `IWeb3Authenticator.LoginAsync(..)` during the application flow.

Then you can access the address by: `IWeb3IdentityCache.Identity.Address`. Example:

```csharp
public class MyClass
{
    private readonly IWeb3IdentityCache web3IdentityCache;

    public MyClass(IWeb3IdentityCache web3IdentityCache)
    {
        this.web3IdentityCache = web3IdentityCache;
    }

    public void DoSomething()
    {
        Web3Address ownUserId = web3IdentityCache.Identity!.Address;
        Debug.Log($"My address is: {ownUserId}");
    }
}
```


## How to Authorize a Request with the Auth-Chain

Some systems in Decentraland require a user authorization to access different kinds of resources, like saving your profile.

Here is how you can get an `AuthChain`:

```csharp
using AuthChain authChain = IWeb3IdentityCache.Identity!.Sign("your payload information");
```

Depending on the required request structure, it may vary how you can fill the parameters, but here is an example:
```csharp
private readonly IWeb3IdentityCache web3IdentityProvider;

private void SignRequest(UnityWebRequest unityWebRequest)
{
    using AuthChain authChain = web3IdentityProvider.Identity!.Sign("your payload information");

    var i = 0;

    foreach (AuthLink link in authChain)
    {
        unityWebRequest.SetRequestHeader($"x-identity-auth-chain-{i}", link.ToJson());
        i++;
    }
}
```


## Signing Messages into the Blockchain

Web3 operations require authorization/signing by a wallet (i.e. Metamask). For example an SDK scene wants to allow the user to make an ERC20 transaction of XX tokens, or get the current ERC20 balance, or get the gas price, etc.

Follows the [Metamask JSON RPC API reference](https://docs.metamask.io/wallet/reference/json-rpc-api/) for messaging schemas.

In order to complete the transactions, a request is sent to the [auth-server](https://github.com/decentraland/auth-server) and then a browser tab is opened to allow the user to authorize the transaction. It is required that the user is already logged in, since it requires a valid `authChain`.

By injecting an `EthereumController` provider into the JS context, scenes may request Ethereum transactions following [EthereumController.sendAsync](https://js-sdk-toolchain.pages.dev/funcs/js_runtime_apis.__system_EthereumController_.sendAsync) specification.

### Whitelisting

In order to execute `IEthereumApi.SendAsync(...)` it is required that the RPC method is allowed in the client, otherwise it will throw a `Web3Exception`.

`DappWeb3Authenticator` is the class that implements `IEthereumApi`. Via constructor it is possible to inject a whitelist. For example:

```csharp
new DappWeb3Authenticator(..., whitelistMethods: new HashSet<string> { "eth_signTypedData_v4", "personal_sign", "wallet_getPermissions" });
```

### From the Explorer Client (C#)

```csharp
[Serializable]
public struct GetPermissionResponse
{
    public string id;
    public string parentCapability;
    public string invoker;
    public long date;
    public Caveats[] caveats;

    [Serializable]
    public struct Caveats
    {
        public string type;
        public string[] value;
    }
}

public class MyClass
{
    private readonly IEthereumApi ethereumApi;
    private readonly IWeb3IdentityCache identityCache;

    public MyClass(IEthereumApi ethereumApi,
        IWeb3IdentityCache identityCache)
    {
        this.ethereumApi = ethereumApi;
        this.identityCache = identityCache;
    }

    public async UniTask MakePersonalSign(CancellationToken ct)
    {
        string signature = await ethereumApi.SendAsync<string>(new EthApiRequest
        {
            method = "personal_sign",
            @params = new object[] { "0x00", identityCache.Identity!.Address.ToString() },
        }, ct);

        Debug.Log($"My signature is: {signature}");
    }

    public async UniTask GetPermissions(CancellationToken ct)
    {
        GetPermissionResponse[] permissions = await ethereumApi.SendAsync<GetPermissionResponse[]>(new EthApiRequest
        {
            method = "wallet_getPermissions",
            @params = Array.Empty<object>(),
        }, ct);

        Debug.Log($"List of permissions is: {JsonUtility.ToJson(permissions)}");
    }
}
```

### From the SDK (TypeScript/JavaScript)

The `EthereumApiWrapper.SendAsync(...)` class acts as a bridge to the SDK and responds to [SendAsyncResponse](https://js-sdk-toolchain.pages.dev/interfaces/js_runtime_apis.__system_EthereumController_.SendAsyncResponse), being [jsonAnyResponse](https://js-sdk-toolchain.pages.dev/interfaces/js_runtime_apis.__system_EthereumController_.SendAsyncResponse#jsonAnyResponse) field represented in the schema:
```json
{
    "id": "number",
    "jsonrpc": "2.0",
    "result": "the raw data from https://docs.metamask.io/wallet/reference/json-rpc-api/"
}
```

Requesting user's mana balance:
```typescript
import {createEthereumProvider} from "@dcl/sdk/ethereum-provider";
import {ContractFactory, RequestManager} from "eth-connect";
import manaAbi from "./contracts/manaAbi";

export async function readUserManaBalance() {
    try {
        const provider = createEthereumProvider()
        const requestManager = new RequestManager(provider)
        const factory = new ContractFactory(requestManager, manaAbi)
        const manaSepoliaContractAddress = '0xfa04d2e2ba9aec166c93dfeeba7427b2303befa9'
        const contract = (await factory.at(manaSepoliaContractAddress)) as any
        const userAddress = '0x...'
        const balance = await contract.balanceOf(userAddress)
    } catch (error) {
        console.error('Errored reading user mana balance', error)
    }
}
```

Another example for requesting `eth_signTypedData_v4` method:
```typescript
import {createEthereumProvider, RPCSendableMessage} from "@dcl/sdk/ethereum-provider";

async function request(message: RPCSendableMessage): Promise<any> {
  const provider = createEthereumProvider()
  return new Promise((resolve, reject) =>
      provider.sendAsync(message, (error, result) => {
        if (error) {
          reject(error)
        }
        resolve(result)
      })
  )
}

export async function signTypedData() {
    try {
      const userAddress = '0x...';
      const result = await request({
            id: 20,
            jsonrpc: '2.0',
            method: 'eth_signTypedData_v4',
            params: [
                userAddress,
                {
                    types: {
                        EIP712Domain: [
                            {
                                name: 'name',
                                type: 'string'
                            },
                            {
                                name: 'version',
                                type: 'string'
                            },
                            {
                                name: 'chainId',
                                type: 'uint256'
                            },
                            {
                                name: 'verifyingContract',
                                type: 'address'
                            }
                        ],
                        Person: [
                            {
                                name: 'name',
                                type: 'string'
                            },
                            {
                                name: 'wallet',
                                type: 'address'
                            }
                        ],
                        Mail: [
                            {
                                name: 'from',
                                type: 'Person'
                            },
                            {
                                name: 'to',
                                type: 'Person'
                            },
                            {
                                name: 'contents',
                                type: 'string'
                            }
                        ]
                    },
                    primaryType: 'Mail',
                    domain: {
                        name: 'Ether Mail',
                        version: '1',
                        chainId: 11155111,
                        verifyingContract: '0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC'
                    },
                    message: {
                        from: {
                            name: 'Cow',
                            wallet: '0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826'
                        },
                        to: {
                            name: 'Bob',
                            wallet: '0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB'
                        },
                        contents: 'Hello, Bob!'
                    }
                }
            ]
        })
        console.log('Personal sign result', result)
    } catch (error) {
        console.error('Errored signing message', error)
    }
}
```
