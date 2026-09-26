using DCL.Utility.Types;
using NUnit.Framework;
using System;

namespace Utility.Tests
{
    public class OptionShould
    {
        [Test]
        public void UnwrapTheValueWhenPresent()
        {
            // Arrange
            Option<string> option = Option<string>.Some("value");

            // Act
            string unwrapped = option.Unwrap();

            // Assert
            Assert.That(unwrapped, Is.EqualTo("value"));
        }

        [Test]
        public void ThrowOnUnwrapWhenEmpty()
        {
            // Arrange
            Option<string> option = Option<string>.None;

            // Assert
            Assert.Throws<InvalidOperationException>(() => option.Unwrap());
        }
    }
}
