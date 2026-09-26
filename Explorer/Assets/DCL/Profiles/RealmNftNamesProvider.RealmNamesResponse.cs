using System;

namespace DCL.Profiles
{
    public partial class RealmNftNamesProvider
    {
        [Serializable]
        private struct RealmNamesResponse
        {
            public int totalAmount;
            public NameElement[] elements;

            [Serializable]
            public struct NameElement
            {
                public string name;
            }
        }
    }
}
