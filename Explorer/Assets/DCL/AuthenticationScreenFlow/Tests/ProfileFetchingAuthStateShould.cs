using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class ProfileFetchingAuthStateShould
    {
        // Mirrors ProfileFetchingAuthState.PROFILE_FETCH_TIMEOUT (private)
        private const float FETCH_TIMEOUT_SECONDS = 15f;

        // Long enough for the fetch timeout to fire and be observed
        private const float OBSERVATION_SECONDS = 16.5f;

        private const string FAKE_WALLET = "0x0000000000000000000000000000000000000001";

        [UnityTest]
        public IEnumerator CancelStalledFetchOnTimeout() =>
            UniTask.ToCoroutine(async () =>
            {
                // No states registered: transitions throw and log via the fire-and-forget flow; those logs are irrelevant here
                LogAssert.ignoreFailingMessages = true;

                using var cts = new CancellationTokenSource();
                var root = new GameObject(nameof(ProfileFetchingAuthStateShould));

                try
                {
                    var selfProfile = new StalledSelfProfile();

                    // The controller is only captured for the Cancel button listener, which is never invoked here
                    var controller = (AuthenticationScreenController)FormatterServices.GetUninitializedObject(typeof(AuthenticationScreenController));

                    ProfileFetchingAuthState state = NewState(root, new MVCStateMachine<AuthStateBase>(), controller,
                        new ReactiveProperty<AuthStatus>(AuthStatus.None), selfProfile, skipExistingAccountLobby: false);

                    state.Enter(new ProfileFetchingPayload(Substitute.For<IWeb3Identity>(), true, cts.Token));

                    float deadline = UnityEngine.Time.realtimeSinceStartup + OBSERVATION_SECONDS;

                    while (UnityEngine.Time.realtimeSinceStartup < deadline)
                        await UniTask.Yield();

                    Assert.That(selfProfile.CapturedTokens.Count, Is.EqualTo(1), "the profile fetch must run exactly once (no retries)");

                    Assert.That(selfProfile.CapturedTokens[0].IsCancellationRequested, Is.True,
                        $"the fetch's token must be cancelled once the {FETCH_TIMEOUT_SECONDS}s timeout elapses; " +
                        "an uncancelled token means the request was abandoned and keeps poisoning the repository's ongoing batch");
                }
                finally
                {
                    // Unblock the pending attempt so the detached flow finishes inside this test's ignore-failing-messages window
                    cts.Cancel();

                    for (var i = 0; i < 32; i++)
                        await UniTask.Yield();

                    UnityEngine.Object.DestroyImmediate(root);
                }
            });

        [UnityTest]
        public IEnumerator CompleteExistingAccountLoginWhenLobbyIsSkipped() =>
            CompleteExistingAccountLoginWhenLobbyIsSkippedAsync(isRestoredSession: false, AuthStatus.LoggedIn);

        [UnityTest]
        public IEnumerator CompleteExistingAccountLoginWhenLobbyIsSkippedOnRestoredSession() =>
            CompleteExistingAccountLoginWhenLobbyIsSkippedAsync(isRestoredSession: true, AuthStatus.LoggedInCached);

        [UnityTest]
        public IEnumerator SurfaceExternalCancellationInsteadOfMissingProfile() =>
            UniTask.ToCoroutine(async () =>
            {
                var selfProfile = new StalledSelfProfile();
                using var cts = new CancellationTokenSource();

                UniTask<ProfileReadResult> fetch = ProfileFetchingAuthState.FetchProfileWithTimeoutAsync(
                    selfProfile, TimeSpan.FromSeconds(FETCH_TIMEOUT_SECONDS), cts.Token);

                float deadline = UnityEngine.Time.realtimeSinceStartup + 5f;

                while (selfProfile.CapturedTokens.Count == 0 && UnityEngine.Time.realtimeSinceStartup < deadline)
                    await UniTask.Yield();

                Assert.That(selfProfile.CapturedTokens.Count, Is.EqualTo(1), "the fetch must be in flight before the external cancel");

                cts.Cancel();

                ProfileReadResult result = await fetch;

                Assert.That(result.IsError(out ProfileReadError error), Is.True, $"cancelling the flow token must surface as a result, got {result}");
                Assert.That(error, Is.EqualTo(ProfileReadError.Cancelled), "a cancelled read must not be read as \"no deployed profile\"; that wipes a still-valid cached identity on the cached flow");
            });

        [UnityTest]
        public IEnumerator ReturnCancelledWhenFetchStalls() =>
            UniTask.ToCoroutine(async () =>
            {
                var selfProfile = new StalledSelfProfile();

                ProfileReadResult result = await ProfileFetchingAuthState.FetchProfileWithTimeoutAsync(
                    selfProfile, TimeSpan.FromSeconds(0.25), CancellationToken.None);

                Assert.That(result.IsError(out ProfileReadError error), Is.True, $"a stalled fetch must give up, got {result}");
                Assert.That(error, Is.EqualTo(ProfileReadError.Cancelled));
                Assert.That(selfProfile.CapturedTokens.Count, Is.EqualTo(1), "the fetch must run exactly once");

                Assert.That(selfProfile.CapturedTokens[0].IsCancellationRequested, Is.True,
                    "a timed-out fetch must cancel its own request instead of abandoning it");
            });

        [UnityTest]
        public IEnumerator ReturnNotFoundWhenProfileIsNotDeployed() =>
            UniTask.ToCoroutine(async () =>
            {
                var selfProfile = new MissingProfileSelfProfile();

                ProfileReadResult result = await ProfileFetchingAuthState.FetchProfileWithTimeoutAsync(
                    selfProfile, TimeSpan.FromSeconds(FETCH_TIMEOUT_SECONDS), CancellationToken.None);

                Assert.That(result.IsError(out ProfileReadError error), Is.True, $"expected NotFound, got {result}");
                Assert.That(error, Is.EqualTo(ProfileReadError.NotFound));
                Assert.That(selfProfile.Calls, Is.EqualTo(1), "a genuine \"no deployed profile\" must resolve on the single fetch");
            });

        [UnityTest]
        public IEnumerator ClearARestoredIdentityWhoseProfileIsNotDeployed() =>
            UniTask.ToCoroutine(async () =>
            {
                EcsTestsUtils.SetUpFeaturesRegistry();

                using var cts = new CancellationTokenSource();
                var root = new GameObject(nameof(ProfileFetchingAuthStateShould));

                try
                {
                    IWeb3IdentityCache identityCache = Substitute.For<IWeb3IdentityCache>();
                    var selfProfile = new MissingProfileSelfProfile();

                    ProfileFetchingAuthState state = NewState(root, new MVCStateMachine<AuthStateBase>(), AuthenticationScreenControllerShould.NewNeverShownController(),
                        new ReactiveProperty<AuthStatus>(AuthStatus.None), selfProfile, skipExistingAccountLobby: false, identityCache);

                    state.Enter(new ProfileFetchingPayload(NewIdentity(), isRestoredSession: true, cts.Token));
                    await SettleAsync();

                    Assert.That(selfProfile.Calls, Is.EqualTo(1));
                    identityCache.Received(1).Clear();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    EcsTestsUtils.TearDownFeaturesRegistry();
                }
            });

        [UnityTest]
        public IEnumerator KeepARestoredIdentityWhenTheFetchFails() =>
            UniTask.ToCoroutine(async () =>
            {
                EcsTestsUtils.SetUpFeaturesRegistry();

                using var cts = new CancellationTokenSource();
                var root = new GameObject(nameof(ProfileFetchingAuthStateShould));

                try
                {
                    IWeb3IdentityCache identityCache = Substitute.For<IWeb3IdentityCache>();
                    var selfProfile = new FailingProfileSelfProfile();

                    ProfileFetchingAuthState state = NewState(root, new MVCStateMachine<AuthStateBase>(), AuthenticationScreenControllerShould.NewNeverShownController(),
                        new ReactiveProperty<AuthStatus>(AuthStatus.None), selfProfile, skipExistingAccountLobby: false, identityCache);

                    state.Enter(new ProfileFetchingPayload(NewIdentity(), isRestoredSession: true, cts.Token));
                    await SettleAsync();

                    Assert.That(selfProfile.Calls, Is.EqualTo(1));
                    identityCache.DidNotReceive().Clear();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    EcsTestsUtils.TearDownFeaturesRegistry();
                }
            });

        [UnityTest]
        public IEnumerator StartAvatarSelectionWhenAFreshLoginHasNoDeployedProfile() =>
            UniTask.ToCoroutine(async () =>
            {
                EcsTestsUtils.SetUpFeaturesRegistry();

                // The avatar-selection state is an uninitialized stand-in: entering it throws into the logged connection-error path, but the machine has already moved to it
                LogAssert.ignoreFailingMessages = true;

                using var cts = new CancellationTokenSource();
                var root = new GameObject(nameof(ProfileFetchingAuthStateShould));

                try
                {
                    IWeb3IdentityCache identityCache = Substitute.For<IWeb3IdentityCache>();
                    var selfProfile = new MissingProfileSelfProfile();
                    var machine = new MVCStateMachine<AuthStateBase>();
                    var avatarSelection = (SelectAvatarForNewAccountAuthState)FormatterServices.GetUninitializedObject(typeof(SelectAvatarForNewAccountAuthState));
                    machine.AddStates(avatarSelection);

                    ProfileFetchingAuthState state = NewState(root, machine, AuthenticationScreenControllerShould.NewNeverShownController(),
                        new ReactiveProperty<AuthStatus>(AuthStatus.None), selfProfile, skipExistingAccountLobby: false, identityCache);

                    state.Enter(new ProfileFetchingPayload(NewIdentity(), isRestoredSession: false, cts.Token));
                    await SettleAsync();

                    Assert.That(machine.CurrentState, Is.SameAs(avatarSelection), "a fresh login without a deployed profile goes on to create one");
                    identityCache.DidNotReceive().Clear();
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                    UnityEngine.Object.DestroyImmediate(root);
                    EcsTestsUtils.TearDownFeaturesRegistry();
                }
            });

        // Entering the missing lobby state throws into the connection-error path, so reaching LoggedIn proves the login completed without the lobby
        private static IEnumerator CompleteExistingAccountLoginWhenLobbyIsSkippedAsync(bool isRestoredSession, AuthStatus expectedStatus) =>
            UniTask.ToCoroutine(async () =>
            {
                EcsTestsUtils.SetUpFeaturesRegistry();

                using var cts = new CancellationTokenSource();
                var root = new GameObject(nameof(ProfileFetchingAuthStateShould));

                try
                {
                    AuthenticationScreenController controller = AuthenticationScreenControllerShould.NewNeverShownController();
                    var selfProfile = new ExistingProfileSelfProfile(Profile.NewRandomProfile(FAKE_WALLET));

                    ProfileFetchingAuthState state = NewState(root, new MVCStateMachine<AuthStateBase>(), controller,
                        controller.CurrentState, selfProfile, skipExistingAccountLobby: true);

                    state.Enter(new ProfileFetchingPayload(Substitute.For<IWeb3Identity>(), isRestoredSession, cts.Token));

                    float deadline = UnityEngine.Time.realtimeSinceStartup + 5f;

                    while (controller.CurrentState.Value != expectedStatus && UnityEngine.Time.realtimeSinceStartup < deadline)
                        await UniTask.Yield();

                    Assert.That(selfProfile.Calls, Is.EqualTo(1), "the profile fetch must run exactly once");

                    Assert.That(controller.CurrentState.Value, Is.EqualTo(expectedStatus),
                        $"an existing profile must complete the login through {nameof(AuthenticationScreenController.CompleteExistingAccountLogin)} " +
                        $"instead of entering {nameof(LobbyForExistingAccountAuthState)}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    EcsTestsUtils.TearDownFeaturesRegistry();
                }
            });

        private static ProfileFetchingAuthState NewState(GameObject root, MVCStateMachine<AuthStateBase> machine, AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState, SelfProfile selfProfile, bool skipExistingAccountLobby, IWeb3IdentityCache? identityCache = null)
        {
            AuthenticationScreenView screenView = root.AddComponent<AuthenticationScreenView>();

            var viewGo = new GameObject(nameof(ProfileFetchingAuthView));
            viewGo.transform.SetParent(root.transform);
            StubProfileFetchingAuthView fetchingView = viewGo.AddComponent<StubProfileFetchingAuthView>();

            var buttonGo = new GameObject("CancelButton");
            buttonGo.transform.SetParent(viewGo.transform);
            Button cancelButton = buttonGo.AddComponent<Button>();

            SetBackingField(fetchingView, typeof(ProfileFetchingAuthView), nameof(ProfileFetchingAuthView.CancelButton), cancelButton);
            SetBackingField(screenView, typeof(AuthenticationScreenView), nameof(AuthenticationScreenView.ProfileFetchingAuthView), fetchingView);

            controller.SkipExistingAccountLobby = skipExistingAccountLobby;

            return new ProfileFetchingAuthState(
                machine,
                screenView,
                controller,
                currentState,
                selfProfile,
                identityCache ?? Substitute.For<IWeb3IdentityCache>());
        }

        private static IWeb3Identity NewIdentity()
        {
            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(FAKE_WALLET));
            return identity;
        }

        /// <summary>The fetch flow is fire-and-forget; a few frames let its continuations run.</summary>
        private static async UniTask SettleAsync()
        {
            for (var i = 0; i < 4; i++)
                await UniTask.Yield();
        }

        private static void SetBackingField(object target, Type declaringType, string propertyName, object value)
        {
            FieldInfo? field = declaringType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"auto-property backing field for {declaringType.Name}.{propertyName} not found");
            field!.SetValue(target, value);
        }

        /// <summary>
        ///     Stalled catalyst request: the read settles only when its token is cancelled, as <c>Cancelled</c>.
        /// </summary>
        private class StalledSelfProfile : SelfProfile
        {
            public readonly List<CancellationToken> CapturedTokens = new ();

            public override SelfProfileModel CurrentProfileSnapshot => SelfProfileModel.NoIdentity();

            public override async UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
            {
                CapturedTokens.Add(ct);

                try { return await UniTask.Never<ProfileReadResult>(ct); }
                catch (OperationCanceledException) { return ProfileReadResult.FromError(ProfileReadError.Cancelled); }
            }

            public override UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct, bool localOnly = false) =>
                UniTask.FromResult(ProfileDeployResult.FromError(ProfileDeployError.NoIdentity));
        }

        /// <summary>
        ///     Responsive catalyst with a deployed profile: resolves to it immediately.
        /// </summary>
        private class ExistingProfileSelfProfile : SelfProfile
        {
            private readonly Profile profile;

            public int Calls { get; private set; }

            public override SelfProfileModel CurrentProfileSnapshot =>
                SelfProfileModel.FromIdentified(new Identified(profile.UserId, ProfileKnowledge.FromKnown(profile), ProfileActivity.Idle()));

            public ExistingProfileSelfProfile(Profile profile)
            {
                this.profile = profile;
            }

            public override UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
            {
                Calls++;
                return UniTask.FromResult(ProfileReadResult.FromOk(profile));
            }

            public override UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct, bool localOnly = false) =>
                UniTask.FromResult(ProfileDeployResult.FromOk(edited));
        }

        /// <summary>
        ///     Responsive catalyst with no deployed profile: resolves to <c>NotFound</c> immediately, no cancellation involved.
        /// </summary>
        private class MissingProfileSelfProfile : SelfProfile
        {
            private readonly UserId address = UserId.NewRandom();

            public int Calls { get; private set; }

            public override SelfProfileModel CurrentProfileSnapshot =>
                SelfProfileModel.FromIdentified(new Identified(address, ProfileKnowledge.Missing(), ProfileActivity.Idle()));

            public override UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
            {
                Calls++;
                return UniTask.FromResult(ProfileReadResult.FromError(ProfileReadError.NotFound));
            }

            public override UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct, bool localOnly = false) =>
                UniTask.FromResult(ProfileDeployResult.FromError(ProfileDeployError.NoIdentity));
        }

        /// <summary>Responsive catalyst whose read fails: resolves to <c>FetchFailed</c> immediately, no cancellation involved.</summary>
        private class FailingProfileSelfProfile : SelfProfile
        {
            private readonly UserId address = UserId.NewRandom();

            public int Calls { get; private set; }

            public override SelfProfileModel CurrentProfileSnapshot =>
                SelfProfileModel.FromIdentified(new Identified(address, ProfileKnowledge.FromFailed(new ProfileFailure(new Exception("fetch failed"))), ProfileActivity.Idle()));

            public override UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
            {
                Calls++;
                return UniTask.FromResult(ProfileReadResult.FromError(ProfileReadError.FetchFailed));
            }

            public override UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct, bool localOnly = false) =>
                UniTask.FromResult(ProfileDeployResult.FromError(ProfileDeployError.NoIdentity));
        }
    }

    public class StubProfileFetchingAuthView : ProfileFetchingAuthView
    {
        public override UniTask ShowAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false) =>
            UniTask.CompletedTask;
    }
}
