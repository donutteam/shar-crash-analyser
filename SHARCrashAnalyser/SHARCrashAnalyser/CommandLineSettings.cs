using System;
using System.IO;

namespace SHARCrashAnalyser;

internal class CommandLineSettings
{
    public bool Help { get; private set; } = false;
    public bool IsCLI { get; private set; } = false;
    public bool Verbose { get; private set; } = false;
    public bool NoColour { get; private set; } = false;
    public bool Pause { get; private set; } = false;
    public string DumpPath { get; private set; } = null;
    public string CSVPath { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shar_symbols.csv");
    public string HacksPDBsPath { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HacksPDBs");
    public bool NoModules { get; private set; } = false;
    public bool VerboseModules { get; private set; } = false;
    public bool DumpStrings { get; private set; } = false;
    public string StringsFilter { get; private set; } = null;
    public bool UpdateSymbols { get; private set; } = false;
    public uint StackDepth { get; private set; } = 128u;

    public CommandLineSettings(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-?":
                case "--help":
                    Help = true;
                    break;
                case "-v":
                case "--verbose":
                    Verbose = true;
                    break;
                case "-nc":
                case "--nocolour":
                case "--nocolor":
                    NoColour = true;
                    break;
                case "-ng":
                case "--nogui":
                    IsCLI = true;
                    break;
                case "-p":
                case "--pause":
                    Pause = true;
                    break;
                case "-i":
                case "--input":
                    if (i + 1 < args.Length)
                        DumpPath = args[++i];
                    break;
                case "-c":
                case "--csv":
                    if (i + 1 < args.Length)
                        CSVPath = args[++i];
                    break;
                case "-h":
                case "--hacks":
                    if (i + 1 < args.Length)
                        HacksPDBsPath = args[++i];
                    break;
                case "-nm":
                case "--nomodules":
                    NoModules = true;
                    break;
                case "-vm":
                case "--verbosemodules":
                    VerboseModules = true;
                    break;
                case "-ds":
                case "--dumpstrings":
                    DumpStrings = true;
                    break;
                case "-sf":
                case "--stringsfilter":
                    if (i + 1 < args.Length)
                        StringsFilter = args[++i];
                    break;
                case "-us":
                case "--updatesymbols":
                    UpdateSymbols = true;
                    break;
                case "-sd":
                case "--stackdepth":
                    if (i + 1 < args.Length)
                        if (uint.TryParse(args[++i], out var stackDepth) && stackDepth % 4 == 0)
                            StackDepth = stackDepth;
                    break;
            }
        }

        if (args.Length > 0 && string.IsNullOrWhiteSpace(DumpPath) && File.Exists(args[args.Length - 1]))
            DumpPath = args[args.Length - 1];
    }
}
