using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiles.Helpers;
using DCL.Profiles.Self;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using Utility.Fsm;

namespace DCL.Profiles.Tests
{
    public class SelfProfileCmdExecutorShould
    {
        private class RecordingInbox : IMsgInbox<SelfProfileMsg>
        {
            public readonly List<SelfProfileMsg> Sent = new ();

            public void Send(in SelfProfileMsg msg) =>
                Sent.Add(msg);
        }

        private static readonly UserId ALICE = UserId.New("0xAlice").Unwrap();
        private static readonly UserId BOB = UserId.New("0xBob").Unwrap();
        private static readonly URN BASE_EMOTE = "urn:decentraland:off-chain:base-emotes:wave";
        private static readonly URN FORCED_WEARABLE = "urn:decentraland:off-chain:base-avatars:red_hoodie";

        private IProfileRepository profileRepository = null!;
        private IProfileCache profileCache = null!;
        private MemoryWeb3IdentityCache identityCache = null!;
        private IWearableStorage wearableStorage = null!;
        private IEmoteStorage emoteStorage = null!;
        private IEquippedWearables equippedWearables = null!;
        private IEquippedEmotes equippedEmotes = null!;
        private World world = null!;
        private Entity playerEntity;
        private RecordingInbox inbox = null!;
        private SelfProfileCmdExecutor executor = null!;

        [SetUp]
        public void SetUp()
        {
            // Constructing a Profile validates its name against the feature flags.
            EcsTestsUtils.SetUpFeaturesRegistry();

            profileRepository = Substitute.For<IProfileRepository>();
            profileCache = Substitute.For<IProfileCache>();
            identityCache = new MemoryWeb3IdentityCache();
            wearableStorage = Substitute.For<IWearableStorage>();
            emoteStorage = Substitute.For<IEmoteStorage>();
            emoteStorage.BaseEmotesUrns.Returns(new List<URN> { BASE_EMOTE });
            equippedWearables = Substitute.For<IEquippedWearables>();
            equippedEmotes = Substitute.For<IEquippedEmotes>();
            world = World.Create();
            playerEntity = world.Create();
            inbox = new RecordingInbox();
            executor = NewExecutor(new ForcedWearables());
        }

        [TearDown]
        public void TearDown()
        {
            executor.Dispose();
            World.Destroy(world);
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void ReportTheFetchedProfile()
        {
            // Arrange
            Profile fetched = NewProfile(ALICE, 3);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(fetched));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchSucceeded(out FetchSucceeded msg), Is.True);
            Assert.That(msg.Address, Is.EqualTo(ALICE));
            Assert.That(msg.Profile, Is.Not.SameAs(fetched), "the cache owns the fetched instance");
            Assert.That(msg.Profile.IsSameProfile(fetched), Is.True);
        }

        [Test]
        public void FetchFromTheCatalystWithASingleImmediateRequest()
        {
            // Arrange
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 3)));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            profileRepository.Received(1).GetAsync(ALICE.Value, 0, null, Arg.Any<CancellationToken>(), false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet, ProfileTier.Kind.Full, Arg.Any<IPartitionComponent?>());
        }

        [Test]
        public void ReportNotFoundWhenTheRepositoryReturnsNull()
        {
            // Arrange
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(null));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchNotFound(out UserId? address), Is.True);
            Assert.That(address, Is.EqualTo(ALICE));
        }

        [Test]
        public void ReportAFailureWhenTheFetchThrows()
        {
            // Arrange
            var error = new TimeoutException("catalyst timed out");
            AnyGet().Returns(UniTask.FromException<ProfileTier?>(error));
            ExpectReportedException(error);

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchFailed(out FetchFailed msg), Is.True);
            Assert.That(msg.Address, Is.EqualTo(ALICE));
            Assert.That(msg.Failure.Exception, Is.SameAs(error));
        }

        [Test]
        public void FillAnEmptyEmoteWheelWithTheBaseEmotes()
        {
            // Arrange
            Profile fetched = NewProfile(ALICE, 3);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(fetched));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchSucceeded(out FetchSucceeded msg), Is.True);
            Assert.That(msg.Profile.Avatar.Emotes[0], Is.EqualTo(BASE_EMOTE));
        }
        [Test]
        public void MarkTheFetchedProfileAsConnectedWhenTheIdentityIsNotAGuest()
        {
            // Arrange
            identityCache.Identity = NewIdentity(LoginMethod.METAMASK);
            Profile fetched = NewProfile(ALICE, 3);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(fetched));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchSucceeded(out FetchSucceeded msg), Is.True);
            Assert.That(msg.Profile.HasConnectedWeb3, Is.True);
            Assert.That(fetched.HasConnectedWeb3, Is.False, "the cached instance is left as fetched");
        }

        [Test]
        public void KeepTheFetchedProfileUnconnectedWhenTheIdentityIsAGuest()
        {
            // Arrange
            identityCache.Identity = NewIdentity(LoginMethod.GUEST);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 3)));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchSucceeded(out FetchSucceeded msg), Is.True);
            Assert.That(msg.Profile.HasConnectedWeb3, Is.False);
        }

        [Test]
        public void ApplyForcedWearablesToTheFetchedProfile()
        {
            // Arrange
            executor.Dispose();
            executor = NewExecutor(new ForcedWearables(new[] { FORCED_WEARABLE }));
            Profile fetched = NewProfile(ALICE, 3);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(fetched));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            // Has.Member, not Does.Contain: URN converts to string implicitly and would be compared as a substring.
            Assert.That(SingleSent().IsFetchSucceeded(out FetchSucceeded msg), Is.True);
            Assert.That(msg.Profile.Avatar.Wearables, Has.Member(FORCED_WEARABLE));
            Assert.That(fetched.Avatar.Wearables, Has.No.Member(FORCED_WEARABLE), "the cached instance is left as fetched");
        }

        [Test]
        public void ReportAFetchFailureWhenACancellationFromElsewhereSurfaces()
        {
            // Arrange
            // UniTask surfaces a faulted cancellation as its own OperationCanceledException, so only the type is observable.
            AnyGet().Returns(UniTask.FromException<ProfileTier?>(new OperationCanceledException("another token")));
            LogAssert.Expect(LogType.Exception, new Regex(nameof(OperationCanceledException)));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchFailed(out FetchFailed msg), Is.True, "a cancellation the executor did not request is a failure, or the model stays Fetching");
            Assert.That(msg.Failure.Exception, Is.TypeOf<OperationCanceledException>());
        }

        [Test]
        public void ReportAFetchFailureWhenApplyingTheSessionOverridesThrows()
        {
            // Arrange
            var error = new InvalidOperationException("emote storage is not ready");
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 3)));
            emoteStorage.BaseEmotesUrns.Returns(_ => throw error);
            ExpectReportedException(error);

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchFailed(out FetchFailed msg), Is.True);
            Assert.That(msg.Failure.Exception, Is.SameAs(error));
        }

        [Test]
        public void ReportNothingForAFetchCancelledByANewerFetch()
        {
            // Arrange
            var pending = new UniTaskCompletionSource();
            AnyGet().Returns(call => CompleteWhenReleasedAsync(pending, call.Arg<CancellationToken>(), NewProfile(BOB, 1)));

            // Act
            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);
            executor.Execute(SelfProfileCmd.FromFetch(BOB), inbox);
            pending.TrySetResult();

            // Assert
            Assert.That(SingleSent().IsFetchSucceeded(out FetchSucceeded msg), Is.True);
            Assert.That(msg.Address, Is.EqualTo(BOB));
        }

        [Test]
        public void CancelTheActivityInFlightOnReset()
        {
            // Arrange
            CancellationToken fetchToken = StartPendingFetch();

            // Act
            executor.Execute(SelfProfileCmd.ResetLocalState(), inbox);

            // Assert
            Assert.That(fetchToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public void CancelTheActivityInFlightOnDispose()
        {
            // Arrange
            CancellationToken fetchToken = StartPendingFetch();

            // Act
            executor.Dispose();

            // Assert
            Assert.That(fetchToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public void ClearTheLocalRegistriesOnReset()
        {
            // Act
            executor.Execute(SelfProfileCmd.ResetLocalState(), inbox);

            // Assert
            wearableStorage.Received(1).ClearOwnedNftRegistry();
            emoteStorage.Received(1).ClearOwnedNftRegistry();
            equippedWearables.Received(1).Clear();
            equippedEmotes.Received(1).UnEquipAll();
            Assert.That(inbox.Sent, Is.Empty);
        }

        [Test]
        public void PutThePublishedProfileInTheCache()
        {
            // Arrange
            Profile published = NewProfile(ALICE, 3);
            ProfileTier cached = default;
            profileCache.Set(Arg.Any<string>(), Arg.Do<ProfileTier>(tier => cached = tier));

            // Act
            executor.Execute(SelfProfileCmd.FromPublish(published), inbox);

            // Assert
            profileCache.Received(1).Set(ALICE.Value, Arg.Any<ProfileTier>());
            Assert.That(cached.IsFull(out Profile? full), Is.True);
            Assert.That(full, Is.Not.SameAs(published), "the cache disposes what it replaces, so it gets a copy");
            Assert.That(full!.IsSameProfile(published), Is.True);
            Assert.That(inbox.Sent, Is.Empty);
        }

        [Test]
        public void KeepThePublishedProfileIntactWhenTheCacheReplacesIt()
        {
            // Arrange
            // The real cache disposes the instance a later write replaces, as the repository does on every catalyst read.
            var cache = new DefaultProfileCache();
            executor.Dispose();
            profileCache = cache;
            executor = NewExecutor(new ForcedWearables());
            Profile published = Profile.NewRandomProfile(ALICE.Value);
            published.ClearLinks();

            // Act
            executor.Execute(SelfProfileCmd.FromPublish(published), inbox);
            cache.Set(ALICE.Value, Profile.NewRandomProfile(ALICE.Value));

            // Assert
            Assert.That(published.Links, Is.Not.Null, "the profile the model keeps must not be the instance the cache disposed");
        }

        [Test]
        public void KeepThePlayerEntityProfileIntactWhenTheCacheReplacesIt()
        {
            // Arrange
            // The real cache disposes the instance a later write replaces, as the repository does when a deploy completes.
            var cache = new DefaultProfileCache();
            executor.Dispose();
            profileCache = cache;
            executor = NewExecutor(new ForcedWearables());
            world.Add(playerEntity, NewProfile(ALICE, 2));
            Profile published = Profile.NewRandomProfile(ALICE.Value);
            published.ClearLinks();

            // Act
            executor.Execute(SelfProfileCmd.FromPublish(published), inbox);
            cache.Set(ALICE.Value, Profile.NewRandomProfile(ALICE.Value));

            // Assert
            Assert.That(world.Get<Profile>(playerEntity).Links, Is.Not.Null, "the player entity must not hold the instance the cache disposed");
        }

        [Test]
        public void DisposeTheProfileThePlayerEntityReplaces()
        {
            // Arrange
            Profile replaced = NewProfile(ALICE, 2);
            replaced.ClearLinks();
            world.Add(playerEntity, replaced);

            // Act
            executor.Execute(SelfProfileCmd.FromPublish(NewProfile(ALICE, 3)), inbox);

            // Assert
            Assert.That(replaced.Links, Is.Null, "the entity owns its instance, so the one it replaces is released");
        }

        [Test]
        public void HandThePictureOfTheReplacedProfileToTheNewEntityProfile()
        {
            // Arrange
            URLAddress faceUrl = URLAddress.FromString("https://example.com/face.png");
            Profile replaced = NewProfile(ALICE, 2);
            replaced.GetCompact().FaceSnapshotUrl = faceUrl;
            replaced.ProfilePicture = new StreamableLoadingResult<SpriteData>.WithFallback(ProfileUtils.DEFAULT_PROFILE_PIC);
            world.Add(playerEntity, replaced);
            Profile published = NewProfile(ALICE, 3);
            published.GetCompact().FaceSnapshotUrl = faceUrl;

            // Act
            executor.Execute(SelfProfileCmd.FromPublish(published), inbox);

            // Assert
            Assert.That(world.Get<Profile>(playerEntity).ProfilePicture, Is.Not.Null, "the entity's new instance keeps the loaded picture");
        }

        [Test]
        public void PublishTheVersionTheDeployStamps()
        {
            // Arrange
            world.Add(playerEntity, NewProfile(ALICE, 3));
            Profile edited = NewProfile(ALICE, 3);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 4)));

            // Act
            executor.Execute(SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, edited, 4)), SelfProfileCmd.FromPublish(edited) }), inbox);

            // Assert
            Assert.That(world.Get<Profile>(playerEntity).Version, Is.EqualTo(4));
            profileCache.Received(1).Set(ALICE.Value, Arg.Is<ProfileTier>(cached => cached.IsFull(out Profile? full) && full.Version == 4));
        }

        [Test]
        public void ReplaceTheProfileOnThePlayerEntityWhenItCarriesOne()
        {
            // Arrange
            world.Add(playerEntity, NewProfile(ALICE, 2));
            Profile published = NewProfile(ALICE, 3);

            // Act
            executor.Execute(SelfProfileCmd.FromPublish(published), inbox);

            // Assert
            Profile onEntity = world.Get<Profile>(playerEntity);
            Assert.That(onEntity.IsSameProfile(published), Is.True);
            Assert.That(onEntity.IsDirty, Is.True);
        }

        [Test]
        public void LeaveThePlayerEntityAloneUntilItCarriesAProfile()
        {
            // Act
            executor.Execute(SelfProfileCmd.FromPublish(NewProfile(ALICE, 3)), inbox);

            // Assert
            Assert.That(world.Has<Profile>(playerEntity), Is.False);
        }

        [Test]
        public void DeployThenReportTheSavedProfile()
        {
            // Arrange
            Profile sent = NewProfile(BOB, 4);
            Profile saved = NewProfile(ALICE, 4);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(saved));

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version)), inbox);

            // Assert
            profileRepository.Received(1).SetAsync(Arg.Is<Profile>(deployed => !ReferenceEquals(deployed, sent) && deployed.IsSameProfile(sent)), Arg.Any<CancellationToken>());
            Assert.That(sent.UserId, Is.EqualTo(ALICE), "the deployed profile is stamped with the address");
            Assert.That(SingleSent().IsDeploySucceeded(out DeploySucceeded msg), Is.True);
            Assert.That(msg.Address, Is.EqualTo(ALICE));
            Assert.That(msg.Sent, Is.SameAs(sent));
            Assert.That(msg.Saved, Is.Not.SameAs(saved), "the cache owns the re-read instance");
            Assert.That(msg.Saved.IsSameProfile(saved), Is.True);
        }

        [Test]
        public void ReReadTheDeployedVersionFromTheCatalyst()
        {
            // Arrange
            Profile sent = NewProfile(ALICE, 4);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 4)));

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version)), inbox);

            // Assert
            profileRepository.Received(1).GetAsync(ALICE.Value, 4, null, Arg.Any<CancellationToken>(), false,
                IProfileRepository.FetchBehaviour.ForceFetchFromCatalyst | IProfileRepository.FetchBehaviour.DelayUntilResolved,
                ProfileTier.Kind.Full, Arg.Any<IPartitionComponent?>());
        }

        [Test]
        public void ReportADeployFailureWhenTheSaveThrows()
        {
            // Arrange
            Profile sent = NewProfile(ALICE, 4);
            var error = new InvalidOperationException("deploy rejected");
            profileRepository.SetAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromException(error));
            ExpectReportedException(error);

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version)), inbox);

            // Assert
            Assert.That(SingleSent().IsDeployFailed(out DeployFailed msg), Is.True);
            Assert.That(msg.Address, Is.EqualTo(ALICE));
            Assert.That(msg.Sent, Is.SameAs(sent));
            Assert.That(msg.Exception, Is.SameAs(error));
            profileRepository.DidNotReceiveWithAnyArgs().GetAsync(default!, default, default, default, default, default, default);
        }

        [Test]
        public void ReportADeployFailureWhenACancellationFromElsewhereSurfaces()
        {
            // Arrange
            Profile sent = NewProfile(ALICE, 4);
            profileRepository.SetAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromException(new OperationCanceledException("a superseded deploy")));
            LogAssert.Expect(LogType.Exception, new Regex(nameof(OperationCanceledException)));

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version)), inbox);

            // Assert
            Assert.That(SingleSent().IsDeployFailed(out DeployFailed msg), Is.True, "a cancellation the executor did not request is a failure, or the model stays Deploying");
            Assert.That(msg.Sent, Is.SameAs(sent));
            Assert.That(msg.Exception, Is.TypeOf<OperationCanceledException>());
        }

        [Test]
        public void ReportADeployFailureWhenTheSavedProfileIsMissing()
        {
            // Arrange
            Profile sent = NewProfile(ALICE, 4);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(null));
            LogAssert.Expect(LogType.Exception, new Regex(nameof(ProfileNotFoundAfterDeployException)));

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version)), inbox);

            // Assert
            Assert.That(SingleSent().IsDeployFailed(out DeployFailed msg), Is.True);
            Assert.That(msg.Sent, Is.SameAs(sent));
            Assert.That(msg.Exception, Is.TypeOf<ProfileNotFoundAfterDeployException>());
        }

        [Test]
        public void KeepTheDeployInFlightWhenANewerEditDeploys()
        {
            // Arrange
            CancellationToken firstToken = default;
            profileRepository.SetAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                if (!firstToken.CanBeCanceled)
                    firstToken = call.Arg<CancellationToken>();

                return new UniTaskCompletionSource().Task;
            });

            Profile first = NewProfile(ALICE, 4);
            Profile second = NewProfile(ALICE, 5);
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, first, first.Version)), inbox);

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, second, second.Version)), inbox);

            // Assert
            Assert.That(firstToken.CanBeCanceled, Is.True, "the executor should pass a cancellable token to the repository");
            Assert.That(firstToken.IsCancellationRequested, Is.False);
        }

        [Test]
        public void SkipTheDeployInAFakingSession()
        {
            // Arrange
            executor.Dispose();
            executor = NewExecutor(new ForcedWearables(new[] { FORCED_WEARABLE }));
            Profile sent = NewProfile(ALICE, 4);

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version)), inbox);

            // Assert
            profileRepository.DidNotReceiveWithAnyArgs().SetAsync(default!, default);
            Assert.That(SingleSent().IsDeploySucceeded(out DeploySucceeded msg), Is.True);
            Assert.That(msg.Sent, Is.SameAs(sent));
            Assert.That(msg.Saved, Is.SameAs(sent));
        }

        [Test]
        public void SkipTheDeployInAPreviewSession()
        {
            // Arrange
            executor.Dispose();
            executor = NewExecutor(new ForcedWearables(), skipCatalystDeploy: true);
            Profile sent = NewProfile(ALICE, 4);

            // Act
            executor.Execute(SelfProfileCmd.FromDeploy(new DeployCmd(ALICE, sent, sent.Version + 1)), inbox);

            // Assert
            profileRepository.DidNotReceiveWithAnyArgs().SetAsync(default!, default);
            Assert.That(SingleSent().IsDeploySucceeded(out DeploySucceeded msg), Is.True);
            Assert.That(msg.Sent, Is.SameAs(sent));
            Assert.That(msg.Saved, Is.SameAs(sent));
            Assert.That(sent.Version, Is.EqualTo(4), "a version the catalyst never received must not be announced");
        }

        [Test]
        public void ExecuteABatchInOrder()
        {
            // Arrange
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 3)));

            // Act
            executor.Execute(SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.ResetLocalState(), SelfProfileCmd.FromFetch(ALICE) }), inbox);

            // Assert
            Received.InOrder(() =>
            {
                equippedWearables.Clear();
                profileRepository.GetAsync(ALICE.Value, 0, null, Arg.Any<CancellationToken>(), false,
                    IProfileRepository.FetchBehaviour.EnforceSingleGet, ProfileTier.Kind.Full, Arg.Any<IPartitionComponent?>());
            });

            Assert.That(SingleSent().IsFetchSucceeded(out _), Is.True);
        }

        [Test]
        public void KeepExecutingABatchWhenACommandThrows()
        {
            // Arrange
            var error = new InvalidOperationException("cache is closed");
            profileCache.When(cache => cache.Set(Arg.Any<string>(), Arg.Any<ProfileTier>())).Do(_ => throw error);
            AnyGet().Returns(UniTask.FromResult<ProfileTier?>(NewProfile(ALICE, 3)));
            ExpectReportedException(error);

            // Act
            executor.Execute(SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.FromPublish(NewProfile(ALICE, 2)), SelfProfileCmd.FromFetch(ALICE) }), inbox);

            // Assert
            Assert.That(SingleSent().IsFetchSucceeded(out _), Is.True, "the fetch after the failing publish must still run");
        }

        [Test]
        public void DoNothingForNone()
        {
            // Act
            executor.Execute(SelfProfileCmd.None(), inbox);

            // Assert
            Assert.That(inbox.Sent, Is.Empty);
            profileRepository.DidNotReceiveWithAnyArgs().GetAsync(default!, default, default, default, default, default, default);
        }

        private SelfProfileCmdExecutor NewExecutor(ForcedWearables forcedWearables, bool skipCatalystDeploy = false) =>
            new (profileRepository, profileCache, identityCache, wearableStorage, emoteStorage, equippedWearables, equippedEmotes,
                forcedWearables, forcedEmotes: null, world, playerEntity, skipCatalystDeploy);

        private static IWeb3Identity NewIdentity(LoginMethod method)
        {
            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Method.Returns(method);
            return identity;
        }

        private static Profile NewProfile(UserId userId, int version) =>
            new (userId, "self", new Avatar()) { Version = version };

        /// <summary>The repository call the executor makes for any fetch or re-read, ready for <c>Returns</c>.</summary>
        private UniTask<ProfileTier?> AnyGet() =>
            profileRepository.GetAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(),
                Arg.Any<bool>(), Arg.Any<IProfileRepository.FetchBehaviour>(), Arg.Any<ProfileTier.Kind>(), Arg.Any<IPartitionComponent?>());

        /// <summary>Starts a fetch whose repository call never completes and returns the token the executor gave it.</summary>
        private CancellationToken StartPendingFetch()
        {
            CancellationToken token = default;
            AnyGet().Returns(call =>
            {
                token = call.Arg<CancellationToken>();
                return new UniTaskCompletionSource<ProfileTier?>().Task;
            });

            executor.Execute(SelfProfileCmd.FromFetch(ALICE), inbox);

            Assert.That(token.CanBeCanceled, Is.True, "the executor should pass a cancellable token to the repository");
            return token;
        }

        /// <summary>Behaves like the repository: completes when released, and throws if its token was cancelled meanwhile.</summary>
        private static async UniTask<ProfileTier?> CompleteWhenReleasedAsync(UniTaskCompletionSource release, CancellationToken ct, Profile profile)
        {
            await release.Task;
            ct.ThrowIfCancellationRequested();
            return profile;
        }

        private SelfProfileMsg SingleSent()
        {
            Assert.That(inbox.Sent, Has.Count.EqualTo(1), "exactly one message should reach the inbox");
            return inbox.Sent[0];
        }

        /// <summary>The executor reports every failure to Sentry through <c>ReportHub</c>, which the test runner sees as a logged exception.</summary>
        private static void ExpectReportedException(Exception exception) =>
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape($"{exception.GetType().Name}: {exception.Message}")));
    }
}
