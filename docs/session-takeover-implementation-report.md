# Session takeover client implementation

September 16 superseding update: see `handshake-ban-sep16-report.md` for the signed-handshake review fix, PR #490 source/artifact provenance and accepted release-overlap decision. Earlier cross-replica/replay blocker wording below is historical: brief rollout overlap is accepted (not fixed), durable multi-replica architecture is out of scope, and a verified immutable PR-CI protocol snapshot now exists. Consumer pin/build verification and controlled Cloud acceptance remain distinct gates.

## Scope and plan

Implementation follows `session-takeover-client-plan.md` (September 15 amendment and acceptance matrix). The existing checkout at `E:/Decentraland/unity-explorer` remains on `codex/it2-sep15-unity-explorer`, based on the preserved `d7b949e91a` work. At the start, only `.claude/Library-6.4-pulse-lsd/` was untracked. No additional checkout or Library copy was made.

1. Import canonical generated Archipelago messages and fixtures from the protocol owner.
2. Share authenticated session status through the existing identity-cache lifetime, with logical-session, socket-listener, and room-operation fences.
3. Integrate WS and Pulse control, bounded pending/recovery, and existing popup/re-authentication flow.
4. Execute Unity EditMode/PlayMode acceptance and repository lint; retain local commits for review.

## Implemented behavior

`SessionControl` resides in the Web3 identity assembly, shared per `IWeb3IdentityCache` through a weak-key table. Room `StartAsync` cannot reset its terminal state, and explicit authenticated control does not depend on the legacy `StopOnDuplicateIdentity` flag. Generic LiveKit `ParticipantRemoved` behavior remains separate.

- Authenticated `KR_NEW_SESSION` and Pulse `DUPLICATE_SESSION` latch supersession; `KR_BANNED` and Pulse `BANNED` show access denied. Unknown kick/status values stop automatic recovery without claiming confirmed supersession.
- Pending holds room recovery and Pulse reauthentication. A lost WS may recover using the existing ephemeral key after `retry_after_ms`; it does not create a new identity. A fixed watchdog bounds waiting, repeated status cannot extend it indefinitely, and unreasonable waits fail closed. Initial lost status/assignment also has a 30-second watchdog while initial Pulse authentication remains allowed.
- Only a non-empty ordinary island assignment releases pending. Terminal state rejects late assignments. Socket/listener generations reject callbacks from a replaced transport; room-operation revisions reject credential work spanning a pending/terminal transition.
- Pulse routing latches duplicate-session rejection even when it arrives during the initial handshake, before the ordinary disconnect callback. Terminal state suppresses automatic retries and stops existing transports/rooms.
- The existing duplicate-session popup displays pending, failed, connected-elsewhere, or access-denied text. `Reconnect here` clears the cached identity and invokes the existing logout/authentication initialization flow. Recovery stays suppressed if authentication returns the displaced ephemeral key; only a different key advances the logical session. Ban has no reconnect action.

## Generated contract provenance

Protocol source commit: `be8111cd238ca25712ab8394345120c624f448c2`.

The imported `Explorer/Assets/Protocol/DecentralandProtocol/Archipelago.gen.cs` is byte-for-byte identical to the protocol owner's generated artifact, SHA-256 `9896bebec52112e53212b4368b08d97af80d16adc1b9777ea3b57589263e7b17`. Existing metadata is retained. Canonical fixtures are copied to `Explorer/TestResources/iteration-2/session-control.json`.

Wire additions: `ServerPacket.session_status=7`; `SessionStatusMessage.state=1`, `retry_after_ms=2`; `SessionStatus` unspecified/pending/failed = 0/1/2; `KR_NEW_SESSION=0` unchanged, `KR_BANNED=1`. No handwritten schema, generated definitions, local package path, or unrelated dependency pin was introduced.

Reproduce with the protocol repository's `scripts/build-session-artifacts.ps1` at that commit (protoc 22.2), then import the generated C# while retaining its `.meta`. Remote publication is not required to compile this vendored C# consumer; coordinated server publication and pins remain release work.

## Verification evidence

All heavy commands use nonblocking `Local\DCL_It2_HeavyVerification`; occupied attempts exit 75 and perform no tests. Each acquired run checks physical RAM before launching and holds the mutex through child exit. Existing Unity 6000.5.9f1 and `Explorer/Library` are used.

- RED compile: `takeover-edit-red.log` reported CS0246 for the UI-to-Multiplayer assembly reference. No tests executed in that failed build. Moving the shared state to Web3 fixed the dependency direction.
- GREEN initial EditMode: `takeover-edit-green.xml`, 39/39 passed, Unity exit 0.
- GREEN expanded EditMode: `takeover-edit-final.xml`, 50/50 passed, Unity exit 0 (before final canonical-fixture assertion).
- GREEN final EditMode: `takeover-EditMode-final.xml`, 51/51 passed, Unity exit 0, including all 11 canonical protocol golden messages.
- GREEN PlayMode: `takeover-play.xml`, 12/12 passed, Unity exit 0. Includes pending then assignment, terminal kick with legacy flag disabled, ban/unknown distinctions, replaced-listener fencing, and prior heartbeat/reconnect/takeover acceptance.
- GREEN expanded PlayMode: `takeover-PlayMode-final.xml`, 13/13 passed, Unity exit 0, adding the previous-room-disconnect race fence.
- Inspection completed with `inspectcode.exe Explorer/Explorer.sln --no-build --verbosity=INFO --properties:Configuration=Debug --disable-settings-layers:SolutionPersonal`, using the bundled Windows runtime config; repository warning filter exited 0. Its no-build resolution reported a false redundant `LiveKit.Internal` import: removing it produced a real CS0246 for `FfiHandleFactory`. The import was restored. `takeover-EditMode-head.log` and `takeover-PlayMode-head.log` record those exit-1 compile attempts, with no tests executed. No zero-warning claim is made from this inspection.
- RED full EditMode: `takeover-EditMode-verified.xml`, 25,942 total, 25,925 passed, 6 failed, 11 skipped (200.569 seconds), Unity exit 2. Four failures reproduce the prior Windows Prefs baseline; two new convention failures rejected direct `Interlocked`/`Volatile` APIs. Replaced them with existing `DCLInterlocked`/`DCLVolatile` wrappers, preserving atomic generation fencing without convention suppressions. The paired PlayMode run remained 13/13 green.
- Full EditMode at implementation commit `47fa03a797`: `takeover-EditMode-release-candidate.xml`, 25,942 total, 25,927 passed, **4 failed**, 11 skipped (195.902 seconds), Unity exit 2. Only the four previously recorded `DCL.Prefs.Tests.FileDCLPlayerPrefsSlotReclamationShould` failures remain. Both new threading-convention failures are GREEN. This is not an all-green full suite.
- Paired `takeover-PlayMode-release-candidate.xml`: 13/13 passed, Unity exit 0.

Evidence files are under `C:/Users/agapo/AppData/Local/Temp/dcl-t06/`.

Implementation commit: `47fa03a79741506482b769dcf79a4de3b1847f9a`, directly atop preserved `d7b949e91a944f298083d972f6d0951a8393e857`. It changes 27 source/fixture paths; the complete list below also includes the follow-up PlayMode assembly reference. The report is a separate documentation commit. No remote branch was updated.

A final popup-only follow-up handles generic LiveKit notice arriving before authenticated supersession. The existing visible notice switches to authenticated status instead of remaining in an indefinite legacy wait. Its regression instantiates the existing prefab, runs the MVC lifecycle, invokes the actual `ExitButton.onClick` rebound as `Reconnect here`, checks the cache is empty inside authentication, supplies a different ephemeral identity, and verifies recovery. This still does not claim live external wallet authentication or Cloud acceptance.

Popup follow-up commit: `d7752c727a910f1d912e40e29e48fe504cb540ee`. Its first EditMode attempt (`takeover-popup-final.xml`) had 52 passing cases and exit 0, but the suite itself was **Failed(Child)**: resetting the real input singleton calls `Destroy`, which is invalid in EditMode. Therefore that attempt is not counted as GREEN. The UI lifecycle regression was moved to PlayMode, with an explicit reference to the existing UI assembly. `takeover-EditMode-handoff.log` records the intermediate missing-UI-reference compile failure before that reference was added.

Final test placement commit: `966ff0767df45aef77b2be3962b498a4fb8dcebd`. Final focused evidence on this source:

- `takeover-EditMode-handoff-green.xml`: suite **Passed**, 51/51 passed, zero failures, Unity exit 0.
- `takeover-PlayMode-handoff-green.xml`: suite **Passed**, 14/14 passed, zero failures, Unity exit 0. The actual popup/button lifecycle case passed in 0.721 seconds.
- `git diff d7b949e91a..HEAD --check`: no whitespace errors. The canonical generated C# SHA-256 still matches the protocol owner's artifact.

The full suite was run at `47fa03a797`; the subsequent popup/test-placement changes received the final focused EditMode and PlayMode runs above, not another full 25,942-case run. Code-standards checks influenced the use of the existing WebGL-safe threading wrappers and real Unity assembly/test boundaries.

Final repository inspection at `966ff0767d` completed with exit 0; the standard filter also exited 0 (`takeover-inspect-final.json`, `takeover-inspect-final-filtered.json`). **Lint is not certified clean.** Remaining diagnostics in touched files include existing naming/nullability/unused-member findings and no-build false positives (including the compiler-required LiveKit import). Newly authored diagnostic sites requiring review are the listener cancellation closure at `LiveConnectionArchipelagoSignFlow.cs:87` (unsubscribed before disposal, with concurrent post-disposal cancellation caught), the bounded popup-test authentication callback at `ArchipelagoHeartbeatAcceptanceShould.cs:322–323` (awaited before its cache/identity disposal), and a redundant test qualifier at line 314. These are retained explicitly rather than hidden with inspection suppression directives. The final Unity compiler and executable tests above are the runtime evidence; repository/CI lint review remains a handoff gate.

Test command basis: `D:/Unity/Editor/6000.5.9f1/Editor/Unity.exe -batchmode -nographics -projectPath E:/Decentraland/unity-explorer/Explorer -runTests -testPlatform EditMode -testCategory !Performance`, with explicit `-testResults` and `-logFile` paths. PlayMode uses `-testPlatform PlayMode -testFilter ArchipelagoHeartbeatAcceptanceShould`. Each process runs synchronously inside the mutex/RAM guard. The focused EditMode filter was `SessionControlShould;PulseMultiplayerServiceShould;ArchipelagoIslandRoomReconnectShould;FeaturesRegistryArchipelagoHeartbeatsShould;ArchipelagoHeartbeatKillSwitchShould`.

## Exact amendment paths

- `Explorer/Assets/DCL/Infrastructure/Global/Dynamic/CommsContainer.cs`
- `Explorer/Assets/DCL/Infrastructure/Global/Dynamic/DynamicWorldContainer.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/LiveConnections/ArchipelagoSignedConnection.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/ArchipelagoIslandRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/ChatConnectiveRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/Fixed/FixedConnectiveRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/SignFlow/LiveConnectionArchipelagoSignFlow.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/GateKeeper/Rooms/GateKeeperSceneRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Pulse/PulseMultiplayerService.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Pulse/Tests/PulseMultiplayerServiceShould.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Rooms/Connective/ConnectiveRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Rooms/InteriorRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Movement/Systems/PulseContainer.cs`
- `Explorer/Assets/DCL/Multiplayer/Movement/Systems/PulseMultiplayerBus.Disconnects.cs`
- `Explorer/Assets/DCL/Multiplayer/Movement/Systems/PulseMultiplayerBus.Handshake.cs`
- `Explorer/Assets/DCL/Multiplayer/Movement/Systems/PulseMultiplayerBus.cs`
- `Explorer/Assets/DCL/PluginSystem/Global/DuplicateIdentityPlugin.cs`
- `Explorer/Assets/DCL/Tests/PlayMode/ArchipelagoHeartbeatAcceptanceShould.cs`
- `Explorer/Assets/DCL/Tests/PlayMode/DCL.PlayMode.Tests.asmdef`
- `Explorer/Assets/DCL/UI/DuplicateIdentityPopup/DuplicateIdentityWindow.prefab`
- `Explorer/Assets/DCL/UI/DuplicateIdentityPopup/DuplicateIdentityWindowController.cs`
- `Explorer/Assets/DCL/UI/DuplicateIdentityPopup/DuplicateIdentityWindowView.cs`
- `Explorer/Assets/Protocol/DecentralandProtocol/Archipelago.gen.cs`
- `Explorer/Assets/DCL/Tests/Editor/SessionControlShould.cs`
- `Explorer/Assets/DCL/Tests/Editor/SessionControlShould.cs.meta`
- `Explorer/Assets/DCL/Web3/Identities/SessionControl.cs`
- `Explorer/Assets/DCL/Web3/Identities/SessionControl.cs.meta`
- `Explorer/TestResources/iteration-2/session-control.json`
- `docs/session-takeover-implementation-report.md`

## Integration gates

The connector owner confirmed final same-session replacement is close-only on the old socket, with no `KR_NEW_SESSION`; the accepted socket retains its Welcome/registry entry. Sending that terminal reason for socket replacement is incompatible: a kick arriving before replacement acceptance can poison the shared logical session. A future explicit replacement notification requires a distinct approved enum and regenerated consumers.

Cross-repository review must verify exact session targeting, ordered kick-before-close, pending/status replay following lost control and reconnection, and no obsolete Gatekeeper credential issuance. Client tests use deterministic socket/transport doubles; they do not establish real LiveKit Cloud revocation or two-client end-to-end correctness. Cloud next-second cutoff/removal evidence and coordinated server/client release remain required. No push, deployment, publication, CI retry, or remote PR edit is performed here.

Historical connector candidate `822be713d5c3b52bb65c93d691234c4e7d7b1e95` (report head `4ef5c4c`) records an observed RED cross-replica ordering probe: an older held mint resumed after a newer event and published OLD. Its pending/failed replay and remembered displaced-session checks are replica-local TTL mirrors, not durable ownership authority. The September 16 decision accepts brief release overlap as a limitation, not a blocker; this evidence remains RED and is not described as fixed. Distributed ownership fencing and durable multi-replica replay are out of scope. Single-process ordering and bounded visible recovery after process loss remain required. A verified immutable PR #490 CI artifact now exists; consumer pin/build verification, two-client LiveKit Cloud acceptance, review evidence and undrafting remain separate gates. See `handshake-ban-sep16-report.md` for exact provenance.

## Verification cleanup and preserved state

Only changes generated by this turn's Unity runs were restored: `Explorer/Assets/TextMesh Pro/Fonts & Materials/AtkinsonHyperlegibleMono SDF.asset`, `Explorer/Assets/TextMesh Pro/Fonts & Materials/NotoSerif SDF.asset`, `Explorer/ProjectSettings/EditorBuildSettings.asset`, `Explorer/ProjectSettings/ProjectAuditorSettings.asset`, and `Explorer/Assets/Resources/DOTweenSettings.asset`. The last asset's normalized Git blob was verified identical to HEAD before refreshing its index stat data.

After an exact `git clean -n` preview, removed only these untracked generated files: `Explorer/Assets/GPUInstancerPro/Resources.meta`, `Explorer/Assets/GPUInstancerPro/Resources/GPUIShaderBindings.asset`, and `Explorer/Assets/GPUInstancerPro/Resources/GPUIShaderBindings.asset.meta`. Untracked deletions are not recoverable through Git; Unity can regenerate these files. No recursive broad cleanup, ignored-file cleanup, or nested-repository deletion was used.

Preserved `.claude/Library-6.4-pulse-lsd/`, `Explorer/Library/`, and `avatar-preview-renderer/Library/`. A new untracked `Explorer/.codex/config.toml` appeared during the turn and was preserved, not staged or removed. No commits or branches were discarded. Final source working tree is clean apart from these preserved untracked paths and this report until its documentation commit.
