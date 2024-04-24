using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using ECS.TestSuite;

namespace DCL.Multiplayer.SDK.Tests
{
    public class AvatarEmoteCommandPropagationSystemShould : UnitySystemTestBase<AvatarEmoteCommandPropagationSystem>
    {
        // [Test]
        // public void UpdatePlayerSDKDataWithEmoteEvents()
        // {
        //     scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
        //     scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
        //     fakeCharacterUnityTransform.position = Vector3.one;
        //
        //     var profile = new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar());
        //     world.Add(entity, profile, new CharacterTransform(fakeCharacterUnityTransform));
        //
        //     Assert.IsFalse(world.Has<PlayerProfileDataComponent>(entity));
        //
        //     system.Update(0);
        //
        //     Assert.IsTrue(world.TryGet(entity, out PlayerProfileDataComponent playerSDKDataComponent));
        //     Assert.AreEqual(FAKE_USER_ID, playerSDKDataComponent.Address);
        //     Assert.AreEqual(profile.Name, playerSDKDataComponent.Name);
        //     Assert.IsNotNull(playerSDKDataComponent.CRDTEntity);
        //     Assert.IsTrue(scene1World.TryGet(playerSDKDataComponent.SceneWorldEntity, out PlayerProfileDataComponent scenePlayerSDKDataComponent));
        //     Assert.AreEqual(playerSDKDataComponent, scenePlayerSDKDataComponent);
        //
        //     var emoteUrn1 = "thunder-kiss-65";
        //     var emoteUrn2 = "thunder-kiss-66";
        //
        //     var emoteIntent = new CharacterEmoteIntent
        //         { EmoteId = emoteUrn1 };
        //
        //     IEmote fakeEmote = Substitute.For<IEmote>();
        //
        //     emoteCache.TryGetEmote(Arg.Any<URN>(), out fakeEmote).Returns(true);
        //
        //     // var emoteComponent = new CharacterEmoteComponent
        //     // {
        //     //     EmoteUrn = emoteUrn1,
        //     //     EmoteLoop = true,
        //     // };
        //
        //     Assert.AreNotEqual(emoteComponent.EmoteUrn, playerSDKDataComponent.PlayingEmote);
        //     Assert.AreNotEqual(emoteComponent.EmoteLoop, playerSDKDataComponent.LoopingEmote);
        //
        //     world.Add(entity, emoteIntent);
        //
        //     system.Update(0);
        //     Assert.IsTrue(world.TryGet(entity, out playerSDKDataComponent));
        //     Assert.AreEqual(emoteComponent.EmoteUrn, playerSDKDataComponent.PlayingEmote);
        //     Assert.AreEqual(emoteComponent.EmoteLoop, playerSDKDataComponent.LoopingEmote);
        //
        //     emoteComponent.EmoteUrn = emoteUrn2;
        //     emoteComponent.EmoteLoop = false;
        //
        //     world.Set(entity, emoteComponent);
        //
        //     system.Update(0);
        //     Assert.IsTrue(world.TryGet(entity, out playerSDKDataComponent));
        //     Assert.IsTrue(playerSDKDataComponent.PreviousEmote.Equals(emoteUrn1));
        //     Assert.AreEqual(emoteComponent.EmoteUrn, playerSDKDataComponent.PlayingEmote);
        //     Assert.AreEqual(emoteComponent.EmoteLoop, playerSDKDataComponent.LoopingEmote);
        // }
    }
}
