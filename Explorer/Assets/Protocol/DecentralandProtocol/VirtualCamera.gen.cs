// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: decentraland/sdk/components/virtual_camera.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace DCL.ECSComponents {

  /// <summary>Holder for reflection information generated from decentraland/sdk/components/virtual_camera.proto</summary>
  public static partial class VirtualCameraReflection {

    #region Descriptor
    /// <summary>File descriptor for decentraland/sdk/components/virtual_camera.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static VirtualCameraReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CjBkZWNlbnRyYWxhbmQvc2RrL2NvbXBvbmVudHMvdmlydHVhbF9jYW1lcmEu",
            "cHJvdG8SG2RlY2VudHJhbGFuZC5zZGsuY29tcG9uZW50cxo6ZGVjZW50cmFs",
            "YW5kL3Nkay9jb21wb25lbnRzL2NvbW1vbi9jYW1lcmFfdHJhbnNpdGlvbi5w",
            "cm90byKTAQoPUEJWaXJ0dWFsQ2FtZXJhElAKEmRlZmF1bHRfdHJhbnNpdGlv",
            "bhgBIAEoCzI0LmRlY2VudHJhbGFuZC5zZGsuY29tcG9uZW50cy5jb21tb24u",
            "Q2FtZXJhVHJhbnNpdGlvbhIbCg5sb29rX2F0X2VudGl0eRgCIAEoDUgAiAEB",
            "QhEKD19sb29rX2F0X2VudGl0eUIUqgIRRENMLkVDU0NvbXBvbmVudHNiBnBy",
            "b3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::DCL.ECSComponents.CameraTransitionReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::DCL.ECSComponents.PBVirtualCamera), global::DCL.ECSComponents.PBVirtualCamera.Parser, new[]{ "DefaultTransition", "LookAtEntity" }, new[]{ "LookAtEntity" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  /// PBVirtualCamera represents a camera to be used at some point in time during the scene execution
  /// * The defaultTransition represents the transition TOWARDS this camera.
  /// * The lookAtEntity defines to which entity the Camera has to look at constantly (independent from 
  /// the holding entity transform).
  /// </summary>
  public sealed partial class PBVirtualCamera : pb::IMessage<PBVirtualCamera>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<PBVirtualCamera> _parser = new pb::MessageParser<PBVirtualCamera>(() => new PBVirtualCamera());
    private pb::UnknownFieldSet _unknownFields;
    private int _hasBits0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<PBVirtualCamera> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::DCL.ECSComponents.VirtualCameraReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBVirtualCamera() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBVirtualCamera(PBVirtualCamera other) : this() {
      _hasBits0 = other._hasBits0;
      defaultTransition_ = other.defaultTransition_ != null ? other.defaultTransition_.Clone() : null;
      lookAtEntity_ = other.lookAtEntity_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PBVirtualCamera Clone() {
      return new PBVirtualCamera(this);
    }

    /// <summary>Field number for the "default_transition" field.</summary>
    public const int DefaultTransitionFieldNumber = 1;
    private global::DCL.ECSComponents.CameraTransition defaultTransition_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::DCL.ECSComponents.CameraTransition DefaultTransition {
      get { return defaultTransition_; }
      set {
        defaultTransition_ = value;
      }
    }

    /// <summary>Field number for the "look_at_entity" field.</summary>
    public const int LookAtEntityFieldNumber = 2;
    private uint lookAtEntity_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public uint LookAtEntity {
      get { if ((_hasBits0 & 1) != 0) { return lookAtEntity_; } else { return 0; } }
      set {
        _hasBits0 |= 1;
        lookAtEntity_ = value;
      }
    }
    /// <summary>Gets whether the "look_at_entity" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasLookAtEntity {
      get { return (_hasBits0 & 1) != 0; }
    }
    /// <summary>Clears the value of the "look_at_entity" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearLookAtEntity() {
      _hasBits0 &= ~1;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as PBVirtualCamera);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(PBVirtualCamera other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(DefaultTransition, other.DefaultTransition)) return false;
      if (LookAtEntity != other.LookAtEntity) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (defaultTransition_ != null) hash ^= DefaultTransition.GetHashCode();
      if (HasLookAtEntity) hash ^= LookAtEntity.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (defaultTransition_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(DefaultTransition);
      }
      if (HasLookAtEntity) {
        output.WriteRawTag(16);
        output.WriteUInt32(LookAtEntity);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (defaultTransition_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(DefaultTransition);
      }
      if (HasLookAtEntity) {
        output.WriteRawTag(16);
        output.WriteUInt32(LookAtEntity);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (defaultTransition_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(DefaultTransition);
      }
      if (HasLookAtEntity) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(LookAtEntity);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(PBVirtualCamera other) {
      if (other == null) {
        return;
      }
      if (other.defaultTransition_ != null) {
        if (defaultTransition_ == null) {
          DefaultTransition = new global::DCL.ECSComponents.CameraTransition();
        }
        DefaultTransition.MergeFrom(other.DefaultTransition);
      }
      if (other.HasLookAtEntity) {
        LookAtEntity = other.LookAtEntity;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            if (defaultTransition_ == null) {
              DefaultTransition = new global::DCL.ECSComponents.CameraTransition();
            }
            input.ReadMessage(DefaultTransition);
            break;
          }
          case 16: {
            LookAtEntity = input.ReadUInt32();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            if (defaultTransition_ == null) {
              DefaultTransition = new global::DCL.ECSComponents.CameraTransition();
            }
            input.ReadMessage(DefaultTransition);
            break;
          }
          case 16: {
            LookAtEntity = input.ReadUInt32();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code