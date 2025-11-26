namespace TMech.AudioNormalizer
{
    public sealed record FfmpegResult<T>
    {
        public bool Success { get; init; }
        public string Output { get; init; } = string.Empty;
        public T? Data { get; init; }
    }
}
