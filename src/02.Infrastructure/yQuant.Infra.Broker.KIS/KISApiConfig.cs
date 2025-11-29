using System.Collections.Generic;

namespace yQuant.Infra.Broker.KIS;

using System.Text.Json;
using System.Text.Json.Serialization;

public class KISApiConfig
{
    public string BaseUrl { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new();

    public static KISApiConfig Load(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            return new KISApiConfig();
        }
        var json = System.IO.File.ReadAllText(path);
        return JsonSerializer.Deserialize<KISApiConfig>(json) ?? new KISApiConfig();
    }

    public bool TryGetValue(string key, out EndpointConfig value)
    {
        value = null;
        if (ExtensionData.TryGetValue(key, out var element))
        {
            try
            {
                value = element.Deserialize<EndpointConfig>();
                return value != null;
            }
            catch
            {
                // Ignore deserialization errors (e.g. if key is not an EndpointConfig)
                return false;
            }
        }
        return false;
    }
}

public class EndpointConfig
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? TrId { get; set; }
    public Dictionary<string, string>? TrIdMap { get; set; }
    public Dictionary<string, ParameterConfig> Parameters { get; set; } = new();
}

public class ParameterConfig
{
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
}
