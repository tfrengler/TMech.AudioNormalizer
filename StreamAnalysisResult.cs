using System.Text.Json.Serialization;

namespace TMech.AudioNormalizer
{
    public sealed record StreamAnalysisResult
    {
        [JsonPropertyName("streams")]
        public Stream[] Streams { get; set; } = [];
    }

    public sealed record Stream
    {
        [JsonPropertyName("codec_name")]
        public string CodecName { get; set; } = string.Empty;

        [JsonPropertyName("sample_rate")]
        public string SampleRate { get; set; } = string.Empty;

        [JsonPropertyName("channels")]
        public string Channels { get; set; } = string.Empty;

        [JsonPropertyName("bit_rate")]
        public string BitRate { get; set; } = string.Empty;
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(StreamAnalysisResult))]
    internal sealed partial class StreamAnalysisResultContext : JsonSerializerContext;
}
