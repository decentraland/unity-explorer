using System;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionEventBus
    {
        public event Action CommunityCreated;

        public void OnCommunityCreated() =>
            CommunityCreated?.Invoke();
    }
}
