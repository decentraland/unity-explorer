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

The domain is **composed into** each url rather than substituted afterwards: every template
in `RawUrl` interpolates the `hostDomain` field (`$"https://peer.{hostDomain}/about"`), so
there is no `decentraland.` literal repeated across the table, no placeholder token, and no
replace pass on resolution. `Url()` only caches what `RawUrl` produced.

`hostDomain` equals `BaseDomain` except in the today environment, which resolves the handful
of hosts it serves from `.today` in its constructor and then flips the field to org for
everything resolved afterwards. That is the one reason urls must stay lazily resolved, and
the one case where `BaseDomain` names the environment rather than where every host lives.

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
  and *instead of* them. Because that grants trust, the value is read while the deep link
  is still deferred, so only the command line can set it.
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
| Genesis City manifest (`WorldManifestProvider`) | the production manifest | A static S3 artifact, not a host under the base domain. |

A deployment that mirrors *mainnet* is therefore out of scope for this flag.

## Not covered

- The `MainSceneLoader` trusted-realm fast-path still lists decentraland hosts
  explicitly. A custom realm is not short-circuited there and is validated against the
  deployment's own catalyst server list instead (which does follow the base domain), so
  the flow works — it just costs one request.
- `LoadHybridSceneSystemLogic` / `IGetHash` (legacy SDK6 hybrid-scene content) and
  `LocalIpfsRealm` (reached only by the test `IRealmData.Fake`) keep their decentraland
  constants.
- Intentionally decentraland: `ThirdWebAuthenticator` RPC endpoints, the goerli /
  test-scene constants, and the marketplace / blog / docs links.
