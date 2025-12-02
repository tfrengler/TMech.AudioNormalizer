using System.Collections.Generic;
using System.Diagnostics;
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
        private long _processedFiles;
        private long _failed;

        public AudioNormalizer(FfmpegService ffmpeg, List<FileInfo> audioFiles)
        {
            System.ArgumentNullException.ThrowIfNull(ffmpeg);

            _inputFiles = audioFiles;
            _ffmpeg = ffmpeg;
            _semaphore = new SemaphoreSlim(4, 4);
            _processedFiles = 0;
        }

        public async Task Start()
        {
            if (_inputFiles.Count == 0)
            {
                return;
            }

            var timer = Stopwatch.StartNew();
            (_, int progressOutputLine) = System.Console.GetCursorPosition();

            var cts = new CancellationTokenSource();
            var reportTask = new Task(() => {
            
                while (!cts.IsCancellationRequested)
                {
                    (int currentLeft, int currentLine) = System.Console.GetCursorPosition();
                    long processedCount = Interlocked.Read(ref _processedFiles);
            
                    System.Console.SetCursorPosition(0, progressOutputLine);
                    System.Console.Write(string.Empty.PadRight(System.Console.BufferWidth));
                    System.Console.SetCursorPosition(0, progressOutputLine);
                    System.Console.Write($"{processedCount} files processed...");
            
                    System.Console.SetCursorPosition(currentLeft, currentLine);
                    Thread.Sleep(1000);
                }
            }, cts.Token);
            reportTask.Start();

            var tasks = new List<Task>();

            foreach(var currentInputFile in _inputFiles)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _semaphore.WaitAsync();

                    try
                    {
                        if (!await ProcessAudioFile(currentInputFile)) {
                            Interlocked.Increment(ref _failed);
                        }
                    }
                    finally
                    {
                        Interlocked.Increment(ref _processedFiles);
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            cts.Cancel();
            await reportTask;

            (_, int currentLine) = System.Console.GetCursorPosition();
            System.Console.SetCursorPosition(0, currentLine);
            System.Console.Write(string.Empty.PadRight(System.Console.BufferWidth));
            System.Console.SetCursorPosition(0, currentLine);

            string finalLogOutput = $"All done. Time taken: {timer.Elapsed} ({_failed} file(s) failed)";
            System.Console.WriteLine(finalLogOutput);
            await Program.Log.Emit(LogMessage.Info(finalLogOutput));
        }

        private static bool IsWithinLoudnessThreshold(LoudnormResult loudnormResult)
        {
            double actualLoudness = System.Convert.ToDouble(loudnormResult.InputIntegratedLoudnessTarget);
            double targetLoudness = System.Convert.ToDouble(FfmpegService.IntegratedLoudnessTarget);
            return actualLoudness > (targetLoudness - 1.0d) && actualLoudness < (targetLoudness + 1.0d);
        }

        private static void RenameFile(FileInfo target, FileInfo current)
        {
            string outputPathAndName = Path.Combine(Program.Config.OutputDir.FullName, target.Name);
            current.MoveTo(outputPathAndName);
        }

        private async Task<bool> ProcessAudioFile(FileInfo inputFile)
        {
            var logOutput = new List<LogMessage>()
            {
                LogMessage.Info($"-------------Begin processing {inputFile.FullName}"),
                LogMessage.Info("Analyzing audio stream")
            };
            var processTimer = Stopwatch.StartNew();
            var taskTimer = Stopwatch.StartNew();

            try
            {
                var analyzeAudioStreamResult = await _ffmpeg.AnalyzeAudioStream(inputFile);
                logOutput.Add(LogMessage.Info($"Done ({taskTimer.Elapsed})"));
                taskTimer.Restart();

                if (!analyzeAudioStreamResult.Success)
                {
                    logOutput.Add(LogMessage.Error(analyzeAudioStreamResult.Output));
                    return false;
                }
                Debug.Assert(analyzeAudioStreamResult.Data is not null);

                logOutput.Add(
                    LogMessage.Debug(
                        JsonSerializer.Serialize(
                            analyzeAudioStreamResult.Data,
                            StreamAnalysisResultContext.Default.StreamAnalysisResult)
                    )
                );

                logOutput.Add(LogMessage.Info("Analyzing loudness"));
                var analyzeLoudnessResult = await _ffmpeg.AnalyzeLoudness(inputFile);

                if (!analyzeLoudnessResult.Success)
                {
                    logOutput.Add(LogMessage.Error(analyzeLoudnessResult.Output));
                    return false;
                }
                Debug.Assert(analyzeLoudnessResult.Data is not null);

                logOutput.Add(LogMessage.Info($"Done ({taskTimer.Elapsed})"));
                taskTimer.Restart();

                logOutput.Add(
                    LogMessage.Debug(
                        JsonSerializer.Serialize(
                            analyzeLoudnessResult.Data,
                            LoudnormResultContext.Default.LoudnormResult)
                    )
                );

                logOutput.Add(LogMessage.Info("Stripping existing ReplayGain tags"));
                var stripReplayGainTagsResult = await _ffmpeg.StripReplayGainTags(inputFile);

                if (!stripReplayGainTagsResult.Success)
                {
                    logOutput.Add(LogMessage.Error(stripReplayGainTagsResult.Output));
                    return false;
                }
                Debug.Assert(stripReplayGainTagsResult.Data is not null);

                logOutput.Add(LogMessage.Info($"Done ({taskTimer.Elapsed})"));
                taskTimer.Restart();

                if (IsWithinLoudnessThreshold(analyzeLoudnessResult.Data))
                {
                    logOutput.Add(LogMessage.Info($"Audio loudness is within threshold ({analyzeLoudnessResult.Data.InputIntegratedLoudnessTarget}), skipping normalization"));
                    RenameFile(inputFile, stripReplayGainTagsResult.Data);
                    return true;
                }
                
                logOutput.Add(LogMessage.Info("Applying loudness normalization"));

                var normalizeLoudnessResult = await _ffmpeg.NormalizeLoudness(
                    stripReplayGainTagsResult.Data,
                    analyzeAudioStreamResult.Data,
                    analyzeLoudnessResult.Data
                );

                logOutput.Add(LogMessage.Info($"Done ({taskTimer.Elapsed})"));
                stripReplayGainTagsResult.Data?.Delete();

                if (!normalizeLoudnessResult.Success)
                {
                    logOutput.Add(LogMessage.Error(normalizeLoudnessResult.Output));
                    return false;
                }
                Debug.Assert(normalizeLoudnessResult.Data is not null);

                logOutput.Add(LogMessage.Info($"Done processing file ({processTimer.Elapsed})"));
                RenameFile(inputFile, normalizeLoudnessResult.Data);

                return true;
            }
            finally
            {
                await Program.Log.Emit(logOutput);
            }
        }
    }
}
