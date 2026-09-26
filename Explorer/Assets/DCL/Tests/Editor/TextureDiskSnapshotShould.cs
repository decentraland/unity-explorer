using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Textures;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Tests
{
    public class TextureDiskSnapshotShould
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RetainAllChunksAfterTextureStorageIsReleased(bool destroyTexture)
        {
            // Arrange
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var expected = new byte[256 * 256 * 4];

            for (int i = 0; i < expected.Length; i++)
                expected[i] = (byte)(i % 251);

            texture.LoadRawTextureData(expected);
            texture.Apply();

            try
            {
                using var iterator = new TextureDiskSerializer().Serialize(new TextureData(AnyTexture.FromTexture2D(texture)));

                // Act
                if (destroyTexture)
                    Object.DestroyImmediate(texture);
                else
                    texture.Apply(false, true);

                // Assert
                Assert.That(iterator.MoveNext(), Is.True);
                Assert.That(iterator.Current.Length, Is.EqualTo(16));
                int offset = 0;

                while (iterator.MoveNext())
                {
                    byte[] chunk = iterator.Current.ToArray();
                    var expectedChunk = new byte[chunk.Length];
                    System.Array.Copy(expected, offset, expectedChunk, 0, chunk.Length);
                    CollectionAssert.AreEqual(expectedChunk, chunk);
                    offset += chunk.Length;
                }

                Assert.That(offset, Is.EqualTo(expected.Length));
            }
            finally
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReleaseOwnedSourceWhenIterationEndsEarly(bool readFirstChunk)
        {
            // Arrange
            int disposed = 0;

            // Act
            using (var iterator = SerializeMemoryIterator<int>.New(0, static (_, _, _) => 0,
                       static (_, _, _) => false, _ => disposed++))
            {
                if (readFirstChunk)
                    iterator.MoveNext();
            }

            // Assert
            Assert.That(disposed, Is.EqualTo(1));
        }
    }
}
