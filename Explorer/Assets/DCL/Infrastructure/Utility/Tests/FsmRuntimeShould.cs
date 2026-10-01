using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using Utility.Fsm;

namespace Utility.Tests
{
    public class FsmRuntimeShould
    {
        private enum MsgKind : byte
        {
            Add,
            AddThenEcho,
        }

        private enum CmdKind : byte
        {
            None,
            Echo,
            Observe,
            Throw,
        }

        private readonly struct CounterMsg
        {
            public readonly MsgKind Kind;
            public readonly int Amount;

            public CounterMsg(MsgKind kind, int amount)
            {
                Kind = kind;
                Amount = amount;
            }

            public override string ToString() =>
                $"{Kind}({Amount})";
        }

        private readonly struct CounterCmd
        {
            public readonly CmdKind Kind;
            public readonly int Amount;

            public CounterCmd(CmdKind kind, int amount = 0)
            {
                Kind = kind;
                Amount = amount;
            }

            public override string ToString() =>
                $"{Kind}({Amount})";
        }

        private const string TAG = "CounterFsm";
        private const int THREADS = 8;
        private const int SENDS_PER_THREAD = 1000;

        private static readonly List<CounterMsg> APPLIED = new ();

        private RecordingExecutor executor = null!;
        private FsmRuntime<int, CounterMsg, CounterCmd> runtime = null!;

        [SetUp]
        public void SetUp()
        {
            APPLIED.Clear();
            executor = new RecordingExecutor();
            runtime = new FsmRuntime<int, CounterMsg, CounterCmd>(TAG, 0, Update, executor);
            executor.Runtime = runtime;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ApplyMessagesInArrivalOrder()
        {
            // Arrange
            runtime.Send(new CounterMsg(MsgKind.Add, 1));
            runtime.Send(new CounterMsg(MsgKind.Add, 2));
            runtime.Send(new CounterMsg(MsgKind.Add, 3));

            // Act
            runtime.Drain();

            // Assert
            Assert.That(runtime.ModelSnapshot, Is.EqualTo(6));
            Assert.That(APPLIED.Count, Is.EqualTo(3));
            Assert.That(APPLIED[0].Amount, Is.EqualTo(1));
            Assert.That(APPLIED[1].Amount, Is.EqualTo(2));
            Assert.That(APPLIED[2].Amount, Is.EqualTo(3));
        }

        [Test]
        public void ExposeNewModelBeforeExecutingCommand()
        {
            // Arrange
            runtime.Send(new CounterMsg(MsgKind.Add, 41));

            // Act
            runtime.Drain();

            // Assert
            Assert.That(executor.ObservedModels, Is.EqualTo(new[] { 41 }));
        }

        [Test]
        public void ApplyMessagesSentByExecutorAfterTheQueuedOnesWithoutNesting()
        {
            // Arrange
            runtime.Send(new CounterMsg(MsgKind.AddThenEcho, 5));
            runtime.Send(new CounterMsg(MsgKind.Add, 1));
            executor.DrainReentrantly = true;

            // Act
            runtime.Drain();

            // Assert
            Assert.That(runtime.ModelSnapshot, Is.EqualTo(11));
            Assert.That(APPLIED.Count, Is.EqualTo(3));
            Assert.That(APPLIED[0].Kind, Is.EqualTo(MsgKind.AddThenEcho));
            Assert.That(APPLIED[1].Amount, Is.EqualTo(1));
            Assert.That(APPLIED[2].Kind, Is.EqualTo(MsgKind.Add));
            Assert.That(APPLIED[2].Amount, Is.EqualTo(5));
            Assert.That(executor.MaxExecuteDepth, Is.EqualTo(1));
        }

        [Test]
        public void AcceptSendsFromMultipleThreads()
        {
            // Arrange
            var threads = new Thread[THREADS];

            IMsgInbox<CounterMsg> inbox = runtime;

            for (var i = 0; i < THREADS; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (var j = 0; j < SENDS_PER_THREAD; j++)
                        inbox.Send(new CounterMsg(MsgKind.Add, 1));
                });
            }

            // Act
            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            runtime.Drain();

            // Assert
            Assert.That(runtime.ModelSnapshot, Is.EqualTo(THREADS * SENDS_PER_THREAD));
        }

        [Test]
        public void DoNothingWhenInboxIsEmpty()
        {
            // Act
            runtime.Drain();

            // Assert
            Assert.That(runtime.ModelSnapshot, Is.EqualTo(0));
            Assert.That(executor.Executed.Count, Is.EqualTo(0));
        }

        [Test]
        public void LogEveryMessageBeforeApplyingIt()
        {
            // Arrange
            LogAssert.Expect(LogType.Log, new Regex($@"\[{TAG}\] msg: Add\(7\)"));
            LogAssert.Expect(LogType.Log, new Regex($@"\[{TAG}\] model: 7 cmd: Observe\(0\)"));
            runtime.Send(new CounterMsg(MsgKind.Add, 7));

            // Act
            runtime.Drain();

            // Assert
            Assert.That(runtime.ModelSnapshot, Is.EqualTo(7));
        }

        [Test]
        public void KeepDrainingWhenExecutorThrows()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            executor.ThrowOnObserve = true;
            runtime.Send(new CounterMsg(MsgKind.Add, 1));
            runtime.Send(new CounterMsg(MsgKind.Add, 2));

            // Act
            runtime.Drain();

            // Assert
            Assert.That(runtime.ModelSnapshot, Is.EqualTo(3));
            Assert.That(executor.Executed.Count, Is.EqualTo(2));
        }

        private static (int model, CounterCmd cmd) Update(in int model, in CounterMsg msg)
        {
            APPLIED.Add(msg);

            switch (msg.Kind)
            {
                case MsgKind.Add:
                    return (model + msg.Amount, new CounterCmd(CmdKind.Observe));
                case MsgKind.AddThenEcho:
                    return (model + msg.Amount, new CounterCmd(CmdKind.Echo, msg.Amount));
                default:
                    return (model, new CounterCmd(CmdKind.None));
            }
        }

        private class RecordingExecutor : ICmdExecutor<CounterCmd, CounterMsg>
        {
            public readonly List<CounterCmd> Executed = new ();
            public readonly List<int> ObservedModels = new ();

            public FsmRuntime<int, CounterMsg, CounterCmd> Runtime = null!;
            public bool DrainReentrantly;
            public bool ThrowOnObserve;
            public int MaxExecuteDepth;

            private int depth;

            public void Execute(in CounterCmd cmd, IMsgInbox<CounterMsg> inbox)
            {
                depth++;
                MaxExecuteDepth = Math.Max(MaxExecuteDepth, depth);

                try
                {
                    Executed.Add(cmd);

                    switch (cmd.Kind)
                    {
                        case CmdKind.Echo:
                            inbox.Send(new CounterMsg(MsgKind.Add, cmd.Amount));

                            if (DrainReentrantly)
                                Runtime.Drain();

                            break;
                        case CmdKind.Observe:
                            ObservedModels.Add(Runtime.Model);

                            if (ThrowOnObserve)
                                throw new InvalidOperationException("Executor failure");

                            break;
                    }
                }
                finally { depth--; }
            }
        }
    }
}
