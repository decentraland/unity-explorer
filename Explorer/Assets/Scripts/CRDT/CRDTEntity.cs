using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CRDT
{
    /// <summary>
    ///     Entity {<br />
    ///     uint32_t id;<br />
    ///     struct {<br />
    ///     uint16_t version;<br />
    ///     uint16_t number;<br />
    ///     }<br />
    ///     <br />
    ///     We could use int directly but then there is a chance to assign the wrong structure accidently
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public readonly struct CRDTEntity : IComparable<CRDTEntity>, IEquatable<CRDTEntity>
    {
        [FieldOffset(0)]
        public readonly int Id;

        public CRDTEntity(int id)
        {
            Id = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CRDTEntity Create(int number, int version) =>
            new (number | (version << 16));

        public int EntityNumber => Id & 0xffff;

        public int EntityVersion => (Id >> 16) & 0xffff;

        public int CompareTo(CRDTEntity other) =>
            Id.CompareTo(other.Id);

        public bool Equals(CRDTEntity other) =>
            Id.Equals(other.Id);

        public override string ToString() =>
            $"E: number {EntityNumber} version {EntityVersion}";

        public static implicit operator CRDTEntity(int id) =>
            new (id);
    }
}
