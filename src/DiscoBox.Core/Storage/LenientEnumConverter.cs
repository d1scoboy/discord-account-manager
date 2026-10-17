using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscoBox.Core.Storage;

public class LenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToRead, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (T)Enum.ToObject(typeof(T), reader.GetInt32());
        }

        var text = reader.GetString();
        if (text is not null && Enum.TryParse<T>(text, true, out var parsed))
        {
            return parsed;
        }

        if (text == "Free" && typeof(T) == typeof(Models.ProxyStatus))
        {
            return (T)(object)Models.ProxyStatus.Valid;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
