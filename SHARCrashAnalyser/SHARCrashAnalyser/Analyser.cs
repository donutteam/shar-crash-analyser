using DbgEngWrapper;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
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

        var createCode = WDebugClient.DebugCreate(null, out var client);
        if (createCode != 0)
            throw new Exception($"Failed to create debug client. Exit code: {createCode}");

        var ctrl = (WDebugControl)client;
        var symbs = (WDebugSymbols)client;
        var dataSpaces = (WDebugDataSpaces)client;

        try
        {
            client.OpenDumpFileWide(filePath, 0);
            ctrl.WaitForEvent(DEBUG_WAIT.DEFAULT, uint.MaxValue);
            symbs.SetScopeFromStoredEvent();

            uint simpsonsIndex;
            ulong simpsonsBase = 0;

            var getNumberModulesCode = symbs.GetNumberModules(out var loadedModules, out var unloadedModules);
            if (getNumberModulesCode != 0)
                throw new Exception($"Failed to get number of modules. Exit code: {getNumberModulesCode}");

            for (var i = 0u; i < loadedModules; i++)
            {
                if (symbs.GetModuleNameStringWide(DEBUG_MODNAME.MODULE, i, 0, out string moduleName) != 0)
                    continue;

                if (moduleName.StartsWith("simpsons", StringComparison.InvariantCultureIgnoreCase))
                {
                    var getModuleCode = symbs.GetModuleByModuleNameWide(moduleName, 0, out simpsonsIndex, out simpsonsBase);
                    if (getModuleCode != 0)
                        throw new Exception($"Failed to get simpsons module. Exit code: {getModuleCode}");

                    break;
                }
            }

            if (simpsonsBase == 0)
                throw new Exception("Simpsons module not found in dump.");

            var getParametersCode = symbs.GetModuleParameters(1, [simpsonsBase], 0, out var parms);
            if (getParametersCode != 0)
                throw new Exception($"Failed to get module parameters. Exit code: {getParametersCode}");
            var moduleSize = parms[0].Size;
            var checksum = parms[0].Checksum;

            if (File.Exists(Program.CommandLineSettings.HacksPDBPath))
            {
                var appendPathCode = symbs.AppendSymbolPathWide(Path.GetDirectoryName(Program.CommandLineSettings.HacksPDBPath));
                if (appendPathCode != 0)
                    throw new Exception($"Failed to append symbol path. Exit code: {appendPathCode}");

                var getModuleCode = symbs.GetModuleByModuleNameWide("Hacks", 0, out var hacksIndex, out _);
                if (getModuleCode != 0)
                    throw new Exception($"Failed to get hacks module. Exit code: {getModuleCode}");

                var reloadCode = symbs.ReloadWide($"/f /i {hacksIndex} \"{Program.CommandLineSettings.HacksPDBPath}\"");
                if (reloadCode != 0)
                    throw new Exception($"Failed to reload for hacks PDB. Exit code: {reloadCode}");
            }

            var getStackTraceCode = ctrl.GetStackTraceEx(0UL, 0UL, 0UL, 100, out var frames);
            if (getStackTraceCode != 0)
                throw new Exception($"Failed to get stack trace. Exit code: {getStackTraceCode}");

            var sb = new StringBuilder();

            if (!KnownChecksums.TryGetValue(checksum, out var release))
                release = "Unkown";
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
                    var lines = File.ReadAllLines(Program.CommandLineSettings.CSVPath);
                    var funcs = new List<FunctionEntry>(lines.Length - 1);

                    foreach (var line in lines.Skip(1))
                    {
                        if (!line.StartsWith("\"") || !line.EndsWith("\""))
                            continue;

                        var parts = line.Substring(1, line.Length - 2).Split(new string[] { "\",\"" }, StringSplitOptions.None);
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

                    foreach (var frame in frames)
                    {
                        if (frame.InstructionOffset == 0)
                            continue;

                        var func = funcs.FirstOrDefault(f => frame.InstructionOffset >= f.Address && frame.InstructionOffset < (f.Address + f.Size));
                        if (func == null)
                            continue;

                        var addSyntheticSymbolCode = symbs.AddSyntheticSymbolWide(func.Address, func.Size, func.Name, DEBUG_ADDSYNTHSYM.DEFAULT, out _);
                        if (addSyntheticSymbolCode != 0)
                            throw new Exception($"Failed to add synthetic symbole. Exit code: {addSyntheticSymbolCode}");
                    }
                }
            }

            client.SetOutputCallbacksWide(new DebugOutputCallback(ref sb));

            sb.AppendLine("=== EXCEPTION RECORD ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, ".exr -1", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

            ctrl.ExecuteWide(DEBUG_OUTCTL.IGNORE, ".excr", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine("=== REGISTERS ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "r", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

            sb.AppendLine("=== STACK TRACE ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "kn", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

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

                    for (var i = 0u; i < bytesRequested; i += 8)
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

            sb.AppendLine("=== MODULES ===");
            ctrl.ExecuteWide(DEBUG_OUTCTL.AMBIENT_TEXT, "lmv", DEBUG_EXECUTE.DEFAULT);
            sb.AppendLine();

            client.SetOutputCallbacksWide(null);

            return sb.ToString();
        }
        finally
        {
            client.Dispose();
        }
    }

    private class FunctionEntry
    {
        public ulong Address { get; set; }
        public uint Size { get; set; }
        public string Name { get; set; }

        public FunctionEntry(ulong address, uint size, string name)
        {
            Address = address;
            Size = size;
            Name = name;
        }
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
