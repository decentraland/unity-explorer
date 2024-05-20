using Arch.Core;
using DCL.Diagnostics;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITransformRelationData
    {
        private const int CHILDREN_DEFAULT_CAPACITY = 10;

        /// <summary>
        /// Sorted list from left to right
        /// This collection is pooled alongside the parent `UITransformComponent` it's contained in.
        /// </summary>
        [CanBeNull]
        private SortedList<int, EntityReference> children;

        /// <summary>
        /// `rightOf` can have the default value
        /// </summary>
        [CanBeNull]
        private List<EntityReference> unsortedChildren;

        internal EntityReference parent;

        internal int rightOf;

        /// <summary>
        /// Indicates that resorting is required
        /// </summary>
        internal bool layoutIsDirty;

        public IList<EntityReference> Children => children?.Values;

        public void AddChild(EntityReference thisEntity, EntityReference childEntity, ref UITransformRelationData childComponent)
        {
            children ??= new SortedList<int, EntityReference>(CHILDREN_DEFAULT_CAPACITY);
            unsortedChildren ??= new List<EntityReference>(CHILDREN_DEFAULT_CAPACITY);

            AddChild(childEntity, childComponent.rightOf);

            childComponent.parent = thisEntity;

            layoutIsDirty = true;
        }

        private void AddChild(EntityReference childEntity, int rightOf)
        {
            // rightOf can have the default value, in this case ignore it
            if (rightOf > 0)
                children!.Add(rightOf, childEntity);
            else
                unsortedChildren!.Add(childEntity);
        }

        public void RemoveChild(ref UITransformRelationData childComponent, EntityReference entity)
        {
            if (childComponent.rightOf > 0)
                children!.Remove(childComponent.rightOf);
            else
                unsortedChildren!.Remove(entity);
        }

        public void ChangeChildRightOf(int oldRightOf, int newRightOf, EntityReference entityReference)
        {
            if (oldRightOf > 0)
            {
                if (!children!.Remove(oldRightOf))
                    ReportHub.LogError(ReportCategory.SCENE_UI, $"Failed to find rightOf entity {oldRightOf} in children");
            }
            else
            {
                if (!unsortedChildren!.Remove(entityReference))
                    ReportHub.LogError(ReportCategory.SCENE_UI, $"Failed to find entity {entityReference} in unsortedChildren");
            }

            AddChild(entityReference, newRightOf);

            layoutIsDirty = true;
        }

        public void Dispose()
        {
            children?.Clear();
            unsortedChildren?.Clear();
        }
    }
}
