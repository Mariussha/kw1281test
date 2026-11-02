namespace BitFab.KW1281Test.Gui;

internal enum Kind { Text, OpenFile, SaveFile, Speed, Flag }

/// <summary>
/// One command argument, shown as one input field. A Flag is a checkbox that adds its Label as a
/// keyword argument (e.g. "full").
/// </summary>
internal record Param(string Label, string Hint, Kind Kind = Kind.Text, bool Optional = false);

internal record Command(string Description, bool Writes, Param[] Params);

/// <summary>The CLI commands (mirrors Program.ShowUsage) and how the GUI fields become arguments.</summary>
internal static class Commands
{
    private static Param Req(string label, string hint) => new(label, hint);
    private static Param Opt(string label, string hint) => new(label, hint, Optional: true);
    private static Param Flag(string keyword, string hint) => new(keyword, hint, Kind.Flag, true);
    private static Param Load(string hint) => new("File", hint, Kind.OpenFile);

    private static readonly Param Save = new("File", "Blank = automatic name", Kind.SaveFile, true);
    private static readonly Param Speed = new("Speed", "Link speed", Kind.Speed, true);
    private static readonly Param Start = Req("Start", "Start address, decimal or hex (e.g. 0x2000)");
    private static readonly Param Length = Req("Length", "Number of bytes, decimal or hex (e.g. 0x400)");
    private static readonly Param Address = Req("Address", "Decimal or hex (e.g. 0x1109)");
    private static readonly Param Value = Req("Value", "Byte value, decimal or hex (e.g. 0x8A)");
    private static readonly Param Channel = Req("Channel", "0-99");
    private static readonly Param ChannelValue = Req("Value", "0-65535");
    private static readonly Param Login = Opt("Login", "0-65535");
    private static readonly Param Group = Req("Group", "0-255 (0 = raw data). Up/Down change it while running.");
    private static readonly Param FastInit = Flag(
        "fastinit", "ISO 14230 fast init (only for a later CAN-init EDC16 that ignores slow init)");

    private static Command Reads(string description, params Param[] ps) => new(description, false, ps);
    private static Command Writes(string description, params Param[] ps) => new(description, true, ps);

    public static readonly SortedDictionary<string, Command> All = new()
    {
        ["ActuatorTest"] = Reads("Run the output (actuator) tests one at a time: N = next, Q = quit."),
        ["AdaptationRead"] = Reads("Read an adaptation channel.", Channel, Login),
        ["AdaptationSave"] = Writes("Change and save an adaptation channel.", Channel, ChannelValue, Login),
        ["AdaptationTest"] = Reads(
            "Try an adaptation channel value without saving it.", Channel, ChannelValue, Login),
        ["AutoScan"] = Reads("List every controller that answers (tries addresses 00-7F; slow)."),
        ["BasicSetting"] = Reads("Basic settings: live values of a group. Up/Down change group, Q quits.", Group),
        ["ClarionVWPremium4SafeCode"] = Reads("Get the SAFE code of a Clarion VW Premium 4 radio (address 56)."),
        ["ClearFaultCodes"] = Writes("Clear the fault codes."),
        ["DelcoVWPremium5SafeCode"] = Reads("Get the SAFE code of a Delco VW Premium 5 radio (address 56)."),
        ["DumpEdc15Eeprom"] = Reads("Save the EDC15 ECU EEPROM to a file.", Save),
        ["DumpEdc15Flash"] = Reads("Save the EDC15 ECU flash to a file.", Speed, Save),
        ["DumpEdc15FlashBoot"] = Reads(
            "Save the EDC15 flash in boot mode (ECU must be put in C167 boot mode before power-up).", Save),
        ["DumpEdc16Flash"] = Reads("Save the EDC16 ECU 2 MB flash to a file.", Speed, Save),
        ["DumpEeprom"] = Reads(
            "Save EEPROM to a file. Airbag/Cluster: leave Start and Length blank to save all of it.",
            Opt("Start", Start.Hint), Opt("Length", Length.Hint), Save),
        ["DumpMarelliMem"] = Reads("Save Marelli cluster memory to a file.", Start, Length, Save),
        ["DumpMem"] = Reads("Save memory to a file.", Start, Length, Save),
        ["DumpRam"] = Reads("Save RAM to a file.", Start, Length, Save),
        ["DumpRBxMem"] = Reads(
            "Save Bosch RBx cluster memory to a file. Cluster: leave Start and Length blank to save all.",
            Opt("Start", Start.Hint), Opt("Length", Length.Hint), Save),
        ["DumpRom"] = Reads("Save ROM to a file.", Start, Length, Save),
        ["FindLogins"] = Reads("Find logins the controller accepts, starting from a known good one.",
            Req("Login", "Known good login, 0-65535")),
        ["GetSKC"] = Reads("Get the immobilizer secret key code (SKC)."),
        ["GroupRead"] = Reads("Live measuring blocks. Up/Down change group, Q quits.", Group, FastInit),
        ["LoadEdc15Eeprom"] = Writes("Write a file into the EDC15 EEPROM.",
            Opt("Start", "EEPROM start address, decimal or hex (default 0)"), Load("EEPROM image to write")),
        ["LoadEdc15Flash"] = Writes("Write a flash image to an EDC15 ECU.",
            Load("Flash image to write"), Speed,
            Flag("full", "Write every sector (default: only sectors whose checksum differs)"),
            Flag("noverify", "Skip the checksum verify after writing")),
        ["LoadEdc15FlashBoot"] = Writes(
            "Write a flash image in boot mode (ECU must be put in C167 boot mode before power-up).",
            Load("Flash image to write")),
        ["LoadEdc16Flash"] = Writes("Write a 2 MB flash image to an EDC16 ECU.",
            Load("2 MB flash image to write"), Speed,
            Flag("full", "Write every block (default: only blocks whose checksum differs)"),
            Flag("unverified", "Skip the checksum verify after writing"),
            Flag("fastinit", "Prime with an ISO 14230 fast init (only for a later CAN-init EDC16)")),
        ["LoadEeprom"] = Writes("Write a file into the EEPROM.", Start, Load("Binary data to write")),
        ["MapEeprom"] = Reads("Map the EEPROM (Cluster / CCM)."),
        ["ReadEeprom"] = Reads("Read one EEPROM byte.", Address),
        ["ReadFaultCodes"] = Reads("Read the fault codes."),
        ["ReadIdent"] = Reads("Read the controller identification."),
        ["ReadRAM"] = Reads("Read one RAM byte.", Address),
        ["ReadROM"] = Reads("Read one ROM byte.", Address),
        ["ReadSoftwareVersion"] = Reads("Read the software version (Cluster only)."),
        ["Reset"] = Reads("Reset the controller."),
        ["SetSoftwareCoding"] = Writes("Change the software coding.",
            Req("Coding", "Decimal or hex, max 32767"), Req("Workshop", "Workshop code, decimal or hex, max 99999")),
        ["ToggleRB4Mode"] = Writes("Toggle the Bosch RB4 cluster mode."),
        ["WriteEdc15Eeprom"] = Writes("Change bytes in the EDC15 EEPROM.",
            Req("Changes", "Address/value pairs, e.g. 0x1E 0x00 0x1F 0x01 (addresses 0-0x1FF)")),
        ["WriteEeprom"] = Writes("Write one EEPROM byte.", Address, Value),
        ["WriteRAM"] = Writes("Write one RAM byte.", Address, Value),
    };

    /// <summary>
    /// The CLI arguments after the command name. values[i] is the field for Params[i]. Blank
    /// optional fields are left out (the CLI's optional arguments are trailing or can come in any
    /// order); Text fields may hold several space-separated arguments, file paths stay whole.
    /// </summary>
    /// <exception cref="ArgumentException">A required field is blank.</exception>
    public static List<string> BuildArgs(string command, IReadOnlyList<string> values)
    {
        var ps = All[command].Params;
        var args = new List<string>();
        for (var i = 0; i < ps.Length; i++)
        {
            var value = values[i].Trim();
            if (value.Length == 0)
            {
                if (!ps[i].Optional)
                {
                    throw new ArgumentException($"{command}: {ps[i].Label} is required.");
                }
            }
            else if (ps[i].Kind == Kind.Text)
            {
                args.AddRange(value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                args.Add(value);
            }
        }
        return args;
    }
}
