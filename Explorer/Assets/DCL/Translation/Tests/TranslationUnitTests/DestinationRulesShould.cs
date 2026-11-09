using DCL.UI.InputFieldFormatting;
using NUnit.Framework;

namespace DCL.Translation
{
    [TestFixture]
    public class DestinationRulesShould
    {
        [TestCase(0, 0, true)]
        [TestCase(-150, -150, true)]
        [TestCase(150, 150, true)]
        [TestCase(162, 158, true)]
        [TestCase(163, 150, true)]
        [TestCase(-152, 0, false)]
        [TestCase(0, -152, false)]
        [TestCase(0, 151, false)]
        [TestCase(200, 0, false)]
        [TestCase(1000, -500, false)]
        public void TakeAParcelOnlyInsideGenesisCity(int x, int y, bool expected)
        {
            Assert.AreEqual(expected, DestinationRules.IsParcel(x, y));
        }

        [TestCase("mana.dcl.eth", true)]
        [TestCase("Mana1.DCL.eth", false)]
        [TestCase("mana", false)]
        [TestCase("your.world.eth", false)]
        [TestCase("my-world.dcl.eth", false)]
        [TestCase("mana.dcl.eth.", false)]
        [TestCase(" mana.dcl.eth", false)]
        [TestCase("", false)]
        public void TakeAWorldOnlyAsOneWordWithItsDomain(string name, bool expected)
        {
            Assert.AreEqual(expected, DestinationRules.IsWorld(name));
        }

        [TestCase("eax", true)]
        [TestCase("Eax2", true)]
        [TestCase("3", true)]
        [TestCase("eax.dcl.eth", false)]
        [TestCase("my-world", false)]
        [TestCase("foo bar", false)]
        [TestCase("", false)]
        public void TakeAWorldShortNameOnlyAsOneWordWithNoSymbols(string name, bool expected)
        {
            Assert.AreEqual(expected, DestinationRules.IsWorldShortName(name));
        }
    }
}
