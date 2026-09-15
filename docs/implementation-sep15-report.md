# September 15 Unity implementation report

## Result

T06/WP8 was rebased from pull request #10022 head `55eb84d0df4c4c14d8c643f9093d9e8912f1e8d6` onto the verified live `origin/dev` head `06de13e1d490fe47b90ad4908d68820d6b8c4ffa` on local branch `codex/it2-sep15-unity-explorer`. The rebase retained the current-dev malformed-peer handling while adding `realm` to `worldName`, and kept both the RustSegment and iteration-2 fixture LF rules in `.gitattributes`.

The implementation-owner code/test commit is `49aa0234b520778718a07dee8b5009db84b27260` (`test: cover heartbeat rollout acceptance`). No branch was pushed, no deployment was attempted, and no pull request state was changed.

## Contract and behavior checks

- `archipelago-heartbeats` remains a launch-snapshotted publisher kill switch. Absent or `true` is default-on; `false` skips packet construction and socket sending while connection maintenance continues.
- Socket recovery and a forced disconnect/connect handshake both receive a new island assignment without sending a heartbeat. The same-island guard and cached-session reconnection state remain in `ArchipelagoIslandRoom`.
- `IWeb3IdentityCache.Default` persists the ephemeral identity through `PlayerPrefsIdentityProvider`. `CommsContainer` supplies the same cache to `IArchipelagoIslandRoom.NewDefault`, while `PulseContainer` receives the same cache and `PulseMultiplayerBus.Handshake` signs through `EnsuredIdentity()`. `ArchipelagoSignedConnection` also challenges and signs through `EnsuredIdentity()` and uses that identity's address.
- No configurable presence source, LiveKit fallback, in-service shadow path, WP3d, or obsolete A9 path was added. LiveKit security, capacity, health, crowd, and voice operations were not changed.
- Filtered online-user lookup uses one all-realms `?id=` request and maps `.dcl.eth` realms to `worldName`; unfiltered lookup retains main-realm semantics.
- `only_sdk7` was removed from request contracts and URLs. Existing `PlacesView` SDK filter controls remain unchanged, and no prefab/view asset changed.
- Takeover handling covers both `DuplicateIdentity` and `ParticipantRemoved`; unrelated connected/client-initiated updates do not set the duplicate-identity stop state.

## Executed verification

All Unity, inspection, and other heavy commands acquired `Local\DCL_It2_HeavyVerification` with `WaitOne(0)` in the same PowerShell process, held it until child exit, and started with more than 23 GB physical RAM free.

### TDD and acceptance

- RED: the first PlayMode compile failed because `ForceFreshIslandAssignmentAsync` was private (`CS1061`, `C:\Users\agapo\AppData\Local\Temp\dcl-t06\playmode-red.log`). It was changed to `internal` as a test seam.
- RED: direct use of `LKDisconnectReason` from the PlayMode assembly failed with `CS0246` (`C:\Users\agapo\AppData\Local\Temp\dcl-t06\takeover-red.log`). The tests were rewritten to exercise the actual private `ConnectiveRoom` update handler/state through reflection without expanding assembly dependencies.
- GREEN, final focused EditMode: 41/41 passed in 2.965 s, zero skipped, zero compiler/import failures (`focused-editmode-final.xml` and `.log`). This includes flag absent/on/off, launch snapshot, same-island/reconnect state, golden converter fixture, and one-request all-realms provider coverage.
- GREEN, final PlayMode: 8/8 passed in 0.286 s, zero skipped, zero compiler/import failures (`playmode-acceptance-final.xml` and `.log`). Cases cover heartbeat absent/on/off, socket recovery, forced fresh handshake, both takeover reasons, and unrelated disconnect handling.
- The outer collector for the final pair returned 2 because its PowerShell function assignment captured formatted output together with each numeric exit code. Both Unity logs independently say `Exiting with code 0 (Ok)`, and both NUnit XML files report `Passed` with zero failures.

### Full suite, imports, fixture, and lint

- Full non-performance EditMode compile/run: 25,921 tests; 25,906 passed, 4 failed, 11 skipped, duration 198.159 s (`full-editmode.xml` and `.log`). There were zero C# compiler errors and zero import/GUID failures.
- All four failures are in `DCL.Prefs.Tests.FileDCLPlayerPrefsSlotReclamationShould`: one slot assertion and three Windows claim-file sharing violations. An isolated rerun reproduced the same 4 failures out of 7. Both the test and implementation paths match `origin/dev` byte-for-byte, so this remains an unrelated current-dev/environment gate rather than a WP8 regression.
- The committed fixture and iteration-2 contract/Pulse copies all have SHA-256 `C6932FFD0F63DC01D1A469674B5F44D5D673D4C1716D6A5D7B39F7097921FB57`.
- New `.meta` files were imported without errors; their GUIDs are unique. The new PlayMode test GUID `e906e23077604dbbb678912f4ee14b6f` occurs once under project assets.
- The repository lint shell wrapper selected the bundled Unix ReSharper runtime on the Windows host and exited 1 without a report. Running the same bundled InspectCode binary/rules with `inspectcode.runtimeconfig.json`, then the repository warning filter, completed successfully. After removing two redundant imports and one redundant `base()` call, the final report contains zero findings in changed C# files.

## Exact branch paths changed from `origin/dev`

- `.gitattributes`
- `Explorer/Assets/DCL/FeatureFlags/FeatureFlagsConfiguration.cs`
- `Explorer/Assets/DCL/FeatureFlags/FeatureFlagsStrings.cs`
- `Explorer/Assets/DCL/FeatureFlags/FeaturesRegistry.cs`
- `Explorer/Assets/DCL/FeatureFlags/Tests/FeaturesRegistryArchipelagoHeartbeatsShould.cs`
- `Explorer/Assets/DCL/FeatureFlags/Tests/FeaturesRegistryArchipelagoHeartbeatsShould.cs.meta` (renamed from the removed decorator metadata)
- `Explorer/Assets/DCL/Infrastructure/Global/Dynamic/PlacesAndEventsContainer.cs`
- `Explorer/Assets/DCL/Infrastructure/Utility/DecentralandUrls/DecentralandUrl.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Rooms/ArchipelagoIslandRoom.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/SignFlow/LiveConnectionArchipelagoSignFlow.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Tests/ArchipelagoHeartbeatKillSwitchShould.cs`
- `Explorer/Assets/DCL/Multiplayer/Connections/Archipelago/Tests/ArchipelagoHeartbeatKillSwitchShould.cs.meta`
- `Explorer/Assets/DCL/Multiplayer/Connectivity/OnlinePlayersJsonDtoConverter.cs`
- `Explorer/Assets/DCL/Multiplayer/Connectivity/OnlineUserData.cs`
- `Explorer/Assets/DCL/Multiplayer/Connectivity/WorldInfoOnlineUsersProviderDecorator.cs` (removed)
- `Explorer/Assets/DCL/Navmap/SearchForPlaceAndShowResultsCommand.cs`
- `Explorer/Assets/DCL/NetworkDefinitions/Browser/DecentralandUrlsSource.cs`
- `Explorer/Assets/DCL/NetworkDefinitions/Browser/GatewayUrlsSource.cs`
- `Explorer/Assets/DCL/NetworkDefinitions/Browser/Tests/DecentralandUrlsSourceShould.cs`
- `Explorer/Assets/DCL/Places/PlacesResultsController.cs`
- `Explorer/Assets/DCL/PlacesAPIService/IPlacesAPIClient.cs`
- `Explorer/Assets/DCL/PlacesAPIService/IPlacesAPIService.cs`
- `Explorer/Assets/DCL/PlacesAPIService/PlacesAPIClient.cs`
- `Explorer/Assets/DCL/PlacesAPIService/PlacesAPIService.cs`
- `Explorer/Assets/DCL/Tests/Editor/ArchipelagoHttpOnlineUsersProviderShould.cs`
- `Explorer/Assets/DCL/Tests/Editor/ArchipelagoHttpOnlineUsersProviderShould.cs.meta`
- `Explorer/Assets/DCL/Tests/Editor/OnlinePlayersJsonDtoConverterShould.cs`
- `Explorer/Assets/DCL/Tests/Editor/OnlinePlayersJsonDtoConverterShould.cs.meta`
- `Explorer/Assets/DCL/Tests/PlayMode/ArchipelagoHeartbeatAcceptanceShould.cs`
- `Explorer/Assets/DCL/Tests/PlayMode/ArchipelagoHeartbeatAcceptanceShould.cs.meta`
- `Explorer/TestResources/iteration-2/http/peers-by-id.json`
- `docs/livekit-networking.md`
- `docs/multiplayer.md`
- `docs/implementation-sep15-report.md`

## Authorized checkout cleanup

Before switching branches, the repository root, HEAD, complete status, untracked preview, nested-repository boundaries, active editor/build processes, and Library locations were inspected. No Unity editor process was open. At cleanup execution time there were no remaining tracked worktree changes for `git restore` to discard.

The following untracked, non-ignored targets were previewed and removed from this repository. Untracked deletions are not recoverable through Git:

- `Explorer/Assets/DCL/Web3/Authenticators/Implementations/Test.meta`
- `Explorer/Assets/DCL/Web3/Authenticators/Implementations/Test/`
- `Explorer/Assets/DCL/_SceneContext/ScenesDebug/ScenesConsistency/Reports/`
- `Explorer/Assets/GPUInstancerPro/Resources.meta`
- `Explorer/Assets/GPUInstancerPro/Resources/`
- `Explorer/Assets/StreamingAssets/Js/DebugScenes/1, 95.js`
- `Explorer/Assets/StreamingAssets/Js/DebugScenes/1, 95.js.meta`
- `Explorer/Assets/ThirdWebUnity.meta`
- `Explorer/Assets/ThirdWebUnity/`
- `Explorer/ProfilerCaptures/`
- `Explorer/TestResources/Images/profile_images_dump.json`
- `Explorer/TestResources/Profiles/web_requests_dump_profiles_optimized.json`
- `Explorer/replay_pid64096.log`
- `Explorer/tests-local-results.xml`
- `InspectCodeFiltered.json`
- `InspectCodeFiltered2.json`
- `InspectCodeReport.json`
- `InspectCodeReport2.json`
- `SECURITY.md`
- `docs/superpowers/`
- `inspectcode-run.log`
- `lint-run.log`
- `lint-run2.log`
- `pr-findings.json`
- `pr7291_threads.json`
- `scripts/15.01.pdf`
- `scripts/PTR N1.zip`
- `scripts/PTR N1/`
- `scripts/PTR N2.zip`
- `scripts/PTR N2/`
- `scripts/Performance benchmark report (PDF).zip`
- `scripts/Performance test results (JSON).zip`
- `scripts/PerformanceTestResults.json`
- `scripts/cloudbuild/__pycache__/`
- `scripts/github_summary_failed.md`
- `scripts/github_summary_fixed.md`
- `scripts/github_summary_p75.md`
- `scripts/github_summary_p75_p95.md`
- `scripts/github_summary_test.md`
- `scripts/lint/cleanup-usings.sh`
- `scripts/socket_monitor/socket_reports/`
- `scripts/summary_XY.pdf`
- `scripts/test_existing.pdf`
- `scripts/test_failed.pdf`
- `scripts/test_fixed.pdf`
- `scripts/test_no_params.pdf`
- `scripts/test_p75.pdf`
- `scripts/test_p75_p95.pdf`
- `scripts/test_refactored.md`
- `scripts/test_refactored.pdf`
- `scripts/test_summary.md`
- `nul` (reserved-name file, zero bytes)
- `Explorer/nul` (reserved-name file, 2,117 bytes)

Verification later regenerated four tracked artifacts and three untracked helper paths. The tracked `Explorer/Assets/TextMesh Pro/Fonts & Materials/AtkinsonHyperlegibleMono SDF.asset`, `Explorer/Assets/TextMesh Pro/Fonts & Materials/NotoSerif SDF.asset`, `Explorer/ProjectSettings/EditorBuildSettings.asset`, and `Explorer/ProjectSettings/ProjectAuditorSettings.asset` changes were restored exactly. The untracked `Explorer/Assets/GPUInstancerPro/Resources.meta`, `Explorer/Assets/GPUInstancerPro/Resources/`, and `scripts/.claude/settings.local.json` targets were removed (plus the final empty `scripts/.claude/` directory). The GPU Instancer targets were regenerated and removed after each relevant run.

The following ignored or cache directories were explicitly preserved and were never copied or deleted:

- `.claude/Library-6.4-pulse-lsd/`
- `Explorer/Library/`
- `avatar-preview-renderer/Library/`

## Remaining gates

- The four unrelated `FileDCLPlayerPrefsSlotReclamationShould` failures prevent claiming an all-green full EditMode suite on this machine.
- The PlayMode tests exercise the actual Unity client paths with deterministic mocked socket/LiveKit callbacks. A deployed iteration-1 ws-connector/comms-gatekeeper environment and multi-client manual acceptance were not available in this implementation-only scope.
- Server rollout prerequisites must be deployed and verified before serving `archipelago-heartbeats=false`. Existing sessions must cycle because the flag is launch-snapshotted.
- CI evidence, reviewer approval, and undrafting #10022 remain required parent/repository-owner actions. No CI retry, GitHub review submission, or undraft was performed.
