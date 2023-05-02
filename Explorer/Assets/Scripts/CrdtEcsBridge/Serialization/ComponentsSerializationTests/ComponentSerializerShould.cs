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
    }
}
