using CommunicationData.URLHelpers;
using Global.Dynamic.TeleportOperations;
using NUnit.Framework;
using UnityEngine;

namespace Global.Tests.EditMode
{
    public class TeleportCounterShould
    {
        private TeleportCounter teleportCounter;
        private readonly int teleportBeforeUnload = 3;

        private readonly URLDomain genesis = URLDomain.FromString("Genesis");
        private readonly URLDomain olavra = URLDomain.FromString("Olavra");

        [SetUp]
        public void SetUp()
        {
            teleportCounter = new TeleportCounter(teleportBeforeUnload);
        }

        [Test]
        public void AddSuccessfullTeleport()
        {
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, false);
            Assert.AreEqual(1, teleportCounter.teleportsDone);
        }

        [Test]
        public void IgnoreSameRealmDuplicatedSuccessfullTeleport()
        {
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, false);
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, false);
            Assert.AreEqual(1, teleportCounter.teleportsDone);
        }

        [Test]
        public void AddSucessfullTeleportWhenDifferentRealm()
        {
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, false);
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), olavra, true);
            Assert.AreEqual(2, teleportCounter.teleportsDone);
        }

        [Test]
        public void ClearListAfterLimitReached()
        {
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, false);
            Assert.IsFalse(teleportCounter.ReachedTeleportLimit());
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(2, 2), genesis, false);
            Assert.IsFalse(teleportCounter.ReachedTeleportLimit());
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(3, 3), genesis, false);
            Assert.IsTrue(teleportCounter.ReachedTeleportLimit());
            Assert.AreEqual(0, teleportCounter.teleportsDone);
        }

        [Test]
        public void ClearListAfterLimitReachedWithRealmChage()
        {
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, false);
            Assert.IsFalse(teleportCounter.ReachedTeleportLimit());
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), olavra, true);
            Assert.IsFalse(teleportCounter.ReachedTeleportLimit());
            teleportCounter.AddSuccessfullTeleport(new Vector2Int(3, 3), genesis, false);
            Assert.IsTrue(teleportCounter.ReachedTeleportLimit());
            Assert.AreEqual(0, teleportCounter.teleportsDone);
        }

        [Test]
        public void PingPongingBetweenRealmsDontTriggerTheLimit()
        {
            for (var i = 0; i < 100; i++)
            {
                teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), genesis, true);
                teleportCounter.AddSuccessfullTeleport(new Vector2Int(1, 1), olavra, true);
            }

            Assert.IsFalse(teleportCounter.ReachedTeleportLimit());
            Assert.AreEqual(2, teleportCounter.teleportsDone);
        }
    }
}