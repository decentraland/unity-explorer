using System;
using UnityEngine;
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

    public static class VisualSceneStateConstants
    {
        public const int SCENE_BUCKET_LIMIT = 1;
        public static readonly Vector2Int[] LODS_BUCKET_LIMITS = { new(1, 2), new(2, 5) };
    }
    
}