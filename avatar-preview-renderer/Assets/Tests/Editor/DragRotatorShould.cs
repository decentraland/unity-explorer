using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests
{
    public class DragRotatorShould
    {
        // The rotator's serialized default.
        private const float MAX_PITCH = 30f;
        private const float TOLERANCE = 0.01f;

        private GameObject _subject;
        private DragRotator _rotator;

        [SetUp]
        public void SetUp()
        {
            _subject = new GameObject("subject");
            _rotator = _subject.AddComponent<DragRotator>();
            UnityMessages.Invoke(_rotator, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_subject);
        }

        [Test]
        public void TurnBySetAngles()
        {
            // Act
            _rotator.Rotate(45f, 0f);

            // Assert
            Assert.AreEqual(45f, Angle(), TOLERANCE);
        }

        [Test]
        public void ReturnToTheStartRotationOnAReset()
        {
            // Arrange
            _rotator.SetStartRotation(45f, 0f);
            _rotator.Rotate(30f, 0f);

            // Act
            _rotator.ResetRotation();

            // Assert
            Assert.AreEqual(45f, Angle(), TOLERANCE);
        }

        [Test]
        public void LevelToTheStartTiltWhileAutoRotating()
        {
            // Arrange: auto-rotate due at once, levelling done in one frame, and no turn of its own.
            var serialized = new SerializedObject(_rotator);
            serialized.FindProperty("autoRotateDelay").floatValue = -1f;
            serialized.FindProperty("returnSpeed").floatValue = 1e6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _rotator.AutoRotateSpeed = 0f;
            _rotator.SetStartRotation(0f, 20f);

            // Act
            UnityMessages.Invoke(_rotator, "Update");

            // Assert
            Assert.AreEqual(20f, Angle(), TOLERANCE);
        }

        [Test]
        public void ClampTheTiltOfARotate()
        {
            // Act
            _rotator.Rotate(0f, 90f);

            // Assert
            Assert.AreEqual(MAX_PITCH, Angle(), TOLERANCE);
        }

        [Test]
        public void IgnoreLockedAxesOnARotate()
        {
            // Arrange
            _rotator.AllowHorizontal = false;
            _rotator.AllowVertical = false;

            // Act
            _rotator.Rotate(45f, 20f);

            // Assert
            Assert.AreEqual(0f, Angle(), TOLERANCE);
        }

        [Test]
        public void DropTheInertiaOfADragWhenInputIsDisabled()
        {
            // Arrange: a flick leaves velocity behind.
            _rotator.OnDrag(new Vector2(200f, 50f), 0.016f);

            // Act
            _rotator.InputEnabled = false;
            UnityMessages.Invoke(_rotator, "Update");

            // Assert
            Assert.AreEqual(0f, Angle(), TOLERANCE);
        }

        [Test]
        public void KeepTurningFromADragWhileInputIsEnabled()
        {
            // Arrange
            _rotator.OnDrag(new Vector2(200f, 0f), 0.016f);

            // Act
            UnityMessages.Invoke(_rotator, "Update");

            // Assert
            Assert.Greater(Angle(), 0f);
        }

        private float Angle() => Quaternion.Angle(Quaternion.identity, _subject.transform.rotation);
    }
}
