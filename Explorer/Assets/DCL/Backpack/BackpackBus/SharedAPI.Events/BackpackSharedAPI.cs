using CodeLess.Interfaces;
using System;

namespace DCL.Backpack.BackpackBus
{
    [AutoInterface]
    public class BackpackSharedAPI : IBackpackSharedAPI
    {
        public event Action<string, bool>? WearableEquipped;
        public event Action<string>? WearableUnEquipped;

        public void SendWearableEquipped(string urn, bool isManuallyEquipped) => WearableEquipped?.Invoke(urn, isManuallyEquipped);
        public void SendWearableUnEquipped(string urn) => WearableUnEquipped?.Invoke(urn);
    }
}
