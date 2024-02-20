using System;

namespace DCL.Landscape.Jobs
{
    [Serializable]
    public struct EmptyParcelNeighborData
    {
        public int DownHeight;
        public int UpHeight;
        public int LeftHeight;
        public int RightHeight;

        public int DownLeftHeight;
        public int DownRightHeight;
        public int UpLeftHeight;
        public int UpRightHeight;
    }
}
