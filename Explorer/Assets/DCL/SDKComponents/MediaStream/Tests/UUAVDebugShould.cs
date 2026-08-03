using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UUAV;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the recent-messages ring the "Media Player" debug tab renders.
    ///     The ring is static process state, so every test floods it past capacity
    ///     to make the outcome independent of ordering.
    /// </summary>
    public class UUAVDebugShould
    {
        private const int CAPACITY = 10;

        [Test]
        public void KeepTheLastMessagesInOrder()
        {
            for (var i = 0; i < 15; i++)
                UUAVDebug.Push($"m{i}");

            List<string> messages = new ();
            UUAVDebug.CopyRecentMessages(messages);

            Assert.That(messages, Has.Count.EqualTo(CAPACITY));

            for (var i = 0; i < CAPACITY; i++)
                Assert.That(messages[i], Is.EqualTo($"m{i + 15 - CAPACITY}"));
        }

        [Test]
        public void SurviveConcurrentPushes()
        {
            var tasks = new Task[4];

            for (var t = 0; t < tasks.Length; t++)
            {
                int worker = t;

                tasks[t] = Task.Run(() =>
                {
                    for (var i = 0; i < 100; i++)
                        UUAVDebug.Push($"w{worker}-{i}");
                });
            }

            Task.WaitAll(tasks);

            List<string> messages = new ();
            UUAVDebug.CopyRecentMessages(messages);

            Assert.That(messages, Has.Count.EqualTo(CAPACITY));
            Assert.That(messages, Has.None.Null);
        }
    }
}
