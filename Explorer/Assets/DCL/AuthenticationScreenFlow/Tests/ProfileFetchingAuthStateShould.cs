using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using DCL.Web3.Identities;
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

        [UnityTest]
        public IEnumerator CancelStalledFetchOnTimeout() =>
            UniTask.ToCoroutine(async () =>
            {
                // The state machine has no states registered: transitions attempted by the flow throw and are logged
                // through the fire-and-forget UniTaskVoid; those logs are irrelevant to the invariant under test
                LogAssert.ignoreFailingMessages = true;

                using var cts = new CancellationTokenSource();
                var root = new GameObject(nameof(ProfileFetchingAuthStateShould));

                try
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

                    var machine = new MVCStateMachine<AuthStateBase>();
                    var selfProfile = new StalledSelfProfile();

                    // The controller is only captured for the Cancel button listener, which is never invoked here
                    var controller = (AuthenticationScreenController)FormatterServices.GetUninitializedObject(typeof(AuthenticationScreenController));

                    var state = new ProfileFetchingAuthState(
                        machine,
                        screenView,
                        controller,
                        new ReactiveProperty<AuthStatus>(AuthStatus.None),
                        selfProfile,
                        Substitute.For<IWeb3IdentityCache>());

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
                    // Unblock the pending attempt (even on assertion failure) so the detached flow finishes
                    // inside this test's ignore-failing-messages window instead of erroring into a later test
                    cts.Cancel();

                    for (var i = 0; i < 32; i++)
                        await UniTask.Yield();

                    UnityEngine.Object.DestroyImmediate(root);
                }
            });

        [UnityTest]
        public IEnumerator SurfaceExternalCancellationInsteadOfMissingProfile() =>
            UniTask.ToCoroutine(async () =>
            {
                var selfProfile = new StalledSelfProfile();
                using var cts = new CancellationTokenSource();

                UniTask<Profile?> fetch = ProfileFetchingAuthState.FetchProfileWithTimeoutAsync(
                    selfProfile, TimeSpan.FromSeconds(FETCH_TIMEOUT_SECONDS), cts.Token);

                float deadline = UnityEngine.Time.realtimeSinceStartup + 5f;

                while (selfProfile.CapturedTokens.Count == 0 && UnityEngine.Time.realtimeSinceStartup < deadline)
                    await UniTask.Yield();

                Assert.That(selfProfile.CapturedTokens.Count, Is.EqualTo(1), "the fetch must be in flight before the external cancel");

                cts.Cancel();

                try
                {
                    Profile? result = await fetch;

                    Assert.Fail("cancelling the flow token must surface as OperationCanceledException, not be read as " +
                                $"\"no deployed profile\" (got {(result == null ? "null" : "a profile")}); a null here wipes " +
                                "a still-valid cached identity on the cached flow");
                }
                catch (OperationCanceledException) { }
            });

        [UnityTest]
        public IEnumerator ThrowTimeoutWhenFetchStalls() =>
            UniTask.ToCoroutine(async () =>
            {
                var selfProfile = new StalledSelfProfile();

                try
                {
                    await ProfileFetchingAuthState.FetchProfileWithTimeoutAsync(
                        selfProfile, TimeSpan.FromSeconds(0.25), CancellationToken.None);

                    Assert.Fail("a stalled fetch must surface as TimeoutException");
                }
                catch (TimeoutException) { }

                Assert.That(selfProfile.CapturedTokens.Count, Is.EqualTo(1), "the fetch must run exactly once");

                Assert.That(selfProfile.CapturedTokens[0].IsCancellationRequested, Is.True,
                    "a timed-out fetch must cancel its own request instead of abandoning it");
            });

        [UnityTest]
        public IEnumerator ReturnNullWhenProfileIsNotDeployed() =>
            UniTask.ToCoroutine(async () =>
            {
                var selfProfile = new MissingProfileSelfProfile();

                Profile? result = await ProfileFetchingAuthState.FetchProfileWithTimeoutAsync(
                    selfProfile, TimeSpan.FromSeconds(FETCH_TIMEOUT_SECONDS), CancellationToken.None);

                Assert.That(result, Is.Null);
                Assert.That(selfProfile.Calls, Is.EqualTo(1), "a genuine \"no deployed profile\" must resolve on the single fetch");
            });

        private static void SetBackingField(object target, Type declaringType, string propertyName, object value)
        {
            FieldInfo? field = declaringType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"auto-property backing field for {declaringType.Name}.{propertyName} not found");
            field!.SetValue(target, value);
        }

        /// <summary>
        ///     Simulates a stalled catalyst request. Mirrors the production contract of
        ///     <see cref="SelfProfile.ProfileAsync" />: cancellation is suppressed into a null profile
        ///     (RealmProfileRepository's suppressing GetAsync extension), never surfaced as an exception.
        /// </summary>
        private class StalledSelfProfile : ISelfProfile
        {
            public readonly List<CancellationToken> CapturedTokens = new ();

            public event Action<Profile>? ProfilePropagated;

            public async UniTask<Profile?> ProfileAsync(CancellationToken ct)
            {
                CapturedTokens.Add(ct);

                try { return await UniTask.Never<Profile?>(ct); }
                catch (OperationCanceledException) { return null; }
            }

            public UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true) =>
                UniTask.FromResult<Profile?>(null);

            public UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct, bool updateAvatarInWorld = true) =>
                UniTask.FromResult<Profile?>(null);

            public void Dispose() { }
        }

        /// <summary>
        ///     Simulates a responsive catalyst that has no deployed profile for the address:
        ///     resolves to null immediately, without any cancellation involved.
        /// </summary>
        private class MissingProfileSelfProfile : ISelfProfile
        {
            public int Calls { get; private set; }

            public event Action<Profile>? ProfilePropagated;

            public UniTask<Profile?> ProfileAsync(CancellationToken ct)
            {
                Calls++;
                return UniTask.FromResult<Profile?>(null);
            }

            public UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true) =>
                UniTask.FromResult<Profile?>(null);

            public UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct, bool updateAvatarInWorld = true) =>
                UniTask.FromResult<Profile?>(null);

            public void Dispose() { }
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
