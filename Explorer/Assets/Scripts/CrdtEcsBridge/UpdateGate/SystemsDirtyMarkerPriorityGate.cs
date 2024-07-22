using DCL.ECSComponents;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.UpdateGate
{
    public class SystemsDirtyMarkerPriorityGate : ISystemsUpdateGate
    {
        private readonly HashSet<Type> opened = new ();

        public void Open<T>()
        {
            lock (opened) { opened.Add(typeof(T)); }
        }

        public void Close<T>()
        {
            lock (opened) { opened.Remove(typeof(T)); }
        }

        public bool IsOpen<T>() where T: IDirtyMarker
        {
            lock (opened) { return opened.Contains(typeof(T)); }
        }

        public bool IsClosed<T>() where T: IDirtyMarker
        {
            lock (opened) { return !IsOpen<T>(); }
        }
    }

    public interface ISystemsUpdateGate
    {
        public void Open<T>();

        public void Close<T>();

        bool IsOpen<T>() where T: IDirtyMarker;

        bool IsClosed<T>() where T: IDirtyMarker;
    }
}
