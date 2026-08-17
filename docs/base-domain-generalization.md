# Base-domain generalization

The client can target a non-`decentraland.*` deployment via the `--base-domain`
app arg, instead of only `decentraland.{org,zone,today}`.

## Implemented

- **URL seam.** `DecentralandUrlsSource.ResolveDomain()` performs every
  URL-template substitution. Default path swaps only the `{ENV}` TLD
  (byte-identical to before, today-mixture safe); a custom base domain replaces
  the whole `decentraland.{ENV}` token, moving every backend host.
  `ResolvedBaseDomain` exposes the value, and `GatewayUrlsSource` derives its
  prefix/suffix/non-client host from it. Fed by `AppArgsFlags.BASE_DOMAIN`.
  Covered by `DecentralandUrlsSourceCharacterizationShould` across every URL,
  default and custom domain.
- **Teleport validation** (`ChatEnvironmentValidator`) and the **main-realm
  comms fallback** (`RealmController`) follow the resolved base domain, so a
  custom deployment neither rejects its own realms nor groups comms under
  `decentraland.org`.
- **Realm trust** (`DeepLinkAllowlist.SetCustomBaseDomain`, wired from
  `--base-domain`): a custom deployment's realms/worlds are whitelisted for
  deep-link switching exactly like `decentraland.*` ones.
- **Realm-name server list** (`RealmNamesMap`) and the pre-login **feature-flag
  whitelist URL** (`GetFeatureFlagsUrl`) follow the resolved domain; the
  **smart-wearable content fallback** (`SmartWearableCache`) resolves through the
  source.
- **Gateway routing** (`GatewayUrlsSource`) needs no change: it rewrites the
  still-templated `{subdomain}.decentraland.{ENV}` host to
  `gateway.decentraland.{ENV}/…`, then `ResolveDomain` substitutes the whole
  `decentraland.{ENV}` token (gateway host included) — so a `--base-domain`
  deployment routes supported hosts through `gateway.{base-domain}/…`, letting
  the catalyst gateway proxy them (comms-gatekeeper included). New tests pin the
  gatekeeper family (`GateKeeperSceneAdapter`, `ChatAdapter`,
  `GatekeeperStatus`, `BannedUsers`) routing through the gateway on both default
  and custom domains, plus `LocalGateKeeperSceneAdapter` — whose host `RawUrl`
  resolves eagerly, so it routes through the gateway on the decentraland domain
  and stays on its own host under a custom one.
- **comms-gatekeeper** stays independently overridable via the existing
  `--gatekeeper-url` arg (top priority in `ResolveGatekeeperOverride`), which
  retargets both the main and local scene adapters regardless of base domain or
  gateway routing.
- **Local-scene development** (`LocalGateKeeperSceneAdapter`) resolves the
  local comms-gatekeeper host against the custom base domain directly (its host
  is a literal, not the `{ENV}` template), so a `--base-domain` deployment
  reaches its own local gatekeeper for the local-scene preview flow.
- **Default "Empty place"** (`PlacesAPIResponse`) carries no image instead of a
  hardcoded `peer.decentraland.org` content URL. The content hash was
  DCL-specific and could not be repointed at another peer, so removing it is the
  only domain-neutral option; every consumer instead passes the placeholder
  sprite its own prefab already ships (`DefaultImagePlace.png`) as
  `ImageController.RequestImage`'s `defaultSprite` — `PlaceInfoPanelController`,
  `PlaceElementView` (search results) and `TeleportPromptController`.

## Out of scope (documented, not changed)

The remaining domain references live in static/const contexts and gate rarely-hit
paths a custom deployment functions without; each needs the URL source injected.

- `LoadHybridSceneSystemLogic` / `IGetHash` — SDK6 hybrid-scene content (legacy).
- `LocalIpfsRealm` — reached only by the test `IRealmData.Fake`, not production.
- `MainSceneLoader` trusted-realm fast-path — already degrades to the dynamic
  server list, so custom realms are validated there.

**Stays decentraland (intentional):** `ThirdWebAuthenticator` RPC endpoints (a
DCL RPC proxy), the goerli/test-scene constants, and marketplace/blog/docs links.
