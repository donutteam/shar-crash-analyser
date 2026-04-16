using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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

        if (CommandLineSettings.Help)
        {
            Console.WriteLine("Usage: SHARCrashAnalyser [options] [dump file]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -?, --help                         Show this help message and exit");
            Console.WriteLine("  -v, --verbose                      Enable verbose output");
            Console.WriteLine("  -nc, --nocolour                    Disable coloured output");
            Console.WriteLine("  -ng, --nogui                       Run in CLI mode without GUI");
            Console.WriteLine("  -p, --pause                        Pause before exiting");
            Console.WriteLine("  -i, --input <path>                 Specify input dump path");
            Console.WriteLine("  -c, --csv <path>                   Specify symbols CSV path");
            Console.WriteLine("  -h, --hacks <path>                 Specify Hacks PDB path");
            Console.WriteLine("  -nm, --nomodules                   Exclude modules from analysis");
            Console.WriteLine("  -sd, --stackdepth <depth>          The depth of the raw stack output");
            Console.WriteLine("  -ds, --dumpstrings                 Dump strings in analysis");
            Console.WriteLine("  -sf, --stringsfilter <filter>      Filter dumped strings");
            Console.WriteLine("  -us, --updatesymbols               Force update symbols with latest");

            if (CommandLineSettings.Pause)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            return;
        }

        if (CommandLineSettings.UpdateSymbols || !File.Exists(CommandLineSettings.CSVPath))
            UpdateSymbols().GetAwaiter().GetResult();

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
            var sw = Stopwatch.StartNew();
            var dump = Analyser.AnalyseDump(dumpPath);
            sw.Stop();
            Console.WriteLine($"Analysed in {sw.Elapsed:mm\\:ss\\.fff}.");
            Console.WriteLine();
            if (CommandLineSettings.NoColour)
                Console.WriteLine(dump);
            else
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
            var json = await responseContent.ReadAsStringAsync();

            var doc = JsonConvert.DeserializeObject<GitHubResponse>(json);
            var encodedCSV = doc.Content;
            var csvBytes = Convert.FromBase64String(encodedCSV);
            var csv = Encoding.UTF8.GetString(csvBytes);

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
            string input = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(input))
                return defaultYes;

            if (input == "y" || input == "yes")
                return true;
            if (input == "n" || input == "no")
                return false;

            Console.WriteLine("Please enter 'y' or 'n'.");
        }
    }

    private class GitHubResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("sha")]
        public string Sha { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("git_url")]
        public string GitUrl { get; set; }

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("encoding")]
        public string Encoding { get; set; }
    }
}
