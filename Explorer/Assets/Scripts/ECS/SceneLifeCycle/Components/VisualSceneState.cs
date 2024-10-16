using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ECS.SceneLifeCycle.Components
{
    public struct VisualSceneState
    {
        public VisualSceneStateEnum CurrentVisualSceneState;
        public VisualSceneStateEnum CandidateVisualSceneState;
        public bool IsDirty;
        public float TimeToChange;
    }

    public enum VisualSceneStateEnum
    {
        UNINITIALIZED,
        SHOWING_SCENE,
        SHOWING_LOD,
        ROAD
    }

}
