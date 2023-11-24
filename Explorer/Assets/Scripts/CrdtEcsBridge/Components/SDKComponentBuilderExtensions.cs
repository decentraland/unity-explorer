using CrdtEcsBridge.Serialization;
using DCL.ECSComponents;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    public static class SDKComponentBuilderExtensions
    {
        /// <summary>
        ///     It is possible to provide an `IMessage` with a default Protobuf serializer
        /// </summary>
        public static SDKComponentBuilder<T> WithProtobufSerializer<T>(this SDKComponentBuilder<T> sdkComponentBuilder)
            where T: class, IMessage<T>, new()
        {
            sdkComponentBuilder.serializer = new ProtobufSerializer<T>();
            return sdkComponentBuilder;
        }

        /// <summary>
        ///     Add a custom serializer for components that are not Protobuf messages
        /// </summary>
        public static SDKComponentBuilder<T> WithCustomSerializer<T>(this SDKComponentBuilder<T> sdkComponentBuilder, IComponentSerializer<T> serializer) where T: class, new()
        {
            sdkComponentBuilder.serializer = serializer;
            return sdkComponentBuilder;
        }

        /// <summary>
        ///     Provide a default pool behavior for SDK components, it is a must
        /// </summary>
        public static SDKComponentBuilder<T> WithPool<T>(this SDKComponentBuilder<T> sdkComponentBuilder, Action<T> onGet = null, Action<T> onRelease = null) where T: class, new()
        {
            sdkComponentBuilder.pool = new ComponentPool<T>(onGet: onGet, onRelease: onRelease);
            return sdkComponentBuilder;
        }

        /// <summary>
        ///     Provide a custom pool behavior for SDK components, it is a must
        /// </summary>
        public static SDKComponentBuilder<T> WithPool<T>(this SDKComponentBuilder<T> sdkComponentBuilder, IComponentPool<T> componentPool) where T: class, new()
        {
            sdkComponentBuilder.pool = componentPool;
            return sdkComponentBuilder;
        }

        /// <summary>
        ///     A shortcut to create a standard suite for Protobuf components
        /// </summary>
        /// <returns></returns>
        public static SDKComponentBridge AsProtobufComponent<T>(this SDKComponentBuilder<T> sdkComponentBuilder)
            where T: class, IMessage<T>, IDirtyMarker, new() =>
            sdkComponentBuilder.WithProtobufSerializer()
                               .WithPool(SetAsDirty)
                               .Build();

        /// <summary>
        ///     A shortcut to create a standard suite for Protobuf component which is added as result from Renderer
        /// </summary>
        public static SDKComponentBridge AsProtobufResult<T>(this SDKComponentBuilder<T> sdkComponentBuilder)
            where T: class, IMessage<T>, new() =>
            sdkComponentBuilder.WithProtobufSerializer()
                               .WithPool(ClearProtobufComponent)
                               .Build();

        public static void SetAsDirty(IDirtyMarker dirtyMarker) =>
            dirtyMarker.IsDirty = true;

        public static void ClearProtobufComponent<T>(T component) where T: class, IMessage<T>, new()
        {
            IList<FieldDescriptor> fields = component.Descriptor.Fields.InDeclarationOrder();

            for (var i = 0; i < fields.Count; i++)
                fields[i].Accessor.Clear(component);
        }
    }
}
