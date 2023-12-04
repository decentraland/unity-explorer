﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Utility.PriorityQueue
{
    /// <summary>
    ///     A copy of FastPriorityQueue which is also stable - that is, when two nodes are enqueued with the same priority, they
    ///     are always dequeued in the same order.
    ///     See https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp/wiki/Getting-Started for more information
    /// </summary>
    /// <typeparam name="T">The values in the queue.  Must extend the StablePriorityQueueNode class</typeparam>
    public sealed class StablePriorityQueue<T> : IFixedSizePriorityQueue<T, float>
        where T: StablePriorityQueueNode
    {
        private T[] nodes;
        private long numNodesEverEnqueued;

        /// <summary>
        ///     Instantiate a new Priority Queue
        /// </summary>
        /// <param name="maxNodes">The max nodes ever allowed to be enqueued (going over this will cause undefined behavior)</param>
        public StablePriorityQueue(int maxNodes)
        {
#if DEBUG
            if (maxNodes <= 0) { throw new InvalidOperationException("New queue size cannot be smaller than 1"); }
#endif

            Count = 0;
            nodes = new T[maxNodes + 1];
            numNodesEverEnqueued = 0;
        }

        /// <summary>
        ///     Returns the number of nodes in the queue.
        ///     O(1)
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        ///     Returns the maximum number of items that can be enqueued at once in this queue.  Once you hit this number (ie. once Count == MaxSize),
        ///     attempting to enqueue another item will cause undefined behavior.  O(1)
        /// </summary>
        public int MaxSize => nodes.Length - 1;

        /// <summary>
        ///     Removes every node from the queue.
        ///     O(n) (So, don't do this often!)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Clear()
        {
            Array.Clear(nodes, 1, Count);
            Count = 0;
        }

        /// <summary>
        ///     Returns (in O(1)!) whether the given node is in the queue.
        ///     If node is or has been previously added to another queue, the result is undefined unless oldQueue.ResetNode(node) has been called
        ///     O(1)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public bool Contains(T node)
        {
#if DEBUG
            if (node == null) { throw new ArgumentNullException("node"); }

            if (node.Queue != null && !Equals(node.Queue)) { throw new InvalidOperationException("node.Contains was called on a node from another queue.  Please call originalQueue.ResetNode() first"); }

            if (node.QueueIndex < 0 || node.QueueIndex >= nodes.Length) { throw new InvalidOperationException("node.QueueIndex has been corrupted. Did you change it manually?"); }
#endif

            return nodes[node.QueueIndex] == node;
        }

        /// <summary>
        ///     Enqueue a node to the priority queue.  Lower values are placed in front. Ties are broken by first-in-first-out.
        ///     If the queue is full, the result is undefined.
        ///     If the node is already enqueued, the result is undefined.
        ///     If node is or has been previously added to another queue, the result is undefined unless oldQueue.ResetNode(node) has been called
        ///     O(log n)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Enqueue(T node, float priority)
        {
#if DEBUG
            if (node == null) { throw new ArgumentNullException("node"); }

            if (Count >= nodes.Length - 1) { throw new InvalidOperationException("Queue is full - node cannot be added: " + node); }

            if (node.Queue != null && !Equals(node.Queue)) { throw new InvalidOperationException("node.Enqueue was called on a node from another queue.  Please call originalQueue.ResetNode() first"); }

            if (Contains(node)) { throw new InvalidOperationException("Node is already enqueued: " + node); }

            node.Queue = this;
#endif

            node.Priority = priority;
            Count++;
            nodes[Count] = node;
            node.QueueIndex = Count;
            node.InsertionIndex = numNodesEverEnqueued++;
            CascadeUp(node);
        }

        //Performance appears to be slightly better when this is NOT inlined o_O
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void CascadeUp(T node)
        {
            //aka Heapify-up
            int parent;

            if (node.QueueIndex > 1)
            {
                parent = node.QueueIndex >> 1;
                T parentNode = nodes[parent];

                if (HasHigherPriority(parentNode, node))
                    return;

                //Node has lower priority value, so move parent down the heap to make room
                nodes[node.QueueIndex] = parentNode;
                parentNode.QueueIndex = node.QueueIndex;

                node.QueueIndex = parent;
            }
            else { return; }

            while (parent > 1)
            {
                parent >>= 1;
                T parentNode = nodes[parent];

                if (HasHigherPriority(parentNode, node))
                    break;

                //Node has lower priority value, so move parent down the heap to make room
                nodes[node.QueueIndex] = parentNode;
                parentNode.QueueIndex = node.QueueIndex;

                node.QueueIndex = parent;
            }

            nodes[node.QueueIndex] = node;
        }

#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void CascadeDown(T node)
        {
            //aka Heapify-down
            int finalQueueIndex = node.QueueIndex;
            int childLeftIndex = 2 * finalQueueIndex;

            // If leaf node, we're done
            if (childLeftIndex > Count) { return; }

            // Check if the left-child is higher-priority than the current node
            int childRightIndex = childLeftIndex + 1;
            T childLeft = nodes[childLeftIndex];

            if (HasHigherPriority(childLeft, node))
            {
                // Check if there is a right child. If not, swap and finish.
                if (childRightIndex > Count)
                {
                    node.QueueIndex = childLeftIndex;
                    childLeft.QueueIndex = finalQueueIndex;
                    nodes[finalQueueIndex] = childLeft;
                    nodes[childLeftIndex] = node;
                    return;
                }

                // Check if the left-child is higher-priority than the right-child
                T childRight = nodes[childRightIndex];

                if (HasHigherPriority(childLeft, childRight))
                {
                    // left is highest, move it up and continue
                    childLeft.QueueIndex = finalQueueIndex;
                    nodes[finalQueueIndex] = childLeft;
                    finalQueueIndex = childLeftIndex;
                }
                else
                {
                    // right is even higher, move it up and continue
                    childRight.QueueIndex = finalQueueIndex;
                    nodes[finalQueueIndex] = childRight;
                    finalQueueIndex = childRightIndex;
                }
            }

            // Not swapping with left-child, does right-child exist?
            else if (childRightIndex > Count) { return; }
            else
            {
                // Check if the right-child is higher-priority than the current node
                T childRight = nodes[childRightIndex];

                if (HasHigherPriority(childRight, node))
                {
                    childRight.QueueIndex = finalQueueIndex;
                    nodes[finalQueueIndex] = childRight;
                    finalQueueIndex = childRightIndex;
                }

                // Neither child is higher-priority than current, so finish and stop.
                else { return; }
            }

            while (true)
            {
                childLeftIndex = 2 * finalQueueIndex;

                // If leaf node, we're done
                if (childLeftIndex > Count)
                {
                    node.QueueIndex = finalQueueIndex;
                    nodes[finalQueueIndex] = node;
                    break;
                }

                // Check if the left-child is higher-priority than the current node
                childRightIndex = childLeftIndex + 1;
                childLeft = nodes[childLeftIndex];

                if (HasHigherPriority(childLeft, node))
                {
                    // Check if there is a right child. If not, swap and finish.
                    if (childRightIndex > Count)
                    {
                        node.QueueIndex = childLeftIndex;
                        childLeft.QueueIndex = finalQueueIndex;
                        nodes[finalQueueIndex] = childLeft;
                        nodes[childLeftIndex] = node;
                        break;
                    }

                    // Check if the left-child is higher-priority than the right-child
                    T childRight = nodes[childRightIndex];

                    if (HasHigherPriority(childLeft, childRight))
                    {
                        // left is highest, move it up and continue
                        childLeft.QueueIndex = finalQueueIndex;
                        nodes[finalQueueIndex] = childLeft;
                        finalQueueIndex = childLeftIndex;
                    }
                    else
                    {
                        // right is even higher, move it up and continue
                        childRight.QueueIndex = finalQueueIndex;
                        nodes[finalQueueIndex] = childRight;
                        finalQueueIndex = childRightIndex;
                    }
                }

                // Not swapping with left-child, does right-child exist?
                else if (childRightIndex > Count)
                {
                    node.QueueIndex = finalQueueIndex;
                    nodes[finalQueueIndex] = node;
                    break;
                }
                else
                {
                    // Check if the right-child is higher-priority than the current node
                    T childRight = nodes[childRightIndex];

                    if (HasHigherPriority(childRight, node))
                    {
                        childRight.QueueIndex = finalQueueIndex;
                        nodes[finalQueueIndex] = childRight;
                        finalQueueIndex = childRightIndex;
                    }

                    // Neither child is higher-priority than current, so finish and stop.
                    else
                    {
                        node.QueueIndex = finalQueueIndex;
                        nodes[finalQueueIndex] = node;
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     Returns true if 'higher' has higher priority than 'lower', false otherwise.
        ///     Note that calling HasHigherPriority(node, node) (ie. both arguments the same node) will return false
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private bool HasHigherPriority(T higher, T lower)
        {
            return higher.Priority < lower.Priority ||
                   (higher.Priority == lower.Priority && higher.InsertionIndex < lower.InsertionIndex);
        }

        /// <summary>
        ///     Removes the head of the queue (node with minimum priority; ties are broken by order of insertion), and returns it.
        ///     If queue is empty, result is undefined
        ///     O(log n)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public T Dequeue()
        {
#if DEBUG
            if (Count <= 0) { throw new InvalidOperationException("Cannot call Dequeue() on an empty queue"); }

            if (!IsValidQueue())
            {
                throw new InvalidOperationException("Queue has been corrupted (Did you update a node priority manually instead of calling UpdatePriority()?" +
                                                    "Or add the same node to two different queues?)");
            }
#endif

            T returnMe = nodes[1];

            //If the node is already the last node, we can remove it immediately
            if (Count == 1)
            {
                nodes[1] = null;
                Count = 0;
                return returnMe;
            }

            //Swap the node with the last node
            T formerLastNode = nodes[Count];
            nodes[1] = formerLastNode;
            formerLastNode.QueueIndex = 1;
            nodes[Count] = null;
            Count--;

            //Now bubble formerLastNode (which is no longer the last node) down
            CascadeDown(formerLastNode);
            return returnMe;
        }

        /// <summary>
        ///     Resize the queue so it can accept more nodes.  All currently enqueued nodes are remain.
        ///     Attempting to decrease the queue size to a size too small to hold the existing nodes results in undefined behavior
        ///     O(n)
        /// </summary>
        public void Resize(int maxNodes)
        {
#if DEBUG
            if (maxNodes <= 0) { throw new InvalidOperationException("Queue size cannot be smaller than 1"); }

            if (maxNodes < Count) { throw new InvalidOperationException("Called Resize(" + maxNodes + "), but current queue contains " + Count + " nodes"); }
#endif

            var newArray = new T[maxNodes + 1];
            int highestIndexToCopy = Math.Min(maxNodes, Count);
            Array.Copy(nodes, newArray, highestIndexToCopy + 1);
            nodes = newArray;
        }

        /// <summary>
        ///     Returns the head of the queue, without removing it (use Dequeue() for that).
        ///     If the queue is empty, behavior is undefined.
        ///     O(1)
        /// </summary>
        public T First
        {
            get
            {
#if DEBUG
                if (Count <= 0) { throw new InvalidOperationException("Cannot call .First on an empty queue"); }
#endif

                return nodes[1];
            }
        }

        /// <summary>
        ///     This method must be called on a node every time its priority changes while it is in the queue.
        ///     <b>Forgetting to call this method will result in a corrupted queue!</b>
        ///     Calling this method on a node not in the queue results in undefined behavior
        ///     O(log n)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void UpdatePriority(T node, float priority)
        {
#if DEBUG
            if (node == null) { throw new ArgumentNullException("node"); }

            if (node.Queue != null && !Equals(node.Queue)) { throw new InvalidOperationException("node.UpdatePriority was called on a node from another queue"); }

            if (!Contains(node)) { throw new InvalidOperationException("Cannot call UpdatePriority() on a node which is not enqueued: " + node); }
#endif

            node.Priority = priority;
            OnNodeUpdated(node);
        }

#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void OnNodeUpdated(T node)
        {
            //Bubble the updated node up or down as appropriate
            int parentIndex = node.QueueIndex >> 1;

            if (parentIndex > 0 && HasHigherPriority(node, nodes[parentIndex])) { CascadeUp(node); }
            else
            {
                //Note that CascadeDown will be called if parentNode == node (that is, node is the root)
                CascadeDown(node);
            }
        }

        /// <summary>
        ///     Removes a node from the queue.  The node does not need to be the head of the queue.
        ///     If the node is not in the queue, the result is undefined.  If unsure, check Contains() first
        ///     O(log n)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Remove(T node)
        {
#if DEBUG
            if (node == null) { throw new ArgumentNullException("node"); }

            if (node.Queue != null && !Equals(node.Queue)) { throw new InvalidOperationException("node.Remove was called on a node from another queue"); }

            if (!Contains(node)) { throw new InvalidOperationException("Cannot call Remove() on a node which is not enqueued: " + node); }
#endif

            //If the node is already the last node, we can remove it immediately
            if (node.QueueIndex == Count)
            {
                nodes[Count] = null;
                Count--;
                return;
            }

            //Swap the node with the last node
            T formerLastNode = nodes[Count];
            nodes[node.QueueIndex] = formerLastNode;
            formerLastNode.QueueIndex = node.QueueIndex;
            nodes[Count] = null;
            Count--;

            //Now bubble formerLastNode (which is no longer the last node) up or down as appropriate
            OnNodeUpdated(formerLastNode);
        }

        /// <summary>
        ///     By default, nodes that have been previously added to one queue cannot be added to another queue.
        ///     If you need to do this, please call originalQueue.ResetNode(node) before attempting to add it in the new queue
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void ResetNode(T node)
        {
#if DEBUG
            if (node == null) { throw new ArgumentNullException("node"); }

            if (node.Queue != null && !Equals(node.Queue)) { throw new InvalidOperationException("node.ResetNode was called on a node from another queue"); }

            if (Contains(node)) { throw new InvalidOperationException("node.ResetNode was called on a node that is still in the queue"); }

            node.Queue = null;
#endif

            node.QueueIndex = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
#if NET_VERSION_4_5 // ArraySegment does not implement IEnumerable before 4.5
            IEnumerable<T> e = new ArraySegment<T>(_nodes, 1, _numNodes);
            return e.GetEnumerator();
#else
            for (var i = 1; i <= Count; i++)
                yield return nodes[i];
#endif
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        /// <summary>
        ///     <b>Should not be called in production code.</b>
        ///     Checks to make sure the queue is still in a valid state.  Used for testing/debugging the queue.
        /// </summary>
        public bool IsValidQueue()
        {
            for (var i = 1; i < nodes.Length; i++)
            {
                if (nodes[i] != null)
                {
                    int childLeftIndex = 2 * i;

                    if (childLeftIndex < nodes.Length && nodes[childLeftIndex] != null && HasHigherPriority(nodes[childLeftIndex], nodes[i]))
                        return false;

                    int childRightIndex = childLeftIndex + 1;

                    if (childRightIndex < nodes.Length && nodes[childRightIndex] != null && HasHigherPriority(nodes[childRightIndex], nodes[i]))
                        return false;
                }
            }

            return true;
        }
    }
}
