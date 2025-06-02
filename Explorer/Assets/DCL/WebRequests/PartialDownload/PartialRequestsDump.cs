using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.WebRequests
{
    [CreateAssetMenu(menuName = "Create PartialRequestsDump", fileName = "PartialRequestsDump", order = 0)]
    public class PartialRequestsDump : ScriptableObject
    {
        private readonly object sync = new ();

        [Serializable]
        public struct Record
        {
            public string URL;

            public long ChunkStart;
            public long ChunkEnd;
            public float Timestamp;
        }

        public List<Record> Records = new ();

        private DateTime startTime;

        public void Clear()
        {
            startTime = DateTime.Now;

            lock (sync) { Records?.Clear(); }
        }

        public void Add(string url, long chunkStart, long chunkEnd)
        {
            lock (sync)
            {
                float timestamp;

                if (Records.Count == 0)
                {
                    startTime = DateTime.Now;
                    timestamp = 0;
                }
                else { timestamp = (float)(DateTime.Now - startTime).TotalSeconds; }

                Records.Add(new Record
                {
                    URL = url,
                    ChunkStart = chunkStart,
                    ChunkEnd = chunkEnd,
                    Timestamp = timestamp,
                });
            }
        }

        public void Serialize()
        {
            EditorUtility.SetDirty(this);
        }
    }
}
