using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.TestSuite;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Profiles.Tests
{
    public class RealmProfileRepositoryShould
    {
        // 19 profiles
        internal static readonly string TEST_PROFILES_JSON = $"{Application.dataPath + "/../TestResources/Profiles/Test_profiles.json"}";

        private static readonly URLDomain LAMBDAS = URLDomain.FromString("https://test-url.com/lambdas");
        private static readonly URLDomain PEER_ANNOUNCED_LAMBDAS = URLDomain.FromString("https://peer-announced.example.test/lambdas");

        private RealmProfileRepository repository = null!;
        private IWebRequestController webRequestController = null!;
        private IDecentralandUrlsSource urlsSource = null!;
        private IProfileCache profileCache = null!;

        // 19 profiles
        private List<Profile> dtos = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            webRequestController = Substitute.For<IWebRequestController>();
            urlsSource = Substitute.For<IDecentralandUrlsSource>();
            profileCache = Substitute.For<IProfileCache>();
            repository = NewRepository(false);

            dtos = JsonConvert.DeserializeObject<List<Profile>>(File.ReadAllText(TEST_PROFILES_JSON), RealmProfileRepository.SERIALIZER_SETTINGS)!;
        }

        [Test]
        public async Task ResolveProfilesByGet()
        {
            var onProfilesResolved = new Action[dtos.Count];
            var tasks = new UniTask[dtos.Count];

            for (int i = 0; i < dtos.Count; i++)
            {
                Action? callback = onProfilesResolved[i] = Substitute.For<Action>();
                tasks[i] = repository.GetAsync(dtos[i].UserId.Value, 0, LAMBDAS, CancellationToken.None).ContinueWith(_ => callback());
            }

            // Not resolved yet
            foreach (Action action in onProfilesResolved)
                action.DidNotReceive();

            // Created a pending batch
            AssertBatch(1, dtos.Count);

            foreach (Profile profileJsonDto in dtos)
                repository.ResolveProfile(profileJsonDto.UserId.Value, profileJsonDto, false);

            await UniTask.WhenAll(tasks).Timeout(TimeSpan.FromSeconds(1));

            foreach (Action action in onProfilesResolved)
                action.Received(1);
        }

        [Test]
        public async Task ResolveProfilesByBatch()
        {
            string[] ids = dtos.Select(d => d.UserId.Value).ToArray();
            UniTask<List<Profile>> task = repository.GetAsync(ids, CancellationToken.None, LAMBDAS);

            // Created a pending batch
            AssertBatch(1, dtos.Count);

            foreach (Profile? profileJsonDto in dtos)
                repository.ResolveProfile(profileJsonDto.UserId.Value, profileJsonDto, false);

            List<Profile>? profiles = await task;

            CollectionAssert.AreEquivalent(ids, profiles.Select(p => p.UserId.Value).ToArray());
        }

        [Test]
        public async Task CreateBatchesForDifferentCatalysts()
        {
            CancellationTokenSource cts = new ();

            const int FIRST_IT = 5;

            var tasks1 = new UniTask[FIRST_IT];

            for (int i = 0; i < FIRST_IT; i++)
                tasks1[i] = repository.GetAsync(dtos[i].UserId.Value, 0, URLDomain.FromString("http://test1"), cts.Token).SuppressCancellationThrow();

            const int SECOND_IT = 10;

            var tasks2 = new UniTask[SECOND_IT];

            for (int i = FIRST_IT; i < SECOND_IT + FIRST_IT; i++)
                tasks2[i - FIRST_IT] = repository.GetAsync(dtos[i].UserId.Value, 0, URLDomain.FromString("http://test2"), cts.Token).SuppressCancellationThrow();

            ProfilesBatchRequest[] batches = AssertBatch(2, FIRST_IT + SECOND_IT);

            Assert.That(batches[0].LambdasUrl.Value, Is.EqualTo("http://test1"));
            Assert.That(batches[1].LambdasUrl.Value, Is.EqualTo("http://test2"));

            cts.Cancel();

            await UniTask.WhenAll(tasks1);
            await UniTask.WhenAll(tasks2);
        }

        [Test]
        public async Task EnforceSingleGetIfBatchIncomplete()
        {
            var onProfilesResolved = new Action[2];
            var tasks = new UniTask[2];

            for (int i = 0; i < 2; i++)
            {
                Action? callback = onProfilesResolved[i] = Substitute.For<Action>();
                tasks[i] = repository.GetAsync(dtos[i].UserId.Value, 0, LAMBDAS, CancellationToken.None).ContinueWith(_ => callback());
            }

            Profile? second = dtos[0];

            // Second profile to retrieve via Single Get
            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                 (Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(), Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>())!
                                .Returns(UniTask.FromResult(second));

            // Not resolved yet
            foreach (Action action in onProfilesResolved)
                action.DidNotReceive();

            // Created a pending batch
            AssertBatch(1, 2);

            // The first profile to resolve
            repository.ResolveProfile(dtos[0].UserId.Value, dtos[0], false);

            // The second profile to force through Single GET
            repository.ResolveProfile(dtos[1].UserId.Value, null, false);

            await UniTask.WhenAll(tasks).Timeout(TimeSpan.FromSeconds(1));

            // Still should be completed
            foreach (Action action in onProfilesResolved)
                action.Received(1);
        }

        [Test]
        public async Task ReportAFailedSingleGetByDefault()
        {
            // Arrange
            var error = new Exception("catalyst timed out");
            FailSingleGetWith(error);
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(error.Message)));

            // Act
            Exception? thrown = await SingleGetFailureAsync(IProfileRepository.FetchBehaviour.EnforceSingleGet);
            await FinalizeUnobservedExceptionsAsync();

            // Assert
            Assert.That(thrown, Is.SameAs(error));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task LeaveAFailedSingleGetUnreportedWhenTheCallerReportsFailures()
        {
            // Arrange
            var error = new Exception("catalyst timed out");
            FailSingleGetWith(error);

            // Act
            Exception? thrown = await SingleGetFailureAsync(IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures);
            await FinalizeUnobservedExceptionsAsync();

            // Assert
            Assert.That(thrown, Is.SameAs(error));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ReportAFailedSinglePostByDefault()
        {
            // Arrange
            repository = NewRepository(true);
            var error = new Exception("profiles endpoint timed out");
            FailSinglePostWith(error);
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(error.Message)));

            // Act
            Exception? thrown = await SingleGetFailureAsync(IProfileRepository.FetchBehaviour.EnforceSingleGet);
            await FinalizeUnobservedExceptionsAsync();

            // Assert
            Assert.That(thrown, Is.SameAs(error));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task LeaveAFailedSinglePostUnreportedWhenTheCallerReportsFailures()
        {
            // Arrange
            repository = NewRepository(true);
            var error = new Exception("profiles endpoint timed out");
            FailSinglePostWith(error);

            // Act
            Exception? thrown = await SingleGetFailureAsync(IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures);
            await FinalizeUnobservedExceptionsAsync();

            // Assert
            Assert.That(thrown, Is.SameAs(error));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ResolveASinglePostWithoutTheProfileAsMissing()
        {
            // Arrange
            repository = NewRepository(true);

            webRequestController.SendAsync<GenericPostRequest, GenericPostArguments, GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>, IList<Profile>>
                                 (Arg.Any<RequestEnvelope<GenericPostRequest, GenericPostArguments>>(), Arg.Any<GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>>())!
                                .Returns(_ => UniTask.FromResult<IList<Profile>>(new List<Profile>()));

            // Act
            ProfileTier? profile = await repository.GetAsync(dtos[0].UserId.Value, 0, LAMBDAS, CancellationToken.None, false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures, ProfileTier.Kind.Full);

            // Assert
            Assert.That(profile, Is.Null);
        }

        [Test]
        public async Task FallBackToTheRealmCatalystWhenTheCentralizedEndpointLacksTheProfile()
        {
            // Arrange: the centralized endpoint answers without the profile, the realm's catalyst has it, the peer announces a catalyst of its own
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            AnswerSinglePostWith(new List<Profile>());
            AnswerSingleGetWith(dtos[0]);

            // Act
            ProfileTier? profile = await repository.GetAsync(dtos[0].UserId.Value, 0, PEER_ANNOUNCED_LAMBDAS, CancellationToken.None, false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures, ProfileTier.Kind.Full);

            // Assert: asked of the realm's catalyst, and of no other host
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Value.UserId.Value, Is.EqualTo(dtos[0].UserId.Value));
            ReceivedSingleGetsTo($"{LAMBDAS.Value}/profiles/{dtos[0].UserId.Value}", 1);
            ReceivedSingleGets(1);
        }

        [Test]
        public async Task NotAskTheCatalystWhenTheCentralizedEndpointHasTheProfile()
        {
            // Arrange
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            AnswerSinglePostWith(new List<Profile> { dtos[0] });

            // Act
            ProfileTier? profile = await repository.GetAsync(dtos[0].UserId.Value, 0, LAMBDAS, CancellationToken.None, false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures, ProfileTier.Kind.Full);

            // Assert
            Assert.That(profile, Is.Not.Null);
            ReceivedNoSingleGets();
        }

        [Test]
        public async Task FallBackToTheRealmCatalystWhenACentralizedBatchLacksTheProfile()
        {
            // Arrange
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            AnswerSingleGetWith(dtos[0]);
            UniTask<Profile?> task = repository.GetAsync(dtos[0].UserId.Value, 0, PEER_ANNOUNCED_LAMBDAS, CancellationToken.None);
            AssertBatch(1, 1);

            // Act: the centralized batch answered without the profile
            repository.ResolveProfile(dtos[0].UserId.Value, null, true);
            Profile? profile = await task.Timeout(TimeSpan.FromSeconds(1));

            // Assert
            Assert.That(profile, Is.Not.Null);
            ReceivedSingleGetsTo($"{LAMBDAS.Value}/profiles/{dtos[0].UserId.Value}", 1);
            ReceivedSingleGets(1);
        }

        [TestCase("https://peer.decentraland.org/lambdas", null, true)]
        [TestCase("https://gateway.localhost/peer/lambdas", "https://gateway.localhost/", true)]
        [TestCase("https://test-url.com/lambdas", null, false)]
        public async Task NotFallBackUnlessTheLaunchDeclaresARealmOutsideTheCentralizedNetwork(string realmLambdas, string? gatewayOrigin, bool usesRealmScenePointers)
        {
            // Arrange: the profile is missing from the centralized endpoint; the realm's catalyst is one that endpoint answers for, or the launch never declared the realm
            repository = NewRepository(true);
            HostTheRealmAt(realmLambdas, gatewayOrigin, usesRealmScenePointers);
            AnswerSinglePostWith(new List<Profile>());
            AnswerSingleGetWith(dtos[0]);

            // Act
            ProfileTier? profile = await repository.GetAsync(dtos[0].UserId.Value, 0, PEER_ANNOUNCED_LAMBDAS, CancellationToken.None, false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures, ProfileTier.Kind.Full);

            // Assert
            Assert.That(profile, Is.Null);
            ReceivedNoSingleGets();
        }

        [Test]
        public async Task NotFallBackForACentralizedBatchWhenTheRealmBelongsToTheCentralizedNetwork()
        {
            // Arrange
            repository = NewRepository(true);
            HostTheRealmAt("https://peer.decentraland.org/lambdas", null, true);
            AnswerSingleGetWith(dtos[0]);
            UniTask<Profile?> task = repository.GetAsync(dtos[0].UserId.Value, 0, PEER_ANNOUNCED_LAMBDAS, CancellationToken.None);
            AssertBatch(1, 1);

            // Act: the centralized batch answered without the profile
            repository.ResolveProfile(dtos[0].UserId.Value, null, true);
            Profile? profile = await task.Timeout(TimeSpan.FromSeconds(1));

            // Assert
            Assert.That(profile, Is.Null);
            ReceivedNoSingleGets();
        }

        [Test]
        public async Task DropARealmCatalystAnswerThatCarriesAnotherId()
        {
            // Arrange: the realm's catalyst answers the request for one wallet with the profile of another
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            AnswerSinglePostWith(new List<Profile>());
            AnswerSingleGetWith(dtos[1]);

            // Act
            ProfileTier? profile = await repository.GetAsync(dtos[0].UserId.Value, 0, LAMBDAS, CancellationToken.None, false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures, ProfileTier.Kind.Full);

            // Assert
            Assert.That(profile, Is.Null);
        }

        [Test]
        public async Task ReportAFailedRealmCatalystFallbackAndLeaveTheProfileMissing()
        {
            // Arrange: missing from the centralized endpoint, and the realm's catalyst cannot be reached
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            AnswerSinglePostWith(new List<Profile>());
            var error = new Exception("catalyst unreachable");
            FailSingleGetWith(error);
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(error.Message)));

            // Act
            ProfileTier? profile = await repository.GetAsync(dtos[0].UserId.Value, 0, LAMBDAS, CancellationToken.None, false,
                IProfileRepository.FetchBehaviour.EnforceSingleGet, ProfileTier.Kind.Full);
            await FinalizeUnobservedExceptionsAsync();

            // Assert: still missing, as the centralized endpoint alone answered
            Assert.That(profile, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task PassAFailedRealmCatalystFallbackToTheCallerThatReportsFailures()
        {
            // Arrange
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            AnswerSinglePostWith(new List<Profile>());
            var error = new Exception("catalyst unreachable");
            FailSingleGetWith(error);

            // Act
            Exception? thrown = await SingleGetFailureAsync(IProfileRepository.FetchBehaviour.EnforceSingleGet | IProfileRepository.FetchBehaviour.CallerReportsFailures);
            await FinalizeUnobservedExceptionsAsync();

            // Assert: the caller takes the failure, and nothing is reported here
            Assert.That(thrown, Is.SameAs(error));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task MakeAConcurrentRequestWaitForTheRealmCatalystFallback()
        {
            // Arrange: the centralized batch lacks the profile, and the realm's catalyst has not answered yet
            repository = NewRepository(true);
            HostTheRealmOutsideTheCentralizedNetwork();
            var catalystAnswer = new UniTaskCompletionSource<Profile>();

            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                 (Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(), Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>())!
                                .Returns(_ => catalystAnswer.Task);

            UniTask<Profile?> first = repository.GetAsync(dtos[0].UserId.Value, 0, PEER_ANNOUNCED_LAMBDAS, CancellationToken.None);
            AssertBatch(1, 1);
            repository.ResolveProfile(dtos[0].UserId.Value, null, true);

            // Act: the same profile is requested again while the fallback is in flight
            UniTask<Profile?> second = repository.GetAsync(dtos[0].UserId.Value, 0, PEER_ANNOUNCED_LAMBDAS, CancellationToken.None);

            // Assert: the second request starts no batch of its own until the fallback has answered
            Assert.That(repository.ConsumePendingBatch(), Is.Empty);

            catalystAnswer.TrySetResult(dtos[0]);
            Profile? firstProfile = await first.Timeout(TimeSpan.FromSeconds(1));

            AssertBatch(1, 1);
            repository.ResolveProfile(dtos[0].UserId.Value, dtos[0], true);
            Profile? secondProfile = await second.Timeout(TimeSpan.FromSeconds(1));

            Assert.That(firstProfile, Is.Not.Null);
            Assert.That(secondProfile, Is.Not.Null);
            ReceivedSingleGets(1);
        }

        /// <summary>
        ///     Puts the realm's catalyst at <see cref="LAMBDAS" />, a host outside the base domain the centralized endpoint answers for,
        ///     in a launch that resolves scene pointers against the realm.
        /// </summary>
        private void HostTheRealmOutsideTheCentralizedNetwork() =>
            HostTheRealmAt(LAMBDAS.Value, null, true);

        private void HostTheRealmAt(string realmLambdas, string? gatewayOrigin, bool usesRealmScenePointers)
        {
            urlsSource.UsesRealmScenePointers.Returns(usesRealmScenePointers);
            urlsSource.BaseDomain.Returns(IDecentralandUrlsSource.ORG_DOMAIN);
            urlsSource.GatewayOrigin.Returns(gatewayOrigin);
            urlsSource.Url(DecentralandUrl.Lambdas).Returns(realmLambdas);
        }

        /// <summary>The repository reads the answer from the list it handed to the request, so the profiles are written into that list, as the real request does.</summary>
        private void AnswerSinglePostWith(IList<Profile> profiles) =>
            webRequestController.SendAsync<GenericPostRequest, GenericPostArguments, GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>, IList<Profile>>
                                 (Arg.Any<RequestEnvelope<GenericPostRequest, GenericPostArguments>>(), Arg.Any<GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>>())!
                                .Returns(call =>
                                 {
                                     IList<Profile> target = call.ArgAt<GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>>(1).Target;

                                     foreach (Profile profile in profiles)
                                         target.Add(profile);

                                     return UniTask.FromResult(target);
                                 });

        private void AnswerSingleGetWith(Profile profile) =>
            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                 (Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(), Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>())!
                                .Returns(_ => UniTask.FromResult(profile));

        private void ReceivedNoSingleGets() =>
            webRequestController.DidNotReceive()
                                .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                     (Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(), Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>());

        private void ReceivedSingleGets(int count) =>
            webRequestController.Received(count)
                                .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                     (Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(), Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>());

        private void ReceivedSingleGetsTo(string url, int count) =>
            webRequestController.Received(count)
                                .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                     (Arg.Is<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(envelope => envelope.CommonArguments.URL.Value == url),
                                      Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>());

        private RealmProfileRepository NewRepository(bool useCentralizedProfiles) =>
            new (webRequestController, new PublishIpfsEntityCommand(Substitute.For<IWeb3IdentityCache>(), webRequestController, Substitute.For<IDecentralandUrlsSource>(), Substitute.For<IRealmData>()), urlsSource, profileCache, new ProfilesAnalytics(ProfilesDebug.Create(null, new EntitiesAnalyticsDebug(null)), IAnalyticsController.Null), useCentralizedProfiles);

        private void FailSinglePostWith(Exception error) =>
            webRequestController.SendAsync<GenericPostRequest, GenericPostArguments, GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>, IList<Profile>>
                                 (Arg.Any<RequestEnvelope<GenericPostRequest, GenericPostArguments>>(), Arg.Any<GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<IList<Profile>, GenericPostRequest>>())!
                                .Returns(_ => UniTask.FromException<IList<Profile>>(error));

        private void FailSingleGetWith(Exception error) =>
            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>, Profile>
                                 (Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(), Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<Profile, GenericGetRequest>>())!
                                .Returns(UniTask.FromException<Profile>(error));

        /// <summary>Runs the finalizers now and the player loop tick on which UniTask reports an exception nobody took from a source.</summary>
        private static async UniTask FinalizeUnobservedExceptionsAsync()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await UniTask.Yield();
        }

        private async UniTask<Exception?> SingleGetFailureAsync(IProfileRepository.FetchBehaviour fetchBehaviour)
        {
            try
            {
                await repository.GetAsync(dtos[0].UserId.Value, 0, LAMBDAS, CancellationToken.None, false, fetchBehaviour, ProfileTier.Kind.Full);
                return null;
            }
            catch (Exception e) { return e; }
        }

        private ProfilesBatchRequest[] AssertBatch(int batches, int requests)
        {
            // Created a pending batch
            ProfilesBatchRequest[] pendingBatch = repository.ConsumePendingBatch().ToArray();

            Assert.That(pendingBatch.Length, Is.EqualTo(batches));

            int actualRequests = 0;

            foreach (ProfilesBatchRequest profilesBatchRequest in pendingBatch)
                actualRequests += profilesBatchRequest.PendingRequests.Count;

            Assert.That(actualRequests, Is.EqualTo(requests));

            return pendingBatch;
        }

        [Test]
        public async Task AddOutdatedCachedProfilesToBatch()
        {
            Action onProfileResolved = Substitute.For<Action>();

            string userId = dtos[0].UserId.Value;

            profileCache.TryGet(userId, out Arg.Any<Profile?>())
                        .Returns(c =>
                         {
                             dtos[0].Version = 10;
                             c[1] = dtos[0];
                             return true;
                         });

            UniTask<Profile?> task = repository.GetAsync(userId, 11, LAMBDAS, CancellationToken.None);

            // Created a pending batch
            AssertBatch(1, 1);

            // Not resolved yet
            onProfileResolved.DidNotReceiveWithAnyArgs();

            // Newer version
            dtos[0].Version = 20;

            repository.ResolveProfile(userId, dtos[0], false);

            await task;

            onProfileResolved.Received(1);
        }

        [Test]
        public async Task NotAddCachedProfilesToBatch()
        {
            Action onProfileResolved = Substitute.For<Action>();

            string userId = dtos[0].UserId.Value;

            profileCache.TryGet(userId, Arg.Any<ProfileTier.Kind>(), out Arg.Any<ProfileTier>())
                        .Returns(c =>
                         {
                             dtos[0].Version = 10;
                             c[2] = (ProfileTier)dtos[0];
                             return true;
                         });

            // Lower version
            UniTask<Profile?> task = repository.GetAsync(userId, 9, LAMBDAS, CancellationToken.None);

            // Didn't create a pending batch
            AssertBatch(0, 0);

            // Resolved immediately
            onProfileResolved.Received(1);

            await task;
        }

        [Test]
        public async Task AddBatchToOngoingBatch()
        {
            CancellationTokenSource cts = new ();

            const int FIRST_IT = 5;

            var tasks1 = new UniTask[FIRST_IT];

            for (int i = 0; i < FIRST_IT; i++)
                tasks1[i] = repository.GetAsync(dtos[i].UserId.Value, 0, LAMBDAS, cts.Token).SuppressCancellationThrow();

            // Make tasks ongoing

            AssertBatch(1, FIRST_IT);

            // Tasks are ongoing
            // Don't finish them
            // Add the second batch

            const int SECOND_IT = 12;

            var tasks2 = new UniTask[SECOND_IT];

            for (int i = 0; i < SECOND_IT; i++)
                tasks2[i] = repository.GetAsync(dtos[i].UserId.Value, 0, LAMBDAS, cts.Token).SuppressCancellationThrow();

            // 5 of them are the same so they should be added to the ongoing Batch
            // 7 are new so they are added to the new batch

            AssertBatch(1, SECOND_IT - FIRST_IT);

            cts.Cancel();

            await UniTask.WhenAll(tasks1);
            await UniTask.WhenAll(tasks2);
        }
    }
}
