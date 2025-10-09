using DCL.Backpack.AvatarSection.Outfits.Repository; // Your namespace may vary
using Newtonsoft.Json;
using System;
using DCL.Backpack.AvatarSection.Outfits.Models;

public class OutfitsMetadataConverter : JsonConverter<OutfitsMetadata>
{
    /// <summary>
    ///     Manually writes the OutfitsMetadata object to JSON, ensuring all properties are included.
    ///     This is required for deploying the entity to the Catalyst network.
    /// </summary>
    public override void WriteJson(JsonWriter writer, OutfitsMetadata value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // Main object: { ... }
        writer.WriteStartObject();

        // "outfits": [ ... ]
        writer.WritePropertyName("outfits");
        serializer.Serialize(writer, value.outfits); // Let the serializer handle the list of objects

        // "namesForExtraSlots": [ ... ]
        writer.WritePropertyName("namesForExtraSlots");
        serializer.Serialize(writer, value.namesForExtraSlots); // Let the serializer handle the list of strings

        // End of main object
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Reading JSON is not needed for the deployment process, so this is not implemented.
    ///     The GET /outfits endpoint is handled separately in the OutfitsService.
    /// </summary>
    public override OutfitsMetadata ReadJson(JsonReader reader, Type objectType, OutfitsMetadata existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException("This converter is only for writing (serialization).");
    }
}