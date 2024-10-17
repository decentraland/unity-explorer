using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ECS.SceneLifeCycle.Components
{
    public struct VisualSceneState
    {
        /// <summary>  The current VisualSceneState that the scene is, could be different from the CandidateVisualSceneState if we are delaying the switch from Scene -> LODs </summary>
        public VisualSceneStateEnum CurrentVisualSceneState;
        /// <summary>  The VisualSceneState to which these scene is going to change to</summary>
        public VisualSceneStateEnum CandidateVisualSceneState;
        public bool IsDirty;
        /// <summary>  The time in Seconds that has passed since this scene started trying to change from one VisualSceneState to another</summary>
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
