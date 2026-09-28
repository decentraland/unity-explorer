# Signed-handshake terminal control — September 16

## Scope and base

Independent-review HIGH: Connector can send `Kicked(KR_BANNED)` after SignedChallenge but before Welcome. The ongoing listener is not running yet, so the signed handshake must interpret that packet itself. Work starts at `3bc122e0609f439571446511480fb01448500ac4` on the existing `codex/it2-sep15-unity-explorer` checkout. Initial tracked status was clean; `.claude/Library-6.4-pulse-lsd/` and `Explorer/.codex/` remain preserved. No worktree, clone, cache copy, protocol mutation, remote write or deployment.

Plan: reproduce using actual `ArchipelagoSignedConnection.ConnectAsync` and serialized ChallengeRequest/ChallengeResponse/SignedChallenge/ServerPacket exchanges; handle packet variants before Welcome access with the original logical-session generation; retain ordinary Welcome and transport recovery; test the close/frame race and access-denied UI; verify focused Unity suites and implicated lint sites.

## Protocol provenance update (no regeneration)

The iteration-2 source is PR #490 commit `09dbc6c471a985442269d7c7c5cd8c5151c1ad98`. Its Archipelago schema and fixtures are byte-identical to the original `be8111cd238ca25712ab8394345120c624f448c2` handoff. Existing Unity C# SHA-256 remains `9896bebec52112e53212b4368b08d97af80d16adc1b9777ea3b57589263e7b17`; fixture SHA-256 remains `d3bd07d976f4bdbec57c257e4c7306b70e3fb94ec883e4275e3b3d82ee30c3c2`. Both were rehashed locally. These are historical generated bindings, not newly regenerated #490 output.

The protocol owner verified a real immutable PR-CI snapshot, **not a stable npm release**:

- Version: `1.0.0-35083924613.commit-f1ef305`.
- URL: `https://sdk-team-cdn.decentraland.org/@dcl/protocol/branch//dcl-protocol-1.0.0-35083924613.commit-f1ef305.tgz`.
- Archive SHA-256: `608c57ff3c4cea636599cce2afff8b8f684c40f7e724932c9db181c7c3510ffd`.
- Integrity: `sha512-4R7LZrV+aklrUQy61at9gn+lSQoBe7Lb5ztWEUkV43CWkIffJHfCSNQuOVzuj7paGqGoC45U3k5R+Qqckd4Plg==`.
- Package commit is synthetic PR merge `f1ef3056bee1e46036153188ac3be18084b13a54`, whose tree `47acefda3ee6d8b7ca970b4556cafbcccfaad75d` equals the source-head tree.
- Producer CI: `https://github.com/decentraland/protocol/actions/runs/35083924613/job/104754164647`; compatibility run `35083918841`.

Producer verification is documented in `E:/Decentraland/it2/codex-protocol-iteration2/out-session-takeover/iteration-split-report.md`: 137 packaged source files matched exact Git blobs; clean isolated installation and 11 wire goldens passed. Those producer checks were read, not rerun by Unity. Backend consumers own their pin updates/clean builds. Unity retains the verified vendored C# without gratuitous code generation or dependency changes.

## Acceptance boundaries

The September 16 single-active-Gatekeeper-process decision supersedes earlier report wording: brief rolling-release overlap and the reproduced lagging-replica OLD-after-NEW outcome are **accepted limitations, not blockers, and not fixed**. No distributed locks, shared ownership fencing or durable cross-replica replay are added or required by this fix. Lost control/process restart must still resolve through bounded visible client recovery/failure. Single-process interoperability, consumer artifact verification, independent review and separately authorized controlled LiveKit Cloud acceptance remain relevant gates.

## Executed evidence

Evidence directory: `C:/Users/agapo/AppData/Local/Temp/dcl-t06/`. Heavy runs use nonblocking `Local\DCL_It2_HeavyVerification`, check >=5 GB free RAM, hold the mutex until process exit and release it in `finally`. An initial occupied attempt exited 75 without running tests.

Final focused GREEN on the handshake fix: `handshake-ban-EditMode-green.xml` reports root Passed, 65/65; `handshake-ban-PlayMode-green.xml` reports root Passed, 15/15. Both Unity exits were 0. Existing Unity 6000.5.9f1/Library was used; free RAM was 28.62/28.61 GB. InspectCode and repository warning filter exited 0 (28.47 GB before lint, DOTNET_PROCESSOR_COUNT=2). No findings in the changed signed connection or new handshake fixture. The existing PlayMode popup test retains three AccessToDisposedClosure findings and one redundant qualifier; none is in the new handshake test. Full-project inspector has unrelated baseline warnings and is not claimed clean. Shared mutex released in finally after all processes exited.

- Fixture-only aborted attempt: `handshake-ban-red.log`. `IWeb3Identity.Random.Sign` deliberately throws, so SignedChallenge was never reached and one fixture wait was unbounded. Only that exact Unity process and its identified import worker were stopped after checking command lines. The mutex holder exited and released in `finally`. This is not product RED evidence. Fixtures now use `DecentralandIdentity` and a real local signing account, and every delayed-response wait has cancellation.
- Actual RED: `handshake-ban-red-signed.xml`, 7 tests, 2 passed, 5 failed, Unity exit 2. Ban, supersession, unknown reason, empty response and close-before-kick-delivery all left status Active instead of terminal. Ordinary Welcome and old-generation rejection passed. Mutex released and explicitly handed off to connector/social before further heavy verification.

The implementation passes the authentication-start generation into the response handler and checks both logical and transport generations after receiving. Welcome remains successful. Kicked ban/new-session/unknown map to Banned/Superseded/Unknown; unexpected variants map to Unknown. Existing session suppression closes the transport and refuses later Connect calls. The handshake now awaits the transport receive result instead of racing it against an `IsConnected` polling task: native close state cannot cancel a queued kick before it is interpreted. A genuine receive close/error retains ordinary recovery and caller cancellation remains effective. No new timeout flag, enum, wire definition or UI behavior is introduced.

## Exact changed paths

- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/LiveConnections/ArchipelagoSignedConnection.cs`
- `Explorer/Assets/DCL/Tests/Editor/ArchipelagoSignedHandshakeShould.cs`
- `Explorer/Assets/DCL/Tests/Editor/ArchipelagoSignedHandshakeShould.cs.meta`
- `Explorer/Assets/DCL/Tests/PlayMode/ArchipelagoHeartbeatAcceptanceShould.cs`
- `docs/session-takeover-implementation-report.md`
- `docs/handshake-ban-sep16-report.md`

The transport in the new regression is a deterministic wire-level double. Tests invoke the real signed-connection recovery loop and local signing implementation, parse outgoing ClientPackets and return serialized server messages. The PlayMode case uses the existing popup prefab/controller. This is client executable evidence, not a live Connector/Cloud deployment or external wallet-authentication test.
