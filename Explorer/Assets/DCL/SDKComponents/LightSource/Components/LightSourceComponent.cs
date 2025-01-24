using UnityEngine;

namespace DCL.SDKComponents.LightSource
{
    public struct LightSourceComponent
    {
        public readonly Light lightSourceInstance;

        public LightSourceComponent(Light lightSourceInstance)
        {
            this.lightSourceInstance = lightSourceInstance;
        }
    }
}
