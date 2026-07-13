using System;
using System.Collections.Generic;

namespace DCL.ApplicationsGuards
{
    [Serializable]
    public class BlocklistData
    {
        public List<User> users;

        [Serializable]
        public class User
        {
            public string wallet;
        }
    }
}
