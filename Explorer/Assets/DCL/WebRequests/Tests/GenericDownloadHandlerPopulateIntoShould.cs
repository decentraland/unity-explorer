using DCL.Notifications.Serialization;
using DCL.NotificationsBus.NotificationTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace DCL.WebRequests.Tests
{
    public class GenericDownloadHandlerPopulateIntoShould
    {
        private const string NOTIFICATIONS_PAYLOAD =
            "{\"notifications\":[{\"id\":\"n-1\",\"type\":\"events_started\"},{\"id\":\"n-2\",\"type\":\"badge_granted\"}]}";

        private static JsonSerializer NewNotificationsSerializer() =>
            JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                Converters = new JsonConverter[] { new NotificationJsonDtoConverter(true) },
            });

        private static void PopulateIntoBuffer(List<INotification> buffer, JsonSerializer serializer)
        {
            using var textReader = new StringReader(NOTIFICATIONS_PAYLOAD);
            using var jsonReader = new JsonTextReader(textReader);
            GenericDownloadHandlerUtils.PopulateInto(jsonReader, buffer, serializer);
        }

        [Test]
        public void RouteRootLevelConverterIntoProvidedBuffer()
        {
            var buffer = new List<INotification>();
            JsonSerializer serializer = NewNotificationsSerializer();

            PopulateIntoBuffer(buffer, serializer);

            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer[0], Is.InstanceOf<EventStartedNotification>());
            Assert.That(buffer[1], Is.InstanceOf<BadgeGrantedNotification>());
            Assert.That(buffer[0].Id, Is.EqualTo("n-1"));
        }

        [Test]
        public void NotDuplicateItemsWhenBufferIsClearedBetweenReuses()
        {
            var buffer = new List<INotification>();
            JsonSerializer serializer = NewNotificationsSerializer();

            PopulateIntoBuffer(buffer, serializer);
            buffer.Clear();
            PopulateIntoBuffer(buffer, serializer);

            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        [Test]
        public void ThrowWhenConverterReturnsFreshInstanceInsteadOfPopulatingTarget()
        {
            var buffer = new List<INotification>();

            JsonSerializer serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                Converters = new JsonConverter[] { new FreshInstanceListConverter() },
            });

            using var textReader = new StringReader(NOTIFICATIONS_PAYLOAD);
            using var jsonReader = new JsonTextReader(textReader);

            Assert.Throws<JsonSerializationException>(() => GenericDownloadHandlerUtils.PopulateInto(jsonReader, buffer, serializer));
        }

        [Test]
        public void LeaveBufferEmptyOnNullBodyViaConverterNullGuard()
        {
            var buffer = new List<INotification>();
            JsonSerializer serializer = NewNotificationsSerializer();

            using var textReader = new StringReader("null");
            using var jsonReader = new JsonTextReader(textReader);
            GenericDownloadHandlerUtils.PopulateInto(jsonReader, buffer, serializer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void FallBackToPopulateWhenNoConverterMatchesRootType()
        {
            var target = new PlainDto();
            JsonSerializer serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                Converters = new JsonConverter[] { new NotificationJsonDtoConverter(true) },
            });

            using var textReader = new StringReader("{\"value\":42,\"name\":\"populated\"}");
            using var jsonReader = new JsonTextReader(textReader);
            GenericDownloadHandlerUtils.PopulateInto(jsonReader, target, serializer);

            Assert.That(target.value, Is.EqualTo(42));
            Assert.That(target.name, Is.EqualTo("populated"));
        }

        // The standard converter shape: allocates a fresh result instead of filling existingValue
        private class FreshInstanceListConverter : JsonConverter<List<INotification>>
        {
            public override List<INotification>? ReadJson(JsonReader reader, Type objectType, List<INotification>? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                JObject.Load(reader);
                return new List<INotification>();
            }

            public override void WriteJson(JsonWriter writer, List<INotification>? value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

        private class PlainDto
        {
            // Field names mirror the JSON wire format
            // ReSharper disable InconsistentNaming
            public int value;
            public string? name;

            // ReSharper restore InconsistentNaming
        }
    }
}
