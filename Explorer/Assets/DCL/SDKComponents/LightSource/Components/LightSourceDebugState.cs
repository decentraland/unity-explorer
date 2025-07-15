namespace DCL.SDKComponents.LightSource.Systems
{
    public struct LightSourceDebugState
    {
        public bool ShadowsEnabled;

        public bool PointLightShadowsEnabled;

        public static LightSourceDebugState New() =>
            new ()
            {
                ShadowsEnabled = true,
                PointLightShadowsEnabled = true
            };
    }
}
