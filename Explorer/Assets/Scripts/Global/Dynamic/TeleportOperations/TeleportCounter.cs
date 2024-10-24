using System.Collections.Generic;
using UnityEngine;

namespace Global.Dynamic.TeleportOperations
{
    public class TeleportCounter
    {
        private readonly List<Vector2Int> successfullTeleports;
        private readonly int teleportsBeforeUnload;
        private int teleportsDone;

        public TeleportCounter(int teleportsBeforeUnload)
        {
            successfullTeleports = new List<Vector2Int>(teleportsBeforeUnload);
            this.teleportsBeforeUnload = teleportsBeforeUnload;
        }

        public void AddSuccessfullTeleport(Vector2Int teleportCompleted, bool newRealm)
        {
            //If its a new realm, we ignore the coordinates since they may repeat with Genesis City
            if (newRealm)
            {
                teleportsDone++;
            }
            //If we have already gone there, we ignore it. We want to unload on new scenes only
            else if (!successfullTeleports.Contains(teleportCompleted))
            {
                successfullTeleports.Add(teleportCompleted);
                teleportsDone++;
            }
        }

        public void ClearSuccessfullTeleports()
        {
            successfullTeleports.Clear();
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