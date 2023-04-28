using CrdtEcsBridge.Serialization;
using ECS.ComponentsPooling;
using Google.Protobuf;
using System;

namespace CrdtEcsBridge.Components
{
    public static class SDKComponentBuilderExtensions
    {
        public static ref SDKComponentBuilder<T> WithProtobufSerializer<T>(this ref SDKComponentBuilder<T> sdkComponentBuilder)
            where T: class, IMessage<T>, new()
        {
            sdkComponentBuilder.serializer = new ProtobufSerializer<T>();
            return ref sdkComponentBuilder;
        }

        public static ref SDKComponentBuilder<T> WithCustomSerializer<T>(this ref SDKComponentBuilder<T> sdkComponentBuilder, IComponentSerializer<T> serializer) where T: class, new()
        {
            sdkComponentBuilder.serializer = serializer;
            return ref sdkComponentBuilder;
        }

        public static ref SDKComponentBuilder<T> WithPool<T>(this ref SDKComponentBuilder<T> sdkComponentBuilder, Action<T> onRelease = null) where T: class, new()
        {
            sdkComponentBuilder.pool = new ComponentPool<T>(onRelease);
            return ref sdkComponentBuilder;
        }
    }
}
