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
            Assert.That(target.IsCrowd, Is.False); // Deliberately broken for CI smoke test - revert this line.
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

        [TestCase("myworld.dcl.eth/lobby", "myworld.dcl.eth", "lobby")]
        [TestCase("olavra/PlazaCenter", "olavra", "PlazaCenter")]
        [TestCase("myworld.dcl.eth/main lobby", "myworld.dcl.eth", "main lobby")]
        public void ParseWorldWithSpawnPoint(string param, string world, string spawnPoint)
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget(param);

            // Assert
            Assert.That(target.World, Is.EqualTo(world));
            Assert.That(target.SpawnPoint, Is.EqualTo(spawnPoint));
            Assert.That(target.Parcel, Is.Null);
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }

        [TestCase("myworld.dcl.eth/-51,1/lobby", "myworld.dcl.eth", -51, 1, "lobby")]
        [TestCase("olavra/0,0/PlazaCenter", "olavra", 0, 0, "PlazaCenter")]
        [TestCase("myworld.dcl.eth/12,34/main lobby", "myworld.dcl.eth", 12, 34, "main lobby")]
        public void ParseWorldWithParcelAndSpawnPoint(string param, string world, int x, int y, string spawnPoint)
        {
            // Act
            GotoTarget target = ChatParamUtils.ParseGotoTarget(param);

            // Assert
            Assert.That(target.World, Is.EqualTo(world));
            Assert.That(target.Parcel, Is.EqualTo(new Vector2Int(x, y)));
            Assert.That(target.SpawnPoint, Is.EqualTo(spawnPoint));
            Assert.That(target.IsRandom, Is.False);
            Assert.That(target.IsCrowd, Is.False);
        }

        // Anything that is not a position, "random"/"crowd", "x,y/spawn", "world/x,y",
        // "world/spawn", or "world/x,y/spawn" is treated verbatim as a world name.
        [TestCase("")]
        [TestCase("myworld.dcl.eth/")]
        [TestCase("myworld.dcl.eth/-51,1/")]
        [TestCase("myworld.dcl.eth//lobby")]
        [TestCase("myworld.dcl.eth/-51,1/lob/by")]
        [TestCase("myworld.dcl.eth/lob,by/lobby")]
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
