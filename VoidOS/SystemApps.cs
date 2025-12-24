using static VoidOS.Kernel;
using Cosmos.Core;
using Cosmos.HAL;
using Cosmos.System;
using Cosmos.System.FileSystem.Listing;
using Cosmos.System.FileSystem.VFS;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using VoidOS.Models;
using Sys = Cosmos.System;

namespace VoidOS.Apps
{
    public class StartMenu
    {
        public int X, Y, Width, Height;
        public Color BackgroundColor;
        public List<Control> Controls = new List<Control>();

        public StartMenu(int x, int y, int width, int height, Color backgroundColor)
        {
            X = x; Y = y; Width = width; Height = height; BackgroundColor = backgroundColor;
        }

        public void Draw(Canvas canvas, Font font)
        {
            canvas.DrawFilledRectangle(BackgroundColor, X, Y, Width, Height);
            canvas.DrawRectangle(Color.Black, X, Y, Width, Height);

            foreach (var control in Controls)
            {
                var dummyWindow = new Window(X, Y, Width, Height, "", Color.Transparent);
                control.ParentWindow = dummyWindow;
                control.Draw(canvas, font);
            }
        }
    }
    public class TerminalApp : Window
    {
        private List<string> outputLines = new List<string>();
        private List<string> commandHistory = new List<string>();
        private string prompt = ">";
        private string currentInput = "";
        private string currentPath = @"0:\";
        private const int MaxLines = 29;
        private const int LineHeight = 15;
        private const int Margin = 5;

        public TerminalApp(int x, int y, int width, int height, string title, Color color)
            : base(x, y, width, height, title, color)
        {
            outputLines.Add("VoidOS Terminal v1.0");
            outputLines.Add("Type 'help' for available commands");
            outputLines.Add(" ");
        }

        public void AddOutput(string text)
        {
            outputLines.Add(text);
            if (outputLines.Count > MaxLines)
            {
                outputLines.RemoveAt(0);
            }
        }

        public void ProcessCommand(string command)
        {
            AddOutput($"{currentPath}{prompt} " + command);

            string[] parts = command.Split(' ');
            string cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "help":
                    AddOutput("Available commands:");
                    AddOutput("  help         |- Show this help message");
                    AddOutput("  clear/cls    |- Clear the terminal");
                    AddOutput("  date         |- Show current date and time");
                    AddOutput("  time         |- Show current time");
                    AddOutput("  sysinfo      |- Show system information");
                    AddOutput("  cpu          |- Show detailed CPU information");
                    AddOutput("  mem          |- Show detailed memory information");
                    AddOutput("  echo [text]  |- Display the specified text");
                    AddOutput("  color [0-15] |- Change terminal color");
                    AddOutput("  ver          |- Show OS version");
                    AddOutput("  about        |- About information");
                    AddOutput("  reboot       |- Reboot the system");
                    AddOutput("  shutdown     |- Shutdown the system");
                    AddOutput("  ps           |- List running processes");
                    AddOutput("  history      |- Show command history");
                    AddOutput("  beep         |- Make a beep sound");
                    AddOutput("  uptime       |- Show system uptime");
                    AddOutput("  dir          |- Show a list of files and directories");
                    AddOutput("  cd           |- Use to move between directories");
                    AddOutput("  see/peek     |- Opens the file with notepath");
                    AddOutput("  disks        |- List the system disks");
                    AddOutput("  exit         |- Close the terminal");
                    break;


                case "peek":
                case "see":
                    var filePath = string.Join(" ", parts, 1, parts.Length - 1);
                    if (filePath.StartsWith(@"0:\"))
                    {
                        if (VFSManager.FileExists(filePath))
                        {
                            var item = VFSManager.GetFile(filePath);
                            if (item.mEntryType == DirectoryEntryTypeEnum.File && item.mName.Contains(".txt"))
                            {
                                var notepath = new NotePathApp(100, 100, 400, 300, "NotePath", Color.White);
                                windows.Add(notepath);

                                notepath.OpenFile(item.mFullPath, item.mName);
                                break;
                            }
                        }
                        else
                            AddOutput($"File dont exist: {VoidPath.Combine(currentPath, filePath)}");
                    }
                    if (VFSManager.FileExists(VoidPath.Combine(currentPath, filePath)))
                    {
                        foreach (var item in VFSManager.GetDirectoryListing(currentPath))
                        {
                            if(item.mName == filePath && item.mEntryType == DirectoryEntryTypeEnum.File && item.mName.Contains(".txt"))
                            {
                                var notepath = new NotePathApp(100, 100, 400, 300, "NotePath", Color.White);
                                windows.Add(notepath);

                                notepath.OpenFile(item.mFullPath, item.mName);
                                break;
                            }
                        }
                    }
                    else
                        AddOutput($"File dont exist: {VoidPath.Combine(currentPath, filePath)}");
                    break;

                case "disks":
                    var disks = VFSManager.GetDisks();

                    AddOutput("");
                    AddOutput($"System Disks | Total: {disks.Count}");
                    AddOutput("");

                    for (int i = 0; i < disks.Count; i++)
                    {
                        if (disks[i].Type == Cosmos.HAL.BlockDevice.BlockDeviceType.HardDrive)
                            AddOutput($"Disk {i} | Type: HardDrive    | {"Size: " + disks[i].Host.BlockCount * disks[i].Host.BlockSize / 1024 / 1024 + " MB"}");
                        if (disks[i].Type == Cosmos.HAL.BlockDevice.BlockDeviceType.Removable)
                            AddOutput($"Disk {i} | Type: Removable    | {"Size: " + disks[i].Host.BlockCount * disks[i].Host.BlockSize / 1024 / 1024 + " MB"}");
                        if (disks[i].Type == Cosmos.HAL.BlockDevice.BlockDeviceType.RemovableCD)
                            AddOutput($"Disk {i} | Type: RemovableCD  | {"Size: " + disks[i].Host.BlockCount * disks[i].Host.BlockSize / 1024 / 1024 + " MB"}");
                    }
                    AddOutput("");
                    break;

                case "cd":
                    if (parts.Length < 2) AddOutput("Usage: cd <Directory Name>");
                    var path = string.Join(" ", parts, 1, parts.Length - 1);

                    if (path == ".." && currentPath != @"0:\")
                    {
                        string trimmedPath = currentPath.TrimEnd('\\');
                        int lastSlash = trimmedPath.LastIndexOf('\\');
                        string parentPath = trimmedPath.Substring(0, lastSlash);

                        currentPath = parentPath + @"\";
                        break;
                    }

                    if (path.StartsWith(@"0:\"))
                    {
                        if (VFSManager.DirectoryExists(path))
                        {
                            currentPath = path;
                            break;
                        }
                        else
                        {
                            AddOutput($"Directory dont exist: {path}");
                        }
                    }

                    if (VFSManager.DirectoryExists(VoidPath.Combine(currentPath, path)))
                    {
                        currentPath = VoidPath.Combine(currentPath, path);
                        break;
                    }
                    else
                    {
                        AddOutput($"Directory dont exist: {VoidPath.Combine(currentPath, path)}");
                    }
                    break;

                case "directories":
                case "dir":
                case "ls":
                    var dirs = VFSManager.GetDirectoryListing(currentPath);
                    int dirCount = 0, fileCount = 0;

                    AddOutput($"Directory of {currentPath}");
                    AddOutput("");
                    for (int i = 0; i < dirs.Count; i++)
                    {
                        if (dirs[i].mEntryType == DirectoryEntryTypeEnum.Directory)
                        {
                            AddOutput($" <DIR>  {dirs[i].mName}");
                            dirCount++;
                        }
                        else if (dirs[i].mEntryType == DirectoryEntryTypeEnum.File)
                        {
                            AddOutput($"        {dirs[i].mName}");
                            fileCount++;
                        }
                    }
                    AddOutput("");
                    AddOutput($"Total Directories: {dirCount} | Total Files: {fileCount}");
                    AddOutput("");
                    break;

                case "clear":
                case "cls":
                case "clr":
                    outputLines.Clear();
                    break;

                case "date":
                    AddOutput($"Current date: {RTC.DayOfTheMonth}/{RTC.Month}/{RTC.Year}");
                    AddOutput($"Current time: {RTC.Hour}:{RTC.Minute}:{RTC.Second}");
                    break;

                case "time":
                    AddOutput($"Current time: {RTC.Hour}:{RTC.Minute}:{RTC.Second}");
                    break;

                case "sysinfo":
                    AddOutput($"CPU Speed: {CPU.GetCPUCycleSpeed() / 1_000_000} MHz");
                    AddOutput($"Used Memory: {GCImplementation.GetUsedRAM() * 1024 * 1024} MB");
                    AddOutput($"Available Memory: {GCImplementation.GetAvailableRAM()} MB");
                    AddOutput($"Screen Resolution: {ScreenWidth}x{ScreenHeight}");
                    break;

                case "cpu":
                    AddOutput($"CPU Information:");
                    AddOutput($"  Vendor: {CPU.GetCPUVendorName()}");
                    AddOutput($"  Brand: {CPU.GetCPUBrandString()}");
                    AddOutput($"  Speed: {CPU.GetCPUCycleSpeed() / 1_000_000} MHz");
                    AddOutput($"  Cores: {Environment.ProcessorCount}");
                    break;

                case "mem":
                    AddOutput($"Memory Information:");
                    AddOutput($"  Total RAM: {GCImplementation.GetAvailableRAM()} MB");
                    AddOutput($"  Used RAM: {GCImplementation.GetUsedRAM() * 1024 * 1024} MB");
                    AddOutput($"  Free RAM: {GCImplementation.GetAvailableRAM() - (GCImplementation.GetUsedRAM() * 1024 * 1024)} MB");
                    break;

                case "echo":
                    if (parts.Length > 1)
                    {
                        AddOutput(string.Join(" ", parts, 1, parts.Length - 1));
                    }
                    break;

                case "theme":
                case "color":
                    if (parts.Length > 1)
                    {
                        if (int.TryParse(parts[1], out int colorIndex) && colorIndex >= 0 && colorIndex <= 15)
                        {
                            var colors = new Color[]
                            {
                                    Color.Black, Color.DarkBlue, Color.DarkGreen, Color.DarkCyan,
                                    Color.DarkRed, Color.DarkMagenta, Color.GreenYellow, Color.Gray,
                                    Color.DarkGray, Color.Blue, Color.Green, Color.Cyan,
                                    Color.Red, Color.Magenta, Color.Yellow, Color.White
                            };

                            this.Color = colors[colorIndex];
                            AddOutput($"Terminal color changed to {colorIndex}");
                        }
                        else
                        {
                            AddOutput("Color index must be between 0 and 15");
                        }
                    }
                    else
                    {
                        AddOutput("Usage: color [0-15]");
                    }
                    break;

                case "version":
                case "ver":
                    AddOutput("VoidOS v1.2.1");
                    AddOutput("Build: 2025");
                    break;

                case "abt":
                case "about":
                    AddOutput("VoidOS v1.1.0");
                    AddOutput("A Simple GUI Operating System");
                    AddOutput("Copyright (c) 2025");
                    break;

                case "restart":
                case "reboot":
                    AddOutput("Rebooting system...");
                    Sys.Power.Reboot();
                    break;

                case "halt":
                case "shutdown":
                    AddOutput("Shutting down system...");
                    Sys.Power.Shutdown();
                    break;

                case "ps":
                case "processes":
                case "processlist":
                    AddOutput("Running Processes:");
                    AddOutput($"  PID  NAME           CPU   MEM");
                    AddOutput($"  ---  ----           ---   ---");
                    AddOutput($"  001  Kernel         5%    12MB");
                    AddOutput($"  002  GUI            15%   24MB");
                    AddOutput($"  003  Terminal       2%    4MB");
                    break;

                case "history":
                    AddOutput("Command History:");
                    for (int i = Math.Max(0, commandHistory.Count - 10); i < commandHistory.Count; i++)
                    {
                        AddOutput($"  {i + 1}: {commandHistory[i]}");
                    }
                    break;

                case "beeb":
                case "beep":
                    Cosmos.System.PCSpeaker.Beep();
                    AddOutput("Beep!");
                    break;

                case "uptime":
                    AddOutput($"System uptime: {RTC.Second / 3600}h {(RTC.Second % 3600) / 60}m {RTC.Second % 60}s");
                    break;

                case "exit":
                case "quit":
                    windows.Remove(this);
                    break;

                default:
                    AddOutput($"Unknown command: {cmd}");
                    AddOutput("Type 'help' for available commands");
                    break;
            }

            commandHistory.Add(command);
            if (commandHistory.Count > 100)
            {
                commandHistory.RemoveAt(0);
            }
        }

        public void HandleKey(KeyEvent keyEvent)
        {
            if (keyEvent.Key == ConsoleKeyEx.Enter)
            {
                ProcessCommand(currentInput);
                currentInput = "";
            }
            else if (keyEvent.Key == ConsoleKeyEx.Backspace)
            {
                if (currentInput.Length > 0)
                {
                    currentInput = currentInput.Substring(0, currentInput.Length - 1);
                }
            }
            else if (keyEvent.KeyChar != '\0')
            {
                currentInput += keyEvent.KeyChar;
            }
        }

        public override void Draw(Canvas canvas, Font font)
        {
            base.Draw(canvas, font);
            canvas.DrawFilledRectangle(Color.Black, X + Margin, Y + TitleBarHeight + Margin,
                                       Width - 2 * Margin, Height - TitleBarHeight - 2 * Margin);

            int y = Y + TitleBarHeight + Margin + 5;
            foreach (string line in outputLines)
            {
                canvas.DrawString(line, font, Color.White, X + Margin + 5, y);
                y += LineHeight;
            }
            canvas.DrawString($"{currentPath}{prompt} " + currentInput, font, Color.White, X + Margin + 5, y);

            int cursorX = X + Margin + 5 + (currentInput.Length + (currentPath.Length + 1 + prompt.Length)) * font.Width;
            canvas.DrawLine(Color.White, cursorX, y, cursorX, y + font.Height);
        }
    }
    public class FileExplorerApp : Window
    {
        private List<IOEntry> entries;
        private string currentPath;
        private int selectedIndex = -1;
        private const int EntryHeight = 18;
        private const int IconSize = 16;
        private const int Margin = 5;

        public FileExplorerApp(int x, int y, int width, int height, string title, Color color)
            : base(x, y, width, height, title, color)
        {
            currentPath = @"0:\";
            entries = new List<IOEntry>();
            var upButton = new Button(Width - 70, 30, 65, 25, "Up", Color.Gray, Color.White);
            upButton.OnClick = () => { GoUp(); };
            Controls.Add(upButton);
            Refresh();
        }

        private void Refresh()
        {
            entries.Clear();
            try
            {
                var listing = VFSManager.GetDirectoryListing(currentPath);
                foreach (var entry in listing)
                {
                    entries.Add(new IOEntry(entry));
                }
            }
            catch (Exception e)
            {
                var errorEntry = new IOEntry($"Error: {e.GetType().Name} Message: {e.Message}", "0:/", false);
                entries.Add(errorEntry);
            }
        }

        public void GoUp()
        {
            if (currentPath == @"0:\") return;

            string trimmedPath = currentPath.TrimEnd('\\');
            int lastSlash = trimmedPath.LastIndexOf('\\');
            string parentPath = trimmedPath.Substring(0, lastSlash);

            currentPath = parentPath + @"\";
            Refresh();
        }

        public void HandleMouseClick(int mouseX, int mouseY)
        {
            foreach (var control in Controls)
            {
                if (control.IsPointInside(mouseX, mouseY))
                {
                    if (control is Button button) { button.OnClick?.Invoke(); }
                    return;
                }
            }

            int listY = Y + TitleBarHeight + Margin + 30;
            int clickIndex = (mouseY - listY) / EntryHeight;

            if (clickIndex >= 0 && clickIndex < entries.Count)
            {
                var entry = entries[clickIndex];
                if (entry.IsDirectory)
                {
                    currentPath = entry.FullPath.Replace('/', '\\');
                    if (!currentPath.EndsWith(@"\"))
                    {
                        currentPath += @"\";
                    }
                    Refresh();
                }

                if (!entry.IsDirectory)
                {
                    if(entry.Name.Contains(".txt") || entry.Name.Contains(".text"))
                    {
                        var notepath = new NotePathApp(100, 100, 400, 300, "NotePath", Color.White);
                        windows.Add(notepath);

                        notepath.OpenFile(entry.FullPath, entry.Name);
                    }
                }

                selectedIndex = clickIndex;
            }
        }

        public override void Draw(Canvas canvas, Font font)
        {
            base.Draw(canvas, font);

            canvas.DrawFilledRectangle(Color.DarkGray, X + Margin, Y + TitleBarHeight + Margin, Width - 2 * Margin, Height - TitleBarHeight - 2 * Margin);
            canvas.DrawString(currentPath, font, Color.Black, X + Margin, Y + TitleBarHeight + Margin + 5);

            int listY = Y + TitleBarHeight + Margin + 30;
            int visibleEntries = (Height - TitleBarHeight - Margin - 30 - Margin) / EntryHeight;

            for (int i = 0; i < Math.Min(visibleEntries, entries.Count); i++)
            {
                var entry = entries[i];
                int entryY = listY + i * EntryHeight;

                if (i == selectedIndex)
                {
                    canvas.DrawFilledRectangle(Color.Blue, X + Margin, entryY, Width - 2 * Margin, EntryHeight);
                }

                Color iconColor = entry.IsDirectory ? Color.Yellow : Color.LightGray;
                canvas.DrawFilledRectangle(iconColor, X + Margin + 2, entryY + 1, IconSize, IconSize);
                canvas.DrawRectangle(Color.Black, X + Margin + 2, entryY + 1, IconSize, IconSize);

                Color textColor = i == selectedIndex ? Color.White : Color.Black;
                canvas.DrawString(entry.Name, font, textColor, X + Margin + IconSize + 5, entryY + 2);
            }
        }
    }
    public class NotePathApp : Window
    {
        private List<string> outputLines = new List<string>();
        private int selectedIndex = -1;
        private const int MaxLines = 17;
        private const int LineHeight = 15;
        private const int Margin = 5;
        private string openedFilePath;

        public NotePathApp(int x, int y, int width, int height, string title, Color color) : base(x, y, width, height, title, color)
        {
            var saveBtn = new Button(5, Height - 17, Width - 10, 15, "Save", Color.DarkGreen, Color.Black);
            saveBtn.OnClick = () =>
            {
                if (string.IsNullOrEmpty(openedFilePath))
                    return;

                try
                {
                    string fileDataToWrite = "";
                    foreach (var line in outputLines)
                    {
                        fileDataToWrite = $"{fileDataToWrite}{line}\n";
                    }

                    File.WriteAllText(openedFilePath, fileDataToWrite);
                    Title = Title.Replace(" | Unsaved", "")
                                 .Replace(" | Empty", "");
                }
                catch (Exception e)
                {
                    AddOutput("Save failed: " + e.Message);
                }
            };
            Controls.Add(saveBtn);
        }

        public void AddOutput(string text)
        {
            outputLines.Add(text);
            if (outputLines.Count > MaxLines)
            {
                outputLines.RemoveAt(0);
            }
        }

        public void OpenFile(string fullPathToFile, string fileName = "")
        {
            outputLines.Clear();

            try
            {
                if (VFSManager.FileExists(fullPathToFile))
                {
                    openedFilePath = fullPathToFile;

                    string content = File.ReadAllText(fullPathToFile);
                    if (string.IsNullOrEmpty(content))
                    {
                        Title = $"NotePath - {fileName} | Empty";
                        return;
                    }

                    var lines = File.ReadAllLines(fullPathToFile);
                    foreach (var line in lines)
                    {
                        if (outputLines.Count < MaxLines)
                            AddOutput(line);
                    }
                }
                else
                {
                    AddOutput("Error: File not found.");
                }
            }
            catch (Exception e)
            {
                AddOutput($"Error reading file: {e.Message}");
            }
        }

        public void HandleMouseClick(int mouseX, int mouseY)
        {
            foreach (var control in Controls)
            {
                if (control.IsPointInside(mouseX, mouseY))
                {
                    if (control is Button button) { button.OnClick?.Invoke(); }
                    return;
                }
            }

            int listY = Y + TitleBarHeight + Margin + 5;
            int clickIndex = (mouseY - listY) / LineHeight;

            if (clickIndex >= 0 && clickIndex < outputLines.Count)
            {
                selectedIndex = clickIndex;
            }
        }

        public void HandleKey(KeyEvent keyEvent)
        {
            if (selectedIndex < 0) return;

            string selectedLine = outputLines[selectedIndex];
            if (keyEvent.Key == ConsoleKeyEx.Enter)
            {
                if (selectedIndex == outputLines.Count - 1) AddOutput("");
                if (selectedIndex < MaxLines - 2) selectedIndex++;
                else selectedIndex = MaxLines - 2;
            }
            else if (keyEvent.Key == ConsoleKeyEx.Backspace)
            {
                if (selectedLine.Length > 0)
                {
                    outputLines[selectedIndex] = selectedLine.Substring(0, selectedLine.Length - 1);
                    if(!Title.Contains("Unsaved")) Title += " | Unsaved";
                }
                else if(outputLines.Count > 0 && selectedIndex > 0)
                {
                    selectedIndex--;
                    outputLines.RemoveAt(selectedIndex + 1);
                    if (!Title.Contains("Unsaved")) Title += " | Unsaved";
                }
            }
            else if (keyEvent.KeyChar != '\0')
            {
                outputLines[selectedIndex] = selectedLine + keyEvent.KeyChar;
                if (!Title.Contains("Unsaved")) Title += " | Unsaved";
            }
            else if(keyEvent.Key == ConsoleKeyEx.UpArrow)
            {
                if(outputLines.Count > 0 && selectedIndex >= 1)
                    selectedIndex--;
            }
            else if (keyEvent.Key == ConsoleKeyEx.DownArrow)
            {
                if (selectedIndex == outputLines.Count - 1) return;
                if (selectedIndex < MaxLines - 2) selectedIndex++;
                else selectedIndex = MaxLines - 2;
            }
        }

        public override void Draw(Canvas canvas, Font font)
        {
            base.Draw(canvas, font);
            canvas.DrawFilledRectangle(Color.DarkGray, X + Margin, Y + TitleBarHeight + Margin,
                                       Width - 2 * Margin, Height - TitleBarHeight - 5 * Margin);

            int y = Y + TitleBarHeight + Margin + 5;
            for (int i = 0; i < outputLines.Count; i++)
            {
                string line = outputLines[i];
                Color textColor = (i == selectedIndex) ? Color.White : Color.Black;

                if (i == selectedIndex)
                {
                    canvas.DrawFilledRectangle(Color.Blue, X + Margin, y - 2, Width - 2 * Margin, LineHeight);
                }

                canvas.DrawString(line, font, textColor, X + Margin + 5, y);
                y += LineHeight;
            }
        }
    }
    
}
