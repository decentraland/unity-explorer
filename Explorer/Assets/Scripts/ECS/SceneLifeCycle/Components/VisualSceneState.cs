using System;
using UnityEngine.Serialization;

namespace ECS.SceneLifeCycle.Components
{
    public struct VisualSceneState
    {
        public bool IsDirty;
        public VisualSceneStateEnum CurrentVisualSceneState;
    }

    public enum VisualSceneStateEnum
    {
        SHOWING_SCENE,
        SHOWING_LOD
    }
}