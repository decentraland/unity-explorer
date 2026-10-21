using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Chat.Commands.Tests
{
    public class GotoTeleportAnimationShould
    {
        [Test]
        public async Task WaitForDepartureAndKeepOwnershipUntilNavigationFinishes()
        {
            // Arrange
            var animation = new GotoTeleportAnimation();

            // Act
            UniTask<bool> departure = animation.BeginAsync(CancellationToken.None);

            // Assert
            Assert.That(departure.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(await animation.BeginAsync(CancellationToken.None), Is.False);
            animation.Departure!.TrySetResult();
            Assert.That(await departure, Is.True);
            Assert.That(animation.IsRequested, Is.True);
            animation.Finish();
            Assert.That(animation.IsRequested, Is.False);
        }

        [Test]
        public async Task ReleaseOwnershipWhenDepartureIsCanceled()
        {
            // Arrange
            var animation = new GotoTeleportAnimation();
            using var cts = new CancellationTokenSource();
            UniTask<bool> departure = animation.BeginAsync(cts.Token);

            // Act
            cts.Cancel();
            (bool canceled, _) = await departure.SuppressCancellationThrow();

            // Assert
            Assert.That(canceled, Is.True);
            Assert.That(animation.IsRequested, Is.False);
        }

        [Test]
        public void IgnoreAnAlreadyCanceledRequest()
        {
            // Arrange
            var animation = new GotoTeleportAnimation();

            // Act
            bool started = animation.BeginAsync(new CancellationToken(true)).GetAwaiter().GetResult();

            // Assert
            Assert.That(started, Is.False);
            Assert.That(animation.IsRequested, Is.False);
            Assert.That(animation.Departure, Is.Null);
        }

        [Test]
        public async Task ReleaseOwnershipAfterPresentationFails()
        {
            // Arrange
            var animation = new GotoTeleportAnimation();
            UniTask<bool> departure = animation.BeginAsync(CancellationToken.None);

            // Act
            animation.Departure!.TrySetException(new InvalidOperationException("Presentation failed"));
            try
            {
                await departure;
                Assert.Fail("The presentation failure should propagate.");
            }
            catch (InvalidOperationException) { }

            // Assert
            Assert.That(animation.IsRequested, Is.False);
        }
    }
}
