namespace DCL.Utilities
{
    public class PlayerParcelTrackerService
    {
        private readonly ReactiveProperty<PlayerParcelData> currentParcelData = new (default(PlayerParcelData));

        public IReadonlyReactiveProperty<PlayerParcelData> CurrentParcelData => currentParcelData;

        public void UpdateParcelData(PlayerParcelData newData)
        {
            if (!currentParcelData.Value.Equals(newData)) { currentParcelData.Value = newData; }
        }
    }
}
