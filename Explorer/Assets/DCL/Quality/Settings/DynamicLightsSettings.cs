using DCL.SDKComponents.LightSource;
using System;
using System.Collections.Generic;

namespace DCL.Quality
{
    [Serializable]
    public class DynamicLightsSettings
    {
        public LightSourceSettings.SceneLimitationsSettings SceneLimitations;

        public List<LightSourceSettings.LodSettings> SpotLightsLods;

        public List<LightSourceSettings.LodSettings> PointLightsLods;
    }
}
