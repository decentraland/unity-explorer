# Custom base domain

The client can target a deployment served under a base domain other than
`decentraland.{org,zone,today}` — an independent catalyst stack, for example
`interconnected.online` — by passing the [`--base-domain`](app-arguments.md#base-domain)
app arg.

`--base-domain` selects a fourth environment, `DecentralandEnvironment.Custom`. Modelling
it as an environment rather than as a silent override is deliberate: the domain is only
one of the things an environment decides, and every other decision (chain, identity
storage, message routing) gets an explicit arm for `Custom` instead of falling into a
`default` branch whose value nobody chose.

## The domain seam

`DecentralandUrlsSource.ResolveBaseDomain(environment, customBaseDomain)` is the single
place the base domain is decided, and `IDecentralandUrlsSource.BaseDomain` is the single
place to read it. Anything needing the domain as a *value* — a host-trust suffix, a comms
hostname — reads it there rather than restating a literal.

The domain is **composed into** each url rather than substituted afterwards: every template in
`RawUrl` interpolates `BaseDomain` (`$"https://peer.{BaseDomain}/about"`), so there is no
`decentraland.` literal repeated across the table, no placeholder token, and no replace pass on
resolution. `Url()` only caches what `RawUrl` produced.

`BaseDomain` is written only by the constructor, and the today environment is the reason it is
writable at all: it resolves the handful of hosts it serves from `.today` while being built and
then moves to org for everything resolved afterwards, so it reports org and urls must stay lazily
resolved.

`ResolveBaseDomain` rejects a `Custom` environment without a domain, a domain paired with
any other environment, and anything that is not a bare domain (scheme, userinfo, port or
path) — the value feeds host-trust checks, so a url smuggled in as a domain must not be
accepted. `DecentralandUrlsSourceShould.LeaveNoDecentralandHostBehindOnACustomBaseDomain`
walks every `DecentralandUrl` and fails if one still resolves to a decentraland host, so a
newly added url with a hand-written domain is caught.

## What follows the base domain

- **Every backend url** (`DecentralandUrlsSource`), including the pre-login feature-flag
  host (`GetFeatureFlagsUrl`), the catalyst server list (`RealmNamesMap`) and the
  smart-wearable content fallback (`SmartWearableCache`).
- **Gateway routing** (`GatewayUrlsSource`): supported hosts route through
  `gateway.<base-domain>/…`. `IsGatewayTransformable` accepts exactly a single-label
  subdomain under this client's own `BaseDomain`, so an overridden or flag-driven host — or
  another environment's domain — passes through untouched. It only engages when the
  `use-gateway` flag served by *that deployment's* feature-flags backend enables it.
- **Teleport validation** (`ChatEnvironmentValidator`): realms must sit under the base
  domain — a custom deployment neither rejects its own realms nor accepts decentraland
  ones.
- **Deep-link realm trust** (`DeepLinkAllowlist.SetTrustedBaseDomain`): hosts under the
  custom base domain are trusted for realm switching exactly like `decentraland.*` hosts,
  and *instead of* them.
- **The startup trusted-realm gate** (`MainSceneLoader.IsTrustedRealmAsync`): every host
  under the custom base domain is trusted, so a `--realm` on that deployment — catalyst or
  world — connects without the untrusted-realm confirmation. See below for why.
- **The main-realm comms hostname** (`RealmController`): a custom deployment groups under
  its own `realm-provider.<base-domain>` rather than decentraland's main-realm island.
- **comms-gatekeeper** stays independently overridable through `--gatekeeper-url`, which
  outranks both the base domain and gateway routing, for the main and the local scene
  adapter alike.

## What `Custom` explicitly does *not* change

These decisions are not domain-derived, and each carries an explicit `Custom` arm:

| Decision | `Custom` resolves to | Why |
| --- | --- | --- |
| Ethereum network (`ChainUtils`) | sepolia | A `--base-domain` stack is unverified; mainnet would put real assets behind it. |
| Marketplace credits (`CreditsChainConfig`), donations (`DonationsService`) | Amoy | Same reason: no mainnet contracts. |
| Stored identity slot (`PlayerPrefsIdentityProvider`) | the sepolia slot | The key is chain-scoped, so it must not overwrite the mainnet identity. |
| Community-message router (`LiveKitChatMessagesBus`, `ChatReactionsFactory`) | `message-router-dev-0` | A custom deployment's comms-message-sfu has to join under this identity for relayed messages to authenticate. |
| Genesis City manifest (`WorldManifestProvider`) | *no manifest* — the fetch is skipped | It describes decentraland's own Genesis City; a custom realm reusing one of its realm names is a different world. |

A deployment that mirrors *mainnet* is therefore out of scope for this flag.

## World manifest, parcel loading and roads

Two independent signals decide that a realm *is Genesis City*, and they key off different
things — which matters because only one of them responds to how the deployment names its realm.

**The manifest path is chosen by realm name, matched against decentraland's own list.**
`WorldManifestProvider.MAIN_REALM_NAMES` is a hardcoded set — `main`, `baldr`, `hela`,
`heimdallr`, `shiva`, `artemis`, `loki`, `dg`, `hephaestus`, `unicorn`, `marvel`, `nftworld` —
compared against `configurations.realmName` from the deployment's `/about`. A custom deployment
calling its main realm `main` collides with that list and takes the genesis branch; calling it
anything else takes neither the genesis nor the `.dcl.eth` world branch. Both end at
`WorldManifest.Empty` today, so the name does not change the outcome — but the list is
decentraland's naming, and a deployment cannot opt out of matching it.

**`RealmKind` ignores the name.** It comes from whether `/about` lists fixed scene URNs: none
means `GenesisCity`, otherwise `World` (`RealmData.Reconfigure`). A custom catalyst realm
therefore classifies as `GenesisCity` whatever it is called — so renaming away from `main` does
not change what follows.

**Roads.** `RoadsPresence` switches instanced road rendering purely on
`RealmKind == GenesisCity`, and the geometry comes from `RoadSettingsAsset` — a local
Addressable baked from decentraland's Genesis City layout by `RoadParser`. So a custom
deployment's genesis realm gets **decentraland's road network drawn over its parcels**,
regardless of what its own content looks like. Nothing here changes that: it is driven by
`RealmKind` and a client-side asset, and `--base-domain` only makes it reachable. Correct for
a deployment that mirrors Genesis City, wrong for one with its own layout — fixing it means
gating roads on something other than `RealmKind`, or shipping per-deployment road data.

**Manifest.** `Custom` resolves to no genesis manifest (above), so `WorldManifest.IsEmpty`:

- `LoadPointersByIncreasingRadiusSystem` only filters by occupied parcels when the manifest
  is non-empty, so a custom genesis realm requests pointers for every parcel in radius rather
  than only occupied ones. An optimisation lost, not a break.
- `LoadFixedPointersSystem` and `Landscape`/`TerrainGenerator` take their empty-manifest
  paths, so there is no manifest-derived terrain or spawn coordinate.
- `RealmData.SingleScene` is unaffected for genesis realms (it returns before consulting the
  manifest), but a custom *world* without a manifest is treated as single-scene — i.e. as
  having no gatekeeper-per-room.

Skipping is still the right trade: the alternative was applying decentraland's occupied-parcel
set to a foreign realm, which would filter *out* that deployment's real parcels and silently
fail to load scenes wherever decentraland has none.

The forward path is per-deployment manifests. `FetchNonGenesisManifestAsync` already reads
`{assetBundleRegistry}/worlds/{realmName}/manifest` and the registry follows the base domain,
so a custom deployment can serve manifests for its own worlds today. Only the *genesis*
manifest has no per-deployment source, because it is a static S3 artifact rather than a
registry endpoint.

## Authentication

The login handshake follows the base domain, and both wallet paths are built on it:

| Endpoint | Under a custom base domain |
| --- | --- |
| `ApiAuth` | `auth-api.<base-domain>` |
| `AuthSignatureWebApp` | `https://<base-domain>/auth/requests` |
| `ApiRpc` (external-wallet dapp path) | `wss://rpc.<base-domain>` |
| `ThirdWebAuthenticator`'s chain RPC map | **unchanged** — `rpc.decentraland.org/{network}` |

So a custom deployment **must** serve the auth API and the signing web app, or login cannot
complete. The chain RPC is deliberately inconsistent between the two paths and worth a
decision: `DappWeb3EthereumApi` resolves `ApiRpc` and therefore needs `rpc.<base-domain>`
over wss on the deployment, while the embedded-wallet path keeps reaching decentraland's chain
proxy — which is arguably right (a custom deployment has no Ethereum of its own) but means the
two disagree about where a chain lives.

Two consequences of `Custom` being a non-production stack (see above): signatures are produced
for **sepolia**, and the identity is stored in the **sepolia slot**, which zone also uses — so a
custom session and a zone session share one cached identity.

The signin deep link is unchanged: the `decentraland://` scheme is registered by the client, so
a custom deployment's auth website must emit that same scheme with `signin` and `authRequestId`.

Whoever sets `--base-domain` controls the page the user signs on. That is inherent to
redirecting the auth host rather than something this flag adds, and it is why the value is
operator-supplied.

## Realm trust under a custom base domain

Three separate gates decide whether a realm is trusted, and all three treat the custom base
domain the way they treat `decentraland.*`:

| Gate | Rule |
| --- | --- |
| `MainSceneLoader.IsTrustedRealmAsync` (startup `--realm`) | any host under `BaseDomain` |
| `DeepLinkAllowlist.IsRealmWhitelisted` (deep-link dev params) | any *subdomain* of `BaseDomain`, and the world must also be flag-whitelisted |
| `ChatEnvironmentValidator` (in-session teleport) | any host under `BaseDomain` |

The startup gate needs the domain rule rather than a host list because the fallback it would
otherwise use — the deployment's catalyst server list — enumerates catalysts, not worlds
servers. That is why decentraland's `worlds-content-server` is hardcoded there; without the
domain rule a custom deployment's worlds would prompt for confirmation on every launch.

Widening trust this way costs nothing, because whoever sets the base domain has already
redirected *every* backend host — profiles, content, comms, feature flags. Trusting realms under
that same domain adds no capability on top of that. The deep-link gate stays deliberately
stricter — subdomains only, and still flag-gated on the world name — because the realm *it* sees
is attacker-supplied while the base domain is not.

`--base-domain` itself is applied from the command line only. That is an ordering constraint
rather than a trust boundary: the domain gates which realms `DeepLinkAllowlist` trusts, so it
must be registered before `InitializeDeepLinks()` evaluates a pending link's whitelisted-realm
params — a domain arriving in that same link could not be, since the link's own params would
need gating against a domain it has not supplied yet. The allowlist denies `base-domain` like
the other infrastructure-pointing params, so a link carrying it still reaches the consent
dialog; accepting it changes nothing, and the client logs a warning saying so.

`IDecentralandUrlsSource.IsSubdomainOf` / `IsHostWithinDomain` hold the `.`-boundary check
these gates share, so the rule that rejects `interconnected.online.attacker.com` and
`evil-interconnected.online` lives in one place.

## Stays on decentraland

The off-platform links — Discord, X, the newsletter, CoinGecko — and the `DecentralandWorlds`
blog link are decentraland's own surfaces and keep pointing there. The marketplace, shop, docs
and help links *do* follow the base domain.
