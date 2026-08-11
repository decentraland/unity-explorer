using DCL.Utility.Types;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Profiles.Tests
{
    public class UserIdShould
    {
        [TestCase(null)]
        [TestCase("")]
        public void RejectNullOrEmptyValues(string? raw)
        {
            // Act
            Option<UserId> userId = UserId.New(raw);

            // Assert
            Assert.That(userId.Has, Is.False);
        }

        [Test]
        public void CarryTheRawValueUnchanged()
        {
            // Act
            Option<UserId> userId = UserId.New("0xAbC123");

            // Assert
            Assert.That(userId.Has, Is.True);
            Assert.That(userId.Value.Value, Is.EqualTo("0xAbC123"));
        }

        [Test]
        public void IgnoreCaseOnEquality()
        {
            // Arrange
            UserId upperCase = UserId.New("0xABCDEF").Unwrap();
            UserId lowerCase = UserId.New("0xabcdef").Unwrap();

            // Assert
            Assert.That(upperCase.Equals(lowerCase), Is.True);
            Assert.That(upperCase == lowerCase, Is.True);
            Assert.That(upperCase.GetHashCode(), Is.EqualTo(lowerCase.GetHashCode()));
        }

        [Test]
        public void NotEqualDifferentValuesOrNull()
        {
            // Arrange
            UserId first = UserId.New("0x1111").Unwrap();
            UserId second = UserId.New("0x2222").Unwrap();

            // Assert
            Assert.That(first.Equals(second), Is.False);
            Assert.That(first == null, Is.False);
            Assert.That(null != first, Is.True);
        }

        [Test]
        public void WorkAsCaseInsensitiveDictionaryKey()
        {
            // Arrange
            var dictionary = new Dictionary<UserId, int>
            {
                [UserId.New("0xABCDEF").Unwrap()] = 1,
            };

            // Act
            bool found = dictionary.TryGetValue(UserId.New("0xabcdef").Unwrap(), out int value);

            // Assert
            Assert.That(found, Is.True);
            Assert.That(value, Is.EqualTo(1));
        }

        [Test]
        public void ConvertImplicitlyToItsRawString()
        {
            // Act
            string raw = UserId.New("0xAbC").Unwrap();

            // Assert
            Assert.That(raw, Is.EqualTo("0xAbC"));
        }
    }
}
