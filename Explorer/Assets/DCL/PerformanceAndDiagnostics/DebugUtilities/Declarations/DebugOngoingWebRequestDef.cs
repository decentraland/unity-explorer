using DCL.DebugUtilities.UIBindings;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.DebugUtilities
{
    public class DebugOngoingWebRequestDef : IDebugElementDef
    {
        public ElementBinding<DataSource> Binding;

        public DebugOngoingWebRequestDef(ElementBinding<DataSource> binding)
        {
            Binding = binding;
        }

        public struct DebugWebRequestInfo
        {
            public UnityWebRequest Request;

            public DateTime StartTime;
            public string ShortenedUrl;

            // Nanoseconds
            public ulong Duration;
        }

        public class DataSource
        {
            public Action? Updated { get; set; }

            public List<DebugWebRequestInfo> Requests { get; } = new (50);

            public void UpdateTime(DateTime now)
            {
                for (int i = 0; i < Requests.Count; i++)
                {
                    DebugWebRequestInfo info = Requests[i];
                    info.Duration = (ulong)((now - info.StartTime).TotalMilliseconds * 1_000_000UL);
                }

                Updated?.Invoke();
            }

            public void Add(DebugWebRequestInfo info)
            {
                Requests.Add(info);
                Updated?.Invoke();
            }

            public void Remove(UnityWebRequest uwr)
            {
                // Linear removal from the list is not a problem as the capacity is limited by the budget
                for (int i = 0; i < Requests.Count; i++)
                {
                    if (Requests[i].Request == uwr)
                    {
                        Requests.RemoveAt(i);
                        break;
                    }
                }

                Updated?.Invoke();
            }
        }
    }
}
