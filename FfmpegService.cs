using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer;

public sealed class FfmpegService
{
    private readonly string _ffmpegCommand;
    private readonly string _ffprobeCommand;
    private readonly string _outputDir;

    public const string IntegratedLoudnessTarget = "-16.0";
    public const string MaxTruePeak = "-1.5";
    public const string LoudnessRangeTarget = "11.0";

    public FfmpegService(string ffmpegCommand, string ffprobeCommand, string outputDir)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegCommand);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(ffprobeCommand);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

        _ffmpegCommand = ffmpegCommand;
        _ffprobeCommand = ffprobeCommand;
        _outputDir = outputDir;
    }

    public static async Task<bool> IsFfmpegAvailable(string commandName)
    {
        ProcessResult result = await ProcessHelper
            .Create(commandName)
            .WithTimeout(System.TimeSpan.FromSeconds(5.0d))
            .WithArgument("-version")
            .Run();

        string firstLine = result.Output.FirstOrDefault() ?? string.Empty;
        return result.Status == ProcessStatus.OK && firstLine.StartsWith($"{commandName} version");
    }

    public async Task<FfmpegResult<LoudnormResult>> AnalyzeLoudness(FileInfo file)
    {
        // ffmpeg -i input.ext -af loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json -f null -
        ProcessResult result = await ProcessHelper
            .Create(_ffmpegCommand)
            .WithTimeout(System.TimeSpan.FromSeconds(180.0d))
            .WithArgument("-i")
            .WithArgument(file.FullName)
            .WithArgument("-af")
            .WithArgument($"loudnorm=I={IntegratedLoudnessTarget}:TP={MaxTruePeak}:LRA={LoudnessRangeTarget}:print_format=json")
            .WithArgument("-f")
            .WithArgument("null")
            .WithArgument("-")
            .Run();

        var output = result.Errors;

        if (result.Status != ProcessStatus.OK)
        {
            return new()
            {
                Success = false,
                Output = "Error while analyzing loudness: ffmpeg process failed or could not be started. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        int loudnormResultStartIndex = -1;
        for (int index = 0; index < output.Count; index++)
        {
            if (output[index][0] == '{')
            {
                loudnormResultStartIndex = index;
                break;
            }
        }

        if (loudnormResultStartIndex == -1)
        {
            return new()
            {
                Success = false,
                Output = "Error while analyzing loudness: ffmpeg output does not appear to contain analysis results. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        IEnumerable<string> jsonParts = new System.ArraySegment<string>(
            output.ToArray(),
            loudnormResultStartIndex,
            12
        );

        if (jsonParts.Last() != "}")
        {
            return new()
            {
                Success = false,
                Output = "Error while analyzing loudness: ffmpeg output is malformed or not in the format we expected. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        string jsonAsString = string.Concat(jsonParts);
        LoudnormResult? returnData = JsonSerializer.Deserialize(jsonAsString, LoudnormResultContext.Default.LoudnormResult);

        Debug.Assert(returnData is not null);
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.InputIntegratedLoudnessTarget));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.InputMaxTruePeak));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.InputLoudnessRangeTarget));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.InputThreshold));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.OutputIntegratedLoudnessTarget));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.OutputMaxTruePeak));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.OutputLoudnessRangeTarget));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.OutputThreshold));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.NormalizationType));
        Debug.Assert(!string.IsNullOrWhiteSpace(returnData.TargetOffsetGain));

        return new()
        {
            Success = true,
            Data = returnData
        };
    }

    public async Task<FfmpegResult<StreamAnalysisResult>> AnalyzeAudioStream(FileInfo file)
    {
        // ffprobe -v error -select_streams a:0 -show_entries stream=codec_name,bit_rate,sample_rate,channels -of json
        ProcessResult result = await ProcessHelper
            .Create(_ffprobeCommand)
            .WithTimeout(System.TimeSpan.FromSeconds(10.0d))
            .WithArgument("-v")
            .WithArgument("error")
            .WithArgument("-select_streams")
            .WithArgument("a:0")
            .WithArgument("-show_entries")
            .WithArgument("stream=codec_name,bit_rate,sample_rate,channels")
            .WithArgument("-of")
            .WithArgument("json")
            .WithArgument(file.FullName)
            .Run();

        var output = result.Output;

        if (result.Status != ProcessStatus.OK)
        {
            var errorOutput = new List<string>();
            errorOutput.AddRange(result.Output);
            errorOutput.AddRange(result.Errors);

            return new()
            {
                Success = false,
                Output = "Error analysing audio stream: ffprobe process failed or could not be started. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, errorOutput)
            };
        }

        StreamAnalysisResult returnData;

        try
        {
            returnData = JsonSerializer.Deserialize(
                string.Concat(output),
                StreamAnalysisResultContext.Default.StreamAnalysisResult
            ) ?? new();
        }
        catch(JsonException)
        {
            var errorOutput = new List<string>();
            errorOutput.AddRange(result.Output);
            errorOutput.AddRange(result.Errors);

            return new()
            {
                Success = false,
                Output = "Error analysing audio stream: ffprobe did not return a usable result (audio file is unreadable, corrupt or perhaps not an actual audio file). "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, errorOutput)
            };
        }

        Debug.Assert(returnData.Streams is not null);
        //Debug.Assert(returnData.Streams.Length > 0);
        //Debug.Assert(!string.IsNullOrWhiteSpace(returnData.Streams[0].CodecName));
        //Debug.Assert(!string.IsNullOrWhiteSpace(returnData.Streams[0].SampleRate));
        //Debug.Assert(!string.IsNullOrWhiteSpace(returnData.Streams[0].BitRate));
        //Debug.Assert(returnData.Streams[0].Channels > 0);

        return new()
        {
            Success = true,
            Data = returnData
        };
    }

    public async Task<FfmpegResult<FileInfo>> StripReplayGainTags(FileInfo file)
    {
        string outputFile = Path.Combine(_outputDir, "Stripped_" + file.Name);
        var returnData = new FileInfo(outputFile);

        if (returnData.Exists)
        {
            returnData.Delete();
        }

        // ffmpeg -i input.ext -map_metadata 0 -metadata REPLAYGAIN_TRACK_GAIN= -metadata REPLAYGAIN_ALBUM_GAIN= -metadata REPLAYGAIN_TRACK_PEAK= -metadata REPLAYGAIN_ALBUM_PEAK= -c copy cleaned.ext
        ProcessResult result = await ProcessHelper
            .Create(_ffmpegCommand)
            .WithTimeout(System.TimeSpan.FromSeconds(60.0d))
            .WithArgument("-i")
            .WithArgument(file.FullName)
            .WithArgument("-map_metadata")
            .WithArgument("0")
            .WithArgument("-metadata")
            .WithArgument("REPLAYGAIN_TRACK_GAIN=")
            .WithArgument("-metadata")
            .WithArgument("REPLAYGAIN_ALBUM_GAIN=")
            .WithArgument("-metadata")
            .WithArgument("REPLAYGAIN_TRACK_PEAK=")
            .WithArgument("-metadata")
            .WithArgument("REPLAYGAIN_ALBUM_PEAK=")
            .WithArgument("-c")
            .WithArgument("copy")
            .WithArgument(outputFile)
            .Run();

        var output = result.Errors;

        if (result.Status != ProcessStatus.OK)
        {
            return new()
            {
                Success = false,
                Output = "Error removing ReplayGain tags: ffmpeg process failed or could not be started. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        if (!File.Exists(outputFile))
        {
            return new()
            {
                Success = false,
                Output = "Error removing ReplayGain tags: no output file was generated by ffmpeg thus indicating an error. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        return new()
        {
            Success = true,
            Data = returnData
        };
    }

    public async Task<FfmpegResult<FileInfo>> NormalizeLoudness(FileInfo file, StreamAnalysisResult streamData, LoudnormResult loudnessData)
    {
        System.ArgumentNullException.ThrowIfNull(file);
        System.ArgumentNullException.ThrowIfNull(streamData);
        System.ArgumentNullException.ThrowIfNull(loudnessData);

        string outputFile = Path.Combine(_outputDir, "Normalized_" + file.Name);
        var returnData = new FileInfo(outputFile);

        if (returnData.Exists)
        {
            returnData.Delete();
        }

        string[] loudnormInputData =
        [
            $"I={IntegratedLoudnessTarget}",
            $"TP={MaxTruePeak}",
            $"LRA={LoudnessRangeTarget}",
            $"measured_I={loudnessData.InputIntegratedLoudnessTarget}",
            $"measured_TP={loudnessData.InputMaxTruePeak}",
            $"measured_LRA={loudnessData.InputLoudnessRangeTarget}",
            $"measured_thresh={loudnessData.InputThreshold}",
            $"offset={loudnessData.TargetOffsetGain}",
            $"linear={loudnessData.NormalizationType == "linear"}"
        ];

        EncodingData encodingData = DetermineEncodingData(streamData);
        if (encodingData.Codec == "<:INVALID:>")
        {
            return new()
            {
                Success = false,
                Output = "Unsupported codec: " + streamData.Streams[0].CodecName
            };
        }

        // ffmpeg -i input.ext -af loudnorm=I=-16:TP=-1.5:LRA=11:measured_I=-27.45:measured_TP=-1.20:measured_LRA=5.30:measured_thresh=-37.20:offset=3.45:linear=true -c:a libmp3lame -b:a 192k -ar 44100 -ac 2 output.ext
        var process = ProcessHelper
            .Create(_ffmpegCommand)
            .WithTimeout(System.TimeSpan.FromSeconds(180.0d))
            .WithArgument("-i")
            .WithArgument(file.FullName)
            .WithArgument("-af")
            .WithArgument($"loudnorm={string.Join(':', loudnormInputData)}")
            .WithArgument("-c:v")
            .WithArgument("copy")
            .WithArgument("-c:a")
            .WithArgument(encodingData.Codec)
            .WithArgument(encodingData.BitrateOrQuality[0])
            .WithArgument(encodingData.BitrateOrQuality[1])
            .WithArgument("-ar")
            .WithArgument(encodingData.SampleRate)
            .WithArgument("-ac")
            .WithArgument(encodingData.Channels)
            .WithArguments(encodingData.ExtraArguments)
            .WithArgument(outputFile);

        ProcessResult result = await process.Run();
        List<string> output = result.Errors;

        if (result.Status != ProcessStatus.OK)
        {
            return new()
            {
                Success = false,
                Output = "Error applying loudness normalization: ffmpeg process failed or could not be started. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        returnData.Refresh();
        bool failure = !returnData.Exists || (returnData.Exists && returnData.Length == 0);

        if (failure)
        {
            if (returnData.Exists) returnData.Delete();

            return new()
            {
                Success = false,
                Output = "Error applying loudness normalization: ffmpeg output file is missing or empty indicating that something went wrong. "
                + "PROCESS OUTPUT:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, output)
            };
        }

        return new()
        {
            Success = true,
            Data = returnData
        };
    }

    private sealed record EncodingData
    {
        public string Codec { get; set; } = string.Empty;
        public string[] BitrateOrQuality { get; set; } = [];
        public string Channels { get;set; } = string.Empty;
        public string SampleRate { get; set; } = string.Empty;
        public string[] ExtraArguments { get; set; } = [];
    }

    private static EncodingData DetermineEncodingData(StreamAnalysisResult input)
    {
        var streamData = input.Streams[0];
        EncodingData returnData = new()
        {
            Channels = System.Convert.ToString(streamData.Channels),
            SampleRate = streamData.SampleRate
        };

        switch(streamData.CodecName)
        {
            case "mp3":
                returnData.Codec = "libmp3lame";
                returnData.BitrateOrQuality = ["-q:a", "0"];
                break;

            case "aac":
                returnData.Codec = "aac";
                returnData.BitrateOrQuality = ["-b:a", "192k"];
                returnData.ExtraArguments = ["-movflags", "+faststart"];
                break;

            case "opus":
                returnData.Codec = "libopus";
                returnData.BitrateOrQuality = ["-b:a", "160k"];
                break;

            default:
                returnData.Codec = "<:INVALID:>";
                break;
        }

        return returnData;
    }
}
