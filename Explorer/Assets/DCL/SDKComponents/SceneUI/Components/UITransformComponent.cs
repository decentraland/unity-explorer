using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Utils;
using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UITransformComponent : IPoolableComponentProvider<UITransformComponent>
    {
        public VisualElement Transform;
        public EntityReference Parent;
        public HashSet<EntityReference> Children;
        public bool IsHidden;
        public int RightOf;

        UITransformComponent IPoolableComponentProvider<UITransformComponent>.PoolableComponent => this;
        Type IPoolableComponentProvider<UITransformComponent>.PoolableComponentType => typeof(UITransformComponent);

        internal EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public PointerEventType? PointerEventTriggered;
        public RepeatedField<PBPointerEvents.Types.Entry> RegisteredPointerEvents { get; internal set; }

        public void Initialize(string componentName, Entity entity, ref PBUiTransform sdkModel)
        {
            Transform ??= new VisualElement();
            Transform.name = UiElementUtils.BuildElementName(componentName, entity);
            Parent = EntityReference.Null;
            Children = HashSetPool<EntityReference>.Get();
            IsHidden = false;
            RightOf = sdkModel.RightOf;

            PointerEventTriggered = null;
            this.RegisterPointerCallbacks(
                _ => PointerEventTriggered = PointerEventType.PetDown,
                _ => PointerEventTriggered = PointerEventType.PetUp);
            this.RegisterPointerEvents(null);
        }

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
            this.UnregisterPointerCallbacks();
        }
    }
}
