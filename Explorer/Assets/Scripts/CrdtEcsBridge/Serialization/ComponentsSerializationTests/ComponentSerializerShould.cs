using DCL.ECSComponents;
using Google.Protobuf;
using NUnit.Framework;
using System;
using System.Buffers;

namespace CrdtEcsBridge.Serialization.ComponentsSerializationTests
{
    public class ComponentSerializerShould
    {

        public void DeserializeIntoExistingProtobufInstance()
        {
            var serializer = new ProtobufSerializer<PBMeshCollider>();

            var message = new PBMeshCollider
            {
                Sphere = new PBMeshCollider.Types.SphereMesh(),
                CollisionMask = 0b10100,
            };

            byte[] byteArray = message.ToByteArray();

            var newMessage = new PBMeshCollider();

            serializer.DeserializeInto(newMessage, byteArray.AsSpan());
            Assert.AreEqual(message, newMessage);
        }


        public void SerializeIntoProtobufSpan()
        {
            //Arrange
            var serializer = new ProtobufSerializer<PBMeshCollider>();

            var message = new PBMeshCollider
            {
                Sphere = new PBMeshCollider.Types.SphereMesh(),
                CollisionMask = 0b10100,
            };

            Span<byte> byteArray = message.ToByteArray().AsSpan();

            //Act
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(message.CalculateSize());
            var memoryBuffer = new Memory<byte>(rentedArray, 0, message.CalculateSize());
            serializer.SerializeInto(message, memoryBuffer.Span);

            //Asert
            Assert.AreEqual(byteArray.Length, memoryBuffer.Span.Length);
        }


        public void SerializeAndDeserialize()
        {
            //Arrange
            var serializer = new ProtobufSerializer<PBMeshCollider>();

            var message = new PBMeshCollider
            {
                Sphere = new PBMeshCollider.Types.SphereMesh(),
                CollisionMask = 0b10100,
            };

            //Act
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(message.CalculateSize());
            var memoryBuffer = new Memory<byte>(rentedArray, 0, message.CalculateSize());
            serializer.SerializeInto(message, memoryBuffer.Span);

            var deserializedMessage = new PBMeshCollider();
            serializer.DeserializeInto(deserializedMessage, memoryBuffer.Span);

            //Assert
            Assert.AreEqual(message, deserializedMessage);
        }
    }
}
