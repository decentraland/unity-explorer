using Arch.Core;
using CRDT;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using JetBrains.Annotations;
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
            public Node Next { get; set; }
            public Node Previous { get; set; }

            private Node()
            {
            }

            private void Reset()
            {
                EntityId = 0;
                Next = null;
                Previous = null;
            }

            public void Setup(CRDTEntity entityId)
            {
                EntityId = entityId;
            }

            public override string ToString() =>
                $"{EntityId.ToString()}, Next: {Next?.EntityId.ToString()}, Previous: {Previous?.EntityId.ToString()}";
        }

        private const int CHILDREN_DEFAULT_CAPACITY = 10;

        internal Node head;
        private Dictionary<CRDTEntity, Node> nodes;
        private Dictionary<CRDTEntity, Node> pendingRightOf; // key is the left entity

        internal EntityReference parent;

        internal CRDTEntity rightOf;

        /// <summary>
        /// Indicates that resorting is required
        /// </summary>
        internal bool layoutIsDirty;

        public void AddChild(EntityReference thisEntity, CRDTEntity childEntity, ref UITransformRelationLinkedData childComponent)
        {
            Node newNode = Node.POOL.Get();
            newNode.Setup(childEntity);

            nodes ??= new Dictionary<CRDTEntity, Node>(CHILDREN_DEFAULT_CAPACITY);
            pendingRightOf ??= new Dictionary<CRDTEntity, Node>(CHILDREN_DEFAULT_CAPACITY);

            // If the element is first (or for some reason unspecified)
            if (childComponent.rightOf.Id > 0)
            {
                if (nodes.TryGetValue(childComponent.rightOf, out Node leftNode))
                {
                    newNode.Next = leftNode.Next;

                    if (leftNode.Next != null)
                        leftNode.Next.Previous = newNode;

                    leftNode.Next = newNode;
                    newNode.Previous = leftNode;
                }
                else
                {
                    // If rightOfEntityId is not yet in the list, add to pending
                    pendingRightOf[childComponent.rightOf] = newNode;
                }
            }
            else if (head != null)
            {
                // if the element is unsorted pull it to the head - make it a new head
                newNode.Next = head;
                head.Previous = newNode;
                head = newNode;
            }

            nodes[childEntity] = newNode;

            head ??= newNode;

            ResolvePending(childEntity, newNode);

            childComponent.parent = thisEntity;

            layoutIsDirty = true;
        }

        private void ResolvePending(CRDTEntity newlyAddedEntityId, Node leftNode)
        {
            if (pendingRightOf.TryGetValue(newlyAddedEntityId, out var rightNode))
            {
                if (leftNode.Next != null)
                    leftNode.Next.Previous = rightNode;

                leftNode.Next = rightNode;
                rightNode.Previous = leftNode;

                if (rightNode == head)
                {
                    // if the current head is the right node then the left-most becomes the new head
                    for (head = rightNode; head.Previous != null; head = head.Previous)
                    {
                    }
                }

                pendingRightOf.Remove(newlyAddedEntityId);
            }
        }

        public void RemoveChild(CRDTEntity child, ref UITransformRelationLinkedData childData)
        {
            Node nodeToRemove = nodes[child];

            if (nodeToRemove == head)
            {
                head = head.Next;
                head.Previous = null;
            }
            else
            {
                if (nodeToRemove.Previous != null)
                    nodeToRemove.Previous.Next = nodeToRemove.Next;
                if (nodeToRemove.Next != null)
                    nodeToRemove.Next.Previous = nodeToRemove.Previous;
            }
            nodes.Remove(child);

            // Update pendingRightOf if necessary
            pendingRightOf.Remove(child);
            pendingRightOf.Remove(childData.rightOf);

            childData.parent = EntityReference.Null;

            Node.POOL.Release(nodeToRemove);
        }

        public void ChangeChildRightOf(CRDTEntity oldRightOf, CRDTEntity newChildEntity, ref UITransformRelationLinkedData newChildData)
        {
            var thisEntity = newChildData.parent;

            RemoveChild(oldRightOf, ref newChildData);
            AddChild(thisEntity, newChildEntity, ref newChildData);
        }

        public void Dispose()
        {
            // Release all nodes
            while (head != null)
            {
                Node next = head.Next;
                Node.POOL.Release(head);
                head = next;
            }

            nodes?.Clear();
            pendingRightOf?.Clear();
            head = null;
        }
    }
}
