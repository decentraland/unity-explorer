namespace DCL.LOD.Components
{
    public static class SceneLODInfoUtils
    {
        public static byte SetLODResult(byte result, int lodLevel)
        {
            result |= (byte)(1 << lodLevel);
            return result;
        }

        public static int CountLOD(byte loadedLODs)
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
    }
}