using DCL.ECSComponents;
using NUnit.Framework;
using System;

namespace DCL.SyntheticInput.Tests
{
    public class InputActionNamesShould
    {
        [TestCase(InputAction.IaPointer, "pointer")]
        [TestCase(InputAction.IaPrimary, "primary")]
        [TestCase(InputAction.IaAction3, "action_3")]
        [TestCase(InputAction.IaAny, "any")]
        [TestCase(InputAction.IaModifier, "modifier")]
        public void SpellTheMemberWithoutItsPrefixInSnakeCase(InputAction action, string expected)
        {
            Assert.That(InputActionNames.ToWire(action), Is.EqualTo(expected));
        }

        [Test]
        public void RoundTripEveryMember()
        {
            foreach (InputAction action in (InputAction[])Enum.GetValues(typeof(InputAction)))
            {
                string wire = InputActionNames.ToWire(action);

                Assert.That(InputActionNames.TryParse(wire, out InputAction parsed), Is.True, wire);
                Assert.That(parsed, Is.EqualTo(action), wire);
            }
        }

        [TestCase("ACTION_3")]
        [TestCase("action3")]
        [TestCase("Action 3")]
        public void ParseIgnoringCaseUnderscoresAndSpaces(string value)
        {
            Assert.That(InputActionNames.TryParse(value, out InputAction action), Is.True);
            Assert.That(action, Is.EqualTo(InputAction.IaAction3));
        }

        [Test]
        public void RefuseAnUnknownName()
        {
            Assert.That(InputActionNames.TryParse("IA_POINTER", out _), Is.False, "the prefix is not part of the wire name");
            Assert.That(InputActionNames.TryParse("", out _), Is.False);
        }
    }
}
