# Unconditional heartbeat retirement — September 16

## Exact task paths

- `Explorer/Assets/DCL/FeatureFlags/FeatureFlagsConfiguration.cs`
- `Explorer/Assets/DCL/FeatureFlags/FeatureFlagsStrings.cs`
- `Explorer/Assets/DCL/FeatureFlags/FeaturesRegistry.cs`
- `Explorer/Assets/DCL/FeatureFlags/Tests/FeaturesRegistryArchipelagoHeartbeatsShould.cs`
- `Explorer/Assets/DCL/FeatureFlags/Tests/FeaturesRegistryArchipelagoHeartbeatsShould.cs.meta`
- `Explorer/Assets/DCL/Infrastructure/Global/Dynamic/CommsContainer.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/ArchipelagoIslandRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/IArchipelagoIslandRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/SignFlow/IArchipelagoSignFlow.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/SignFlow/LiveConnectionArchipelagoSignFlow.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/SignFlow/LogArchipelagoSignFlow.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Tests/ArchipelagoHeartbeatKillSwitchShould.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Tests/ArchipelagoHeartbeatKillSwitchShould.cs.meta`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Tests/SignFlowShould.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Demo/ArchipelagoRoomPlayground.cs`
- `Explorer/Assets/DCL/Tests/PlayMode/ArchipelagoHeartbeatAcceptanceShould.cs`
- `docs/implementation-sep15-report.md`
- `docs/livekit-networking.md`
- `docs/session-takeover-implementation-report.md`
- `Explorer/Assets/DCL/Tests/Editor/ArchipelagoHeartbeatRetirementShould.cs`
- `Explorer/Assets/DCL/Tests/Editor/ArchipelagoHeartbeatRetirementShould.cs.meta`
- `docs/heartbeat-retirement-sep16-report.md`

## Scope

Direct iteration-2 cutover, atop verified handshake fix `e9dccde775` (65/65 EditMode and 15/15 PlayMode). Existing checkout/Library only. Local branch renamed from `codex/it2-sep15-unity-explorer` to `fix/it2-unity-session-cutover`, preserving history and changes. The user subsequently authorized a scoped commit/push and reuse of draft PR #10022; no merge or deployment is authorized.

Removed the heartbeat FeatureId (77; other explicit values unchanged), string, registry entry, default-on helper, room publisher, sign-flow send API and serializer/log decorator. Removed obsolete flag-state fixtures and their metadata. Removed the now-unused character and memory-pool dependencies from the heartbeat path. Wire-generated protocol definitions are intentionally unchanged, not handwritten or regenerated.

Replacement EditMode/PlayMode tests drive repeated real room cycles over the real sign flow and observe the socket boundary for zero sends. Existing socket/fresh-handshake assignment, Pulse, pending/failed/superseded/ban, stale callback and fresh-identity UI coverage remains. Pulse/ENet movement, WebSocket transport keepalive and LiveKit security/capacity/voice are unchanged. Generic feature-flagged LiveKit disconnect handling remains separate from authenticated terminal session control.

## Release contract

Backend Pulse presence, session-addressed assignment/reconnect, and pending/failure/supersession/ban controls must be ready and verified before releasing this client. There is no old-client fallback, presence-source switch, LiveKit presence fallback, shadow path, heartbeat flag or flag rollback. Review/CI and authorized controlled LiveKit Cloud end-to-end acceptance remain external gates. Brief rolling-release overlap remains accepted, not a blocker or claimed fix.

## Evidence

**Final retirement changes are UNTESTED.** No tests, builds, compiler, lint, formatting or Unity editor were run in the final code-only/PR preparation phase, per explicit user instruction. No successful compile or test result is claimed for the retirement head. Handshake genuine RED (7 total, 5 failed) and earlier GREEN are preserved in `handshake-ban-sep16-report.md`; those results predate retirement and do not certify it. Retirement is a structural deletion with replacement test source; no separate pre-deletion RED was run for this step.

First retirement verification (`retirement-EditMode-green.log`) exited 1 at compilation: removal of the heartbeat code also removed a still-required `DCL.Web3.Identities` import from the sign flow. Restored the import. No tests ran, no GREEN is claimed for this attempt, and this compile failure is not a deliberate product RED. The process released the mutex in finally and handed the queue to connector/Gatekeeper before retry. Free RAM was 28.73 GB; Unity job-worker-count=2, DOTNET_PROCESSOR_COUNT=2 and MONO_GC_PARAMS=max-heap-size=2g.

The second attempt (`retirement-EditMode-green2.log`) also exited 1 at compilation: the demo's removed character dependency led to removal of `DCL.Utility`, still needed by `ILaunchMode`. That import was restored by source edit. No tests ran. Both attempts released the mutex; subsequent process inventory confirmed no task-owned verification process remained. Verification stopped on user instruction, with no further slot handoff.

An unowned formatting/member-order-only diff appeared in `Explorer/Assets/DCL/Web3/Identities/SessionControl.cs`. It is preserved and excluded from this task's commit. Its SHA-256 remains `3CF7AEE3142879F670D1AC6F1CAED0F53777EEEECB468E6531E12A21F65580A6`. Earlier committed SessionControl implementation remains in the PR; only the unrelated working-tree formatting is excluded. Existing caches/configuration and generated asset/project-setting changes are also excluded and preserved.

## Draft PR and base

Reuse https://github.com/decentraland/unity-explorer/pull/10022 targeting `dev`, remote head branch `feat/it2-wp8-pulse-presence`. Remote head inspected at `55eb84d0df4c4c14d8c643f9093d9e8912f1e8d6`; range-diff accounts for all 11 original PR commits in the rebased history, with converter compatibility adaptations and additional scoped local work. Update only with a lease on that exact head, never an unconditional force push. No `codex/` branch is published.

The candidate includes merged #9980 (`328fd02ac08ed6909b94314c3a14168ef1d8252f`) and was rebased on `06de13e1d490fe47b90ad4908d68820d6b8c4ffa`. Current remote `dev` is `34bab01365c48a3da7049174c1169ed12aa98726`; its later commits, including protocol migration #10060, have not been integrated or validated here. Current-dev integration, compiler/EditMode/PlayMode acceptance, CI/review and coordinated backend release readiness remain gates. Draft status must not imply release readiness.
