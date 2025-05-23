// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: decentraland/sdk/components/common/camera_transition.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace DCL.ECSComponents {

  /// <summary>Holder for reflection information generated from decentraland/sdk/components/common/camera_transition.proto</summary>
  public static partial class CameraTransitionReflection {

    #region Descriptor
    /// <summary>File descriptor for decentraland/sdk/components/common/camera_transition.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static CameraTransitionReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CjpkZWNlbnRyYWxhbmQvc2RrL2NvbXBvbmVudHMvY29tbW9uL2NhbWVyYV90",
            "cmFuc2l0aW9uLnByb3RvEiJkZWNlbnRyYWxhbmQuc2RrLmNvbXBvbmVudHMu",
            "Y29tbW9uIkYKEENhbWVyYVRyYW5zaXRpb24SDgoEdGltZRgBIAEoAkgAEg8K",
            "BXNwZWVkGAIgASgCSABCEQoPdHJhbnNpdGlvbl9tb2RlQhSqAhFEQ0wuRUNT",
            "Q29tcG9uZW50c2IGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::DCL.ECSComponents.CameraTransition), global::DCL.ECSComponents.CameraTransition.Parser, new[]{ "Time", "Speed" }, new[]{ "TransitionMode" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  /// Defines the transition used towards the camera that contains the CameraTransition.
  /// This structure may be updated in the future to specify from/to entities and to have easing functions.
  /// </summary>
  public sealed partial class CameraTransition : pb::IMessage<CameraTransition>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<CameraTransition> _parser = new pb::MessageParser<CameraTransition>(() => new CameraTransition());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<CameraTransition> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::DCL.ECSComponents.CameraTransitionReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public CameraTransition() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public CameraTransition(CameraTransition other) : this() {
      switch (other.TransitionModeCase) {
        case TransitionModeOneofCase.Time:
          Time = other.Time;
          break;
        case TransitionModeOneofCase.Speed:
          Speed = other.Speed;
          break;
      }

      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public CameraTransition Clone() {
      return new CameraTransition(this);
    }

    /// <summary>Field number for the "time" field.</summary>
    public const int TimeFieldNumber = 1;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public float Time {
      get { return transitionModeCase_ == TransitionModeOneofCase.Time ? (float) transitionMode_ : 0F; }
      set {
        transitionMode_ = value;
        transitionModeCase_ = TransitionModeOneofCase.Time;
      }
    }

    /// <summary>Field number for the "speed" field.</summary>
    public const int SpeedFieldNumber = 2;
    /// <summary>
    /// meters per second; e.g. speed 1 -> 1 meter per second
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public float Speed {
      get { return transitionModeCase_ == TransitionModeOneofCase.Speed ? (float) transitionMode_ : 0F; }
      set {
        transitionMode_ = value;
        transitionModeCase_ = TransitionModeOneofCase.Speed;
      }
    }

    private object transitionMode_;
    /// <summary>Enum of possible cases for the "transition_mode" oneof.</summary>
    public enum TransitionModeOneofCase {
      None = 0,
      Time = 1,
      Speed = 2,
    }
    private TransitionModeOneofCase transitionModeCase_ = TransitionModeOneofCase.None;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public TransitionModeOneofCase TransitionModeCase {
      get { return transitionModeCase_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearTransitionMode() {
      transitionModeCase_ = TransitionModeOneofCase.None;
      transitionMode_ = null;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as CameraTransition);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(CameraTransition other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(Time, other.Time)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(Speed, other.Speed)) return false;
      if (TransitionModeCase != other.TransitionModeCase) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (transitionModeCase_ == TransitionModeOneofCase.Time) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(Time);
      if (transitionModeCase_ == TransitionModeOneofCase.Speed) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(Speed);
      hash ^= (int) transitionModeCase_;
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
      if (transitionModeCase_ == TransitionModeOneofCase.Time) {
        output.WriteRawTag(13);
        output.WriteFloat(Time);
      }
      if (transitionModeCase_ == TransitionModeOneofCase.Speed) {
        output.WriteRawTag(21);
        output.WriteFloat(Speed);
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
      if (transitionModeCase_ == TransitionModeOneofCase.Time) {
        output.WriteRawTag(13);
        output.WriteFloat(Time);
      }
      if (transitionModeCase_ == TransitionModeOneofCase.Speed) {
        output.WriteRawTag(21);
        output.WriteFloat(Speed);
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
      if (transitionModeCase_ == TransitionModeOneofCase.Time) {
        size += 1 + 4;
      }
      if (transitionModeCase_ == TransitionModeOneofCase.Speed) {
        size += 1 + 4;
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(CameraTransition other) {
      if (other == null) {
        return;
      }
      switch (other.TransitionModeCase) {
        case TransitionModeOneofCase.Time:
          Time = other.Time;
          break;
        case TransitionModeOneofCase.Speed:
          Speed = other.Speed;
          break;
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
          case 13: {
            Time = input.ReadFloat();
            break;
          }
          case 21: {
            Speed = input.ReadFloat();
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
          case 13: {
            Time = input.ReadFloat();
            break;
          }
          case 21: {
            Speed = input.ReadFloat();
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
