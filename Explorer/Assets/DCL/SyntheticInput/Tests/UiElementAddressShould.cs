using DCL.SyntheticInput.UiSimulation;
using NUnit.Framework;
using System;

namespace DCL.SyntheticInput.Tests
{
    public class UiElementAddressShould
    {
        [Test]
        public void StripTheCloneSuffix()
        {
            Assert.That(UiElementAddress.NormalizeName("SidebarUI(Clone)").ToString(), Is.EqualTo("SidebarUI"));
            Assert.That(UiElementAddress.NormalizeName("SidebarUI").ToString(), Is.EqualTo("SidebarUI"));
            Assert.That(UiElementAddress.NormalizeName("(Clone)").ToString(), Is.EqualTo(""));
        }

        [TestCase("Item", "Item", 0)]
        [TestCase("Item[2]", "Item", 2)]
        [TestCase("Item[12]", "Item", 12)]
        [TestCase("Weird[]", "Weird[]", 0)]
        [TestCase("[3]", "[3]", 0)]
        [TestCase("Data[x]", "Data[x]", 0)]
        public void ParseSiblingIndexSegments(string segment, string expectedName, int expectedIndex)
        {
            UiElementAddress.ParseSegment(segment.AsSpan(), out ReadOnlySpan<char> name, out int siblingIndex);

            Assert.That(name.ToString(), Is.EqualTo(expectedName));
            Assert.That(siblingIndex, Is.EqualTo(expectedIndex));
        }

        [Test]
        public void FormatItselfByAddressForm()
        {
            Assert.That(UiElementAddress.Sdk(42).ToString(), Is.EqualTo("sdk:crdt=42"));
            Assert.That(UiElementAddress.UguiPath("A/B").ToString(), Is.EqualTo("ugui:path=A/B"));
            Assert.That(UiElementAddress.UguiInstance(7).ToString(), Is.EqualTo("ugui:id=7"));
            Assert.That(UiElementAddress.UguiAltId("guid").ToString(), Is.EqualTo("ugui:altId=guid"));
        }
    }
}
