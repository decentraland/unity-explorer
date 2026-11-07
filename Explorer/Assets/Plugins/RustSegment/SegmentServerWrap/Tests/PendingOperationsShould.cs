using NUnit.Framework;

namespace Plugins.RustSegment.SegmentServerWrap.Tests
{
    public class PendingOperationsShould
    {
        private PendingOperations<string> pending = null!;

        [SetUp]
        public void SetUp()
        {
            pending = new PendingOperations<string>();
        }

        [Test]
        public void HandBackTheEntryWhenTheOperationCompletesAfterRegistration()
        {
            Assert.IsTrue(pending.TryRegister(7, "strings"));

            Assert.IsTrue(pending.TryComplete(7, out string? entry));
            Assert.AreEqual("strings", entry);
        }

        [Test]
        public void RefuseRegistrationWhenTheOperationAlreadyCompleted()
        {
            Assert.IsFalse(pending.TryComplete(7, out _));

            Assert.IsFalse(pending.TryRegister(7, "strings"));
        }

        [Test]
        public void ForgetAnEarlyCompletionOnceItsRegistrationWasRefused()
        {
            pending.TryComplete(7, out _);
            pending.TryRegister(7, "strings");

            Assert.IsTrue(pending.TryRegister(7, "next"));
            Assert.IsTrue(pending.TryComplete(7, out string? entry));
            Assert.AreEqual("next", entry);
        }

        [Test]
        public void KeepEarlyCompletionsPerOperation()
        {
            pending.TryComplete(1, out _);

            Assert.IsTrue(pending.TryRegister(2, "strings"));
        }
    }
}
