using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server;

/**
 * Server that listens for custom GlazeWM commands.
 */
[SuppressMessage("ReSharper", "ConvertSwitchStatementToSwitchExpression")]
internal class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 65432;
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "glazewm_daemon.log");

    private static void Main(string[] _)
    {
        try
        {
            StartServer();
        }
        catch (Exception ex)
        {
            LogError($"Daemon crashed during startup: {ex}");
        }
    }

    private static void StartServer()
    {
        TcpListener? server = null;
        try
        {
            server = new TcpListener(IPAddress.Parse(Host), Port);
            server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Start();
            LogInfo($"Listening for commands on {Host}:{Port}...");

            while (true)
                try
                {
                    using var client = server.AcceptTcpClient();
                    using var stream = client.GetStream();

                    // 1 kib read buffer (should be plenty for simple text commands)
                    var buffer = new byte[1024];
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead <= 0) continue;
                    var commandLine = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    try
                    {
                        PerformCommand(commandLine);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to execute command '{commandLine}': {ex}");
                    }
                    finally
                    {
                        // Acknowledge that the command was received
                        var response = Encoding.UTF8.GetBytes("OK");
                        stream.Write(response, 0, response.Length);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Connection error: {ex}");
                }
        }
        catch (Exception ex)
        {
            LogError($"Socket accept error: {ex}");
        }
        finally
        {
            server?.Stop();
        }
    }

    #region GlazeWM Helper Logic

    private static void PerformCommand(string commandLine)
    {
        LogInfo($"Running command: {commandLine}");
        var split = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (split.Length == 0)
        {
            LogInfo("Invalid command: " + commandLine);
            return;
        }

        var command = split[0];
        var arg = split.Length > 1 && int.TryParse(split[1], out var parsedArg) ? parsedArg : 0;

        switch (command)
        {
            case "move_active_window_to_workspace":
                MoveActiveWindowToWorkspace(ClampWorkspaceId(arg));
                break;
            case "move_active_window_to_workspace_relative":
                MoveActiveWindowToWorkspaceRelative(arg);
                break;
            case "focus_workspace":
                FocusWorkspace(ClampWorkspaceId(arg));
                break;
            case "focus_workspace_relative":
                FocusWorkspaceRelative(arg);
                break;
            default:
                LogError($"Unknown command: {command}");
                break;
        }
    }

    private static int[] QueryActiveWindow()
    {
        try
        {
            var jsonOutput = RunGlazeWmCommand("query workspaces");
            using var doc = JsonDocument.Parse(jsonOutput);
            var workspaces = doc.RootElement.GetProperty("data").GetProperty("workspaces");

            foreach (var workspace in workspaces.EnumerateArray())
            {
                if (!workspace.GetProperty("hasFocus").GetBoolean()) continue;
                var name = workspace.GetProperty("name").GetString();
                if (string.IsNullOrEmpty(name)) continue;

                var parts = name.Split('-');
                if (parts.Length == 2) return [int.Parse(parts[0]), int.Parse(parts[1])];
            }
        }
        catch (Exception ex)
        {
            LogError($"Error querying workspaces: {ex.Message}");
        }

        return [];
    }

    private static int ClampWorkspaceId(int workspaceId)
    {
        switch (workspaceId)
        {
            case < 0: return 9;
            case > 9: return 0;
            default: return workspaceId;
        }
    }

    private static string FormatWorkspaceId(int monitorId, int workspaceId)
    {
        return $"{monitorId}-{workspaceId}";
    }

    private static void FocusWorkspace(int workspaceId)
    {
        var data = QueryActiveWindow();
        if (data.Length == 0)
        {
            LogInfo("No active window");
            return;
        }

        var targetWorkspace = FormatWorkspaceId(data[0], ClampWorkspaceId(workspaceId));
        StartGlazeWmCommand($"command focus --workspace {targetWorkspace}");
    }

    private static void FocusWorkspaceRelative(int direction)
    {
        var data = QueryActiveWindow();
        if (data.Length == 0)
        {
            LogInfo("No active window");
            return;
        }

        var targetWorkspace = FormatWorkspaceId(data[0], ClampWorkspaceId(data[1] + direction));
        LogInfo($"goto {targetWorkspace}");
        StartGlazeWmCommand($"command focus --workspace {targetWorkspace}");
    }

    private static void MoveActiveWindowToWorkspace(int workspaceId)
    {
        var data = QueryActiveWindow();
        if (data.Length == 0)
        {
            LogInfo("No active window");
            return;
        }

        var targetWorkspace = FormatWorkspaceId(data[0], workspaceId);
        StartGlazeWmCommand($"command move --workspace {targetWorkspace}");
        StartGlazeWmCommand($"command focus --workspace {targetWorkspace}");
    }

    private static void MoveActiveWindowToWorkspaceRelative(int direction)
    {
        var data = QueryActiveWindow();
        if (data.Length == 0)
        {
            LogInfo("No active window");
            return;
        }

        var targetWorkspace = FormatWorkspaceId(data[0], ClampWorkspaceId(data[1] + direction));
        LogInfo($"Moving active window to {targetWorkspace}");
        StartGlazeWmCommand($"command move --workspace {targetWorkspace}");
        StartGlazeWmCommand($"command focus --workspace {targetWorkspace}");
    }

    private static void StartGlazeWmCommand(string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "glazewm";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true; // Prevents console windows
        process.Start();
    }

    private static string RunGlazeWmCommand(string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "glazewm";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true; // Prevents console windows

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    #endregion

    #region Logging

    // Simple thread-safe logging mechanism
    private static readonly Lock LogLock = new();

    private static void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    private static void LogError(string message)
    {
        WriteLog("ERROR", message);
    }

    private static void WriteLog(string level, string message)
    {
        lock (LogLock)
        {
            try
            {
                Console.WriteLine("[" + level + "] " + message);
                File.AppendAllText(
                    LogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {level} - {message}{Environment.NewLine}"
                );
            }
            catch
            {
                // Fail silently if the log file is locked
            }
        }
    }

    #endregion
}