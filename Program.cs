using System.Linq;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer;

// https://trac.ffmpeg.org/wiki/AudioVolume
// https://ffmpeg.org/ffmpeg-filters.html#loudnorm
// https://www.reddit.com/r/ffmpeg/comments/vultx7/what_exactly_are_the_values_of_loudnorm/
// https://k.ylo.ph/2016/04/04/loudnorm.html

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        var config = new Config(args);

        var audioFiles = config.InputDir
            .EnumerateFiles()
            .Where(file =>
            {
                return
                    file.Extension.Equals(".mp3", System.StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".opus", System.StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".m4a", System.StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        System.Console.WriteLine($"Found {audioFiles.Count} audio files to normalize");
        if (audioFiles.Count == 0)
        {
            System.Console.WriteLine("Nothing to do, quitting");
            return 0;
        }

        var ffmpeg = new FfmpegService(
            config.FfmpegFile is null ? "ffmpeg" : config.FfmpegFile.FullName,
            config.FfprobeFile is null ? "ffprobe" : config.FfprobeFile.FullName,
            config.OutputDir.FullName
        );

        var audioNormalizer = new AudioNormalizer(ffmpeg, audioFiles);
        await audioNormalizer.Start();

        return 0;
    }
}