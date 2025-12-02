using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer;

// https://trac.ffmpeg.org/wiki/AudioVolume
// https://ffmpeg.org/ffmpeg-filters.html#loudnorm
// https://www.reddit.com/r/ffmpeg/comments/vultx7/what_exactly_are_the_values_of_loudnorm/
// https://k.ylo.ph/2016/04/04/loudnorm.html

internal static class Program
{
    public static Logger Log {get; private set;} = null!;
    public static Config Config { get; private set; } = null!;

    internal static async Task<int> Main(string[] args)
    {
        Config = new Config(args);

        string outputPath = Path.Combine(Config.OutputDir.FullName, "AudioNormalizer.log");
        Log = new Logger(new FileInfo(outputPath));

        var audioFiles = Config.InputDir
            .EnumerateFiles()
            .Where(file =>
            {
                string fileExt = file.Extension.Trim();
                return
                    fileExt.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase) ||
                    fileExt.EndsWith(".opus", System.StringComparison.OrdinalIgnoreCase) ||
                    fileExt.EndsWith(".m4a", System.StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        System.Console.WriteLine($"Found {audioFiles.Count} audio file(s) to normalize (loudness target: {FfmpegService.IntegratedLoudnessTarget})");

        if (audioFiles.Count == 0)
        {
            System.Console.WriteLine("Nothing to do, quitting");
            return 0;
        }

        var ffmpeg = new FfmpegService(
            Config.FfmpegFile is null ? "ffmpeg" : Config.FfmpegFile.FullName,
            Config.FfprobeFile is null ? "ffprobe" : Config.FfprobeFile.FullName,
            Config.OutputDir.FullName
        );

        var audioNormalizer = new AudioNormalizer(ffmpeg, audioFiles);
        await audioNormalizer.Start();

        return 0;
    }
}