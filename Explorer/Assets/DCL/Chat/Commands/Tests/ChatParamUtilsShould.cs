using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.Commands.Tests
{
    [TestFixture]
    public class ChatParamUtilsShould
    {
        [TestCase("12,34", 12, 34)]
        [TestCase("-51,1", -51, 1)]
        [TestCase("0,0", 0, 0)]
        public void ParseParcel(string param, int x, int y)
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget(param);

            // Assert
            Assert.That(target.Parcel, Is.EqualTo(new Vector2Int(x, y)));
            Assert.That(target.World, Is.Null);
            Assert.That(target.SpawnPoint, Is.Null);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }

        [Test]
        public void ParseRandom()
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget("random");

            // Assert
            Assert.That(target.IsRandom, Is.True);
            Assert.That(target.IsCrowd, Is.False);
            Assert.That(target.World, Is.Null);
            Assert.That(target.Parcel, Is.Null);
            Assert.That(target.SpawnPoint, Is.Null);
        }

        [Test]
        public void ParseCrowd()
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget("crowd");

            // Assert
            Assert.That(target.IsCrowd, Is.True);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.World, Is.Null);
            Assert.That(target.Parcel, Is.Null);
            Assert.That(target.SpawnPoint, Is.Null);
        }

        [Test]
        public void ParseWorld()
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget("myworld.dcl.eth");

            // Assert
            Assert.That(target.World, Is.EqualTo("myworld.dcl.eth"));
            Assert.That(target.Parcel, Is.Null);
            Assert.That(target.SpawnPoint, Is.Null);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }

        [Test]
        public void ParseWorldWithParcel()
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget("myworld.dcl.eth/-51,1");

            // Assert
            Assert.That(target.World, Is.EqualTo("myworld.dcl.eth"));
            Assert.That(target.Parcel, Is.EqualTo(new Vector2Int(-51, 1)));
            Assert.That(target.SpawnPoint, Is.Null);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }

        [TestCase("12,34/lobby", 12, 34, "lobby")]
        [TestCase("-51,1/PlazaCenter", -51, 1, "PlazaCenter")]
        [TestCase("0,0/theatre", 0, 0, "theatre")]
        public void ParseParcelWithSpawnPoint(string param, int x, int y, string spawnPoint)
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget(param);

            // Assert
            Assert.That(target.Parcel, Is.EqualTo(new Vector2Int(x, y)));
            Assert.That(target.SpawnPoint, Is.EqualTo(spawnPoint));
            Assert.That(target.World, Is.Null);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }

        // Anything that is not a position, "random"/"crowd", "x,y/spawn", or "world/x,y"
        // is treated verbatim as a world name.
        [TestCase("")]
        [TestCase("myworld.dcl.eth/lobby")]
        [TestCase("myworld.dcl.eth/")]
        [TestCase("/12,34")]
        [TestCase("12,34,56")]
        [TestCase("12,x")]
        [TestCase("12,")]
        [TestCase("12,34/")]
        [TestCase("12,34/lob/by")]
        [TestCase("12,34/lob,by")]
        public void TreatEverythingElseAsWorld(string param)
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget(param);

            // Assert
            Assert.That(target.World, Is.EqualTo(param));
            Assert.That(target.Parcel, Is.Null);
            Assert.That(target.SpawnPoint, Is.Null);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }
    }
}
