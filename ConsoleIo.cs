using BitFab.KW1281Test.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace BitFab.KW1281Test;

/// <summary>
/// Key input and in-place status lines that work on a real console AND when stdin/stdout are
/// piped (e.g. kw1281test-gui driving this process). Piped, each stdin line is one key, named
/// like <see cref="ConsoleKey"/> ("UpArrow", "DownArrow", "N", "Q", "Y"...).
/// </summary>
internal static class ConsoleIo
{
    /// <summary>Printed (piped stdin only) when we block until a key line arrives.</summary>
    public const string WaitingForInput = "WAITING_FOR_INPUT";

    /// <summary>Prefix of a status line that replaces the previous one (piped stdout only).</summary>
    public const string LivePrefix = "LIVE: ";

    private static readonly Lazy<BlockingCollection<ConsoleKey>> PipedKeys = new(() =>
    {
        var keys = new BlockingCollection<ConsoleKey>();
        new Thread(() =>
        {
            while (Console.In.ReadLine() is { } line)
            {
                if (Enum.TryParse<ConsoleKey>(line.Trim(), ignoreCase: true, out var key))
                {
                    keys.Add(key);
                }
            }
            keys.CompleteAdding();
        }) { IsBackground = true }.Start();
        return keys;
    });

    /// <summary>Stdin closed counts as a pending key (Q, see <see cref="ReadKey"/>) so polling
    /// loops still end.</summary>
    public static bool KeyAvailable => Console.IsInputRedirected
        ? PipedKeys.Value.Count > 0 || PipedKeys.Value.IsAddingCompleted
        : Console.KeyAvailable;

    public static ConsoleKey ReadKey(bool intercept = true)
    {
        if (!Console.IsInputRedirected)
        {
            return Console.ReadKey(intercept).Key;
        }

        var keys = PipedKeys.Value;
        if (keys.Count == 0)
        {
            Console.WriteLine(WaitingForInput);
        }
        // Stdin closed (caller went away): Q, which quits or declines every prompt.
        return keys.TryTake(out var key, Timeout.Infinite) ? key : ConsoleKey.Q;
    }

    /// <summary>
    /// Replace the current console line with message (piped: a <see cref="LivePrefix"/> line).
    /// Also writes the message to the log file.
    /// </summary>
    public static void Overlay(string message)
    {
        if (Console.IsOutputRedirected)
        {
            Log.WriteLine(LivePrefix + message, LogDest.Console);
        }
        else
        {
            (int left, int top) = Console.GetCursorPosition();
            Console.SetCursorPosition(0, top);
            if (left > 0)
            {
                Log.Write(new string(' ', left), LogDest.Console);
                Console.SetCursorPosition(0, top);
            }
            Log.Write(message, LogDest.Console);
        }
        Log.WriteLine(message, LogDest.File);
    }
}
