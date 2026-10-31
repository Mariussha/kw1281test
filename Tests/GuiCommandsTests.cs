using BitFab.KW1281Test.Gui;
using Shouldly;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class GuiCommandsTests
{
    [TestMethod]
    public void BuildArgs_SkipsBlankOptionals_SplitsTextFields_KeepsFilePathsWhole()
    {
        Commands.BuildArgs("LoadEdc16Flash", [@"C:\my dumps\ecu.bin", "high", "", "unverified", ""])
            .ShouldBe([@"C:\my dumps\ecu.bin", "high", "unverified"]);
        Commands.BuildArgs("WriteEdc15Eeprom", [" 0x1E 0  0x1F 1 "]).ShouldBe(["0x1E", "0", "0x1F", "1"]);
        Commands.BuildArgs("DumpEeprom", ["", "", "cluster.bin"]).ShouldBe(["cluster.bin"]);
        Should.Throw<ArgumentException>(() => Commands.BuildArgs("GroupRead", ["", "fastinit"]));
    }
}
