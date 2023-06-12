using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.Components
{
    public struct MaterialComponent
    {
        public enum LifeCycle : byte
        {
            LoadingNotStarted = 0,
            LoadingInProgress = 1,
            LoadingFinished = 2,
            MaterialApplied = 3,
        }

        public MaterialData Data;

        /// <summary>
        ///     The current status of the material loading
        /// </summary>
        public LifeCycle Status;

        /// <summary>
        ///     The final material ready for consumption
        /// </summary>
        public Material Result;

        public Promise AlbedoTexPromise;
        public Promise EmissiveTexPromise;
        public Promise AlphaTexPromise;
        public Promise BumpTexPromise;

        public MaterialComponent(MaterialData data)
        {
            AlbedoTexPromise = Promise.NULL;
            EmissiveTexPromise = Promise.NULL;
            AlphaTexPromise = Promise.NULL;
            BumpTexPromise = Promise.NULL;

            Data = data;
            Status = LifeCycle.LoadingNotStarted;
            Result = null;
        }
    }
}
