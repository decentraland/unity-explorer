using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Friends;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.TestTools;

namespace DCL.Chat.ChatServices.Tests
{
    [TestFixture]
    public class ChatMemberListServiceShould
    {
        private const string ALICE = "0xaaaa000000000000000000000000000000000001";
        private const string BOB = "0xbbbb000000000000000000000000000000000002";
        private const string CAROL = "0xcccc000000000000000000000000000000000003";
        private const string DAVE = "0xdddd000000000000000000000000000000000004";

        // Keeps the retry loop fast; the production delay is irrelevant to the behaviour under test
        private const int RETRY_DELAY_MS = 10;

        private IProfileRepository profileRepository = null!;
        private ICurrentChannelUserStateService userState = null!;
        private CurrentChannelService currentChannelService = null!;
        private ChatEventBus eventBus = null!;
        private ChatChannel channel = null!;
        private ChatMemberListService service = null!;

        private HashSet<string> online = null!;
        private List<string> publishedIds = null!;
        private List<string> publishedNames = null!;
        private List<int> publishedCounts = null!;
        private int lastCounter;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            profileRepository = Substitute.For<IProfileRepository>();

            var wrapper = new ProfileRepositoryWrapper(profileRepository, Substitute.For<IProfileCache>(),
                Substitute.For<ISpriteCache>(), Substitute.For<IWeb3IdentityCache>());

            online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            userState = Substitute.For<ICurrentChannelUserStateService>();
            userState.OnlineParticipants.Returns(online);

            userState.When(s => s.CopyOnlineParticipantsTo(Arg.Any<HashSet<string>>()))
                     .Do(call =>
                      {
                          HashSet<string> destination = call.Arg<HashSet<string>>();
                          destination.Clear();
                          destination.UnionWith(online);
                      });

            // The current channel is a shared static property, so every test needs a fresh instance to get past the equality early-out
            channel = new ChatChannel(ChatChannel.ChatChannelType.USER, Guid.NewGuid().ToString());
            currentChannelService = new CurrentChannelService();
            currentChannelService.SetCurrentChannel(channel, userState);

            eventBus = new ChatEventBus();
            publishedIds = new List<string>();
            publishedNames = new List<string>();
            publishedCounts = new List<int>();
            lastCounter = -1;

            service = new ChatMemberListService(wrapper, Substitute.For<IFriendsService>(), currentChannelService, eventBus, RETRY_DELAY_MS);
            service.OnMemberCountUpdated += count => lastCounter = count;
            service.Start();

            service.StartLiveMemberUpdates(members =>
            {
                publishedIds.Clear();
                publishedNames.Clear();

                foreach (ChatMemberListData member in members)
                {
                    publishedIds.Add(member.Profile.UserId.Value);
                    publishedNames.Add(member.Name);
                }

                publishedCounts.Add(members.Count);
            });
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            currentChannelService.Dispose();
        }

        [UnityTest]
        public IEnumerator NotDropMemberWhoseProfileIsTemporarilyNull() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                SetOnline(ALICE, BOB, CAROL);
                StubProfile(ALICE, "alice");
                StubProfile(BOB, "bob");
                StubProfileSequence(CAROL, (ProfileTier?)null, Compact(CAROL, "carol"));

                // Act
                await service.RequestInitialMemberListAsync();

                // Assert
                Assert.That(publishedCounts, Is.EqualTo(new[] { 3, 3 }), "one row per online wallet from the first publish, republished once the profile lands");
                Assert.That(publishedIds, Is.EquivalentTo(new[] { ALICE, BOB, CAROL }));
                Assert.That(publishedNames, Does.Contain("carol"));
            });

        [UnityTest]
        public IEnumerator KeepListWhenOneProfileFetchThrows() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                SetOnline(ALICE, BOB, CAROL);
                StubProfile(ALICE, "alice");
                StubProfile(BOB, "bob");

                profileRepository.GetAsync(CAROL, 0, Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(), true,
                                  IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>())
                                 .Returns(UniTask.FromException<ProfileTier?>(new InvalidOperationException("profiles endpoint down")));

                // Act
                await service.RequestInitialMemberListAsync();

                // Assert
                Assert.That(publishedIds, Is.EquivalentTo(new[] { ALICE, BOB, CAROL }));
                Assert.That(publishedNames, Does.Contain("alice").And.Contain("bob"));
            });

        [UnityTest]
        public IEnumerator AgreeWithCounterAfterRefresh() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                SetOnline(ALICE, BOB);
                StubProfile(ALICE, "alice");
                StubProfile(BOB, "bob");
                await service.RequestInitialMemberListAsync();
                Assert.That(publishedIds.Count, Is.EqualTo(online.Count));

                // Act
                online.Add(DAVE);
                StubProfile(DAVE, "dave");
                eventBus.RaiseUserStatusUpdatedEvent(channel.Id, ChatChannel.ChatChannelType.USER, DAVE, true);
                await UniTask.Yield();

                // Assert
                Assert.That(publishedIds, Is.EquivalentTo(new[] { ALICE, BOB, DAVE }));
                Assert.That(lastCounter, Is.EqualTo(3));
            });

        [UnityTest]
        public IEnumerator NotDuplicateMembersWhenRefreshesOverlap() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                SetOnline(ALICE);
                var gate = new UniTaskCompletionSource<ProfileTier?>();

                profileRepository.GetAsync(ALICE, 0, Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(), true,
                                  IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>())
                                 .Returns(gate.Task.Preserve());

                // Act
                UniTask first = service.RequestInitialMemberListAsync();
                UniTask second = service.RequestInitialMemberListAsync();
                gate.TrySetResult(Compact(ALICE, "alice"));
                await UniTask.WhenAll(first, second);

                // Assert
                Assert.That(publishedCounts, Is.EqualTo(new[] { 1 }), "only the latest refresh publishes");
                Assert.That(publishedIds, Is.EqualTo(new[] { ALICE }));
            });

        [UnityTest]
        public IEnumerator KeepWalletPlaceholderAfterMaxRetries() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                SetOnline(ALICE, BOB, CAROL);
                StubProfile(ALICE, "alice");
                StubProfile(BOB, "bob");
                StubProfileSequence(CAROL, (ProfileTier?)null);

                // Act
                await service.RequestInitialMemberListAsync();

                // Assert
                Assert.That(publishedIds, Is.EqualTo(new[] { ALICE, BOB, CAROL }), "placeholders are listed after the resolved members");
                Assert.That(publishedNames[2], Is.EqualTo(CAROL[..6]));

                _ = profileRepository.Received(1 + ChatMemberListService.MAX_UNRESOLVED_RETRIES)
                                     .GetAsync(CAROL, 0, Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(), true,
                                      IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>());
            });

        private void SetOnline(params string[] ids)
        {
            online.Clear();
            online.UnionWith(ids);
        }

        private void StubProfile(string id, string name) =>
            StubProfileSequence(id, Compact(id, name));

        private void StubProfileSequence(string id, params ProfileTier?[] results)
        {
            var tasks = new UniTask<ProfileTier?>[results.Length];

            for (var i = 0; i < results.Length; i++)
                tasks[i] = UniTask.FromResult(results[i]);

            profileRepository.GetAsync(id, 0, Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(), true,
                                  IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>())
                             .Returns(tasks[0], tasks[1..]);
        }

        private static ProfileTier Compact(string id, string name) =>
            new Profile.CompactInfo(UserId.New(id).Unwrap(), name);
    }
}
