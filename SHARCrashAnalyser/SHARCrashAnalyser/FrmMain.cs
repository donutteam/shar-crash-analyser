using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SHARCrashAnalyser;

public partial class FrmMain : Form
{
    public FrmMain() => InitializeComponent();

    private void FrmMain_Load(object sender, EventArgs e)
    {
        Text = Program.Title;
        if (!string.IsNullOrWhiteSpace(Program.CommandLineSettings.DumpPath))
            AnalyseDump(Program.CommandLineSettings.DumpPath);

        RTBAnalysis.AllowDrop = true;
        RTBAnalysis.DragDrop += FrmMain_DragDrop;
        RTBAnalysis.DragEnter += FrmMain_DragEnter;
    }

    private void FrmMain_DragDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files)
        {
            if (Path.GetExtension(file).Equals(".dmp", StringComparison.InvariantCultureIgnoreCase))
            {
                AnalyseDump(file);
                return;
            }
        }
    }

    private void FrmMain_DragEnter(object sender, DragEventArgs e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void TxtDumpPath_Enter(object sender, EventArgs e) => TxtDumpPath.SelectAll();

    private void BtnBrowse_Click(object sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog()
        {
            CheckFileExists = true,
            CheckPathExists = true,
            Filter = "Crash Dumps (*.dmp)|*.dmp",
            Multiselect = false,
            Title = "Open SHAR Crash Dump",
            InitialDirectory = string.IsNullOrWhiteSpace(TxtDumpPath.Text) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Lucas' Simpsons Hit & Run Mod Launcher", "Crashes") : Path.GetDirectoryName(TxtDumpPath.Text)
        };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        AnalyseDump(ofd.FileName);
    }

    private void AnalyseDump(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show($"Error reading crash dump:\r\nFile path \"{filePath}\" not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            BtnBrowse.Enabled = false;
            TxtDumpPath.Text = filePath;

            string dump = Analyser.AnalyseDump(filePath);
            if (Program.CommandLineSettings.NoColour)
            {
                RTBAnalysis.Clear();
                RTBAnalysis.Text = dump;
            }
            else
            {
                SetColouredRTBText(RTBAnalysis, dump);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading crash dump:\r\n{ex}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            BtnBrowse.Enabled = true;
        }
    }

    private void SetColouredRTBText(RichTextBox rtb, string text)
    {
        rtb.Clear();
        rtb.Text = text;

        HighlightRegex(rtb, @"=== .* ===", Color.Blue, true);

        HighlightRegex(rtb, @"\b(eax|ebx|ecx|edx|esi|edi|ebp|esp|eip|efl)\b", Color.DarkSalmon, true);

        HighlightRegex(rtb, @"\b[0-9a-fA-F]{8}\b", Color.DarkGreen, false);
    }

    private void HighlightRegex(RichTextBox rtb, string pattern, Color colour, bool bold)
    {
        var matches = Regex.Matches(rtb.Text, pattern, RegexOptions.IgnoreCase);
        var originalSelection = rtb.SelectionStart;

        foreach (Match m in matches)
        {
            rtb.Select(m.Index, m.Length);
            rtb.SelectionColor = colour;
            if (bold)
                rtb.SelectionFont = new(rtb.Font, FontStyle.Bold);
        }

        rtb.SelectionStart = originalSelection;
        rtb.SelectionLength = 0;
        rtb.SelectionColor = rtb.ForeColor;
    }
}
