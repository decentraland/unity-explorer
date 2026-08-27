using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    public class OpenInvalidMediaShould : UUAVTestBase
    {
        [UnityTest]
        public IEnumerator RejectGarbageBytes()
        {
            UUAVPlayer player = CreatePlayer(out _);
            player.OpenMedia(UrlFor(Fixtures.Garbage));
            yield return AwaitOpenRejected(player);
        }

        [UnityTest]
        public IEnumerator RejectTruncatedContainer()
        {
            UUAVPlayer player = CreatePlayer(out _);
            player.OpenMedia(UrlFor(Fixtures.Truncated));
            yield return AwaitOpenRejected(player);
        }

        [UnityTest]
        public IEnumerator RejectHttpNotFound()
        {
            UUAVPlayer player = CreatePlayer(out _);
            player.OpenMedia(UrlFor("does_not_exist.mp4"));
            yield return AwaitOpenRejected(player);
        }

        [UnityTest]
        public IEnumerator RejectConnectionRefused()
        {
            UUAVPlayer player = CreatePlayer(out _);
            player.OpenMedia(FixtureServer.ConnectionRefusedUrl());
            yield return AwaitOpenRejected(player);
        }

        [UnityTest]
        public IEnumerator RejectDisallowedProtocol()
        {
            // ftp is outside the runtime's protocol whitelist on every target
            UUAVPlayer player = CreatePlayer(out _);
            player.OpenMedia("ftp://127.0.0.1:1/media.mp4");
            yield return AwaitOpenRejected(player);
        }

        [UnityTest]
        public IEnumerator RecoverAfterFailedOpen()
        {
            // Arrange: drive the player into a rejected open first
            UUAVPlayer player = CreatePlayer(out _);
            player.OpenMedia(UrlFor(Fixtures.Garbage));
            yield return AwaitOpenRejected(player);

            // Act: the same player must accept valid media afterwards
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));

            // Assert
            yield return AwaitClockRunning(player);
        }

        // an invalid open either surfaces asynchronously as Error or is
        // rejected synchronously and never leaves Closed; both are correct.
        // Reaching Ready or Playing is the failure.
        private static IEnumerator AwaitOpenRejected(UUAVPlayer player)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < StateTimeout)
            {
                UUAVState state = player.State;
                if (state == UUAVState.Error)
                {
                    yield break;
                }

                if (state is UUAVState.Ready or UUAVState.Playing)
                {
                    Assert.Fail($"invalid media was accepted\n{Wait.Diagnostics(player)}");
                }

                yield return null;
            }

            Assert.That(
                player.State,
                Is.EqualTo(UUAVState.Closed),
                $"open neither errored nor stayed rejected\n{Wait.Diagnostics(player)}"
            );
        }
    }
}
