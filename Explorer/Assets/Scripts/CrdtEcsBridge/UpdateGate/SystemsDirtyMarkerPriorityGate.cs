using DCL.ECSComponents;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.UpdateGate
{
    public class SystemsDirtyMarkerPriorityGate : ISystemsUpdateGate
    {
        private HashSet<Type> opened;

        public void Open<T>()
        {
            opened.Add(typeof(T));
        }

        public void Close<T>()
        {
            opened.Remove(typeof(T));
        }

        public bool IsOpen<T>() where T : IDirtyMarker =>
            opened.Contains(typeof(T));

        public bool IsClosed<T>() where T : IDirtyMarker =>
            !IsOpen<T>();
    }

    public interface ISystemsUpdateGate
    {
        public void Open<T>();
        public void Close<T>();

        bool IsOpen<T>() where T: IDirtyMarker;
        bool IsClosed<T>() where T: IDirtyMarker;
    }
}
