using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiles.Self;
using DCL.Web3;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Profiles.Tests
{
    public class SelfProfileShould
    {
        private const string WALLET = "0x0000000000000000000000000000000000000001";

        // More reads than a request list holds, so the oldest ones are dropped and must be issued again.
        private const int READS_IN_ONE_FRAME = RequestIds.CAPACITY + 4;

        private IProfileRepository profileRepository = null!;
        private World world = null!;
        private SelfProfile? selfProfile;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();
            profileRepository = Substitute.For<IProfileRepository>();
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            selfProfile?.Dispose();
            World.Destroy(world);
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [UnityTest]
        public IEnumerator AnswerEveryReadIssuedInOneFrameOnceTheProfileIsKnown() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                AnyGet().Returns(UniTask.FromResult<ProfileTier?>(Profile.NewRandomProfile(WALLET)));
                selfProfile = NewSelfProfile();
                ProfileReadResult first = await selfProfile.ProfileAsync(CancellationToken.None);
                Assert.That(first.IsOk(out _), Is.True, $"the profile must be known before the burst, got {first}");

                // Act
                ProfileReadResult[] results = await UniTask.WhenAll(ReadBurst());

                // Assert
                AssertAllOk(results);
            });

        [UnityTest]
        public IEnumerator AnswerEveryReadIssuedInOneFrameWhileTheFetchIsInFlight() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                var fetch = new UniTaskCompletionSource<ProfileTier?>();
                AnyGet().Returns(fetch.Task);
                selfProfile = NewSelfProfile();

                // Act
                UniTask<ProfileReadResult[]> reads = UniTask.WhenAll(ReadBurst());
                await UniTask.Yield();
                fetch.TrySetResult(Profile.NewRandomProfile(WALLET));
                ProfileReadResult[] results = await reads;

                // Assert
                AssertAllOk(results);
            });

        [UnityTest]
        public IEnumerator ReturnCancelledOnlyForTheCallersToken() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                AnyGet().Returns(new UniTaskCompletionSource<ProfileTier?>().Task);
                selfProfile = NewSelfProfile();
                using var cts = new CancellationTokenSource();
                UniTask<ProfileReadResult> read = selfProfile.ProfileAsync(cts.Token);

                // Act
                await UniTask.Yield();
                cts.Cancel();
                ProfileReadResult result = await read;

                // Assert
                Assert.That(result.IsCancelled, Is.True, $"expected Cancelled, got {result}");
            });

        [UnityTest]
        public IEnumerator EndAPendingReadAsCancelledOnDispose() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                AnyGet().Returns(new UniTaskCompletionSource<ProfileTier?>().Task);
                selfProfile = NewSelfProfile();
                UniTask<ProfileReadResult> read = selfProfile.ProfileAsync(CancellationToken.None);
                await UniTask.Yield();

                // Act
                selfProfile.Dispose();
                ProfileReadResult result = await read;

                // Assert
                Assert.That(result.IsCancelled, Is.True, $"expected Cancelled, got {result}");
            });

        [UnityTest]
        public IEnumerator RevertToAnIntactProfileWhenADeployFails() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                Profile fetched = Profile.NewRandomProfile(WALLET);
                fetched.ClearLinks();
                AnyGet().Returns(UniTask.FromResult<ProfileTier?>(fetched));

                var rejection = new InvalidOperationException("deploy rejected");
                profileRepository.SetAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromException(rejection));
                LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(rejection.Message)));

                // The real cache disposes the instance a publish replaces.
                selfProfile = NewSelfProfile(new DefaultProfileCache());
                ProfileReadResult first = await selfProfile.ProfileAsync(CancellationToken.None);
                Assert.That(first.IsOk(out Profile? known), Is.True, $"the profile must be known before the edit, got {first}");
                Profile edited = new ProfileBuilder().From(known!).WithGuestMode(known!.HasConnectedWeb3).Build();

                // Act
                ProfileDeployResult deploy = await selfProfile.DeployProfileAsync(edited, CancellationToken.None);
                ProfileReadResult afterRevert = await selfProfile.ProfileAsync(CancellationToken.None);

                // Assert
                Assert.That(deploy.IsError(out ProfileDeployError error), Is.True, $"expected a failed deploy, got {deploy}");
                Assert.That(error, Is.EqualTo(ProfileDeployError.DeployFailed));
                Assert.That(afterRevert.IsOk(out Profile? reverted), Is.True, $"expected the reverted profile, got {afterRevert}");
                Assert.That(reverted!.Links, Is.Not.Null, "the revert point must not be the instance the cache disposed");
            });

        /// <summary>Built after the repository is arranged: the drain loop fetches on the next frame, whichever frame the test body starts in.</summary>
        private SelfProfile NewSelfProfile(IProfileCache? profileCache = null)
        {
            IEmoteStorage emoteStorage = Substitute.For<IEmoteStorage>();
            emoteStorage.BaseEmotesUrns.Returns(new List<URN>());

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(WALLET));

            return new SelfProfile(profileRepository, new MemoryWeb3IdentityCache { Identity = identity }, Substitute.For<IEquippedWearables>(),
                Substitute.For<IWearableStorage>(), emoteStorage, Substitute.For<IEquippedEmotes>(), forcedEmotes: null, profileCache ?? Substitute.For<IProfileCache>(),
                world, world.Create(), new ForcedWearables());
        }

        private UniTask<ProfileReadResult>[] ReadBurst()
        {
            var reads = new UniTask<ProfileReadResult>[READS_IN_ONE_FRAME];

            for (var i = 0; i < reads.Length; i++)
                reads[i] = selfProfile!.ProfileAsync(CancellationToken.None);

            return reads;
        }

        private static void AssertAllOk(ProfileReadResult[] results)
        {
            for (var i = 0; i < results.Length; i++)
                Assert.That(results[i].IsOk(out _), Is.True, $"read {i} of {results.Length} got {results[i]}");
        }

        private UniTask<ProfileTier?> AnyGet() =>
            profileRepository.GetAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(),
                Arg.Any<bool>(), Arg.Any<IProfileRepository.FetchBehaviour>(), Arg.Any<ProfileTier.Kind>(), Arg.Any<IPartitionComponent?>());
    }
}
