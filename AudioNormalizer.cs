using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer
{
    public sealed class AudioNormalizer
    {
        private readonly FfmpegService _ffmpeg;
        private readonly List<FileInfo> _inputFiles;
        private readonly SemaphoreSlim _semaphore;

        public AudioNormalizer(FfmpegService ffmpeg, List<FileInfo> audioFiles)
        {
            System.ArgumentNullException.ThrowIfNull(ffmpeg);

            _inputFiles = audioFiles;
            _ffmpeg = ffmpeg;
            _semaphore = new SemaphoreSlim(4, 4);
        }

        public async Task Start()
        {
            if (_inputFiles.Count == 0)
            {
                return;
            }

            var tasks = new List<Task>();

            foreach(var currentInputFile in _inputFiles)
            {
                await _semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    System.Console.WriteLine($"[DBG]: File '{currentInputFile.Name}' started processing on thread {System.Environment.CurrentManagedThreadId}");
                    try
                    {
                        await ProcessAudioFile(currentInputFile);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessAudioFile(FileInfo inputFile)
        {
            System.Console.WriteLine("Analyzing audio stream...");
            var analyzeAudioStreamResult = await _ffmpeg.AnalyzeAudioStream(inputFile);
            System.Console.WriteLine(JsonSerializer.Serialize(analyzeAudioStreamResult, StreamAnalysisResultContext.Default.StreamAnalysisResult));

            if (!analyzeAudioStreamResult.Success)
            {
                return;
            }

            System.Console.WriteLine("Analyzing loudness...");
            var analyzeLoudnessResult = await _ffmpeg.AnalyzeLoudness(inputFile);
            System.Console.WriteLine(JsonSerializer.Serialize(analyzeLoudnessResult, LoudnormResultContext.Default.LoudnormResult));

            if (!analyzeLoudnessResult.Success)
            {
                return;
            }

            System.Console.WriteLine("Stripping existing ReplayGain tags...");
            var stripReplayGainTagsResult = await _ffmpeg.StripReplayGainTags(inputFile);

            if (!stripReplayGainTagsResult.Success)
            {
                return;
            }

            System.Console.WriteLine("Applying loudness normalization...");
            var normalizeLoudnessResult = await _ffmpeg.NormalizeLoudness(
                stripReplayGainTagsResult.Data!,
                analyzeAudioStreamResult.Data!,
                analyzeLoudnessResult.Data!
            );

            if (!normalizeLoudnessResult.Success)
            {
                return;
            }

            stripReplayGainTagsResult.Data?.Delete();
        }
    }
}
