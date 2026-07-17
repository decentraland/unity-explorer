using Arch.Core;
using DCL.CharacterCamera;
using DCL.McpServer.Tools;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    public class GetPlayerStateToolShould
    {
        private World world = null!;
        private GetPlayerStateTool tool = null!;

        [SetUp]
        public void Setup()
        {
            world = World.Create();
            tool = new GetPlayerStateTool(world, world.Create(), new ExposedCameraData(), Substitute.For<ICurrentSceneInfo>());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void DeclareAnObjectOutputSchema()
        {
            Assert.That(tool.OutputSchema!["type"]!.Value<string>(), Is.EqualTo("object"));
        }

        [Test]
        public void ModelTheAddressAsANullableString()
        {
            var addressType = (JArray)tool.OutputSchema!["properties"]!["address"]!["type"]!;
            Assert.That(addressType.ToObject<string[]>(), Is.EqualTo(new[] { "string", "null" }));
        }

        [Test]
        public void ModelTheCameraAsANestedObject()
        {
            JToken camera = tool.OutputSchema!["properties"]!["camera"]!;

            Assert.That(camera["type"]!.Value<string>(), Is.EqualTo("object"));
            Assert.That(camera["properties"]!["mode"]!["type"]!.Value<string>(), Is.EqualTo("string"));
        }
    }
}
