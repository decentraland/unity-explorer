#if ALTTESTER
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.SyntheticInput.AltTester
{
    /// <summary>
    ///     Start/poll bridge for the AltTester probes: CallStaticMethod is synchronous on the main thread, so a
    ///     multi-frame gesture cannot be awaited inside one call — the test starts it, gets an operation id and
    ///     polls. Never throws towards the test: failures (the operation's own timeout included) come back as
    ///     error payloads. A small ring holds the most recent operations; an evicted id polls as an error.
    /// </summary>
    internal static class AltOperationRegistry
    {
        private const int CAPACITY = 32;

        private static readonly Slot?[] SLOTS = new Slot?[CAPACITY];
        private static int nextId;

        private sealed class Slot
        {
            public int Id;
            public bool Done;
            public string? PayloadJson;
        }

        /// <summary>Registers the operation and returns the id to poll. Main thread only (CallStaticMethod guarantees it).</summary>
        public static int Start(UniTask<string> operation)
        {
            int id = ++nextId;
            var slot = new Slot { Id = id };
            SLOTS[id % CAPACITY] = slot;

            AwaitAsync(operation, slot).Forget();
            return id;
        }

        public static string PollJson(int operationId)
        {
            Slot? slot = SLOTS[operationId % CAPACITY];

            if (slot == null || slot.Id != operationId)
                return new JObject { ["done"] = true, ["ok"] = false, ["error"] = "unknown or evicted operation id (poll sooner, or start fewer concurrent operations)" }.ToString();

            if (!slot.Done)
                return new JObject { ["done"] = false }.ToString();

            return new JObject { ["done"] = true, ["result"] = new JRaw(slot.PayloadJson!) }.ToString();
        }

        private static async UniTaskVoid AwaitAsync(UniTask<string> operation, Slot slot)
        {
            string payload;

            try
            {
                payload = await operation;
            }
            catch (OperationCanceledException)
            {
                payload = ErrorPayload("the operation was cancelled");
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.SYNTHETIC_INPUT);
                payload = ErrorPayload(e.Message);
            }

            slot.PayloadJson = payload;
            slot.Done = true;
        }

        internal static string ErrorPayload(string error) =>
            new JObject { ["ok"] = false, ["error"] = error }.ToString();
    }
}
#endif
