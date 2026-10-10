namespace DCL.SDKComponents.LightSource.Systems
{
    public struct LightSourceDebugState
    {
        public bool LightsEnabled;

        public bool ShadowsEnabled;

        public bool PointLightShadowsEnabled;

        public static LightSourceDebugState New() =>
            new ()
            {
                LightsEnabled = true,
                ShadowsEnabled = true,
                PointLightShadowsEnabled = true
            };
    }
}
