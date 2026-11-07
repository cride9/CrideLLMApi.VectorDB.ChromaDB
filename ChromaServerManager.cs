using System.Diagnostics;

namespace CrideLLMApi.VectorDB.ChromaDB;

internal sealed class ChromaServerManager : IAsyncDisposable
{
    private readonly SemaphoreSlim _startupLock = new(1, 1);

    private Process? _process;
    private bool _startedByUs;

    public async Task EnsureRunningAsync(ChromaOptions options, CancellationToken cancellationToken = default)
    {
        if ( await IsAvailableAsync(options.EndPoint, cancellationToken) )
        {
            EnsureManagedServer(options);
            return;
        }

        if ( !options.AutoStart )
        {
            throw new InvalidOperationException(
                $"ChromaDB is not reachable at {options.EndPoint}. " +
                "Start ChromaDB manually or enable automatic startup.");
        }

        await _startupLock.WaitAsync(cancellationToken);

        try
        {
            // Another request may have started it while waiting.
            if ( await IsAvailableAsync(options.EndPoint, cancellationToken) )
            {
                EnsureManagedServer(options);
                return;
            }

            Directory.CreateDirectory(options.RuntimePath);
            Directory.CreateDirectory(options.DataPath);

            if (!string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                StartServer(Path.GetFullPath(options.ExecutablePath), options);
                await WaitUntilReadyAsync(options.EndPoint, cancellationToken);
                return;
            }

            if (!options.InstallOnStartup)
            {
                throw new InvalidOperationException(
                    "ChromaDB is unavailable and package installation at startup is disabled. " +
                    "Configure ExecutablePath or start the local Chroma server beforehand.");
            }

            var python = await FindPythonAsync(cancellationToken);

            if ( python is null )
            {
                throw new InvalidOperationException(
                    "ChromaDB is not reachable and automatic startup failed " +
                    "because Python could not be found. " +
                    "Install Python or start ChromaDB manually.");
            }

            var venvPath =  Path.Combine(options.RuntimePath, "venv");

            await EnsureEnvironmentAsync(python, venvPath, cancellationToken);

            StartServer(GetChromaExecutable(venvPath), options);

            await WaitUntilReadyAsync(options.EndPoint, cancellationToken);
        }
        finally
        {
            _startupLock.Release( );
        }
    }

    private void EnsureManagedServer(ChromaOptions options)
    {
        if (options.RequireManagedServer && (!_startedByUs || _process is null || _process.HasExited))
            throw new InvalidOperationException("ChromaDB port is already occupied; its data directory cannot be verified.");
    }

    private static async Task<PythonCommand?> FindPythonAsync(CancellationToken cancellationToken)
    {
        PythonCommand[ ] candidates =
        [
            new("python"),
            new("python3"),
            new("py", "-3")
        ];

        foreach ( var candidate in candidates )
        {
            try
            {
                using var process =
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName =
                                candidate.Executable,

                            Arguments =
                                BuildArguments(
                                    candidate.ArgumentsPrefix,
                                    "--version"),

                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });

                if ( process is null )
                    continue;

                await process.WaitForExitAsync(cancellationToken);

                if ( process.ExitCode == 0 )
                    return candidate;
            }
            catch
            {
                // Try next candidate.
            }
        }

        return null;
    }

    private static async Task EnsureEnvironmentAsync(PythonCommand python, string venvPath,
        CancellationToken cancellationToken)
    {
        var venvPython = GetVenvPythonPath(venvPath);

        var chromaExecutable = GetChromaExecutable(venvPath);

        /*
         * If both Python and Chroma already exist inside the
         * managed environment, there is nothing to install.
         */
        if ( File.Exists(venvPython) &&
            File.Exists(chromaExecutable) )
        {
            return;
        }

        if ( !Directory.Exists(venvPath) )
        {
            Console.WriteLine("[CrideLLMApi] Creating ChromaDB Python environment...");

            await RunProcessAsync(python.Executable,
                BuildArguments(
                    python.ArgumentsPrefix,
                    $"-m venv \"{venvPath}\""),
                cancellationToken);
        }

        venvPython = GetVenvPythonPath(venvPath);

        if ( !File.Exists(venvPython) )
        {
            throw new InvalidOperationException(
                "The ChromaDB virtual environment was created, " +
                "but its Python executable could not be found.");
        }

        Console.WriteLine("[CrideLLMApi] Installing ChromaDB...");

        await RunProcessAsync(venvPython, "-m pip install --upgrade pip", cancellationToken);

        await RunProcessAsync(venvPython, "-m pip install chromadb", cancellationToken);

        chromaExecutable = GetChromaExecutable(venvPath);

        if ( !File.Exists(chromaExecutable) )
        {
            throw new InvalidOperationException(
                "ChromaDB was installed, but the Chroma CLI executable " +
                "could not be found inside the managed environment.");
        }
    }

    private void StartServer(string executable, ChromaOptions options)
    {
        if ( !File.Exists(executable) )
        {
            throw new FileNotFoundException(
                "ChromaDB CLI executable was not found.",
                executable);
        }

        Console.WriteLine($"[CrideLLMApi] Starting ChromaDB at {options.EndPoint}...");

        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.RuntimePath
        };
        foreach (var argument in new[] { "run", "--host", options.EndPoint.Host, "--port", options.EndPoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--path", options.DataPath })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["ANONYMIZED_TELEMETRY"] = "False";

        _process =
            Process.Start(start);

        if ( _process is null )
        {
            throw new InvalidOperationException(
                "Failed to start the ChromaDB process.");
        }

        _startedByUs = true;

        _process.OutputDataReceived +=
            (_, args) =>
            {
                if ( options.ForwardServerLogs && !string.IsNullOrWhiteSpace(args.Data) )
                    Console.WriteLine(
                        $"[ChromaDB] {args.Data}");
            };

        _process.ErrorDataReceived +=
            (_, args) =>
            {
                if ( options.ForwardServerLogs && !string.IsNullOrWhiteSpace(args.Data) )
                    Console.WriteLine(
                        $"[ChromaDB] {args.Data}");
            };

        _process.BeginOutputReadLine( );
        _process.BeginErrorReadLine( );
    }

    private static async Task WaitUntilReadyAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        const int attempts = 120;

        for ( var i = 0; i < attempts; i++ )
        {
            cancellationToken.ThrowIfCancellationRequested( );

            if ( await IsAvailableAsync(endpoint, cancellationToken) )
            {
                Console.WriteLine($"[CrideLLMApi] ChromaDB ready at {endpoint}");

                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException(
            $"ChromaDB was started but did not become available at {endpoint}.");
    }

    private static async Task<bool> IsAvailableAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var client =
                new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
                {
                    BaseAddress = endpoint,
                    Timeout =
                        TimeSpan.FromMilliseconds(500)
                };

            using var response =
                await client.GetAsync(
                    "/api/v2/heartbeat",
                    cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunProcessAsync(string executable, string arguments, CancellationToken cancellationToken)
    {
        using var process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,

                    RedirectStandardOutput = true,
                    RedirectStandardError = true,

                    UseShellExecute = false,
                    CreateNoWindow = true
                });

        if ( process is null )
        {
            throw new InvalidOperationException(
                $"Failed to start process '{executable}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync( cancellationToken);

        var stderrTask = process.StandardError.ReadToEndAsync( cancellationToken);

        await process.WaitForExitAsync( cancellationToken);

        var stdout = await stdoutTask;

        var stderr = await stderrTask;

        if ( process.ExitCode == 0 )
            return;

        throw new InvalidOperationException(
            $"Process '{executable}' exited with code {process.ExitCode}.\n" +
            $"STDOUT:\n{stdout}\n" +
            $"STDERR:\n{stderr}");
    }

    private static string GetVenvPythonPath(
        string venvPath)
    {
        return OperatingSystem.IsWindows( )
            ? Path.Combine(venvPath, "Scripts", "python.exe")
            : Path.Combine(venvPath, "bin", "python");
    }

    private static string GetChromaExecutable(
        string venvPath)
    {
        return OperatingSystem.IsWindows( )
            ? Path.Combine(venvPath, "Scripts", "chroma.exe")
            : Path.Combine(venvPath, "bin", "chroma");
    }

    private static string BuildArguments(string prefix, string arguments)
    {
        return string.IsNullOrWhiteSpace(prefix)
            ? arguments
            : $"{prefix} {arguments}";
    }

    public async ValueTask DisposeAsync( )
    {
        await _startupLock.WaitAsync( );

        try
        {
            if ( !_startedByUs ||
                _process is null )
            {
                return;
            }

            if ( !_process.HasExited )
            {
                try
                {
                    _process.Kill(
                        entireProcessTree: true);

                    await _process.WaitForExitAsync( );
                }
                catch
                {
                }
            }

            _process.Dispose( );
            _process = null;
        }
        finally
        {
            _startupLock.Release( );
            _startupLock.Dispose( );
        }
    }
}
