using DCL.Utilities;
using Utility;

namespace DCL.Utilities
{
    public class PlayerParcelTrackerService
    {
        private readonly ReactiveProperty<PlayerParcelData> currentParcelData;

        public IReadonlyReactiveProperty<PlayerParcelData> CurrentParcelData => currentParcelData;

        public PlayerParcelTrackerService()
        {
            currentParcelData = new ReactiveProperty<PlayerParcelData>(default);
        }

        public void UpdateParcelData(PlayerParcelData newData)
        {
            if (!currentParcelData.Value.Equals(newData))
            {
                currentParcelData.Value = newData;
            }
        }
    }
}
