using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Plugins.RustSegment.SegmentServerWrap
{
    /// <summary>
    ///     Pairs each native operation with its completion in whichever order the two arrive, so its entry is handed back exactly once.
    ///     Not synchronised: the owner guards it with its lock.
    /// </summary>
    internal sealed class PendingOperations<T>
    {
        private readonly Dictionary<ulong, T> registered = new ();
        private readonly HashSet<ulong> completedBeforeRegistration = new ();

        /// <returns>False when the operation already completed: the caller releases the entry itself</returns>
        public bool TryRegister(ulong operationId, T entry)
        {
            if (completedBeforeRegistration.Remove(operationId))
                return false;

            registered[operationId] = entry;
            return true;
        }

        /// <returns>False when the operation is not registered yet: its registration is refused instead</returns>
        public bool TryComplete(ulong operationId, [MaybeNullWhen(false)] out T entry)
        {
            if (registered.Remove(operationId, out entry))
                return true;

            completedBeforeRegistration.Add(operationId);
            return false;
        }
    }
}
