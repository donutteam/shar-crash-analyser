using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SHARCrashAnalyser;

internal static class Program
{
    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;

    internal static CommandLineSettings CommandLineSettings;

    public static string Title => $"SHAR Crash Analyser v{Assembly.GetExecutingAssembly().GetName().Version.ToString()}";

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        Console.Title = Title;
        CommandLineSettings = new(args);

        if (CommandLineSettings.UpdateSymbols || !File.Exists(CommandLineSettings.CSVPath))
        {
            // TODO
        }

        if (CommandLineSettings.IsCLI)
        {
            RunCommandLine();
            return;
        }

        var handle = GetConsoleWindow();
        ShowWindow(handle, SW_HIDE);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new FrmMain());
    }

    static void RunCommandLine()
    {
        if (CommandLineSettings.Help)
        {
            Console.WriteLine("Usage: SHARCrashAnalyser [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -?, --help                Show this help message and exit");
            Console.WriteLine("  -ng, --nogui              Run in CLI mode without GUI");
            Console.WriteLine("  -np, --nopause            Do not pause before exiting");
            Console.WriteLine("  -i, --input <path>        Specify input dump path");
            Console.WriteLine("  -c, --csv <path>          Specify CSV output path");
            Console.WriteLine("  -h, --hacks <path>        Specify Hacks PDB path");
            Console.WriteLine("  -us, --updatesymbols      Force update symbols with latest");

            if (!CommandLineSettings.NoPause)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
            return;
        }

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
            Console.WriteLine(dump);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"There was an error analysing dump: {ex}");
        }

        if (!CommandLineSettings.NoPause)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}
