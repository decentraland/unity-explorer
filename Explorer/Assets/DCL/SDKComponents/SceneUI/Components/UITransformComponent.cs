using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UITransformComponent
    {
        public VisualElement Transform;
        public HashSet<EntityReference> Children;
        public bool IsHidden;
        public PointerEventType? PointerEventTriggered;

        public UITransformRelationData RelationData;

        internal EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public void Initialize(string componentName, Entity entity, EntityReference rightOf)
        {
            Transform ??= new VisualElement();
            Transform.name = UiElementUtils.BuildElementName(componentName, entity);
            Children = HashSetPool<EntityReference>.Get();
            IsHidden = false;
            PointerEventTriggered = null;

            RelationData.parent = EntityReference.Null;
            RelationData.rightOf = rightOf;
        }

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
            this.UnregisterPointerCallbacks();
            RelationData.Dispose();
        }
    }
}
