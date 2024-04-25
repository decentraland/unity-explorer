using Arch.Core;
using DCL.Diagnostics;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITransformRelationData : IDisposable
    {
        private const int CHILDREN_DEFAULT_CAPACITY = 10;

        private static readonly ObjectPool<LinkedListNode<EntityReference>> NODES_POOL =
            new (() => new LinkedListNode<EntityReference>(EntityReference.Null), defaultCapacity: 500);

        /// <summary>
        /// Sorted list from left to right
        /// This collection is pooled alongside the parent `UITransfromComponent` it's contained in.
        /// </summary>
        [CanBeNull]
        private LinkedList<EntityReference> children;

        /// <summary>
        /// Instead of a separate list for unsorted children, we keep a reference to the first unsorted node.
        /// All unsorted nodes are added to the end of the list
        /// </summary>
        [CanBeNull]
        private LinkedListNode<EntityReference> firstUnsortedNode;

        [CanBeNull]
        private Dictionary<EntityReference, LinkedListNode<EntityReference>> entityToNodeMap;

        internal EntityReference parent;

        internal EntityReference rightOf;

        /// <summary>
        /// Node of this transform in the parent list
        /// </summary>
        private LinkedListNode<EntityReference> nodeInParentList;

        public void AddChild(EntityReference thisEntity, EntityReference childEntity, ref UITransformRelationData childComponent)
        {
            children ??= new LinkedList<EntityReference>();
            entityToNodeMap ??= new Dictionary<EntityReference, LinkedListNode<EntityReference>>(CHILDREN_DEFAULT_CAPACITY);

            var childNode = NODES_POOL.Get();
            childNode.Value = childEntity;

            childComponent.nodeInParentList = childNode;
            childComponent.parent = parent;

            children.AddLast(childNode);
            firstUnsortedNode ??= childNode;

            entityToNodeMap[childEntity] = childNode;

            // We can't sort at this point because we can't make sure that all siblings are already added
        }

        public void SortNewEntities(World world)
        {
            // handle insertion according to the order.
            // insert immediately after the sibling on the left, thus the order of other dependencies won't be broken

            // iterate over the unsorted nodes
            while (firstUnsortedNode != null)
            {
                var unsortedChild = firstUnsortedNode.Value;

                ref readonly var unsortedChildComponent = ref world.Get<UITransformRelationData>(unsortedChild.Entity);
                MoveChild(firstUnsortedNode, unsortedChildComponent.rightOf);

                firstUnsortedNode = firstUnsortedNode.Next;
            }
        }

        private void MoveChild(LinkedListNode<EntityReference> child, EntityReference rightOf)
        {
            if (entityToNodeMap!.TryGetValue(rightOf, out LinkedListNode<EntityReference> leftNode))
                children!.AddAfter(child, leftNode);
            else
                ReportHub.LogError(ReportCategory.SCENE_UI, $"Failed to find rightOf entity {rightOf.Entity} in parent list");
        }

        public void RemoveChild(ref UITransformRelationData childComponent)
        {
            childComponent.nodeInParentList = null;

            children!.Remove(nodeInParentList);
            NODES_POOL.Release(nodeInParentList);
        }

        public void ReevaluateChildOrder(in UITransformRelationData childComponent)
        {
            Assert.AreEqual(children, childComponent.nodeInParentList.List);
            MoveChild(childComponent.nodeInParentList, childComponent.rightOf);
        }

        public void Dispose()
        {
            entityToNodeMap?.Clear();
            children?.Clear();
        }
    }
}
