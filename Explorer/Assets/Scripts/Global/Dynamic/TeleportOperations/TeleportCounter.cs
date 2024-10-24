using System.Collections.Generic;
using CommunicationData.URLHelpers;
using UnityEngine;

namespace Global.Dynamic.TeleportOperations
{
    public class TeleportCounter
    {
        private readonly List<Vector2Int> successfullTeleports;
        private readonly HashSet<URLDomain> visitedRealms; 
        private readonly int teleportsBeforeUnload;
        internal int teleportsDone;

        public TeleportCounter(int teleportsBeforeUnload)
        {
            successfullTeleports = new List<Vector2Int>(teleportsBeforeUnload);
            visitedRealms = new HashSet<URLDomain>(teleportsBeforeUnload);
            this.teleportsBeforeUnload = teleportsBeforeUnload;
        }

        public void AddSuccessfullTeleport(Vector2Int teleportCompleted, URLDomain realmName, bool teleportToNewRealm)
        {
            if (teleportToNewRealm)
            {
                if (!visitedRealms.Contains(realmName))
                {
                    //New realm, we add it to the list since coordinates may repeat with Genesis City
                    visitedRealms.Add(realmName);
                    teleportsDone++;
                }
            }
            //If we have already gone there, we ignore it. We want to unload on new scenes only
            else
            {
                if (!successfullTeleports.Contains(teleportCompleted))
                {
                    successfullTeleports.Add(teleportCompleted);
                    teleportsDone++;
                }
            }
        }

        public void ClearSuccessfullTeleports()
        {
            successfullTeleports.Clear();
            visitedRealms.Clear();
            teleportsDone = 0;
        }

        public bool ReachedTeleportLimit()
        {
            if (teleportsDone >= teleportsBeforeUnload)
            {
                ClearSuccessfullTeleports();
                return true;
            }

            return false;
        }
    }
}