using DCL.ECSComponents;
using Google.Protobuf;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.Serialization.ComponentsSerializationTests
{
    public class ComponentSerializerShould
    {
        [Test]
        public void DeserializeIntoExistingProtobufInstance()
        {
            var serializer = new ProtobufSerializer<PBMeshCollider>();

            var message = new PBMeshCollider
            {
                Sphere = new PBMeshCollider.Types.SphereMesh(),
                CollisionMask = 0b10100
            };

            var byteArray = message.ToByteArray();

            var newMessage = new PBMeshCollider();

            serializer.DeserializeInto(newMessage, byteArray.AsSpan());
            Assert.AreEqual(message, newMessage);
        }

        [Test]
        public void SerializeProtobufInstance()
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
            ReadOnlyMemory<byte> serializedSpanArray = serializer.Serialize(message);

            //Asert
            Assert.AreEqual(byteArray.Length, serializedSpanArray.Length);

            //Arrange
            var newMessage1 = new PBMeshCollider();
            var newMessage2 = new PBMeshCollider();

            //Act
            serializer.DeserializeInto(newMessage1, serializedSpanArray.Span);
            serializer.DeserializeInto(newMessage2, byteArray);

            //Assert
            Assert.AreEqual(newMessage1, newMessage2);
        }
    }
}
