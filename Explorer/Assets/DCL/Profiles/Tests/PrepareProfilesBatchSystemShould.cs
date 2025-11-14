using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.Profiles.Tests
{
    public class PrepareProfilesBatchSystemShould : UnitySystemTestBase<PrepareProfilesBatchSystem>
    {
        private IBatchedProfileRepository repository;

        private static readonly URLDomain LAMBDAS = URLDomain.FromString("http://localhost");

        [SetUp]
        public void SetUp()
        {
            repository = Substitute.For<IBatchedProfileRepository>();

            repository.PostUrl(Arg.Any<URLDomain>()).Returns(c => URLAddress.FromString(c.Arg<URLDomain>().Value));

            system = new PrepareProfilesBatchSystem(world, TimeSpan.FromSeconds(2), repository);
        }

        [Test]
        [TestCase(3, true)]
        [TestCase(0.5f, false)]
        public async Task RespectHeartbeat(float delay, bool created)
        {
            var batch = ProfilesBatchRequest.Create(LAMBDAS);
            batch.PendingRequests["test_id"] = new ProfilesBatchRequest.Input(new UniTaskCompletionSource<Profile?>(), PartitionComponent.TOP_PRIORITY);

            repository.ConsumePendingBatch().Returns(new[] { batch });

            system!.Update(0);

            // The first iteration is never deferred
            CheckPromise(true);

            await Task.Delay(TimeSpan.FromSeconds(delay));

            system!.Update(0);

            CheckPromise(created);

            void CheckPromise(bool created)
            {
                QueryDescription query = new QueryDescription().WithAll<GetProfilesBatchIntent, IPartitionComponent>();

                int count = created ? 1 : 0;

                Assert.That(world.CountEntities(query), Is.EqualTo(count));

                if (!created)
                    return;

                Span<Entity> entities = stackalloc Entity[1];
                world.GetEntities(query, entities);

                GetProfilesBatchIntent actual = world.Get<GetProfilesBatchIntent>(entities[0]);
                Assert.That(actual.Lambdas, Is.EqualTo(LAMBDAS));
                CollectionAssert.AreEqual(actual.Ids, new[] { "test_id" });

                world.Destroy(query);
            }
        }

        [Test]
        public void CreateSeparatePromisesForDifferentLambdas()
        {
            var batches = new ProfilesBatchRequest[5];

            var lambdas = new URLDomain[5];

            for (int i = 0; i < 5; i++)
            {
                var batch = ProfilesBatchRequest.Create(lambdas[i] = URLDomain.FromString($"https://test-lambda{i}"));
                batch.PendingRequests["test_id"] = new ProfilesBatchRequest.Input(new UniTaskCompletionSource<Profile?>(), PartitionComponent.TOP_PRIORITY);
                batches[i] = batch;
            }

            repository.ConsumePendingBatch().Returns(batches);

            system!.Update(0);

            // The first iteration is never deferred

            QueryDescription query = new QueryDescription().WithAll<GetProfilesBatchIntent, IPartitionComponent>();

            Assert.That(world.CountEntities(query), Is.EqualTo(5));

            var results = new List<URLDomain>(5);

            world.Query(query, (ref GetProfilesBatchIntent intent) => results.Add(intent.Lambdas));

            CollectionAssert.AreEquivalent(lambdas, results);
        }

        [Test]
        public void PickLowestPartitionFromBatch()
        {
            var batch = ProfilesBatchRequest.Create(LAMBDAS);

            for (byte i = 0; i < 5; i++) { batch.PendingRequests[$"test_id{i}"] = new ProfilesBatchRequest.Input(new UniTaskCompletionSource<Profile?>(), new PartitionComponent { Bucket = i }); }

            repository.ConsumePendingBatch().Returns(new[] { batch });

            system!.Update(0);

            QueryDescription query = new QueryDescription().WithAll<GetProfilesBatchIntent, IPartitionComponent>();

            Assert.That(world.CountEntities(query), Is.EqualTo(1));

            byte bucket = byte.MaxValue;

            world.Query(query, (ref GetProfilesBatchIntent _, ref IPartitionComponent p) => bucket = p.Bucket);

            Assert.That(bucket, Is.EqualTo(0));
        }
    }
}
