#if ALTTESTER
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.SyntheticInput.AltTester
{
    /// <summary>
    ///     Start/poll bridge for the AltTester probes: <c>CallStaticMethod</c> returns synchronously, so a multi-frame
    ///     gesture cannot be awaited inside one call. Failures, timeouts included, come back as error payloads and
    ///     never as exceptions.
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

        /// <summary>Main thread only: the slot ring is not thread-safe.</summary>
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
