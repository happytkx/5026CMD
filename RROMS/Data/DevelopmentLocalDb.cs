// Resolves LocalDB's current pipe before SQL Client connects. This avoids the
// failing automatic LocalDB lookup seen when starting under Visual Studio.
// Only used for Windows Development; hosted SQL Server connections are unchanged.
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace RROMS.Data;

public static class DevelopmentLocalDb
{
    public static async Task<string?> Resolve(string? connectionString, bool development)
    {
        if (!development || !OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var connection = new SqlConnectionStringBuilder(connectionString);
        const string prefix = "(localdb)\\";
        if (!connection.DataSource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var instance = connection.DataSource[prefix.Length..];
        var tool = FindTool();
        if (tool == null) return connectionString;

        var info = await Run(tool, "info", instance);
        var pipe = Pipe(info.Output);
        if (pipe == null)
        {
            var start = await Run(tool, "start", instance);
            if (start.ExitCode != 0)
                throw new InvalidOperationException($"LocalDB instance '{instance}' could not start. Run SqlLocalDB start {instance} to inspect the database runtime error. No database was deleted or recreated.");
            info = await Run(tool, "info", instance);
            pipe = Pipe(info.Output);
        }
        if (pipe == null)
            throw new InvalidOperationException($"LocalDB instance '{instance}' did not provide a connection pipe after startup.");

        // The pipe changes after a restart, so resolve it on every application launch.
        connection.DataSource = pipe;
        return connection.ConnectionString;
    }

    private static string? Pipe(string output)
    {
        var match = Regex.Match(output, @"np:\\\\.\\pipe\\[^\s]+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static string? FindTool()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SQL Server");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(folder => Path.Combine(folder, "Tools", "Binn", "SqlLocalDB.exe"))
            .FirstOrDefault(File.Exists);
    }

    private static async Task<(int ExitCode, string Output)> Run(string tool, string command, string instance)
    {
        var start = new ProcessStartInfo(tool)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(instance);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to launch the LocalDB utility.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The LocalDB utility did not respond within 30 seconds.");
        }
        await error;
        return (process.ExitCode, await output);
    }
}
