using Arch.Core;
using CRDT;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITransformRelationLinkedData
    {
        public class Node
        {
            internal static readonly ObjectPool<Node> POOL = new (() => new Node(), actionOnRelease: x => x.Reset(),
                defaultCapacity: PoolConstants.SCENES_COUNT * 100, maxSize: PoolConstants.SCENES_MAX_CAPACITY * 100);

            public CRDTEntity EntityId { get; private set; }
            public CRDTEntity RightOf { get; set; }
            public Node? Next { get; set; }
            public Node? Previous { get; set; }

            /// <summary>
            ///     Position in the parent's AddChild sequence; orders siblings that the rightOf chain leaves unordered.
            /// </summary>
            internal int insertionIndex;

            internal bool visited;

            private Node()
            {
            }

            private void Reset()
            {
                EntityId = 0;
                RightOf = 0;
                Next = null;
                Previous = null;
                insertionIndex = 0;
                visited = false;
            }

            public void Setup(CRDTEntity entityId)
            {
                EntityId = entityId;
            }

            public override string ToString() =>
                $"{EntityId.ToString()}, Next: {Next?.EntityId.ToString()}, Previous: {Previous?.EntityId.ToString()}";
        }

        private const int CHILDREN_DEFAULT_CAPACITY = 10;

        /// <summary>
        ///     First sibling of the chain produced by the last <see cref="RebuildLinkedList" />; null once the children change.
        /// </summary>
        internal Node? head;

        private Dictionary<CRDTEntity, Node>? nodes;
        private Dictionary<CRDTEntity, Node>? reverseRightOf; // key is the rightOf target, used for O(n) rebuild
        private List<Node>? chainStarts; // scratch list reused by RebuildLinkedList
        private int insertionCounter;

        internal Entity parent;

        internal CRDTEntity rightOf;

        /// <summary>
        /// Indicates that resorting is required
        /// </summary>
        internal bool layoutIsDirty;

        internal bool ContainsNode(CRDTEntity entity) =>
            nodes != null && nodes.ContainsKey(entity);

        public void AddChild(Entity thisEntity, CRDTEntity childEntity, ref UITransformRelationLinkedData childComponent)
        {
            nodes ??= new Dictionary<CRDTEntity, Node>(CHILDREN_DEFAULT_CAPACITY);

            Node newNode = Node.POOL.Get();
            newNode.Setup(childEntity);
            newNode.RightOf = childComponent.rightOf;
            newNode.insertionIndex = insertionCounter++;
            nodes[childEntity] = newNode;

            childComponent.parent = thisEntity;

            head = null;
            layoutIsDirty = true;
        }

        public void RemoveChild(CRDTEntity child, ref UITransformRelationLinkedData childData)
        {
            // Child could be already removed from the nodes list if its entity was deleted
            if (nodes == null || !nodes.Remove(child, out Node? nodeToRemove))
                return;

            childData.parent = Entity.Null;

            head = null;
            layoutIsDirty = true;

            Node.POOL.Release(nodeToRemove);
        }

        internal void UpdateNodeRightOf(CRDTEntity childEntity, CRDTEntity newRightOf)
        {
            if (nodes != null && nodes.TryGetValue(childEntity, out var node))
                node.RightOf = newRightOf;

            layoutIsDirty = true;
        }

        /// <summary>
        ///     Rebuilds the sibling chain from the stored rightOf values so that every node is reachable from <see cref="head" />:
        ///     each rightOf == 0 node starts a chain (in insertion order), and nodes left unreachable by a duplicate,
        ///     dangling or cyclic rightOf are appended at the end.
        /// </summary>
        internal void RebuildLinkedList()
        {
            head = null;

            if (nodes == null || nodes.Count == 0)
                return;

            Dictionary<CRDTEntity, Node> reverse = reverseRightOf ??= new Dictionary<CRDTEntity, Node>(CHILDREN_DEFAULT_CAPACITY);
            reverse.Clear();
            List<Node> starts = chainStarts ??= new List<Node>(CHILDREN_DEFAULT_CAPACITY);
            starts.Clear();

            // Single pass: reset pointers, collect chain starts, build the reverse map (rightOf target → node)
            foreach (KeyValuePair<CRDTEntity, Node> kvp in nodes)
            {
                Node node = kvp.Value;
                node.Next = null;
                node.Previous = null;
                node.visited = false;

                if (node.RightOf.Id == 0)
                {
                    starts.Add(node);
                    continue;
                }

                if (!reverse.TryGetValue(node.RightOf, out Node claimant))
                {
                    reverse[node.RightOf] = node;
                    continue;
                }

                // Duplicate rightOf target: two nodes claim to be after the same sibling.
                // This happens when a new node is inserted between existing siblings and the
                // existing sibling's rightOf hasn't been updated yet (e.g., CRDT update for
                // the existing sibling was not processed by ResolveSiblingsOrder).
                // The earlier-added node keeps the slot; the other one is appended by the leftovers pass below.
                ReportHub.LogWarning(
                    new ReportData(ReportCategory.SCENE_UI),
                    $"[UISort] Duplicate rightOf target {node.RightOf}: "
                    + $"both {claimant.EntityId} and {node.EntityId} claim it. "
                    + "The later-added one is appended to the end of the chain.");

                if (node.insertionIndex < claimant.insertionIndex)
                    reverse[node.RightOf] = node;
            }

            Node? tail = null;

            // Chain every start in insertion order, each followed by the nodes that point at it
            SortByInsertionIndex(starts);

            for (var i = 0; i < starts.Count; i++)
                tail = AppendChain(starts[i], tail, reverse);

            // Leftovers: nodes still unreachable (duplicate loser, dangling rightOf target, or a cycle) are appended so none is lost
            starts.Clear();

            foreach (KeyValuePair<CRDTEntity, Node> kvp in nodes)
                if (!kvp.Value.visited)
                    starts.Add(kvp.Value);

            SortByInsertionIndex(starts);

            for (var i = 0; i < starts.Count; i++)
            {
                Node node = starts[i];

                // Already reached through an earlier leftover's chain
                if (node.visited)
                    continue;

                // Duplicate losers were already reported above
                bool isDuplicateLoser = reverse.TryGetValue(node.RightOf, out Node claimant) && claimant != node;

                if (!isDuplicateLoser)
                    ReportHub.LogWarning(
                        new ReportData(ReportCategory.SCENE_UI),
                        $"[UISort] Sibling {node.EntityId} has rightOf={node.RightOf} which is not a reachable sibling. "
                        + "Appending it to the end of the chain.");

                tail = AppendChain(node, tail, reverse);
            }
        }

        /// <summary>
        ///     Links <paramref name="start" /> after <paramref name="tail" /> (or makes it the head), then follows the
        ///     reverse map through the not-yet-visited nodes that point at it. Returns the new tail.
        /// </summary>
        private Node AppendChain(Node start, Node? tail, Dictionary<CRDTEntity, Node> reverse)
        {
            if (tail == null)
                head = start;
            else
            {
                tail.Next = start;
                start.Previous = tail;
            }

            Node current = start;
            current.visited = true;

            while (reverse.TryGetValue(current.EntityId, out Node next) && !next.visited)
            {
                current.Next = next;
                next.Previous = current;
                next.visited = true;
                current = next;
            }

            return current;
        }

        /// <summary>
        ///     Allocation-free insertion sort; the list holds the few siblings the rightOf chain leaves unordered.
        /// </summary>
        private static void SortByInsertionIndex(List<Node> list)
        {
            for (var i = 1; i < list.Count; i++)
            {
                Node node = list[i];
                int j = i - 1;

                while (j >= 0 && list[j].insertionIndex > node.insertionIndex)
                {
                    list[j + 1] = list[j];
                    j--;
                }

                list[j + 1] = node;
            }
        }

        public void Dispose()
        {
            if (nodes != null)
            {
                foreach (var kvp in nodes)
                    Node.POOL.Release(kvp.Value);

                nodes.Clear();
            }

            reverseRightOf?.Clear();
            chainStarts?.Clear();
            head = null;
        }
    }
}
