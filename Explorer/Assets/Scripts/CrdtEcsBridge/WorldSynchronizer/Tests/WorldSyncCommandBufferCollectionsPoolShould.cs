using CRDT;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CrdtEcsBridge.WorldSynchronizer.Tests
{
    public class WorldSyncCommandBufferCollectionsPoolShould
    {
        private WorldSyncCommandBufferCollectionsPool pool;


        public void SetUp()
        {
            pool = WorldSyncCommandBufferCollectionsPool.Create();
        }


        public void TearDown()
        {
            WorldSyncCommandBufferCollectionsPool.Create();
        }


        public async Task BeMultiThreaded([Values(1, 2, 4, 8, 16, 32, 64, 128, 256)] int threadsCount)
        {
            var pools = new ConcurrentBag<WorldSyncCommandBufferCollectionsPool>();

            var tasks = new UniTaskCompletionSource[threadsCount];

            void LaunchThread(int i)
            {
                var thread = new Thread(() =>
                {
                    pools.Add(WorldSyncCommandBufferCollectionsPool.Create());
                    Thread.Sleep(100);

                    tasks[i].TrySetResult();
                });

                thread.Start();
            }

            for (var i = 0; i < threadsCount; i++)
            {
                tasks[i] = new UniTaskCompletionSource();
                LaunchThread(i);
            }

            await UniTask.WhenAll(tasks.Select(x => x.Task));

            Assert.AreEqual(threadsCount, pools.Count);
        }


        public void DisallowGetMainDictionaryMultipleTimes()
        {
            Dictionary<CRDTEntity, Dictionary<int, BatchState>> md = pool.GetMainDictionary();

            Assert.Throws<ThreadStateException>(() => pool.GetMainDictionary());
        }


        public void DisallowGetDeletedEntitiesMultipleTimes()
        {
            var pool = WorldSyncCommandBufferCollectionsPool.Create();
            List<CRDTEntity> md = pool.GetDeletedEntities();

            Assert.Throws<ThreadStateException>(() => pool.GetDeletedEntities());
        }


        public void CleanMainDictionaryOnRelease()
        {
            Dictionary<CRDTEntity, Dictionary<int, BatchState>> md = pool.GetMainDictionary();
            md.Add(123, new Dictionary<int, BatchState>());

            pool.ReleaseMainDictionary(md);
            CollectionAssert.IsEmpty(md);
        }


        public void CleanDeletedEntitiesOnRelease()
        {
            List<CRDTEntity> md = pool.GetDeletedEntities();
            md.Add(123);

            pool.ReleaseDeletedEntities(md);
            CollectionAssert.IsEmpty(md);
        }
    }
}
