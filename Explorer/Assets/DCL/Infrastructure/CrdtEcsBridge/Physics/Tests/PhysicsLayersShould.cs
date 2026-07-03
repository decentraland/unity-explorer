using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using NUnit.Framework;

namespace CrdtEcsBridge.Physics.Tests
{
    [TestFixture]
    public class PhysicsLayersShould
    {
        [Test]
        public void TryGetUnityLayerFromSDKLayer_ReturnsAvatarHit_ForPlayerOnly()
        {
            ColliderLayer mask = ColliderLayer.ClPlayer;

            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            // Avatar-only masks land on SDKAvatarHit (player capsule passes through; trigger areas + raycasts still detect).
            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.SDK_AVATAR_HIT_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_ReturnsAvatarHit_ForMainPlayerOnly()
        {
            ColliderLayer mask = ColliderLayer.ClMainPlayer;

            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.SDK_AVATAR_HIT_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_ReturnsAvatarHit_ForPlayerAndMainPlayer()
        {
            ColliderLayer mask = ColliderLayer.ClPlayer | ColliderLayer.ClMainPlayer;

            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.SDK_AVATAR_HIT_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_ReturnsCharacterOnly_ForPlayerWithPhysics()
        {
            // Mixed mask: CL_PHYSICS wins — mesh must stay solid against the player capsule.
            ColliderLayer mask = ColliderLayer.ClPlayer | ColliderLayer.ClPhysics;

            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.CHARACTER_ONLY_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_PreservesPhysicsBranch()
        {
            // Arrange
            ColliderLayer mask = ColliderLayer.ClPhysics;

            // Act
            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.CHARACTER_ONLY_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_PreservesPointerBranch()
        {
            // Arrange
            ColliderLayer mask = ColliderLayer.ClPointer;

            // Act
            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.ON_POINTER_EVENT_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_PhysicsAndPointer_StillReturnsDefault()
        {
            // Arrange
            ColliderLayer mask = ColliderLayer.ClPhysics | ColliderLayer.ClPointer;

            // Act
            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(PhysicsLayers.DEFAULT_LAYER, unityLayer);
        }

        [Test]
        public void TryGetUnityLayerFromSDKLayer_FallsBack_WhenMaskHasNoMappableLayer()
        {
            // Arrange
            ColliderLayer mask = ColliderLayer.ClNone;

            // Act
            bool result = PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(0, unityLayer);
        }

        [Test]
        public void CreateUnityLayerMaskFromSDKMask_IncludesOtherAvatarsAndAvatarHit_WhenMaskHasPlayer()
        {
            ColliderLayer mask = ColliderLayer.ClPlayer;

            int unityMask = PhysicsLayers.CreateUnityLayerMaskFromSDKMask(mask);

            Assert.AreNotEqual(0, unityMask & (1 << PhysicsLayers.OTHER_AVATARS_LAYER),
                "Expected OTHER_AVATARS_LAYER bit to be set when SDK mask contains CL_PLAYER.");
            Assert.AreNotEqual(0, unityMask & (1 << PhysicsLayers.CHARACTER_LAYER),
                "Expected CHARACTER_LAYER to be included when SDK mask contains CL_PLAYER.");
            Assert.AreNotEqual(0, unityMask & (1 << PhysicsLayers.SDK_AVATAR_HIT_LAYER),
                "Expected SDK_AVATAR_HIT_LAYER to be included when SDK mask contains CL_PLAYER.");
        }

        [Test]
        public void CreateUnityLayerMaskFromSDKMask_IncludesAvatarHitButNotOtherAvatars_WhenMaskOnlyHasMainPlayer()
        {
            ColliderLayer mask = ColliderLayer.ClMainPlayer;

            int unityMask = PhysicsLayers.CreateUnityLayerMaskFromSDKMask(mask);

            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.OTHER_AVATARS_LAYER),
                "Expected OTHER_AVATARS_LAYER bit NOT to be set when only CL_MAIN_PLAYER is in the SDK mask.");
            Assert.AreNotEqual(0, unityMask & (1 << PhysicsLayers.CHARACTER_LAYER),
                "Expected CHARACTER_LAYER to be included when SDK mask contains CL_MAIN_PLAYER.");
            Assert.AreNotEqual(0, unityMask & (1 << PhysicsLayers.SDK_AVATAR_HIT_LAYER),
                "Expected SDK_AVATAR_HIT_LAYER to be included when SDK mask contains CL_MAIN_PLAYER.");
        }

        [Test]
        public void CreateUnityLayerMaskFromSDKMask_ExcludesCharacter_ForPhysicsOnlyMask()
        {
            // CL_PHYSICS is not in PLAYER_QUALIFYING_BITS — CHARACTER_LAYER must NOT be reached.
            ColliderLayer mask = ColliderLayer.ClPhysics;

            // Act
            int unityMask = PhysicsLayers.CreateUnityLayerMaskFromSDKMask(mask);

            // Assert
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.CHARACTER_LAYER),
                "Expected CHARACTER_LAYER NOT to be set for a CL_PHYSICS-only mask.");
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.OTHER_AVATARS_LAYER),
                "Expected OTHER_AVATARS_LAYER NOT to be set for a CL_PHYSICS-only mask.");
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.SDK_AVATAR_HIT_LAYER),
                "Expected SDK_AVATAR_HIT_LAYER NOT to be set for a CL_PHYSICS-only mask.");
        }

        [Test]
        public void CreateUnityLayerMaskFromSDKMask_ExcludesCharacter_ForPointerOnlyMask()
        {
            // Arrange: CL_POINTER alone is NOT in PLAYER_QUALIFYING_BITS — pointer raycasts must
            // not unconditionally hit the local player capsule.
            ColliderLayer mask = ColliderLayer.ClPointer;

            // Act
            int unityMask = PhysicsLayers.CreateUnityLayerMaskFromSDKMask(mask);

            // Assert
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.CHARACTER_LAYER),
                "Expected CHARACTER_LAYER NOT to be set for a CL_POINTER-only mask.");
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.OTHER_AVATARS_LAYER),
                "Expected OTHER_AVATARS_LAYER NOT to be set for a CL_POINTER-only mask.");
        }

        [Test]
        public void CreateUnityLayerMaskFromSDKMask_ExcludesCharacter_ForCustomOnlyMask()
        {
            // Arrange: CL_CUSTOM* bits are NOT in PLAYER_QUALIFYING_BITS — custom-layer raycasts
            // must not unconditionally hit the local player capsule.
            ColliderLayer mask = ColliderLayer.ClCustom1;

            // Act
            int unityMask = PhysicsLayers.CreateUnityLayerMaskFromSDKMask(mask);

            // Assert
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.CHARACTER_LAYER),
                "Expected CHARACTER_LAYER NOT to be set for a CL_CUSTOM1-only mask.");
            Assert.AreEqual(0, unityMask & (1 << PhysicsLayers.OTHER_AVATARS_LAYER),
                "Expected OTHER_AVATARS_LAYER NOT to be set for a CL_CUSTOM1-only mask.");
        }

        [Test]
        public void IsAvatarOnlyMask_TrueForAvatarBitsOnly()
        {
            Assert.IsTrue(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClPlayer));
            Assert.IsTrue(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClMainPlayer));
            Assert.IsTrue(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClPlayer | ColliderLayer.ClMainPlayer));
        }

        [Test]
        public void IsAvatarOnlyMask_FalseForNonAvatarOrMixedMasks()
        {
            Assert.IsFalse(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClNone));
            Assert.IsFalse(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClPhysics));
            Assert.IsFalse(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClPointer));
            Assert.IsFalse(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClCustom1));
            // Mixed: avatar bit + non-avatar bit must NOT be avatar-only.
            Assert.IsFalse(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClPlayer | ColliderLayer.ClPhysics));
            Assert.IsFalse(PhysicsLayers.IsAvatarOnlyMask(ColliderLayer.ClMainPlayer | ColliderLayer.ClPointer));
        }
    }
}
