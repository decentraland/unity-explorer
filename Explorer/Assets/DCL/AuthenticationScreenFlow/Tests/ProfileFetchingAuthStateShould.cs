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
        private const float ATTEMPT_TIMEOUT_SECONDS = 15f;

        // Long enough for the first attempt to time out and a retry to start, short enough to stay before a second timeout
        private const float OBSERVATION_SECONDS = 16.5f;

        [UnityTest]
        public IEnumerator CancelStalledFetchAndRetryOnTimeout() =>
            UniTask.ToCoroutine(async () =>
            {
                // The state machine has no states registered: transitions attempted by the flow throw and are logged
                // through the fire-and-forget UniTaskVoid; those logs are irrelevant to the invariant under test
                LogAssert.ignoreFailingMessages = true;

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

                    using var cts = new CancellationTokenSource();

                    state.Enter(new ProfileFetchingPayload(Substitute.For<IWeb3Identity>(), true, cts.Token));

                    float deadline = UnityEngine.Time.realtimeSinceStartup + OBSERVATION_SECONDS;

                    while (UnityEngine.Time.realtimeSinceStartup < deadline)
                        await UniTask.Yield();

                    Assert.That(selfProfile.CapturedTokens.Count, Is.GreaterThanOrEqualTo(1), "the profile fetch was never started");

                    Assert.That(selfProfile.CapturedTokens[0].IsCancellationRequested, Is.True,
                        $"the first fetch attempt's token must be cancelled once the {ATTEMPT_TIMEOUT_SECONDS}s attempt timeout elapses; " +
                        "an uncancelled token means the request was abandoned and keeps poisoning the repository's ongoing batch");

                    Assert.That(selfProfile.CapturedTokens.Count, Is.GreaterThanOrEqualTo(2),
                        "a timed-out attempt must be retried before the login flow gives up");

                    // Unblock the pending attempt so the flow finishes inside the ignore-failing-messages window
                    cts.Cancel();

                    for (var i = 0; i < 32; i++)
                        await UniTask.Yield();
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
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
    }

    public class StubProfileFetchingAuthView : ProfileFetchingAuthView
    {
        public override UniTask ShowAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false) =>
            UniTask.CompletedTask;
    }
}
