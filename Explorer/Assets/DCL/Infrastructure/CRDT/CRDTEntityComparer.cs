using System.Collections.Generic;

namespace CRDT
{
    /// <summary>
    ///     If we don't provide out own implementation than by default Dictionary will allocate memory
    ///     by boxing
    /// </summary>
    public class CRDTEntityComparer : IEqualityComparer<CRDTEntity>
    {
        public static readonly CRDTEntityComparer INSTANCE = new ();

        public bool Equals(CRDTEntity x, CRDTEntity y) =>
            x.Equals(y);

        public int GetHashCode(CRDTEntity obj) =>

            // Can't use `obj.GetHashCode()` because it will box the object
            obj.Id;
    }
}
