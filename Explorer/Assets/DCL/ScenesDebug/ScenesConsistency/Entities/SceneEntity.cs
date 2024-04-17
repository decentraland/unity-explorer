using System;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency.Entities
{
    [Serializable]
    public class SceneEntity
    {
        public Vector2Int coordinate;
        public string status;

        public SceneEntity(string raw)
        {
            try
            {
                string[] items = raw.Split('\t');
                string[] coordinatesRaw = items[0].Split(',')!;

                int x = ParsedInt(coordinatesRaw[0]!);
                int y = ParsedInt(coordinatesRaw[1]!);
                coordinate = new Vector2Int(x, y);
                status = items[1].Trim();
            }
            catch (Exception e) { throw new Exception($"Cannot parse entity from {raw}", e); }
        }

        private int ParsedInt(string raw)
        {
            try { return int.Parse(raw); }
            catch (Exception e) { throw new Exception($"Cannot parse {raw} to int", e); }
        }

        public bool IsRunning() =>
            status is "RUNNING";
    }
}
