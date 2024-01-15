using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ECS.SceneLifeCycle.Components
{
    public struct VisualSceneState
    {
        public VisualSceneStateEnum CurrentVisualSceneState;
        public bool IsDirty;
    }

    public enum VisualSceneStateEnum
    {
        SHOWING_SCENE,
        SHOWING_LOD
    }
    
}