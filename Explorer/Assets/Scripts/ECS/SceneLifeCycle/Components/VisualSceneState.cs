using System;
using UnityEngine.Serialization;

namespace ECS.SceneLifeCycle.Components
{
    public struct VisualSceneState
    {
        public bool isDirty;
        public VisualSceneStateEnum currentVisualSceneState;
    }

    public enum VisualSceneStateEnum
    {
        SHOWING_SCENE,
        SHOWING_LOD
    }
}