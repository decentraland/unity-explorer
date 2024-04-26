using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using System;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UITransformComponent
    {
        private static readonly Comparison<VisualElement> CACHED_COMPARISON = TabIndexComparison;

        public VisualElement Transform;
        public bool IsHidden;
        public PointerEventType? PointerEventTriggered;

        public UITransformRelationData RelationData;

        internal EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public void Initialize(string componentName, Entity entity, int rightOf)
        {
            Transform ??= new VisualElement();
            Transform.name = UiElementUtils.BuildElementName(componentName, entity);
            IsHidden = false;
            PointerEventTriggered = null;

            RelationData.parent = EntityReference.Null;
            RelationData.rightOf = rightOf;
        }

        public void SortIfRequired(World world)
        {
            if (!RelationData.layoutIsDirty)
                return;

            Assert.IsNotNull(RelationData.Children);

            // Instead of creating a new collection with VisualElements keep the index in the tabIndex

            for (var i = 0; i < RelationData.Children.Count; i++)
            {
                EntityReference child = RelationData.Children[i];

                if (world.IsAlive(child))
                {
                    var childTransform = world.Get<UITransformComponent>(child);
                    childTransform.Transform.tabIndex = i;
                }
            }

            Transform.Sort(CACHED_COMPARISON);

            RelationData.layoutIsDirty = false;
        }

        private static int TabIndexComparison(VisualElement x, VisualElement y) =>
            x.tabIndex.CompareTo(y.tabIndex);

        public void Dispose()
        {
            this.UnregisterPointerCallbacks();
            RelationData.Dispose();
        }
    }
}
