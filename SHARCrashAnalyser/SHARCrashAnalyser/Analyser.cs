using DbgEngWrapper;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SHARCrashAnalyser;

internal static class Analyser
{
    private const uint EXPECTED_CHECKSUM = 0x265B92;
    private static readonly Dictionary<uint, string> KnownChecksums = new()
    {
        {0x265B92, "English"},
        {0x26A31A, "German"}
    };

    internal static string AnalyseDump(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Could not find crash dump as the specified path", filePath);

        Stopwatch sw = Program.CommandLineSettings.Verbose ? Stopwatch.StartNew() : null;

        var createCode = WDebugClient.DebugCreate(null, out var client);
        if (createCode != 0)
            throw new Exception($"Failed to create debug client. Exit code: {createCode:X}");

        var ctrl = (WDebugControl)client;
        var symbs = (WDebugSymbols)client;
        var dataSpaces = (WDebugDataSpaces)client;
        var registers = (WDebugRegisters)client;

        if (Program.CommandLineSettings.Verbose)
        {
            sw.Stop();
            Console.WriteLine($"Created debug client in {sw.Elapsed:mm\\:ss\\.fff}.");
        }

        try
        {
            if (Directory.Exists(Program.CommandLineSettings.HacksPDBsPath))
            {
                if (Program.CommandLineSettings.Verbose)
                    sw.Restart();

                LoadModuleDir(ref symbs, Program.CommandLineSettings.HacksPDBsPath);

                if (Program.CommandLineSettings.Verbose)
                {
                    sw.Stop();
                    Console.WriteLine($"Loaded hacks PDBs in {sw.Elapsed:mm\\:ss\\.fff}.");
                }
            }

            if (Program.CommandLineSettings.Verbose)
                sw.Restart();

            client.OpenDumpFileWide(filePath, 0);
            ctrl.WaitForEvent(DEBUG_WAIT.DEFAULT, uint.MaxValue);
            symbs.SetScopeFromStoredEvent();

            if (Program.CommandLineSettings.Verbose)
            {
                sw.Stop();
                Console.WriteLine($"Opened dump file in {sw.Elapsed:mm\\:ss\\.fff}.");
            }

            if (Program.CommandLineSettings.Verbose)
                sw.Restart();

            var frames = new DEBUG_STACK_FRAME_EX[100];
            var getStackTraceCode = ctrl.GetStackTraceEx(0UL, 0UL, 0UL, out frames);
            if (getStackTraceCode != 0)
                throw new Exception($"Failed to get stack trace. Exit code: {getStackTraceCode:X}");

            if (Program.CommandLineSettings.Verbose)
            {
                sw.Stop();
                Console.WriteLine($"Got stack trace in {sw.Elapsed:mm\\:ss\\.fff}.");
            }

            uint simpsonsIndex = uint.MaxValue;
            ulong simpsonsBase = 0;

            if (Program.CommandLineSettings.Verbose)
                sw.Restart();

            var getNumberModulesCode = symbs.GetNumberModules(out var loadedModules, out var unloadedModules);
            if (getNumberModulesCode != 0)
                throw new Exception($"Failed to get number of modules. Exit code: {getNumberModulesCode:X}");

            for (var i = 0u; i < loadedModules; i++)
            {
                if (symbs.GetModuleNameStringWide(DEBUG_MODNAME.MODULE, i, 0, out string moduleName) != 0)
                    continue;

                if (moduleName.StartsWith("simpsons", StringComparison.InvariantCultureIgnoreCase))
                {
                    var getModuleCode = symbs.GetModuleByModuleNameWide(moduleName, 0, out simpsonsIndex, out simpsonsBase);
                    if (getModuleCode != 0)
                        throw new Exception($"Failed to get simpsons module. Exit code: {getModuleCode:X}");
                    break;
                }
            }

            if (simpsonsIndex == uint.MaxValue)
                throw new Exception("Simpsons module not found in dump.");

            if (Program.CommandLineSettings.Verbose)
            {
                sw.Stop();
                Console.WriteLine($"Got module base in {sw.Elapsed:mm\\:ss\\.fff}.");
            }

            if (Program.CommandLineSettings.Verbose)
                sw.Restart();

            var getParametersCode = symbs.GetModuleParameters(1, [simpsonsBase], 0, out var parms);
            if (getParametersCode != 0)
                throw new Exception($"Failed to get module parameters. Exit code: {getParametersCode:X}");
            var moduleSize = parms[0].Size;
            var checksum = parms[0].Checksum;

            if (Program.CommandLineSettings.Verbose)
            {
                sw.Stop();
                Console.WriteLine($"Got module parameters in {sw.Elapsed:mm\\:ss\\.fff}.");
            }

            var sb = new StringBuilder("=== ANALYSER ERRORS ===\r\n");

            List<FunctionEntry> funcsSorted = [];
            if (!KnownChecksums.TryGetValue(checksum, out var release))
                release = "Unknown";
            if (checksum != EXPECTED_CHECKSUM)
            {
                sb.AppendLine("Unsupported game version detected.");
                sb.AppendLine($"Expected checksum: {EXPECTED_CHECKSUM:X8} (English). Found checksum: {checksum:X8} ({release}).");
                sb.AppendLine("Simpsons functions not mapped.");
                sb.AppendLine();
            }
            else
            {
                if (!File.Exists(Program.CommandLineSettings.CSVPath))
                {
                    sb.AppendLine($"SHAR symbols file \"{Program.CommandLineSettings.CSVPath}\" not found.");
                    sb.AppendLine("Simpsons functions not mapped.");
                    sb.AppendLine();
                }
                else
                {
                    if (Program.CommandLineSettings.Verbose)
                        sw.Restart();

                    var lines = File.ReadAllLines(Program.CommandLineSettings.CSVPath);
                    var funcs = new List<FunctionEntry>(lines.Length - 1);

                    foreach (var line in lines.Skip(1))
                    {
                        if (!line.StartsWith("\"") || !line.EndsWith("\""))
                            continue;

                        var parts = line.Substring(1, line.Length - 2).Split(["\",\""], StringSplitOptions.None);
                        if (parts.Length != 3)
                            continue;

                        if (!ulong.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address))
                            continue;
                        if (!uint.TryParse(parts[1], out var size))
                            continue;
                        if (string.IsNullOrWhiteSpace(parts[2]))
                            continue;

                        funcs.Add(new(address, size, parts[2]));
                    }

                    funcsSorted = [.. funcs.OrderBy(x => x.Address)];

                    if (Program.CommandLineSettings.Verbose)
                    {
                        sw.Stop();
                        Console.WriteLine($"Read symbols file in {sw.Elapsed:mm\\:ss\\.fff}.");
                    }

                    if (Program.CommandLineSettings.Verbose)
                        sw.Restart();

                    var funcsAdded = new HashSet<ulong>();

                    foreach (var frame in frames)
                    {
                        if (frame.InstructionOffset == 0)
                            continue;

                        var func = FindFunction(funcsSorted, frame.InstructionOffset);
                        if (func == null)
                            continue;

                        if (!funcsAdded.Add(func.Address))
                            continue;

                        var addSyntheticSymbolCode = symbs.AddSyntheticSymbolWide(func.Address, func.Size, func.Name, DEBUG_ADDSYNTHSYM.DEFAULT, out _);
                        if (addSyntheticSymbolCode != 0)
                        {
                            switch ((uint)addSyntheticSymbolCode)
                            {
                                case 0x800700B7: // already exists
                                    continue;
                                default:
                                    sb.AppendLine($"Failed to add synthetic symbol {func.Name} (0x{func.Address:X}). Exit code: {addSyntheticSymbolCode:X}");
                                    break;
                            }
                        }
                    }

                    if (Program.CommandLineSettings.Verbose)
                    {
                        sw.Stop();
                        Console.WriteLine($"Loaded symbols in {sw.Elapsed:mm\\:ss\\.fff}.");
                    }
                }
            }

            if (sb.Length == 25)
                sb.Clear();
            else
                sb.AppendLine();

            client.SetOutputCallbacksWide(new DebugOutputCallback(ref sb));

            if (Program.CommandLineSettings.Verbose)
                sw.Restart();

            sb.AppendLine("=== EXCEPTION RECORD ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, ".exr -1", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();
            ctrl.ExecuteWide(DEBUG_OUTCTL.IGNORE, ".excr", DEBUG_EXECUTE.DEFAULT);

            sb.AppendLine("=== FAULTING INSTRUCTION ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "u @eip-5 L10", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

            sb.AppendLine("=== STACK TRACE ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "kn", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

            sb.AppendLine("=== REGISTERS ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "r", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

            var registerStringsHeaderAdded = false;
            if (registers.GetNumberRegisters(out var reguistersCount) == 0)
            {
                for (var i = 0u; i < reguistersCount; i++)
                {
                    if (registers.GetDescriptionWide(i, out var registerName, out var registerDesc) != 0)
                        continue;

                    if (registers.GetValue(i, out var registerValue) != 0)
                        continue;
                    var regValue = registerValue.I64;

                    if (registerName == "esp")
                    {
                        if (dataSpaces.ReadVirtual(regValue, Program.CommandLineSettings.StackDepth, out var stackBuffer) == 0)
                        {
                            for (int j = 0; j < Program.CommandLineSettings.StackDepth; j += 4)
                            {
                                uint stackValue = BitConverter.ToUInt32(stackBuffer, j);

                                if (TryReadString(ref dataSpaces, stackValue, out var espStr))
                                {
                                    if (!registerStringsHeaderAdded)
                                    {
                                        sb.AppendLine("=== REGISTER POINTERS (STRINGS) ===");
                                        registerStringsHeaderAdded = true;
                                    }
                                    sb.AppendLine($"esp: {regValue + (ulong)j:X8}  {stackValue:X8} - {espStr}");
                                }
                            }
                        }
                        continue;
                    }

                    if (regValue < 0x1000)
                        continue;

                    if (TryReadString(ref dataSpaces, regValue, out var str))
                    {
                        if (!registerStringsHeaderAdded)
                        {
                            sb.AppendLine("=== REGISTER POINTERS (STRINGS) ===");
                            registerStringsHeaderAdded = true;
                        }
                        sb.AppendLine($"{registerName}: {regValue:X8} - {str}");
                    }
                }
            }
            if (registerStringsHeaderAdded)
                sb.AppendLine();

            sb.AppendLine("=== STACK (RAW) ===");
            //ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "dps esp", DEBUG_EXECUTE.DEFAULT);
            if (registers.GetIndexByNameWide("esp", out var espIndex) == 0 && registers.GetValue(espIndex, out var espValue) == 0 && dataSpaces.ReadVirtual(espValue.I64, Program.CommandLineSettings.StackDepth, out var espBuffer) == 0)
            {
                for (int i = 0; i < espBuffer.Length; i += 4)
                {
                    ulong stackAddr = espValue.I64 + (ulong)i;
                    uint value = BitConverter.ToUInt32(espBuffer, i);
                    ulong addr = value;

                    string symbol = "";

                    if (addr >= simpsonsBase && addr < simpsonsBase + moduleSize)
                    {
                        var func = FindFunction(funcsSorted, addr);
                        if (func != null)
                        {
                            ulong offset = addr - func.Address;
                            symbol = $"Simpsons!{func.Name}+0x{offset:X}";
                        }
                        else
                        {
                            symbol = $"Simpsons+0x{addr - simpsonsBase:X}";
                        }
                    }

                    sb.AppendLine($"{stackAddr:X8}  {addr:X8} {symbol}");
                }
            }
            sb.AppendLine();

            if (Program.CommandLineSettings.DumpStrings)
            {
                sb.AppendLine("=== STRINGS ===");
                if (!string.IsNullOrEmpty(Program.CommandLineSettings.StringsFilter))
                    sb.AppendLine($"Filter: {Program.CommandLineSettings.StringsFilter}");

                var strings = DumpStrings(ref dataSpaces, simpsonsBase, moduleSize, 7);
                foreach (var s in strings)
                    sb.AppendLine(s);

                sb.AppendLine();
            }

            if (ctrl.GetLastEventInformationWide(out var eventType, out var processId, out var threadId, out var extraInformation, out var description) == 0)
            {
                var faultAddress = extraInformation.Exception.ExceptionRecord.ExceptionAddress;
                sb.AppendLine("=== FAULT ADDRESS ===");
                sb.AppendLine($"0x{faultAddress:X8}");
                sb.AppendLine();

                var startAddress = faultAddress - 32UL;
                var bytesRequested = 64u;
                if (faultAddress >= 32 && dataSpaces.ReadVirtual(startAddress, bytesRequested, out var buffer) == 0)
                {
                    sb.AppendLine("=== MEMORY AROUND FAULT ===");

                    for (var i = 0u; i < bytesRequested; i += 16)
                    {
                        var lineAddress = startAddress + i;
                        var hexBytes = new StringBuilder();
                        for (var j = 0; j < 16; j++)
                            if (i + j < bytesRequested)
                                hexBytes.Append($"{buffer[i + j]:X2} ");
                        sb.AppendLine($"{lineAddress:X8}: {hexBytes}");
                    }

                    sb.AppendLine();
                }
            }

            if (!Program.CommandLineSettings.NoModules)
            {
                sb.AppendLine("=== MODULES ===");
                ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "lmv", DEBUG_EXECUTE.DEFAULT);
                sb.AppendLine();
            }

            if (ctrl.GetSystemVersionValues(out var platformId, out var win32Major, out var win32Minor, out var kdMajor, out var kdMinor) == 0)
            {
                var platformString = platformId switch
                {
                    2 => "Windows NT",
                    1 => "Windows 9x",
                    _ => "Windows"
                };

                var buildType = kdMajor switch
                {
                    0xF => "Free",
                    0xC => "Checked",
                    _ => "Unknown Type"
                };

                sb.AppendLine("=== OS VERSION ===");
                if (win32Major == 6 && win32Minor == 2 && kdMinor == 9200)
                    sb.AppendLine("Windows 10/11 (Compatibility Mode: Windows 8)");
                else
                    sb.AppendLine($"Microsoft {platformString} {win32Major}.{win32Minor}.{kdMinor} ({buildType})");
                sb.AppendLine();
            }

            if (Program.CommandLineSettings.Verbose)
            {
                sw.Stop();
                Console.WriteLine($"Dumped output in {sw.Elapsed:mm\\:ss\\.fff}.");
            }

            client.SetOutputCallbacksWide(null);

            return sb.ToString();
        }
        finally
        {
            client.EndSession(DEBUG_END.ACTIVE_DETACH);
            client.Dispose();
        }
    }

    private static void LoadModuleDir(ref WDebugSymbols symbs, string path)
    {
        var appendPathCode = symbs.AppendSymbolPathWide(path);
        if (appendPathCode != 0)
            throw new Exception($"Failed to append symbol path \"{path}\". Exit code: {appendPathCode:X}");

        foreach (var directory in Directory.GetDirectories(path))
            LoadModuleDir(ref symbs, directory);
    }

    private static FunctionEntry FindFunction(List<FunctionEntry> funcs, ulong addr)
    {
        int left = 0, right = funcs.Count - 1;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            var f = funcs[mid];

            if (addr < f.Address)
            {
                right = mid - 1;
            }
            else if (addr >= f.Address + f.Size)
            {
                left = mid + 1;
            }
            else
            {
                return f;
            }
        }

        return null;
    }

    private static bool TryReadString(ref WDebugDataSpaces dataSpaces, ulong address, out string result)
    {
        result = null;

        const int maxLength = 256;
        var buffer = new byte[maxLength];

        if (dataSpaces.ReadVirtual(address, maxLength, out buffer) != 0)
            return false;

        int length = 0;

        for (; length < maxLength; length++)
        {
            byte b = buffer[length];

            if (b == 0)
                break;

            if (b < 0x20 || b > 0x7E)
                return false;
        }

        if (length == 0)
            return false;

        result = Encoding.ASCII.GetString(buffer, 0, length);
        return true;
    }

    private static List<string> DumpStrings(ref WDebugDataSpaces dataSpaces, ulong startAddress, ulong size, int minLen)
    {
        var results = new List<string>();
        var sb = new StringBuilder();

        var pageSize = 4096UL;
        var currentOffset = 0UL;

        while (currentOffset < size)
        {
            var currentAddr = startAddress + currentOffset;

            if (dataSpaces.QueryVirtual(currentAddr, out var mbi) == 0)
            {
                bool isReadable = mbi.State == MEM.COMMIT && !mbi.Protect.HasFlag(PAGE.NOACCESS) && !mbi.Protect.HasFlag(PAGE.GUARD);

                if (!isReadable)
                {
                    var bytesToSkip = mbi.BaseAddress + mbi.RegionSize - currentAddr;
                    currentOffset += bytesToSkip;

                    if (sb.Length >= minLen)
                        AddFilteredString(results, sb);

                    sb.Clear();
                    continue;
                }
            }

            var remainingInModule = size - currentOffset;
            var toRead = (uint)Math.Min(pageSize, remainingInModule);

            var readVirtualCode = dataSpaces.ReadVirtual(currentAddr, toRead, out byte[] buffer);
            if (readVirtualCode == 0 && buffer != null)
            {
                ExtractAsciiStrings(buffer, results, sb, minLen);
            }
            else
            {
                if (sb.Length >= minLen)
                    AddFilteredString(results, sb);

                sb.Clear();
            }

            currentOffset += toRead;
        }

        if (sb.Length >= minLen)
            AddFilteredString(results, sb);

        return results;
    }

    private static void ExtractAsciiStrings(byte[] data, List<string> results, StringBuilder sb, int minLen)
    {
        for (int i = 0; i < data.Length; i++)
        {
            var b = data[i];

            if (b >= 32 && b <= 126)
            {
                sb.Append((char)b);
            }
            else
            {
                if (sb.Length > 0)
                {
                    if (sb.Length >= minLen)
                        AddFilteredString(results, sb);

                    sb.Clear();
                }
            }
        }
    }

    private static void AddFilteredString(List<string> results, StringBuilder sb)
    {
        var s = sb.ToString();

        if (!string.IsNullOrEmpty(Program.CommandLineSettings.StringsFilter) && s.IndexOf(Program.CommandLineSettings.StringsFilter, StringComparison.InvariantCultureIgnoreCase) < 0)
            return;

        var isRepeating = true;
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] != s[0])
            {
                isRepeating = false;
                break;
            }
        }
        if (isRepeating)
            return;

        results.Add(s);
    }

    private class FunctionEntry(ulong address, uint size, string name)
    {
        public ulong Address { get; set; } = address;
        public uint Size { get; set; } = size;
        public string Name { get; set; } = name;
    }

    private class DebugOutputCallback : IDebugOutputCallbacksImp
    {
        private readonly StringBuilder _sb;

        public DebugOutputCallback(ref StringBuilder sb)
        {
            _sb = sb;
        }

        public int Output(DEBUG_OUTPUT Mask, string Text)
        {
            if (Mask.HasFlag(DEBUG_OUTPUT.NORMAL))
                _sb.AppendFormat("{1}", Mask, Text.Replace("\n", "\r\n"));

            return 0;
        }
    }
}
