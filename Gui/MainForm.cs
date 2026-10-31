using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace BitFab.KW1281Test.Gui;

/// <summary>
/// Front end for kw1281test: builds the command line from the form, runs kw1281test.exe (from
/// this folder) with piped stdio, shows its output and sends it keys for the interactive commands.
/// </summary>
internal sealed class MainForm : Form
{
    // Printed by kw1281test when its stdio is piped (see ConsoleIo there).
    private const string WaitingForInput = "WAITING_FOR_INPUT";
    private const string LivePrefix = "LIVE: ";
    private static readonly Regex Found = new(@"AutoDetect found: PORT=(\S+) BAUD=(\d+)");

    private static readonly string Folder = AppContext.BaseDirectory;
    private static readonly string Cli = Path.Combine(Folder, OperatingSystem.IsWindows() ? "kw1281test.exe" : "kw1281test");
    private static readonly string SettingsFile = Path.Combine(Folder, "kw1281test-gui.settings");

    private static readonly string[] Addresses =
    [
        "01 - Engine (ECU)", "02 - Transmission", "03 - ABS brakes", "08 - Climate control",
        "09 - Central electrics", "15 - Airbag", "17 - Instrument cluster", "19 - CAN gateway",
        "25 - Immobilizer", "35 - Central locking", "37 - Navigation", "46 - Comfort (CCM)", "56 - Radio",
    ];

    private readonly ComboBox _port = new() { Width = 130 };
    private readonly ComboBox _baud = new() { Width = 80 };
    private readonly ComboBox _address = new() { Width = 190 };
    private readonly ComboBox _command = new() { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _description = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TableLayoutPanel _params = new() { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
    private readonly List<Func<string>> _paramValues = [];
    private readonly TextBox _live = new()
    {
        ReadOnly = true, Dock = DockStyle.Fill, PlaceholderText = "Live values (Group Read / Basic Setting)",
        Font = new Font(FontFamily.GenericMonospace, 10F, FontStyle.Bold),
    };
    private readonly TextBox _log = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
        Dock = DockStyle.Fill, MaxLength = 0, Font = new Font(FontFamily.GenericMonospace, 9F),
    };
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly ToolTip _tips = new();
    private readonly GroupBox _connection;
    private readonly GroupBox _commandBox;
    private readonly Button _run;
    private readonly Button _stop;
    private readonly Button[] _keyButtons;

    private Process? _proc;
    private bool _procWrites;
    private string _idleStatus = "Ready";

    public MainForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        Text = "KW1281Test";
        ClientSize = new Size(940, 700);
        MinimumSize = new Size(760, 520);

        _baud.Items.AddRange(["10400", "9600", "4800", "1200"]);
        _baud.Text = "10400";
        _address.Items.AddRange(Addresses);
        _address.SelectedIndex = 0;
        _tips.SetToolTip(_port, "COM port, or the serial number of an FTDI cable");
        _tips.SetToolTip(_baud, "Usually 10400; some controllers use 9600 or 4800");
        _tips.SetToolTip(_address, "Controller address in hex; pick one or type it");
        var detect = Btn("Auto detect", async (_, _) => await AutoDetect());
        _tips.SetToolTip(detect, "Find the port and baud rate the selected controller answers on");
        _connection = Group("Connection", Flow(
            Lbl("Port"), _port, Btn("Refresh", (_, _) => RefreshPorts()),
            Lbl("Baud"), _baud, Lbl("Address"), _address, detect));

        _command.Items.AddRange([.. Commands.All.Keys]);
        _command.SelectedIndexChanged += (_, _) => ShowParams();
        var commandPanel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        commandPanel.Controls.Add(Flow(_command, _description));
        commandPanel.Controls.Add(_params);
        _commandBox = Group("Command", commandPanel);

        _run = Btn("Run", (_, _) => OnRun());
        _run.Font = new Font(Font, FontStyle.Bold);
        _stop = Btn("Stop", (_, _) => OnStop());
        _keyButtons =
        [
            KeyBtn("▲ Up", ConsoleKey.UpArrow), KeyBtn("▼ Down", ConsoleKey.DownArrow),
            KeyBtn("N  Next", ConsoleKey.N), KeyBtn("Y  Yes", ConsoleKey.Y), KeyBtn("Q  Quit", ConsoleKey.Q),
        ];
        var actions = Flow([_run, _stop, Lbl("   Keys:"), .. _keyButtons, Lbl("   "),
            Btn("Clear log", (_, _) => _log.Clear()),
            Btn("Open folder", (_, _) => Process.Start(new ProcessStartInfo(Folder) { UseShellExecute = true }))]);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(6) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.AddRange([_connection, _commandBox, actions, _live, _log]);
        Controls.Add(root);
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);
        Controls.Add(statusStrip);
        ResumeLayout();
    }

    // Not in the constructor: before its handle exists, a ComboBox drops a Text that isn't one of
    // its items (e.g. a remembered port that is unplugged right now).
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RefreshPorts();
        _command.SelectedItem = "ReadIdent";
        LoadSettings();
        SetRunning(false);
        AppendLog($"Dumps and KW1281Test.log are saved in {Folder}");
        if (!File.Exists(Cli))
        {
            AppendLog($"ERROR: {Path.GetFileName(Cli)} was not found. Put it in the same folder as this program.");
        }
    }

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    private static Label Lbl(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 0, 0, 0) };

    private static Button Btn(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }

    private Button KeyBtn(string text, ConsoleKey key) => Btn(text, (_, _) => SendKey(key));

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private static GroupBox Group(string text, Control content)
    {
        var group = new GroupBox { Text = text, AutoSize = true, Dock = DockStyle.Fill };
        group.Controls.Add(content);
        return group;
    }

    private void RefreshPorts()
    {
        var current = _port.Text;
        _port.Items.Clear();
        _port.Items.AddRange(SerialPort.GetPortNames().Distinct().OrderBy(p => p.Length).ThenBy(p => p).ToArray());
        _port.Text = current.Length > 0 || _port.Items.Count == 0 ? current : (string)_port.Items[0]!;
    }

    private void ShowParams()
    {
        var command = Commands.All[(string)_command.SelectedItem!];
        _description.Text = command.Description + (command.Writes ? "   [writes to the controller]" : "");
        _description.ForeColor = command.Writes ? Color.Firebrick : SystemColors.ControlText;

        _params.SuspendLayout();
        while (_params.Controls.Count > 0)
        {
            _params.Controls[0].Dispose();
        }
        _paramValues.Clear();
        foreach (var p in command.Params)
        {
            var row = new List<Control>();
            switch (p.Kind)
            {
                case Kind.Flag:
                    var check = new CheckBox { Text = p.Hint, AutoSize = true };
                    row.Add(check);
                    _paramValues.Add(() => check.Checked ? p.Label : "");
                    break;
                case Kind.Speed:
                    var speed = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
                    speed.Items.AddRange(["low", "medium", "high"]);
                    speed.SelectedItem = "medium";
                    row.Add(speed);
                    _paramValues.Add(() => (string)speed.SelectedItem!);
                    break;
                default:
                    var text = new TextBox { Width = p.Kind == Kind.Text ? 220 : 420 };
                    row.Add(text);
                    if (p.Kind != Kind.Text)
                    {
                        row.Add(Btn("Browse...", (_, _) => Browse(text, p.Kind == Kind.SaveFile)));
                    }
                    _paramValues.Add(() => text.Text);
                    break;
            }
            if (p.Kind != Kind.Flag)
            {
                row.Add(new Label { Text = p.Hint, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText });
            }

            var optional = p.Optional && p.Kind is not (Kind.Flag or Kind.Speed) ? " (optional)" : "";
            _params.Controls.Add(Lbl(p.Label + optional));
            var flow = Flow([.. row]);
            flow.WrapContents = false;
            flow.Margin = Padding.Empty;
            _params.Controls.Add(flow);
        }
        _params.ResumeLayout();
    }

    private void Browse(TextBox target, bool save)
    {
        using FileDialog dialog = save ? new SaveFileDialog() : new OpenFileDialog();
        dialog.Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*";
        dialog.InitialDirectory = Folder;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    /// <summary>The controller address as the CLI wants it: hex, e.g. "17 - Instrument cluster" -> "17".</summary>
    private string Address()
    {
        var address = _address.Text.Trim().Split(' ', 2)[0];
        if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            address = address[2..];
        }
        if (!int.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n) || n > 0xFF)
        {
            throw new ArgumentException("The address must be a hex controller address, e.g. 01 or 17.");
        }
        return address;
    }

    private async void OnRun()
    {
        var name = (string)_command.SelectedItem!;
        var command = Commands.All[name];
        List<string> args;
        try
        {
            var port = _port.Text.Trim();
            if (port.Length == 0)
            {
                throw new ArgumentException("Choose a port, or click Auto detect.");
            }
            if (!int.TryParse(_baud.Text.Trim(), out var baud) || baud <= 0)
            {
                throw new ArgumentException("The baud rate must be a number, e.g. 10400.");
            }
            args = [port, baud.ToString(CultureInfo.InvariantCulture), Address(), name,
                .. Commands.BuildArgs(name, [.. _paramValues.Select(v => v())])];
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (command.Writes && MessageBox.Show(this,
                $"{name} writes to controller {args[2]}. Wrong data, or an interruption (power, cable, " +
                "Stop), can leave the module unusable.\n\nContinue?",
                "Confirm write", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        await RunCli(args, command.Writes);
    }

    private async Task AutoDetect()
    {
        string address;
        try
        {
            address = Address();
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AppendLog($"Auto detect: trying controller {address} on every port at 10400/9600/4800 baud. " +
                  "Ignition on; this can take a minute.");
        await RunCli(["auto", "auto", address, "AutoDetect"], writes: false);
    }

    private async Task RunCli(List<string> args, bool writes)
    {
        if (!File.Exists(Cli))
        {
            MessageBox.Show(this, $"{Cli} was not found. Put it in the same folder as this program.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var startInfo = new ProcessStartInfo(Cli)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Folder,
        };
        args.ForEach(startInfo.ArgumentList.Add);

        using var proc = new Process { StartInfo = startInfo };
        proc.OutputDataReceived += (_, e) => OnOutputLine(e.Data);
        proc.ErrorDataReceived += (_, e) => OnOutputLine(e.Data);

        AppendLog("");
        AppendLog("> kw1281test " + string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));
        _live.Clear();
        _idleStatus = "Ready";
        _procWrites = writes;
        _proc = proc;
        SetRunning(true);
        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
        }
        catch (Win32Exception ex)
        {
            AppendLog($"Could not start {Cli}: {ex.Message}");
        }
        finally
        {
            _proc = null;
            if (!IsDisposed)
            {
                SetRunning(false);
            }
        }
    }

    /// <summary>Called on a thread-pool thread for every line kw1281test prints.</summary>
    private void OnOutputLine(string? line)
    {
        if (line == null)
        {
            return;
        }
        try
        {
            BeginInvoke(() => ShowOutputLine(line));
        }
        catch (InvalidOperationException)
        {
            // The window is closing.
        }
    }

    private void ShowOutputLine(string line)
    {
        if (line.StartsWith(LivePrefix, StringComparison.Ordinal))
        {
            _live.Text = line[LivePrefix.Length..];
            return;
        }
        if (line.Contains(WaitingForInput, StringComparison.Ordinal))
        {
            _status.Text = "Waiting for you: use the key buttons (or press N / Y / Q / Up / Down).";
            line = line.Replace(WaitingForInput, "").TrimEnd();
            if (line.Length == 0)
            {
                return;
            }
        }
        var found = Found.Match(line);
        if (found.Success)
        {
            _port.Text = found.Groups[1].Value;
            _baud.Text = found.Groups[2].Value;
            _idleStatus = $"Found the controller on {_port.Text} at {_baud.Text} baud.";
        }
        AppendLog(line);
    }

    private void AppendLog(string line) => _log.AppendText(line + Environment.NewLine);

    private void SendKey(ConsoleKey key)
    {
        if (_proc == null)
        {
            return;
        }
        try
        {
            _proc.StandardInput.WriteLine(key.ToString());
            _status.Text = "Running...";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Already exited.
        }
    }

    // Keyboard shortcuts for the interactive commands, like the console version.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        ConsoleKey? key = keyData switch
        {
            Keys.Up => ConsoleKey.UpArrow,
            Keys.Down => ConsoleKey.DownArrow,
            Keys.N => ConsoleKey.N,
            Keys.Y => ConsoleKey.Y,
            Keys.Q or Keys.Escape => ConsoleKey.Q,
            _ => null,
        };
        if (_proc != null && key != null)
        {
            SendKey(key.Value);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnStop()
    {
        if (_proc == null || (_procWrites && MessageBox.Show(this,
                "This command is writing to the controller. Stopping now can leave the module unusable.\n\n" +
                "Stop anyway?", "Stop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes))
        {
            return;
        }
        Kill();
        AppendLog("Stopped.");
    }

    private void Kill()
    {
        try
        {
            _proc?.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }

    private void SetRunning(bool running)
    {
        _connection.Enabled = _commandBox.Enabled = _run.Enabled = !running;
        _stop.Enabled = running;
        foreach (var button in _keyButtons)
        {
            button.Enabled = running;
        }
        _status.Text = running ? "Running..." : _idleStatus;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_proc != null)
        {
            if (MessageBox.Show(this, "A command is still running. Stop it and exit?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            Kill();
        }
        SaveSettings();
        base.OnFormClosing(e);
    }

    private void LoadSettings()
    {
        try
        {
            var lines = File.ReadAllLines(SettingsFile);
            (_port.Text, _baud.Text, _address.Text) = (lines[0], lines[1], lines[2]);
            if (Commands.All.ContainsKey(lines[3]))
            {
                _command.SelectedItem = lines[3];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or IndexOutOfRangeException)
        {
            // First run, or an unreadable settings file: keep the defaults.
        }
    }

    private void SaveSettings()
    {
        try
        {
            File.WriteAllLines(SettingsFile, [_port.Text, _baud.Text, _address.Text, (string)_command.SelectedItem!]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Read-only folder: settings just aren't remembered.
        }
    }
}
