Windows GUI for kw1281test (Windows 10/11, 64-bit, no .NET install needed).

**Install:** unzip both exes into the same folder and run `kw1281test-gui.exe`. Windows may warn that the app is unrecognized (it isn't signed): click **More info** → **Run anyway**.

**New in this build**
- `kw1281test-gui.exe`: every command from a window, with port/baud/address pickers, **Auto detect** for the port and baud rate, Up/Down/N/Y/Q buttons for interactive commands, and confirmation before anything that writes.
- `AutoDetect` command (also usable from the command line).
- EDC15 EEPROM read: connection fix.
- `DumpEeprom` (Airbag/Cluster) works with no file name.
- EDC15 EEPROM dumps get timestamped names, so a new dump never overwrites a backup.

**Known issue:** writing an EDC15 EEPROM (`LoadEdc15Eeprom`) can fail partway. Always keep a backup dump before writing anything.
