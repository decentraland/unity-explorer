using DCL.Billboard.Extensions;
using DCL.ECSComponents;
using System;

namespace DCL.Billboard.Demo.Properties
{
    [Serializable]
    public class BillboardProperties
    {
        public bool useX;
        public bool useY;
        public bool useZ;

        public void ApplyOn(PBBillboard billboard)
        {
            billboard.Apply(useX, useY, useZ);
        }
    }
}
