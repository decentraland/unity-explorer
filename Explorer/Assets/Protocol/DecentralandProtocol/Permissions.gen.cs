// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: decentraland/kernel/apis/permissions.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Decentraland.Kernel.Apis {

  /// <summary>Holder for reflection information generated from decentraland/kernel/apis/permissions.proto</summary>
  public static partial class PermissionsReflection {

    #region Descriptor
    /// <summary>File descriptor for decentraland/kernel/apis/permissions.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static PermissionsReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CipkZWNlbnRyYWxhbmQva2VybmVsL2FwaXMvcGVybWlzc2lvbnMucHJvdG8S",
            "GGRlY2VudHJhbGFuZC5rZXJuZWwuYXBpcyIsChJQZXJtaXNzaW9uUmVzcG9u",
            "c2USFgoOaGFzX3Blcm1pc3Npb24YASABKAgiVAoUSGFzUGVybWlzc2lvblJl",
            "cXVlc3QSPAoKcGVybWlzc2lvbhgBIAEoDjIoLmRlY2VudHJhbGFuZC5rZXJu",
            "ZWwuYXBpcy5QZXJtaXNzaW9uSXRlbSJZChhIYXNNYW55UGVybWlzc2lvblJl",
            "cXVlc3QSPQoLcGVybWlzc2lvbnMYASADKA4yKC5kZWNlbnRyYWxhbmQua2Vy",
            "bmVsLmFwaXMuUGVybWlzc2lvbkl0ZW0iOAoZSGFzTWFueVBlcm1pc3Npb25S",
            "ZXNwb25zZRIbChNoYXNfbWFueV9wZXJtaXNzaW9uGAEgAygIKtYBCg5QZXJt",
            "aXNzaW9uSXRlbRIoCiRQSV9BTExPV19UT19NT1ZFX1BMQVlFUl9JTlNJREVf",
            "U0NFTkUQABIkCiBQSV9BTExPV19UT19UUklHR0VSX0FWQVRBUl9FTU9URRAB",
            "EhMKD1BJX1VTRV9XRUIzX0FQSRACEhQKEFBJX1VTRV9XRUJTT0NLRVQQAxIQ",
            "CgxQSV9VU0VfRkVUQ0gQBBIcChhQSV9BTExPV19NRURJQV9IT1NUTkFNRVMQ",
            "BRIZChVQSV9PUEVOX0VYVEVSTkFMX0xJTksQBjKGAgoSUGVybWlzc2lvbnNT",
            "ZXJ2aWNlEm8KDUhhc1Blcm1pc3Npb24SLi5kZWNlbnRyYWxhbmQua2VybmVs",
            "LmFwaXMuSGFzUGVybWlzc2lvblJlcXVlc3QaLC5kZWNlbnRyYWxhbmQua2Vy",
            "bmVsLmFwaXMuUGVybWlzc2lvblJlc3BvbnNlIgASfwoSSGFzTWFueVBlcm1p",
            "c3Npb25zEjIuZGVjZW50cmFsYW5kLmtlcm5lbC5hcGlzLkhhc01hbnlQZXJt",
            "aXNzaW9uUmVxdWVzdBozLmRlY2VudHJhbGFuZC5rZXJuZWwuYXBpcy5IYXNN",
            "YW55UGVybWlzc2lvblJlc3BvbnNlIgBiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Decentraland.Kernel.Apis.PermissionItem), }, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Decentraland.Kernel.Apis.PermissionResponse), global::Decentraland.Kernel.Apis.PermissionResponse.Parser, new[]{ "HasPermission" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Decentraland.Kernel.Apis.HasPermissionRequest), global::Decentraland.Kernel.Apis.HasPermissionRequest.Parser, new[]{ "Permission" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Decentraland.Kernel.Apis.HasManyPermissionRequest), global::Decentraland.Kernel.Apis.HasManyPermissionRequest.Parser, new[]{ "Permissions" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Decentraland.Kernel.Apis.HasManyPermissionResponse), global::Decentraland.Kernel.Apis.HasManyPermissionResponse.Parser, new[]{ "HasManyPermission" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Enums
  public enum PermissionItem {
    [pbr::OriginalName("PI_ALLOW_TO_MOVE_PLAYER_INSIDE_SCENE")] PiAllowToMovePlayerInsideScene = 0,
    [pbr::OriginalName("PI_ALLOW_TO_TRIGGER_AVATAR_EMOTE")] PiAllowToTriggerAvatarEmote = 1,
    [pbr::OriginalName("PI_USE_WEB3_API")] PiUseWeb3Api = 2,
    [pbr::OriginalName("PI_USE_WEBSOCKET")] PiUseWebsocket = 3,
    [pbr::OriginalName("PI_USE_FETCH")] PiUseFetch = 4,
    [pbr::OriginalName("PI_ALLOW_MEDIA_HOSTNAMES")] PiAllowMediaHostnames = 5,
    [pbr::OriginalName("PI_OPEN_EXTERNAL_LINK")] PiOpenExternalLink = 6,
  }

  #endregion

  #region Messages
  public sealed partial class PermissionResponse : pb::IMessage<PermissionResponse>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<PermissionResponse> _parser = new pb::MessageParser<PermissionResponse>(() => new PermissionResponse());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<PermissionResponse> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Decentraland.Kernel.Apis.PermissionsReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PermissionResponse() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PermissionResponse(PermissionResponse other) : this() {
      hasPermission_ = other.hasPermission_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public PermissionResponse Clone() {
      return new PermissionResponse(this);
    }

    /// <summary>Field number for the "has_permission" field.</summary>
    public const int HasPermissionFieldNumber = 1;
    private bool hasPermission_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasPermission {
      get { return hasPermission_; }
      set {
        hasPermission_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as PermissionResponse);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(PermissionResponse other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (HasPermission != other.HasPermission) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (HasPermission != false) hash ^= HasPermission.GetHashCode();
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
      if (HasPermission != false) {
        output.WriteRawTag(8);
        output.WriteBool(HasPermission);
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
      if (HasPermission != false) {
        output.WriteRawTag(8);
        output.WriteBool(HasPermission);
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
      if (HasPermission != false) {
        size += 1 + 1;
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(PermissionResponse other) {
      if (other == null) {
        return;
      }
      if (other.HasPermission != false) {
        HasPermission = other.HasPermission;
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
          case 8: {
            HasPermission = input.ReadBool();
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
          case 8: {
            HasPermission = input.ReadBool();
            break;
          }
        }
      }
    }
    #endif

  }

  public sealed partial class HasPermissionRequest : pb::IMessage<HasPermissionRequest>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<HasPermissionRequest> _parser = new pb::MessageParser<HasPermissionRequest>(() => new HasPermissionRequest());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<HasPermissionRequest> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Decentraland.Kernel.Apis.PermissionsReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasPermissionRequest() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasPermissionRequest(HasPermissionRequest other) : this() {
      permission_ = other.permission_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasPermissionRequest Clone() {
      return new HasPermissionRequest(this);
    }

    /// <summary>Field number for the "permission" field.</summary>
    public const int PermissionFieldNumber = 1;
    private global::Decentraland.Kernel.Apis.PermissionItem permission_ = global::Decentraland.Kernel.Apis.PermissionItem.PiAllowToMovePlayerInsideScene;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Decentraland.Kernel.Apis.PermissionItem Permission {
      get { return permission_; }
      set {
        permission_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as HasPermissionRequest);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(HasPermissionRequest other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Permission != other.Permission) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (Permission != global::Decentraland.Kernel.Apis.PermissionItem.PiAllowToMovePlayerInsideScene) hash ^= Permission.GetHashCode();
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
      if (Permission != global::Decentraland.Kernel.Apis.PermissionItem.PiAllowToMovePlayerInsideScene) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Permission);
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
      if (Permission != global::Decentraland.Kernel.Apis.PermissionItem.PiAllowToMovePlayerInsideScene) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Permission);
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
      if (Permission != global::Decentraland.Kernel.Apis.PermissionItem.PiAllowToMovePlayerInsideScene) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Permission);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(HasPermissionRequest other) {
      if (other == null) {
        return;
      }
      if (other.Permission != global::Decentraland.Kernel.Apis.PermissionItem.PiAllowToMovePlayerInsideScene) {
        Permission = other.Permission;
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
          case 8: {
            Permission = (global::Decentraland.Kernel.Apis.PermissionItem) input.ReadEnum();
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
          case 8: {
            Permission = (global::Decentraland.Kernel.Apis.PermissionItem) input.ReadEnum();
            break;
          }
        }
      }
    }
    #endif

  }

  public sealed partial class HasManyPermissionRequest : pb::IMessage<HasManyPermissionRequest>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<HasManyPermissionRequest> _parser = new pb::MessageParser<HasManyPermissionRequest>(() => new HasManyPermissionRequest());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<HasManyPermissionRequest> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Decentraland.Kernel.Apis.PermissionsReflection.Descriptor.MessageTypes[2]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasManyPermissionRequest() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasManyPermissionRequest(HasManyPermissionRequest other) : this() {
      permissions_ = other.permissions_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasManyPermissionRequest Clone() {
      return new HasManyPermissionRequest(this);
    }

    /// <summary>Field number for the "permissions" field.</summary>
    public const int PermissionsFieldNumber = 1;
    private static readonly pb::FieldCodec<global::Decentraland.Kernel.Apis.PermissionItem> _repeated_permissions_codec
        = pb::FieldCodec.ForEnum(10, x => (int) x, x => (global::Decentraland.Kernel.Apis.PermissionItem) x);
    private readonly pbc::RepeatedField<global::Decentraland.Kernel.Apis.PermissionItem> permissions_ = new pbc::RepeatedField<global::Decentraland.Kernel.Apis.PermissionItem>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Decentraland.Kernel.Apis.PermissionItem> Permissions {
      get { return permissions_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as HasManyPermissionRequest);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(HasManyPermissionRequest other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if(!permissions_.Equals(other.permissions_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      hash ^= permissions_.GetHashCode();
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
      permissions_.WriteTo(output, _repeated_permissions_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      permissions_.WriteTo(ref output, _repeated_permissions_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      size += permissions_.CalculateSize(_repeated_permissions_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(HasManyPermissionRequest other) {
      if (other == null) {
        return;
      }
      permissions_.Add(other.permissions_);
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
          case 10:
          case 8: {
            permissions_.AddEntriesFrom(input, _repeated_permissions_codec);
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
          case 10:
          case 8: {
            permissions_.AddEntriesFrom(ref input, _repeated_permissions_codec);
            break;
          }
        }
      }
    }
    #endif

  }

  public sealed partial class HasManyPermissionResponse : pb::IMessage<HasManyPermissionResponse>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<HasManyPermissionResponse> _parser = new pb::MessageParser<HasManyPermissionResponse>(() => new HasManyPermissionResponse());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<HasManyPermissionResponse> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Decentraland.Kernel.Apis.PermissionsReflection.Descriptor.MessageTypes[3]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasManyPermissionResponse() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasManyPermissionResponse(HasManyPermissionResponse other) : this() {
      hasManyPermission_ = other.hasManyPermission_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HasManyPermissionResponse Clone() {
      return new HasManyPermissionResponse(this);
    }

    /// <summary>Field number for the "has_many_permission" field.</summary>
    public const int HasManyPermissionFieldNumber = 1;
    private static readonly pb::FieldCodec<bool> _repeated_hasManyPermission_codec
        = pb::FieldCodec.ForBool(10);
    private readonly pbc::RepeatedField<bool> hasManyPermission_ = new pbc::RepeatedField<bool>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<bool> HasManyPermission {
      get { return hasManyPermission_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as HasManyPermissionResponse);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(HasManyPermissionResponse other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if(!hasManyPermission_.Equals(other.hasManyPermission_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      hash ^= hasManyPermission_.GetHashCode();
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
      hasManyPermission_.WriteTo(output, _repeated_hasManyPermission_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      hasManyPermission_.WriteTo(ref output, _repeated_hasManyPermission_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      size += hasManyPermission_.CalculateSize(_repeated_hasManyPermission_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(HasManyPermissionResponse other) {
      if (other == null) {
        return;
      }
      hasManyPermission_.Add(other.hasManyPermission_);
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
          case 10:
          case 8: {
            hasManyPermission_.AddEntriesFrom(input, _repeated_hasManyPermission_codec);
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
          case 10:
          case 8: {
            hasManyPermission_.AddEntriesFrom(ref input, _repeated_hasManyPermission_codec);
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
