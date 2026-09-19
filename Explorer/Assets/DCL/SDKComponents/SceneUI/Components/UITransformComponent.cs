using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UITransformComponent
    {
        private static readonly Comparison<VisualElement> CACHED_COMPARISON = StackingComparison;

        /// <summary>
        ///     The element's <see cref="VisualElement.userData" /> points back at this component so the sibling
        ///     comparison can read <see cref="ZIndex" /> and <see cref="chainIndex" /> from it.
        /// </summary>
        public VisualElement Transform
        {
            get => (IsRoot ? rootTransform : reusableTransform)
                   ?? throw new InvalidOperationException($"{nameof(UITransformComponent)} was not initialized");

            internal set
            {
                value.userData = this;

                if (IsRoot) rootTransform = value;
                else reusableTransform = value;
            }
        }

        /// <summary>
        /// Where child entities and widgets (text, input, dropdown) are added.
        /// When overflow is Scroll, this is the inner ScrollView's contentContainer; otherwise it is Transform.
        /// </summary>
        public VisualElement ContentContainer => InnerScrollView != null ? InnerScrollView.contentContainer : Transform;

        public ScrollView? InnerScrollView { get; set; }

        public bool IsHidden;

        public bool StylesApplied;

        public PointerEventType? PointerEventTriggered;
        public bool IsRoot { get; private set; }
        public int? ZIndex;

        public UITransformRelationLinkedData RelationData;

        internal EventCallback<PointerDownEvent>? currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent>? currentOnPointerUpCallback;
        internal EventCallback<PointerEnterEvent>? currentOnPointerEnterCallback;
        internal EventCallback<PointerLeaveEvent>? currentOnPointerLeaveCallback;

        /// <summary>
        ///     Position in the parent's rightOf chain as of the last sort; orders siblings that share a zIndex.
        /// </summary>
        internal int chainIndex;

        private VisualElement? rootTransform;
        private VisualElement? reusableTransform;

        public void InitializeAsRoot(VisualElement root)
        {
            this.rootTransform ??= root;
            IsHidden = false;
            StylesApplied = false;
            PointerEventTriggered = null;
            ZIndex = null;
            RelationData.parent = Entity.Null;
            RelationData.rightOf = 0;
            IsRoot = true;
        }

        public void InitializeAsChild(string componentName, CRDTEntity entity, CRDTEntity rightOf)
        {
            reusableTransform ??= new VisualElement { userData = this };
            Transform.name = UiElementUtils.BuildElementName(componentName, entity);
            IsHidden = false;
            StylesApplied = false;
            IsRoot = false;
            PointerEventTriggered = null;
            ZIndex = null;
            RelationData.parent = Entity.Null;
            RelationData.rightOf = rightOf;
        }

        public void SortIfRequired(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap)
        {
            if (!RelationData.layoutIsDirty)
                return;

            RelationData.RebuildLinkedList();

            if (RelationData.head == null)
            {
                RelationData.layoutIsDirty = false;
                return;
            }

            var i = 0;

            for (UITransformRelationLinkedData.Node? node = RelationData.head; node != null; node = node.Next)
            {
                if (entitiesMap.TryGetValue(node.EntityId, out Entity child))
                    world.Get<UITransformComponent>(child).chainIndex = i;

                i++;
            }

            ContentContainer.Sort(CACHED_COMPARISON);

            RelationData.layoutIsDirty = false;
        }

        /// <summary>
        ///     Orders siblings by zIndex, then by their position in the rightOf chain, so an explicit zIndex never
        ///     collides with a positional index and equal zIndexes keep the chain order. A zIndex of 0 counts as
        ///     unset, matching CSS where z-index 0 is the default stacking level. Widgets added to the container
        ///     (labels, inputs, dropdowns) carry no component and sort as (0, 0).
        /// </summary>
        private static int StackingComparison(VisualElement x, VisualElement y)
        {
            var xComponent = x.userData as UITransformComponent;
            var yComponent = y.userData as UITransformComponent;

            int zIndexComparison = (xComponent?.ZIndex ?? 0).CompareTo(yComponent?.ZIndex ?? 0);

            return zIndexComparison != 0
                ? zIndexComparison
                : (xComponent?.chainIndex ?? 0).CompareTo(yComponent?.chainIndex ?? 0);
        }

        public void Dispose()
        {
            RelationData.Dispose();

            // If it's not a root, its transform can be reused
            if (IsRoot) return;

            VisualElement transform = Transform;

            if (InnerScrollView != null)
            {
                var scrollView = InnerScrollView;
                var content = scrollView.contentContainer;
                while (content.childCount > 0)
                {
                    var child = content[0];
                    child.RemoveFromHierarchy();
                    transform.Add(child);
                }
                scrollView.RemoveFromHierarchy();
                InnerScrollView = null;
            }

            this.UnregisterPointerCallbacks();
            transform.UnregisterHoverStyleCallbacks();
            transform.RemoveFromHierarchy();
        }
    }
}
