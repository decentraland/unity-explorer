#nullable enable

using System;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Bounded, main-thread-only, min-by-timestamp queue of <see cref="NetworkMovementMessage" /> used as the
    ///     per-peer movement inbox. Replaces the general-purpose
    ///     <c>SimplePriorityQueue&lt;NetworkMovementMessage, double&gt;</c> that allocated two heap objects
    ///     (a node + a per-item <c>List&lt;SimpleNode&gt;</c>) and hashed the whole 15-field struct twice on every
    ///     Enqueue — dead weight on a path hit at ~10 Hz per remote player.
    ///     <para>
    ///         Ordering and overflow semantics are byte-for-byte identical to the old inbox: entries are kept sorted
    ///         ascending by <see cref="NetworkMovementMessage.timestamp" /> (ties broken FIFO, i.e. stable), and
    ///         <see cref="Enqueue" /> trims the minimum (oldest-timestamp) entries until at most
    ///         <see cref="maxMessages" /> remain <b>before</b> inserting — exactly the old
    ///         <c>while (Count &gt; MAX_MESSAGES) Dequeue();</c> then <c>Enqueue()</c>. The physical high-water mark is
    ///         therefore <c>maxMessages + 1</c>, which is the backing array's capacity.
    ///     </para>
    ///     <para>
    ///         This type is a reference type on purpose: <c>RemotePlayerMovementComponent</c> is a struct copied by
    ///         value out of Arch on every <c>World.TryGet</c>, and <c>MovementInbox.EnqueueToEntity</c> mutates one of
    ///         those copies — the shared queue reference is what makes that write visible. Cross-thread traffic is
    ///         already funnelled through <c>MovementInbox</c>'s <c>DCLConcurrentQueue</c> and drained on the main
    ///         thread, so this queue needs no internal locking.
    ///     </para>
    /// </summary>
    public sealed class BoundedNetworkMessageQueue
    {
        private readonly int maxMessages;
        private readonly NetworkMovementMessage[] items;
        private int count;

        /// <summary>Number of messages currently held. O(1).</summary>
        public int Count => count;

        /// <summary>
        ///     Head of the queue (minimum timestamp), without removing it. Throws when empty — mirrors the old
        ///     <c>SimplePriorityQueue.First</c>. O(1).
        /// </summary>
        public NetworkMovementMessage First
        {
            get
            {
                if (count <= 0) throw new InvalidOperationException("Cannot call .First on an empty queue");
                return items[0];
            }
        }

        public BoundedNetworkMessageQueue(int maxMessages)
        {
            this.maxMessages = maxMessages;

            items = new NetworkMovementMessage[maxMessages + 1];
            count = 0;
        }

        /// <summary>
        ///     Insert <paramref name="message" /> keyed by its timestamp, dropping the oldest entries first if the
        ///     logical cap is exceeded. Allocation-free after construction. O(maxMessages) worst case.
        /// </summary>
        public void Enqueue(NetworkMovementMessage message)
        {
            while (count > maxMessages)
                DropFront();

            int i = count;

            while (i > 0 && items[i - 1].timestamp > message.timestamp)
            {
                items[i] = items[i - 1];
                i--;
            }

            items[i] = message;
            count++;
        }

        /// <summary>
        ///     Remove and return the head (minimum timestamp). Throws when empty — mirrors the old
        ///     <c>SimplePriorityQueue.Dequeue</c>. O(maxMessages).
        /// </summary>
        public NetworkMovementMessage Dequeue()
        {
            if (count <= 0) throw new InvalidOperationException("Cannot call Dequeue() on an empty queue");
            return DropFront();
        }

        public void Clear()
        {
            Array.Clear(items, 0, count);
            count = 0;
        }

        private NetworkMovementMessage DropFront()
        {
            NetworkMovementMessage head = items[0];
            count--;
            Array.Copy(items, 1, items, 0, count);
            items[count] = default(NetworkMovementMessage);
            return head;
        }
    }
}
