using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using ECS.TestSuite;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerProfileDataPropagationSystemShould : UnitySystemTestBase<PlayerProfileDataPropagationSystem>
    {
        // [Test]
        // public void UpdatePlayerSDKDataCorrectly()
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
        //     Assert.IsTrue(scene1World.TryGet(playerSDKDataComponent.SceneWorldEntity, out PlayerProfileDataComponent sceneplayerSDKDataComponent));
        //     Assert.AreEqual(playerSDKDataComponent, sceneplayerSDKDataComponent);
        //
        //     world.TryGet(entity, out profile);
        //     profile.IsDirty = true;
        //     profile.Name = "NewName";
        //     world.Set(entity, profile);
        //
        //     system.Update(0);
        //
        //     Assert.IsTrue(world.TryGet(entity, out playerSDKDataComponent));
        //     Assert.IsTrue(world.TryGet(entity, out profile));
        //     Assert.AreEqual(profile.Name, playerSDKDataComponent.Name);
        // }
    }
}
