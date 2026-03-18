using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture]
    public class GetTextureIntentionShould
    {
        [Test]
        public void AvatarTextures_WithDifferentUserIds_AreNotEqual()
        {
            var intention1 = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            var intention2 = new GetTextureIntention(
                userId: "user-456",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.AreNotEqual(intention1, intention2);
            Assert.IsFalse(intention1.Equals(intention2));
        }

        [Test]
        public void AvatarTextures_WithSameUserId_AreEqual()
        {
            var intention1 = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            var intention2 = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.AreEqual(intention1, intention2);
            Assert.IsTrue(intention1.Equals(intention2));
        }

        [Test]
        public void AvatarTextures_WithDifferentUserIds_HaveDifferentHashCodes()
        {
            var intention1 = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            var intention2 = new GetTextureIntention(
                userId: "user-456",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.AreNotEqual(intention1.GetHashCode(), intention2.GetHashCode());
        }

        [Test]
        public void AvatarTextures_WithSameUserId_HaveSameHashCodes()
        {
            var intention1 = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            var intention2 = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.AreEqual(intention1.GetHashCode(), intention2.GetHashCode());
        }

        [Test]
        public void RegularTextures_WithSameUrl_AreEqual()
        {
            var intention1 = new GetTextureIntention(
                url: "https://example.com/texture.png",
                fileHash: "hash123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            var intention2 = new GetTextureIntention(
                url: "https://example.com/texture.png",
                fileHash: "hash123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.AreEqual(intention1, intention2);
            Assert.AreEqual(intention1.GetHashCode(), intention2.GetHashCode());
        }

        [Test]
        public void AvatarTexture_IsRecognizedAsAvatarTexture()
        {
            var intention = new GetTextureIntention(
                userId: "user-123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.IsTrue(intention.IsAvatarTexture);
            Assert.AreEqual("user-123", intention.AvatarTextureUserId);
        }

        [Test]
        public void RegularTexture_IsNotRecognizedAsAvatarTexture()
        {
            var intention = new GetTextureIntention(
                url: "https://example.com/texture.png",
                fileHash: "hash123",
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureType.Albedo,
                reportSource: "test",
                attemptsCount: 1
            );
            
            Assert.IsFalse(intention.IsAvatarTexture);
            Assert.IsNull(intention.AvatarTextureUserId);
        }
    }
}
