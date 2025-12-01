using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer;

public sealed class Logger
{
    private readonly FileInfo _outputFile;
    private readonly SemaphoreSlim _semaphore;

    public Logger(FileInfo outputFile)
    {
        _semaphore = new SemaphoreSlim(0,1);
        _outputFile = outputFile;
    }

    public async Task Info(string message)
    {
        await _semaphore.WaitAsync();
        try {
            await File.AppendAllTextAsync(_outputFile.FullName, $"[INFO]: {message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}