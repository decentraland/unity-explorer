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

            children.Add(childComponent.rightOf, childEntity);

            childComponent.parent = thisEntity;

            layoutIsDirty = true;
        }

        public void RemoveChild(ref UITransformRelationData childComponent)
        {
            children!.Remove(childComponent.rightOf);
        }

        public void ChangeChildRightOf(int oldRightOf, int newRightOf, EntityReference entityReference)
        {
            if (!children!.Remove(oldRightOf))
                ReportHub.LogError(ReportCategory.SCENE_UI, $"Failed to find rightOf entity {oldRightOf} in children");

            children.Add(newRightOf, entityReference);

            layoutIsDirty = true;
        }

        public void Dispose()
        {
            children?.Clear();
        }
    }
}
