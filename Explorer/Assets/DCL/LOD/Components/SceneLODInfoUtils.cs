namespace DCL.LOD.Components
{
    public static class SceneLODInfoUtils
    {
        public static byte SetLODResult(byte result, int lodLevel)
        {
            result |= (byte)(1 << lodLevel);
            return result;
        }

        public static int LODCount(byte loadedLODs)
        {
            int count = 0;
            byte temp = loadedLODs;
            while (temp != 0)
            {
                count += temp & 1;
                temp >>= 1;
            }

            return count;
        }

        public static bool HasLODResult(byte currentLoadLOD, int lodLevel)
        {
            return (currentLoadLOD & (1 << lodLevel)) != 0;
        }

        //This will give us the percent of the screen in which the object will be culled when being at (unloadingDistance - 1) parcel
        public static float CalculateScreenRelativeCullHeight(float tanValue, float distanceToCenter, float objectExtents, float defaultLODBias)
        {
            return objectExtents / (distanceToCenter * tanValue) * defaultLODBias;
        }

        //This will give us the distance at which the LOD change should occur if we consider the percentage at the middle between 
        //cull distance and 100% of the screen
        public static float CalculateLODChangeRelativeHeight(float cullRelativeHeightPercentage, float tanValue, float objectExtents, float defaultLodBias)
        {
            float halfDistancePercentage = ((1 - cullRelativeHeightPercentage) / 2 + cullRelativeHeightPercentage) / defaultLodBias;
            return objectExtents / (halfDistancePercentage * tanValue) - objectExtents;
        }
        
    }
}