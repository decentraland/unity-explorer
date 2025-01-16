using DCL.ECSComponents;
using Decentraland.Common;
using System;
using UnityEngine;

namespace DCL.SDKComponents.LightSource
{
    public struct LightSourceComponent
    {
        public readonly Light lightSourceInstance;
        public bool active;
        public Color3 color;
        public float brightness;
        public float range;
        public PBLightSource.Types.ShadowType shadow;
        public PBLightSource.TypeOneofCase lightMode;

        public LightSourceComponent(Light lightSourceInstance)
        {
            this.lightSourceInstance = lightSourceInstance;
            active = false;
            color = null;
            brightness = 0;
            range = 0;
            shadow = PBLightSource.Types.ShadowType.StNone;
            lightMode = PBLightSource.TypeOneofCase.None;
        }
    }
}
