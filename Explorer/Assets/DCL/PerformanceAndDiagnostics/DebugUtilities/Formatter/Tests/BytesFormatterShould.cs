using NUnit.Framework;

namespace DCL.DebugUtilities.Formatter.Tests
{
    public class BytesFormatterShould
    {

        public void Normalize()
        {
            Assert.That(BytesFormatter.Normalize(1474560, false), Is.EqualTo("1.41 MB"));
        }
    }
}
