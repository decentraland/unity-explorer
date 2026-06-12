using DCL.Utilities;
using NUnit.Framework;

namespace Utility.Tests
{
    public class DisposableSubscriptionShould
    {
        [Test]
        public void NotThrowOnDisposeWhenDefault()
        {
            ReactivePropertyExtensions.DisposableSubscription<bool> subscription = default;

            Assert.DoesNotThrow(() => subscription.Dispose());
        }

        [Test]
        public void UnsubscribeOnDispose()
        {
            var property = new ReactiveProperty<bool>(false);
            var receivedCount = 0;

            ReactivePropertyExtensions.DisposableSubscription<bool> subscription = property.Subscribe(_ => receivedCount++);

            property.UpdateValue(true);
            Assert.That(receivedCount, Is.EqualTo(1));

            subscription.Dispose();

            property.UpdateValue(false);
            Assert.That(receivedCount, Is.EqualTo(1));
        }
    }
}
