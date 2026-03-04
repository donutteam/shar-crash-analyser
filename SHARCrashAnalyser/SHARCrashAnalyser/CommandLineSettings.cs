using System;
using System.IO;

namespace SHARCrashAnalyser;

internal class CommandLineSettings
{
    public bool Help { get; private set; } = false;
    public bool IsCLI { get; private set; } = false;
    public bool Pause { get; private set; } = false;
    public string DumpPath { get; private set; } = null;
    public string CSVPath { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shar_symbols.csv");
    public string HacksPDBPath { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Hacks.pdb");
    public bool NoModules { get; private set; } = false;
    public bool UpdateSymbols { get; private set; } = false;

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
                        HacksPDBPath = args[++i];
                    break;
                case "-nm":
                case "--nomodules":
                    NoModules = true;
                    break;
                case "-us":
                case "--updatesymbols":
                    UpdateSymbols = true;
                    break;
            }
        }
    }
}
