// {
//         "input_i" : "-27.61",
//         "input_tp" : "-4.47",
//         "input_lra" : "18.06",
//         "input_thresh" : "-39.20",
//         "output_i" : "-16.58",
//         "output_tp" : "-1.50",
//         "output_lra" : "14.78",
//         "output_thresh" : "-27.71",
//         "normalization_type" : "dynamic",
//         "target_offset" : "0.58"
// }

using System.Text.Json.Serialization;

namespace TMech.AudioNormalizer;

public sealed record LoudnormResult
{
    [JsonRequired]
    [JsonPropertyName("input_i")]
    public string InputIntegratedLoudnessTarget { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("input_tp")]
    public string InputMaxTruePeak { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("input_lra")]
    public string InputLoudnessRangeTarget { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("input_thresh")]
    public string InputThreshold { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("output_i")]
    public string OutputIntegratedLoudnessTarget { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("output_tp")]
    public string OutputMaxTruePeak { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("output_lra")]
    public string OutputLoudnessRangeTarget { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("output_thresh")]
    public string OutputThreshold { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("normalization_type")]
    public string NormalizationType { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("target_offset")]
    public string TargetOffsetGain { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(LoudnormResult))]
internal sealed partial class LoudnormResultContext : JsonSerializerContext;