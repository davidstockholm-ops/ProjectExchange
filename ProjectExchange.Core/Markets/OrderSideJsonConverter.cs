using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// Allows OrderSide to be parsed as both string ("Buy", "Sell") and integer (0 = Buy, 1 = Sell) to avoid JSON conversion errors.
/// </summary>
public sealed class OrderSideJsonConverter : JsonConverter<OrderSide>
{
    /// <inheritdoc />
    public override OrderSide Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var n))
                    return n == 0 ? OrderSide.Buy : OrderSide.Sell;
                break;
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    break;
                if (Enum.TryParse<OrderSide>(s.Trim(), ignoreCase: true, out var parsed))
                    return parsed;
                break;
        }

        throw new JsonException($"Unable to convert to OrderSide. Expected string (\"Buy\", \"Sell\") or number (0, 1), got {reader.TokenType}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OrderSide value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
