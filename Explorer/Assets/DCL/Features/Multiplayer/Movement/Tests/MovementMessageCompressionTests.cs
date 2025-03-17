using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.Settings;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Utility.ParcelMathHelper;

namespace DCL.Multiplayer.Movement.Tests
{
    [TestFixture]
    public class MovementMessageCompressionTests
    {
        private NetworkMessageEncoder encoder;

        private static MessageEncodingSettings settings;

        private static MessageEncodingSettings Settings
        {
            get
            {
                settings ??= LoadSettings();
                return settings;
            }
        }

        private static ParcelEncoder parcelEncoder => new (Settings.landscapeData.terrainData);

        [SetUp]
        public void SetUp()
        {
            encoder = new NetworkMessageEncoder(Settings);
        }

        private static MessageEncodingSettings LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:MessageEncodingSettings");
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<MessageEncodingSettings>(path);
        }

        [TestCase(0f, 0f)]
        [TestCase(5f, 0.751f)]
        [TestCase(-5f, -1000f)]

        // We don't pass blend values with the message, so they should be reset to 0 on decompress
        public void ShouldResetBlendsToZeroOnDecompress(float moveBlend, float slideBlend)
        {
            // ARRANGE
            var originalMessage = new NetworkMovementMessage
            {
                animState = new AnimationStates
                {
                    MovementBlendValue = moveBlend,
                    SlideBlendValue = slideBlend,
                },
            };

            // ACT
            NetworkMovementMessage decompressedMessage = encoder.Decompress(encoder.Compress(originalMessage));

            // ASSERT
            Assert.That(decompressedMessage.animState.MovementBlendValue, Is.EqualTo(0f));
            Assert.That(decompressedMessage.animState.SlideBlendValue, Is.EqualTo(0f));
        }

        [TestCase(MovementKind.JOG, false, false, false, false, false, false, false)]
        [TestCase(MovementKind.RUN, true, true, true, true, true, true, true)]
        [TestCase(MovementKind.WALK, true, false, true, false, true, false, false)]
        [TestCase(MovementKind.IDLE, false, true, true, false, false, true, true)]
        public void ShouldCorrectlyEncodeAndDecodeAnimations(MovementKind movementKind, bool isSliding, bool isStunned, bool isGrounded, bool isJumping,
            bool isLongJump, bool isLongFall, bool isFalling)
        {
            // ARRANGE
            var originalMessage = new NetworkMovementMessage
            {
                movementKind = movementKind,
                isSliding = isSliding,
                isStunned = isStunned,
                animState = new AnimationStates
                {
                    IsGrounded = isGrounded,
                    IsJumping = isJumping,
                    IsLongJump = isLongJump,
                    IsLongFall = isLongFall,
                    IsFalling = isFalling,
                },
            };

            // ACT
            NetworkMovementMessage decompressedMessage = encoder.Decompress(encoder.Compress(originalMessage));

            // ASSERT
            Assert.AreEqual(originalMessage.isStunned, decompressedMessage.isStunned);

            Assert.AreEqual(originalMessage.movementKind, decompressedMessage.movementKind);
            Assert.AreEqual(originalMessage.isSliding, decompressedMessage.isSliding);

            Assert.AreEqual(originalMessage.animState.IsGrounded, decompressedMessage.animState.IsGrounded);
            Assert.AreEqual(originalMessage.animState.IsJumping, decompressedMessage.animState.IsJumping);
            Assert.AreEqual(originalMessage.animState.IsLongJump, decompressedMessage.animState.IsLongJump);
            Assert.AreEqual(originalMessage.animState.IsLongFall, decompressedMessage.animState.IsLongFall);
            Assert.AreEqual(originalMessage.animState.IsFalling, decompressedMessage.animState.IsFalling);
        }

        private static IEnumerable<TestCaseData> GetParcelMinMaxTestCases()
        {
            yield return new TestCaseData(parcelEncoder.MaxX * PARCEL_SIZE, parcelEncoder.MaxY * PARCEL_SIZE);
            yield return new TestCaseData(parcelEncoder.MinX * PARCEL_SIZE, parcelEncoder.MinY * PARCEL_SIZE);
            yield return new TestCaseData((parcelEncoder.MaxX * PARCEL_SIZE) - 0.001f, (parcelEncoder.MaxY * PARCEL_SIZE) - 0.001f);
            yield return new TestCaseData((parcelEncoder.MinX * PARCEL_SIZE) + 0.001f, (parcelEncoder.MinY * PARCEL_SIZE) + 0.001f);
        }

        [TestCase(-1.2f, -5.5f)]
        [TestCase(-0.001f, -0.001f)]
        [TestCase(0, 0)]
        [TestCase(0.001f, 0.001f)]
        [TestCase(8.0f, 8.356f)]
        [TestCase(15.999f, 15.999f)]
        [TestCase(16, 16)]
        [TestCase(16.001f, 16.001f)]
        [TestCase(16.5f, 16.3f)]
        [TestCase(52.5f, -13.33f)]
        [TestCase(-70f, 116f)]
        [TestCase(-1000f, 1000f)]
        [TestCase(1000f, -1000f)]
        [TestCaseSource(nameof(GetParcelMinMaxTestCases))]
        public void ShouldCorrectlyEncodeAndDecodeXZPositions(float x, float z)
        {
            // Arrange
            float stepSize = PARCEL_SIZE / Mathf.Pow(2, settings.tier3.XZ__BITS);
            float quantizationError = (stepSize / 2f) + 0.0002f; // there is a small deviation at 8.0f point (less then 0.0002f)

            var originalMessage = new NetworkMovementMessage { position = new Vector3(x, 0f, z) };

            // Act
            NetworkMovementMessage decompressedMessage = encoder.Decompress(encoder.Compress(originalMessage));

            // Assert
            Assert.AreEqual(originalMessage.position.x, decompressedMessage.position.x, quantizationError);
            Assert.AreEqual(originalMessage.position.z, decompressedMessage.position.z, quantizationError);

            Debug.Log($" XZ quantization error = {quantizationError} | original: {originalMessage.position.x}, {originalMessage.position.z} | decompressed: {decompressedMessage.position.x}, {decompressedMessage.position.z}");
        }

        private static IEnumerable<float> GetYMaxTestCases()
        {
            yield return Settings.tier3.Y__MAX / 8f;
            yield return Settings.tier3.Y__MAX / 4f;
            yield return Settings.tier3.Y__MAX / 3f;
            yield return Settings.tier3.Y__MAX / 2f;
            yield return Settings.tier3.Y__MAX;
            yield return Settings.tier3.Y__MAX + 0.05f;
            yield return 10 * Settings.tier3.Y__MAX;
        }

        [TestCase(-5.751f)]
        [TestCase(-0.001f)]
        [TestCase(0f)]
        [TestCase(0.751f)]
        [TestCase(4.521f)]
        [TestCase(17.25f)]
        [TestCaseSource(nameof(GetYMaxTestCases))]
        public void ShouldCorrectlyEncodeAndDecodeYPositions(float y)
        {
            // Arrange
            float stepSize = settings.tier3.Y__MAX / Mathf.Pow(2, settings.tier3.Y__BITS);
            float quantizationError = stepSize / 2f;

            var originalMessage = new NetworkMovementMessage
            {
                position = new Vector3(0f, y, 0f),
                velocityTier = 3,
            };

            // Act
            NetworkMovementMessage decompressedMessage = encoder.Decompress(encoder.Compress(originalMessage));

            // Assert
            Assert.That(decompressedMessage.position.y, Is.GreaterThanOrEqualTo(0));
            Assert.AreEqual(Mathf.Clamp(originalMessage.position.y, 0, settings.tier3.Y__MAX), decompressedMessage.position.y, quantizationError);

            Debug.Log($"Y quantization error = {quantizationError} | original: {originalMessage.position.y} | decompressed: {decompressedMessage.position.y}");
        }

        private static IEnumerable<TestCaseData> GetMaxVelocityTestCases()
        {
            yield return new TestCaseData(20.241f, 30f, Settings.tier3.MAX__VELOCITY - 0.003f);
            yield return new TestCaseData(-20.241f, -30f, -Settings.tier3.MAX__VELOCITY + 0.023f);
            yield return new TestCaseData(Settings.tier3.MAX__VELOCITY, -Settings.tier3.MAX__VELOCITY, Settings.tier3.MAX__VELOCITY);
            yield return new TestCaseData(-Settings.tier3.MAX__VELOCITY, Settings.tier3.MAX__VELOCITY, -Settings.tier3.MAX__VELOCITY);
        }

        [TestCase(0, 0, 0)]
        [TestCase(1.153f, 2.753f, 3.523f)]
        [TestCase(-1.153f, -2.753f, -3.523f)]
        [TestCase(5.153f, 10f, 15.523f)]
        [TestCase(-5.153f, -10f, -15.523f)]
        [TestCaseSource(nameof(GetMaxVelocityTestCases))]
        public void ShouldCorrectlyCompressAndDecompressVelocity(float x, float y, float z)
        {
            // Arrange
            float stepSize = 2 * settings.tier3.MAX__VELOCITY / Mathf.Pow(2, settings.tier3.VELOCITY_AXIS_FIELD_SIZE_IN_BITS);
            float quantizationError = stepSize / 2f;

            var originalMessage = new NetworkMovementMessage
            {
                velocity = new Vector3(x, y, z),
                velocityTier = 3,
            };

            if (originalMessage.velocity.magnitude < 0.001f)
                quantizationError += 0.015f; // there is velocity deviation at zero (< 0.015f)

            // Act
            NetworkMovementMessage decompressedMessage = encoder.Decompress(encoder.Compress(originalMessage));

            // Assert
            Assert.AreEqual(originalMessage.velocity.x, decompressedMessage.velocity.x, quantizationError);
            Assert.AreEqual(originalMessage.velocity.y, decompressedMessage.velocity.y, quantizationError);
            Assert.AreEqual(originalMessage.velocity.z, decompressedMessage.velocity.z, quantizationError);

            Debug.Log($" Velocity quantization error = {quantizationError} | original: {originalMessage.velocity} | decompressed: {decompressedMessage.velocity}");
        }

        [TestCase(0f)]
        [TestCase(0.0001f)]
        [TestCase(2.711111f)]
        [TestCase(5.002f)] // 5 sec
        [TestCase(15.004f)] // 15 sec
        [TestCase(30.005f)] // 30 sec
        [TestCase(60.007f)] // 1 min
        [TestCase(20f * 60.001f)] // 20 min
        [TestCase(30f * 60.002f)] // 30 min
        [TestCase(60f * 60.007f)] // 1 hour
        public void ShouldCorrectlyEncodeAndDecodeTimestamp(float t)
        {
            // Arrange
            var originalMessage = new NetworkMovementMessage { timestamp = t };
            var timestampEncoder = new TimestampEncoder(Settings);

            // Act
            NetworkMovementMessage decompressedMessage = encoder.Decompress(encoder.Compress(originalMessage));

            // Assert
            Assert.AreEqual(t % timestampEncoder.BufferSize, decompressedMessage.timestamp, Settings.TIMESTAMP_QUANTUM);
            Debug.Log($"Timestamp quantization = {Settings.TIMESTAMP_QUANTUM}, buffer size = {timestampEncoder.BufferSize / 60} min | original: {t} | decompressed: {decompressedMessage.timestamp}");
        }

        public static IEnumerable<TestCaseData> FloatCompressDecompressTestCases()
        {
            for (var bits = 1; bits < 31; bits++)
                yield return new TestCaseData(0.5f, 0, 1, bits);

            const float RANGE = 4;

            for (var i = 0; i < 30; i++)
            {
                float interpolated = Mathf.Lerp(-RANGE, RANGE, i / 30f);
                yield return new TestCaseData(interpolated, -RANGE, RANGE, 4);
            }
        }

        [Test]
        [TestCaseSource(nameof(FloatCompressDecompressTestCases))]
        public void FloatCompressDecompress(float value, float min, float max, int bits)
        {
            int compressed = FloatQuantizer.Compress(value, min, max, bits);
            Debug.Log($"Value is: 0x{compressed:X4}");
            float decompressed = FloatQuantizer.Decompress(compressed, min, max, bits);
            Debug.Log($"Decompressed value is: {decompressed}, original: {value}");
        }
    }
}
