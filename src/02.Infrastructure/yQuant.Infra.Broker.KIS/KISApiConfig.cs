using System.Collections.Generic;

namespace yQuant.Infra.Broker.KIS;

public class KISApiConfig : Dictionary<string, EndpointConfig>
{
    public string BaseUrl { get; set; } = string.Empty;
}

public class EndpointConfig
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? TrId { get; set; }
    public Dictionary<string, ParameterConfig> Parameters { get; set; } = new();
}

public class ParameterConfig
{
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
}
