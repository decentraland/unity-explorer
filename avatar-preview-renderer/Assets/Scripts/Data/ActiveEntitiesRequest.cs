using System;

namespace Data
{
    [Serializable]
    public class ActiveEntitiesRequest
    {
        public string[] pointers;

        public ActiveEntitiesRequest(string[] pointers)
        {
            this.pointers = pointers;
        }
    }
}