using CrdtEcsBridge.Serialization;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
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
            sdkComponentBuilder.pool = new ComponentPool.WithDefaultCtor<T>(onGet: onGet, onRelease: onRelease);
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
            where T: class, IMessage<T>, IDirtyMarker, new()
        {
            // We clear "on get" because it's called from the background thread unlike "on release"
            Action<T> onGet = SetAsDirty;
            onGet += ClearProtobufComponent;

            return sdkComponentBuilder.WithProtobufSerializer()
                                      .WithPool(onGet)
                                      .Build();
        }

        /// <summary>
        ///     A shortcut to create a standard suite for Protobuf component which is added as result from Renderer
        /// </summary>
        public static SDKComponentBridge AsProtobufResult<T>(this SDKComponentBuilder<T> sdkComponentBuilder)
            where T: class, IMessage<T>, new() =>
            sdkComponentBuilder.WithProtobufSerializer()
                               .WithPool(ClearProtobufComponent)
                               .AsResult()
                               .Build();

        public static SDKComponentBuilder<T> AsResult<T>(this SDKComponentBuilder<T> sdkComponentBuilder) where T: class, IMessage<T>, new()
        {
            sdkComponentBuilder.isResultComponent = true;
            return sdkComponentBuilder;
        }

        public static void SetAsDirty(IDirtyMarker dirtyMarker) =>
            dirtyMarker.IsDirty = true;

        private static void ClearProtobufComponent<T>(T component) where T: class, IMessage<T>, new()
        {
            IList<FieldDescriptor> fields = component.Descriptor.Fields.InDeclarationOrder();

            for (var i = 0; i < fields.Count; i++)
                fields[i].Accessor.Clear(component);
        }
    }
}
