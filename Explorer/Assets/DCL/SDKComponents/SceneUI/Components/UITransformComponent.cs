﻿using Arch.Core;
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

        public VisualElement Transform
        {
            get => IsRoot ? rootTransform : reusableTransform;
            internal set { if (IsRoot) rootTransform = value; else reusableTransform = value;}
        }

        public bool IsHidden;
        public PointerEventType? PointerEventTriggered;
        public bool IsRoot { get; private set; }
        public int? ZIndex = null;

        public UITransformRelationLinkedData RelationData;

        internal EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent> currentOnPointerUpCallback;
        internal EventCallback<PointerEnterEvent> currentOnPointerEnterCallback;
        internal EventCallback<PointerLeaveEvent> currentOnPointerLeaveCallback;

        private VisualElement rootTransform;
        private VisualElement reusableTransform;

        public void InitializeAsRoot(VisualElement root)
        {
            this.rootTransform ??= root;
            IsHidden = false;
            PointerEventTriggered = null;
            ZIndex = null;
            RelationData.parent = Entity.Null;
            RelationData.rightOf = 0;
            IsRoot = true;
        }

        public void InitializeAsChild(string componentName, CRDTEntity entity, CRDTEntity rightOf)
        {
            reusableTransform ??= new VisualElement();
            Transform.name = UiElementUtils.BuildElementName(componentName, entity);
            IsHidden = false;
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

            Assert.IsNotNull(RelationData.head);

            // Instead of creating a new collection with VisualElements keep the index in the tabIndex

            int i = 0;
            for (UITransformRelationLinkedData.Node node = RelationData.head; node != null; node = node.Next)
            {
                var childEntityId = node.EntityId;

                if (entitiesMap.TryGetValue(childEntityId, out var child))
                {
                    var childTransform = world.Get<UITransformComponent>(child);

                    childTransform.Transform.tabIndex = childTransform.ZIndex ?? i;
                }

                i++;
            }

            Transform.Sort(CACHED_COMPARISON);

            RelationData.layoutIsDirty = false;
        }

        private static int TabIndexComparison(VisualElement x, VisualElement y) =>
            x.tabIndex.CompareTo(y.tabIndex);

        public void Dispose()
        {
            RelationData.Dispose();

            // If it's not a root, its transform can be reused
            if (IsRoot) return;

            this.UnregisterPointerCallbacks();
            reusableTransform.tabIndex = 0;
            reusableTransform.RemoveFromHierarchy();
        }
    }
}
