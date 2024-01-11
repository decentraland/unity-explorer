using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.Components
{
    public struct MaterialComponent
    {
        public MaterialData Data;

        /// <summary>
        ///     The current status of the material loading
        /// </summary>
        public StreamableLoading.LifeCycle Status;

        /// <summary>
        ///     The final material ready for consumption
        /// </summary>
        public Material? Result;

        public Promise? AlbedoTexPromise;
        public Promise? EmissiveTexPromise;
        public Promise? AlphaTexPromise;
        public Promise? BumpTexPromise;

        public MaterialComponent(MaterialData data)
        {
            AlbedoTexPromise = null;
            EmissiveTexPromise = null;
            AlphaTexPromise = null;
            BumpTexPromise = null;

            Data = data;
            Status = StreamableLoading.LifeCycle.LoadingNotStarted;
            Result = null;
        }
    }
}
