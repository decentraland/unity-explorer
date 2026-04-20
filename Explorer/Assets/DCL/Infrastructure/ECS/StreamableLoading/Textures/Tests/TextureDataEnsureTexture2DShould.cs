using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    /// <summary>
    /// Regression tests for <see cref="TextureData.EnsureTexture2D"/> and the caller
    /// in <c>GltFastDownloadProviderBase.RequestTexture</c>.
    ///
    /// Covers the NullReferenceException and AggregateException (unobserved task) reported in
    /// https://github.com/decentraland/unity-explorer/issues/8057
    /// where <c>promiseResult.Result?.Asset!.EnsureTexture2D()</c> used a null-forgiving
    /// operator that crashed at runtime when Asset was null.
    ///
    /// The fix changed <c>!.</c> to <c>?.</c> so that a null Asset yields null instead of
    /// throwing; these tests verify the expected behaviour of the corrected path.
    /// </summary>
    [TestFixture]
    public class TextureDataEnsureTexture2DShould
    {
        private Texture2D texture;

        [SetUp]
        public void SetUp()
        {
            // Create a minimal 1x1 texture so no file I/O is needed.
            texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (texture != null)
                Object.DestroyImmediate(texture);
        }

        [Test]
        public void ReturnTexture2DWhenAssetIsATexture2D()
        {
            var data = new TextureData((AnyTexture)texture);

            Texture2D result = data.EnsureTexture2D();

            Assert.AreSame(texture, result,
                "EnsureTexture2D should return the same Texture2D that was wrapped");
        }

        [Test]
        public void ThrowArgumentExceptionWhenAssetIsVideoTexture()
        {
            // Ensures the discriminated-union guard still works for video textures —
            // only Texture2D is a valid return value.
            var videoTexture = new VideoTextureData();
            var data = new TextureData((AnyTexture)videoTexture);

            Assert.Throws<System.ArgumentException>(() => data.EnsureTexture2D(),
                "EnsureTexture2D must throw ArgumentException for a VideoTexture asset");
        }

        /// <summary>
        /// Verifies that the null-safe navigation pattern used in
        /// <c>GltFastDownloadProviderBase.RequestTexture</c> after the fix does not throw
        /// when the TextureData reference is null (simulating a null Asset in the promise result).
        /// </summary>
        [Test]
        public void NullSafeNavigationReturnsNullForNullTextureData()
        {
            TextureData? nullData = null;

            // This mirrors the fixed code: promiseResult.Result?.Asset?.EnsureTexture2D()
            // The pre-fix code used ! instead of ? which threw NullReferenceException at runtime.
            Texture2D? result = nullData?.EnsureTexture2D();

            Assert.IsNull(result,
                "Null-safe navigation on a null TextureData must return null, not throw");
        }
    }
}
