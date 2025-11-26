using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer;

public sealed class ProcessHelper
{
    private Process? _process;
    private readonly List<string> _stdOutput;
    private readonly List<string> _stdError;
    private System.TimeSpan _timeout;
    private readonly List<string> _arguments;
    private readonly List<System.Tuple<string,string>> _envVars;
    private readonly string _commandToRun;
    private DirectoryInfo _workingDir;

    private ProcessHelper(string commandToRun)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(commandToRun);

        _commandToRun = commandToRun;
        _stdError = new();
        _stdOutput = new();
        _arguments = new();
        _envVars = new();
        _timeout = System.TimeSpan.FromSeconds(30.0d);
        _workingDir = new DirectoryInfo(System.AppContext.BaseDirectory);
    }

    public static ProcessHelper Create(string commandToRun)
    {
        return new ProcessHelper(commandToRun);
    }

    public ProcessHelper WithArgument(string argument)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(argument);

        _arguments.Add(argument);
        return this;
    }

    public ProcessHelper WithArguments(IEnumerable<string> arguments)
    {
        System.ArgumentNullException.ThrowIfNull(arguments);

        _arguments.AddRange(arguments);
        return this;
    }

    public ProcessHelper WithEnvironmentVar(string name, string value)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _envVars.Add(new System.Tuple<string, string>(name,value));
        return this;
    }

    public ProcessHelper WithTimeout(System.TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public ProcessHelper WithWorkingDir(string workingDir)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(workingDir);

        _workingDir = new DirectoryInfo(workingDir);
        if (!_workingDir.Exists)
        {
            throw new DirectoryNotFoundException("Working dir does not exist: " + _workingDir.FullName);
        }

        return this;
    }

    public async Task<ProcessResult> Run()
    {
        var info = new ProcessStartInfo(_commandToRun)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workingDir.FullName
        };

        foreach(System.Tuple<string,string> currentEnvVar in _envVars)
        {
            info.EnvironmentVariables[currentEnvVar.Item1] = currentEnvVar.Item2;
        }

        foreach(var currentArg in _arguments)
        {
            info.ArgumentList.Add(currentArg);
        }

        _process = new Process() { StartInfo = info };

        //System.Console.WriteLine("PROCESS ARGUMENTS:");
        //System.Console.WriteLine(string.Join(" ", _process.StartInfo.ArgumentList));

        try
        {
            _process.Start();
        }
        catch(System.Exception error)
        {
            return new ProcessResult(ProcessStatus.FAILED, [], [error.Message]);
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null) _stdOutput.Add(args.Data.Trim());
        };

        _process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null) _stdError.Add(args.Data.Trim());
        };

        var CancellationSignal = new CancellationTokenSource(_timeout);
        try
        {
            await _process.WaitForExitAsync(CancellationSignal.Token);
        }
        catch(System.OperationCanceledException) {
            _process.Kill(true);
            _stdError.Add($"Process did not complete within the timeout ({_timeout})");
            return new ProcessResult(ProcessStatus.TIMED_OUT, _stdOutput, _stdError);
        }

        return new ProcessResult(ProcessStatus.OK, _stdOutput, _stdError);
    }
}

public enum ProcessStatus
{
    OK, FAILED, TIMED_OUT
}

public sealed record ProcessResult
{
    public List<string> Output { get; }
    public List<string> Errors { get; }
    public ProcessStatus Status { get; }

    public ProcessResult(ProcessStatus status, List<string> output, List<string> errors)
    {
        Output = output;
        Errors = errors;
        Status = status;
    }
}