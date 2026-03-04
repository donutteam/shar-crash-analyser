using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SHARCrashAnalyser;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

    private const int SW_HIDE = 0;

    internal static CommandLineSettings CommandLineSettings;

    public static string Title => $"SHAR Crash Analyser v{Assembly.GetExecutingAssembly().GetName().Version}";

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        Console.Title = Title;
        CommandLineSettings = new(args);

        if (CommandLineSettings.UpdateSymbols || !File.Exists(CommandLineSettings.CSVPath))
            UpdateSymbols().GetAwaiter().GetResult();


        if (CommandLineSettings.Help)
        {
            Console.WriteLine("Usage: SHARCrashAnalyser [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -?, --help                Show this help message and exit");
            Console.WriteLine("  -ng, --nogui              Run in CLI mode without GUI");
            Console.WriteLine("  -p, --pause               Pause before exiting");
            Console.WriteLine("  -i, --input <path>        Specify input dump path");
            Console.WriteLine("  -c, --csv <path>          Specify CSV output path");
            Console.WriteLine("  -h, --hacks <path>        Specify Hacks PDB path");
            Console.WriteLine("  -us, --updatesymbols      Force update symbols with latest");

            if (CommandLineSettings.Pause)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            return;
        }

        if (CommandLineSettings.IsCLI)
        {
            RunCommandLine();

            if (CommandLineSettings.Pause)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            return;
        }

        uint[] processList = new uint[1];
        uint processCount = GetConsoleProcessList(processList, (uint)processList.Length);
        
        if (processCount == 1)
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new FrmMain());
    }

    static void RunCommandLine()
    {
        var dumpPath = CommandLineSettings.DumpPath;
        while (!File.Exists(dumpPath))
        {
            Console.WriteLine("Specify dump path:");
            dumpPath = Console.ReadLine();
        }

        Console.WriteLine($"Analysing \"{dumpPath}\"...");

        try
        {
            var dump = Analyser.AnalyseDump(dumpPath);
            PrintColouredConsole(dump);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"There was an error analysing dump: {ex}");
        }
    }

    private static void PrintColouredConsole(string text)
    {
        string pattern = @"(?<header>=== .* ===)|(?<reg>\b(eax|ebx|ecx|edx|esi|edi|ebp|esp|eip|efl)\b)|(?<hex>\b[0-9a-fA-F]{8}\b)";

        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
        int lastIndex = 0;

        foreach (Match m in matches)
        {
            Console.ResetColor();
            Console.Write(text.Substring(lastIndex, m.Index - lastIndex));

            if (m.Groups["header"].Success)
                Console.ForegroundColor = ConsoleColor.Cyan;
            else if (m.Groups["reg"].Success)
                Console.ForegroundColor = ConsoleColor.Magenta;
            else if (m.Groups["hex"].Success)
                Console.ForegroundColor = ConsoleColor.Green;

            Console.Write(m.Value);
            lastIndex = m.Index + m.Length;
        }

        Console.ResetColor();
        Console.WriteLine(text.Substring(lastIndex));
    }

    private static async Task UpdateSymbols()
    {
        if (File.Exists(CommandLineSettings.CSVPath) && !AskYesNo($"Symbols file \"{CommandLineSettings.CSVPath}\" already exists. Do you want to overwrite?", false))
        {
            Console.WriteLine("Not overwriting symbols.");
            return;
        }

        try
        {
            var url = $"https://api.github.com/repos/DonutTeam/shar-crash-analyser/contents/Symbols/shar_symbols.csv?ref=main";
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"SHARCrashAnalyser/{Assembly.GetExecutingAssembly().GetName().Version}");
            httpClient.DefaultRequestHeaders.CacheControl = new()
            {
                NoCache = true,
                NoStore = true,
            };

            Console.WriteLine($"Downloading symbols from \"{url}\"...");
            using var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to download symbols. Status code: {response.StatusCode} ({(int)response.StatusCode}).");
                return;
            }

            using var responseContent = response.Content;
            var csv = await responseContent.ReadAsStringAsync();

            File.WriteAllText(CommandLineSettings.CSVPath, csv);
            Console.WriteLine($"Symbols downloaded to \"{CommandLineSettings.CSVPath}\".");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download symbols. Error: {ex}");
            return;
        }
    }

    private static bool AskYesNo(string question, bool defaultYes = true)
    {
        string defaultOption = defaultYes ? "Y/n" : "y/N";
        while (true)
        {
            Console.Write($"{question} [{defaultOption}]: ");
            string? input = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(input))
                return defaultYes;

            if (input == "y" || input == "yes")
                return true;
            if (input == "n" || input == "no")
                return false;

            Console.WriteLine("Please enter 'y' or 'n'.");
        }
    }
}
