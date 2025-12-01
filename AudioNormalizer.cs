using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer
{
    public sealed class AudioNormalizer
    {
        private readonly FfmpegService _ffmpeg;
        private readonly List<FileInfo> _inputFiles;
        private readonly SemaphoreSlim _semaphore;
        private long _processedFiles;
        private long _failed;

        public AudioNormalizer(FfmpegService ffmpeg, List<FileInfo> audioFiles)
        {
            System.ArgumentNullException.ThrowIfNull(ffmpeg);

            _inputFiles = audioFiles;
            _ffmpeg = ffmpeg;
            _semaphore = new SemaphoreSlim(1, 1);
            _processedFiles = 0;
        }

        public async Task Start()
        {
            if (_inputFiles.Count == 0)
            {
                return;
            }

            var timer = Stopwatch.StartNew();
            long cancelReport = 0;

            // Task reportTask = Task.Run(() => {
            //     (int top, _) = System.Console.GetCursorPosition();

            //     while(Interlocked.Read(ref cancelReport) == 0) {
            //         long processedCount = Interlocked.Read(ref _processedFiles);
            //         System.Console.WriteLine($"{processedCount} files processed out of {_inputFiles.Count}");
            //         Thread.Sleep(1000);
            //         System.Console.SetCursorPosition(0, top);
            //         System.Console.WriteLine(string.Empty.PadRight(System.Console.BufferWidth));
            //     }

            //     System.Console.SetCursorPosition(0, top);
            //     System.Console.WriteLine(string.Empty.PadRight(System.Console.BufferWidth));
            // });

            var tasks = new List<Task>();

            foreach(var currentInputFile in _inputFiles)
            {
                tasks.Add(Task.Run(async () =>
                {
                    System.Console.WriteLine("Waiting to enter semaphore...");
                    await _semaphore.WaitAsync();

                    System.Console.WriteLine($"File '{currentInputFile.Name}' started processing on thread {System.Environment.CurrentManagedThreadId}");

                    try
                    {
                        bool success = await ProcessAudioFile(currentInputFile);
                        System.Console.WriteLine($"ProcessAudioFile result: {success}");
                        if (!success) {
                            Interlocked.Increment(ref _failed);
                        }
                    }
                    catch(System.Exception error)
                    {
                        System.Console.WriteLine("Thread errored: " + error.Message);
                    }
                    finally
                    {
                        System.Console.WriteLine("Releasing semaphore");
                        Interlocked.Increment(ref _processedFiles);
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            // Interlocked.Exchange(ref cancelReport, 1);

            // await reportTask;
            System.Console.WriteLine($"Done. Time taken: {timer.Elapsed}");
        }

        private async Task<bool> ProcessAudioFile(FileInfo inputFile)
        {
            System.Console.WriteLine("Analyzing audio stream...");
            var analyzeAudioStreamResult = await _ffmpeg.AnalyzeAudioStream(inputFile);

            if (!analyzeAudioStreamResult.Success)
            {
                System.Console.WriteLine(analyzeAudioStreamResult.Output);
                return false;
            }

            // await Program.Log.Info(JsonSerializer.Serialize(analyzeAudioStreamResult.Data, StreamAnalysisResultContext.Default.StreamAnalysisResult));

            System.Console.WriteLine("Analyzing loudness");
            var analyzeLoudnessResult = await _ffmpeg.AnalyzeLoudness(inputFile);

            if (!analyzeLoudnessResult.Success)
            {
                System.Console.WriteLine(analyzeLoudnessResult.Output);
                return false;
            }

            // await Program.Log.Info(JsonSerializer.Serialize(analyzeLoudnessResult.Data, LoudnormResultContext.Default.LoudnormResult));

            System.Console.WriteLine("Stripping existing ReplayGain tags");
            var stripReplayGainTagsResult = await _ffmpeg.StripReplayGainTags(inputFile);

            if (!stripReplayGainTagsResult.Success)
            {
                System.Console.WriteLine(stripReplayGainTagsResult.Output);
                return false;
            }

            System.Console.WriteLine("Applying loudness normalization");
            var normalizeLoudnessResult = await _ffmpeg.NormalizeLoudness(
                stripReplayGainTagsResult.Data!,
                analyzeAudioStreamResult.Data!,
                analyzeLoudnessResult.Data!
            );

            stripReplayGainTagsResult.Data?.Delete();

            if (!normalizeLoudnessResult.Success)
            {
                System.Console.WriteLine(normalizeLoudnessResult.Output);
                return false;
            }

            System.Console.WriteLine("Done processing file");
            return true;
        }
    }
}
