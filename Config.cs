using System.IO;
using System.Runtime.InteropServices;
using static System.TupleExtensions;

namespace TMech.AudioNormalizer;

public class Config
{
    public DirectoryInfo InputDir { get; }
    public DirectoryInfo OutputDir { get; }
    public FileInfo? FfmpegFile { get; }
    public FileInfo? FfprobeFile { get; }
    public string FfmpegFileBinaryName { get; }
    public string FfprobeFileBinaryName { get; }

    public Config(string[] args)
    {
        if (args.Length == 0)
        {
            throw new System.Exception("No arguments passed. Expected at least 2 (-inputDir and -outputDir)");
        }

        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        FfmpegFileBinaryName = isWindows ? "ffmpeg.exe" : "ffmpeg";
        FfprobeFileBinaryName = isWindows ? "ffprobe.exe" : "ffprobe";

        for (int index = 0; index < args.Length; index++)
        {
            string currentArg = args[index].Trim().TrimStart('-');
            if (currentArg.StartsWith("inputDir"))
            {
                var argParts = ValidateArg(currentArg);
                InputDir = ParseAndValidateDirectory(argParts.Item2);
                continue;
            }

            if (currentArg.StartsWith("outputDir"))
            {
                var argParts = ValidateArg(currentArg);
                OutputDir = ParseAndValidateDirectory(argParts.Item2);
                continue;
            }

            if (currentArg.StartsWith("ffmpegDir"))
            {
                var argParts = ValidateArg(currentArg);
                (FfmpegFile, FfprobeFile) = ResolveAndValidateFfmpegPath(argParts.Item2);
            }
        }

        if (InputDir is null || OutputDir is null)
        {
            throw new System.Exception("Expected both inputDir and outputDir arguments but one or both were missing");
        }

        if (FfmpegFile is null)
        {
            if (!FfmpegService.IsFfmpegAvailable("ffmpeg").GetAwaiter().GetResult())
            {
                throw new System.Exception("ffmpeg is not globally available. Either set it on your path or pass folder where it resides in argument 'ffmpegDir'");
            }
        }

        if (FfprobeFile is null)
        {
            if (!FfmpegService.IsFfmpegAvailable("ffprobe").GetAwaiter().GetResult())
            {
                throw new System.Exception("ffprobe is not globally available. Either set it on your path or pass folder where it resides in argument 'ffmpegDir'");
            }
        }
    }

    private static System.Tuple<string,string> ValidateArg(string input)
    {
        string[] argParts = input.Split('=', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (argParts.Length != 2)
        {
            throw new System.Exception("Error validating argument. Not in the correct format (arg=value): " + input);
        }

        return new System.Tuple<string, string>(argParts[0], argParts[1]);
    }

    private static DirectoryInfo ParseAndValidateDirectory(string path)
    {
        string absolutePath = Path.GetFullPath(path, System.AppContext.BaseDirectory);
        if (!Directory.Exists(absolutePath))
        {
            throw new DirectoryNotFoundException("Directory does not exist: " + path);
        }

        return new DirectoryInfo(absolutePath);
    }

    public System.Tuple<FileInfo,FileInfo> ResolveAndValidateFfmpegPath(string ffmpegDir)
    {
        var ffmpegDirInfo = ParseAndValidateDirectory(ffmpegDir);

        var ffmpegFile = new FileInfo(Path.Combine(ffmpegDirInfo.FullName, FfmpegFileBinaryName));
        var ffprobeFile = new FileInfo(Path.Combine(ffmpegDirInfo.FullName, FfprobeFileBinaryName));

        if (!ffmpegFile.Exists)
        {
            throw new FileNotFoundException("ffmpeg-binary not found in the directory passed by argument 'ffmpegDir'");
        }

        if (!ffprobeFile.Exists)
        {
            throw new FileNotFoundException("ffprope-binary not found in the directory passed by argument 'ffmpegDir'");
        }

        return new System.Tuple<FileInfo, FileInfo>(ffmpegFile, ffprobeFile);
    }
}