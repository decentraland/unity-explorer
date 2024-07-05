using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UITransformComponent
    {
        private static readonly Comparison<VisualElement> CACHED_COMPARISON = TabIndexComparison;

        public VisualElement? Transform;
        public bool IsHidden;
        public PointerEventType? PointerEventTriggered;

        public UITransformRelationLinkedData RelationData;

        internal EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        private bool isRoot;

        public void InitializeAsRoot(VisualElement root)
        {
            Transform = root;
            IsHidden = false;
            PointerEventTriggered = null;

            RelationData.parent = EntityReference.Null;
            RelationData.rightOf = 0;
            isRoot = true;
        }

        public void Initialize(string componentName, CRDTEntity entity, CRDTEntity rightOf)
        {
            Transform ??= new VisualElement();
            Transform.name = UiElementUtils.BuildElementName(componentName, entity);
            IsHidden = false;
            PointerEventTriggered = null;

            RelationData.parent = EntityReference.Null;
            RelationData.rightOf = rightOf;
        }

        public void SortIfRequired(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap)
        {
            if (!RelationData.layoutIsDirty)
                return;

            Assert.IsNotNull(RelationData.head);

            // Instead of creating a new collection with VisualElements keep the index in the tabIndex

            int i = 0;
            for (UITransformRelationLinkedData.Node node = RelationData.head; node != null; node = node.Next)
            {
                //EntityReference child = node.EntityId;
                var childEntityId = node.EntityId;

                if (entitiesMap.TryGetValue(childEntityId, out var child))
                {
                    var childTransform = world.Get<UITransformComponent>(child);
                    childTransform.Transform!.tabIndex = i;
                }

                i++;
            }

            Transform!.Sort(CACHED_COMPARISON);

            RelationData.layoutIsDirty = false;
        }

        private static int TabIndexComparison(VisualElement x, VisualElement y) =>
            x.tabIndex.CompareTo(y.tabIndex);

        public void Dispose()
        {
            this.UnregisterPointerCallbacks();
            RelationData.Dispose();

            if (isRoot)
                return;

            Transform!.tabIndex = 0;
            Transform.RemoveFromHierarchy();
        }
    }
}
