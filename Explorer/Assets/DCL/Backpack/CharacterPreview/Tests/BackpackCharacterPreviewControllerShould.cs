using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.CharacterPreview.Tests;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.Backpack.CharacterPreview.Tests
{
    [TestFixture]
    public class BackpackCharacterPreviewControllerShould
    {
        private static readonly URN EMOTE_URN = new ("urn:decentraland:off-chain:base-avatars:wave");

        private readonly List<Object> created = new ();

        private BackpackEventBus backpackEventBus = null!;
        private TestPreview preview = null!;
        private World world = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            backpackEventBus = new BackpackEventBus();

            preview = new TestPreview(CharacterPreviewTestViews.Create(created), Substitute.For<ICharacterPreviewFactory>(),
                backpackEventBus, world, Substitute.For<IEquippedEmotes>(), new CharacterPreviewEventBus());
        }

        [TearDown]
        public void TearDown()
        {
            preview.Dispose();
            World.Destroy(world);

            foreach (Object obj in created)
                Object.DestroyImmediate(obj);

            created.Clear();
        }

        [Test]
        public void PreviewASelectedEmoteWhenNoEmoteIsEquipped()
        {
            // Arrange
            IEmote emote = Substitute.For<IEmote>();
            emote.GetUrn().Returns(EMOTE_URN);

            // Act
            backpackEventBus.SendEmoteSelect(emote);

            // Assert
            Assert.IsNotNull(preview.Emotes);
            CollectionAssert.IsEmpty(preview.Emotes);
            LogAssert.NoUnexpectedReceived();
        }

        private class TestPreview : BackpackCharacterPreviewController
        {
            public TestPreview(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, IBackpackEventBus backpackEventBus,
                World world, IEquippedEmotes equippedEmotes, CharacterPreviewEventBus characterPreviewEventBus)
                : base(view, previewFactory, backpackEventBus, world, equippedEmotes, characterPreviewEventBus) { }

            protected override bool hideImageWhileLoading => false;

            public HashSet<URN>? Emotes => previewAvatarModel.Emotes;
        }
    }
}
