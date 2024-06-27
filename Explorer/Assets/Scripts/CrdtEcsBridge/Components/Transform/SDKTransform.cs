using CRDT;
using DCL.ECSComponents;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using JetBrains.Annotations;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CrdtEcsBridge.Components.Transform
{
    /// <summary>
    ///     Special type (not Proto) to serialize/deserialize faster according to the ADR
    /// </summary>
    public class SDKTransform : IDirtyMarker, IMessage
    {
        public CRDTEntity ParentId = 0;
        public Vector3 Position = Vector3.zero;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Scale = Vector3.one;
        public bool IsDirty { get; set; }

        public MessageDescriptor Descriptor => throw new NotSupportedException($"{nameof(Descriptor)} is not supported for {nameof(SDKTransform)}");

        public void MergeFrom(CodedInputStream input)
        {
            throw new NotSupportedException($"{nameof(MergeFrom)} is not supported for {nameof(SDKTransform)}");
        }

        public void WriteTo(CodedOutputStream output)
        {
            throw new NotSupportedException($"{nameof(WriteTo)} is not supported for {nameof(SDKTransform)}");
        }

        public int CalculateSize() =>
            (Marshal.SizeOf<Vector3>() * 2) + Marshal.SizeOf<Quaternion>() + Marshal.SizeOf<CRDTEntity>();

        public void Clear()
        {
            ParentId = 0;
            Position = Vector3.zero;
            Rotation = Quaternion.identity;
            Scale = Vector3.one;
            IsDirty = false;
        }

        [NotNull]
        public override string ToString() =>
            $"({nameof(SDKTransform)} {nameof(ParentId)}: {ParentId}; {nameof(Position)} {Position}; {nameof(Rotation)} {Rotation}; {nameof(Scale)}: {Scale}; {nameof(IsDirty)} {IsDirty})";
    }
}
