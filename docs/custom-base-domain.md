# Custom base domain

The client can target a deployment served under a base domain other than
`decentraland.{org,zone,today}` — an independent catalyst stack, for example
`interconnected.online` — by passing the [`--base-domain`](app-arguments.md#base-domain)
app arg.

`--base-domain` selects a fourth environment, `DecentralandEnvironment.Custom`. Modelling
it as an environment rather than as a silent override is deliberate: the domain is only
one of the things an environment decides, and every other decision gets an explicit arm
for `Custom` instead of falling into a `default` branch whose value nobody chose. Where
the domain cannot imply the answer — the chain — there is a flag for it rather than a
guess.

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

## The chain

A base domain says where the hosts are; it says nothing about which chain they are on, and
nothing in a domain name can. So the chain is named, by
[`--eth-network`](app-arguments.md#eth-network) — `mainnet` or `sepolia`, defaulting to
mainnet.

`ChainUtils.ResolveNetwork` is the single place it is decided, and the resolved
`EthereumNetwork` is what everything downstream is handed. Four decisions used to make it
separately from `DecentralandEnvironment`; none of them does now, so they cannot disagree:

| Decision | Follows the resolved network |
| --- | --- |
| Ethereum net version / chain id / network id (`ChainUtils`) | mainnet or sepolia |
| Marketplace credits (`CreditsChainConfig`) | Polygon with mainnet, Amoy with sepolia |
| Donations MANA contract (`DonationsService`) | Polygon with mainnet, Amoy with sepolia |
| Stored identity slot (`PlayerPrefsIdentityProvider`) | one slot per network |

Each ethereum network carries exactly one polygon network because a deployment is on both or
neither: an identity signed for mainnet has no business spending against test credits
contracts, and a test identity has no business against the real ones.

**Decentraland's own environments cannot be moved.** `ChainUtils.PinnedNetworkOf` fixes org
and today to mainnet and zone to sepolia; `--eth-network` paired with one of them is reported
in the log and dropped. Their contracts, identities and backends are all on that one chain, so
a client pointed at zone signing against mainnet is simply wrong — there is no deployment for
which that combination is what the operator meant. A custom base domain is the only stack the
client knows nothing about, and the only one the flag speaks for.

**A value the client cannot map is refused rather than defaulted**, because the default is
mainnet: reaching it by accident is what puts real contracts and the production identity behind
an operator who asked for a test chain. `--eth-network sepolai`, or the flag with no value at
all, reports the problem and exits instead of launching. As everywhere in this client, the value
is a separate token — `--eth-network sepolia`, never `--eth-network=sepolia`.

Like `--base-domain`, it is applied **from the command line only**: a `decentraland://` link
carrying it is denied, and accepting it in the denied-params dialog does not apply it (the
client logs a warning saying so). A link is attacker-supplied and a consent dialog is a weak
place to decide which chain a wallet signs for.

**Open: the identity slot is keyed by chain, so a mainnet custom deployment shares the
mainnet slot with org.** A session on either can reuse an identity signed on the other, and a
login on either replaces it — so logging in to a custom deployment logs you out of org.
`--eth-network sepolia` moves it to the sepolia slot, which zone also uses.

This is not a property of the identity, which is a wallet signature over a locally generated
ephemeral key and is not chain-scoped at all; the slot key just happens to be named by
environment, and before the chain became explicit it stood in for *which stack minted the
identity*. Scoping the slot by stack — a slot of its own for `Custom`, or one per base domain —
would separate them again without touching which chain the deployment runs on. Worth deciding
before a custom deployment is used against a signed-in production session.

## What `Custom` explicitly does *not* change

These decisions are not domain-derived, and each carries an explicit `Custom` arm:

| Decision | `Custom` resolves to | Why |
| --- | --- | --- |
| Community-message router (`LiveKitChatMessagesBus`, `ChatReactionsFactory`) | `message-router-dev-0` | A custom deployment's comms-message-sfu has to join under this identity for relayed messages to authenticate. |
| Genesis City manifest (`WorldManifestProvider`) | *no manifest* — the fetch is skipped | It describes decentraland's own Genesis City; a custom realm reusing one of its realm names is a different world. |

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

Which chain those signatures are produced for, and which stored-identity slot the session uses,
follow [the chain](#the-chain) — mainnet unless `--eth-network sepolia` says otherwise.

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
