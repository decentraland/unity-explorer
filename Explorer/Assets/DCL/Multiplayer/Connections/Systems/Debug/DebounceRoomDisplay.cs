using System;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class DebounceRoomDisplay : IRoomDisplay
    {
        private readonly IRoomDisplay origin;
        private readonly TimeSpan debounceTime;
        private DateTime lastUpdate = DateTime.MinValue;

        public DebounceRoomDisplay(IRoomDisplay origin, TimeSpan debounceTime)
        {
            this.origin = origin;
            this.debounceTime = debounceTime;
        }

        public void Update()
        {
            if (DateTime.Now - lastUpdate < debounceTime)
                return;

            origin.Update();
            lastUpdate = DateTime.Now;
        }
    }
}
