using System.Collections.Generic;

namespace DCL.Backpack.Gifting.Services
{
    public interface IGiftingPersistence
    {
        void SavePendingUrns(IEnumerable<string> urns);
        HashSet<string> LoadPendingUrns();
    }
}