using Cosmos.Core;
using Cosmos.HAL;
using Cosmos.HAL.Drivers.Video.SVGAII;
using Cosmos.System;
using Cosmos.System.FileSystem;
using Cosmos.System.FileSystem.VFS;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using VoidOS.Apps;
using VoidOS.Models;
using Sys = Cosmos.System;

namespace VoidOS
{
    public class Kernel : Sys.Kernel
    {
        public Canvas canvas;
        public Random random;
        public Font defaultFont;

        public static List<Window> windows;
        public static List<Picture> images;

        public Window draggedWindow;
        public Window focusedWindow;
        public StartMenu startMenuWindow;

        public int dragOffsetX;
        public int dragOffsetY;

        public bool isDragging = false;
        public bool lastMouseState = false;
        public bool isStartMenuOpen = false;
        public bool mouseDownHandled = false;
        public bool screenNeedsRedraw = true;

        public int lastMouseX = 0;
        public int lastMouseY = 0;

        public const int ScreenWidth = 800;
        public const int ScreenHeight = 600;
        public const int TaskbarHeight = 40;

        private readonly Color DesktopColorTop = Color.CornflowerBlue;
        public readonly Color DesktopColorBottom = Color.DarkBlue;
        public readonly Color TaskbarColor = Color.FromArgb(40, 40, 40);
        public readonly Color StartButtonColor = Color.FromArgb(0, 122, 204);


        protected override void BeforeRun()
        {
            System.Console.WriteLine("VoidOS GUI: Initializing...");

            var fs = new CosmosVFS();
            VFSManager.RegisterVFS(fs);

            canvas = FullScreenCanvas.GetFullScreenCanvas(new Mode(ScreenWidth, ScreenHeight, ColorDepth.ColorDepth32));
            MouseManager.ScreenWidth = ScreenWidth;
            MouseManager.ScreenHeight = ScreenHeight;

            windows = new List<Window>();
            random = new Random();
            defaultFont = PCScreenFont.Default;

            startMenuWindow = new StartMenu(0, ScreenHeight - TaskbarHeight - 210, 200, 210, Color.LightGray);

            var shutDownButton = new Button(10, 30, 180, 30, "Shut Down", Color.Gray, Color.White);
            shutDownButton.OnClick = () =>
            {
                Sys.Power.Shutdown();
            };
            startMenuWindow.Controls.Add(shutDownButton);

            var tmDownButton = new Button(10, 70, 180, 30, "Task Manager", Color.Gray, Color.White);
            tmDownButton.OnClick = () =>
            {
                var tmWindow = new Window(200, 200, 300, 240, "Task Manager", Color.Gray);

                var cpu = new Label(10, 30, $"CPU: {CPU.GetCPUCycleSpeed() / 1_000_000} MHz", Color.White);
                var ram = new Label(10, 45, $"Memory: {GCImplementation.GetUsedRAM() * 1024 * 1024}/{GCImplementation.GetAvailableRAM()} MB", Color.White);

                tmWindow.Controls.Add(cpu);
                tmWindow.Controls.Add(ram);

                windows.Add(tmWindow);
            };
            startMenuWindow.Controls.Add(tmDownButton);

            var terminalButton = new Button(10, 110, 180, 30, "Terminal", Color.Gray, Color.White);
            terminalButton.OnClick = () =>
            {
                var terminalWindow = new TerminalApp(150, 50, 500, 500, "Terminal", Color.Black);
                windows.Add(terminalWindow);
            };
            startMenuWindow.Controls.Add(terminalButton);

            var fileExplorerButton = new Button(10, 150, 180, 30, "File Explorer", Color.Gray, Color.White);
            fileExplorerButton.OnClick = () =>
            {
                var fileExplorerWindow = new FileExplorerApp(200, 100, 500, 400, "File Explorer", Color.Gray);
                windows.Add(fileExplorerWindow);
            };
            startMenuWindow.Controls.Add(fileExplorerButton);

            System.Console.WriteLine("Initialization complete.");
        }

        protected override void Run()
        {
            FPSCounter.Update();

            HandleMouse();
            HandleKeyboard();

            if (screenNeedsRedraw)
            {
                DrawDesktop();
                screenNeedsRedraw = false;
            }
        }

        private void HandleMouse()
        {
            int mouseX = (int)MouseManager.X;
            int mouseY = (int)MouseManager.Y;
            bool currentMouseState = MouseManager.MouseState == MouseState.Left;

            if (mouseX != lastMouseX || mouseY != lastMouseY)
            {
                screenNeedsRedraw = true;
            }
            lastMouseX = mouseX;
            lastMouseY = mouseY;

            if (currentMouseState && !mouseDownHandled)
            {
                if (isStartMenuOpen)
                {
                    bool clickConsumed = false;

                    foreach (var control in startMenuWindow.Controls)
                    {
                        if (control is Button button)
                        {
                            int buttonAbsX = startMenuWindow.X + button.X;
                            int buttonAbsY = startMenuWindow.Y + button.Y;

                            if (mouseX >= buttonAbsX && mouseX <= buttonAbsX + button.Width &&
                                mouseY >= buttonAbsY && mouseY <= buttonAbsY + button.Height)
                            {
                                button.OnClick?.Invoke();
                                clickConsumed = true;
                                break;
                            }
                        }
                    }

                    if (clickConsumed)
                    {
                        isStartMenuOpen = false;
                        screenNeedsRedraw = true;
                        mouseDownHandled = true;
                        return;
                    }

                    if (mouseX >= 0 && mouseX <= 80 && mouseY >= ScreenHeight - TaskbarHeight && mouseY <= ScreenHeight)
                    {
                        isStartMenuOpen = false;
                        screenNeedsRedraw = true;
                        mouseDownHandled = true;
                        return;
                    }

                    if (!(mouseX >= startMenuWindow.X && mouseX <= startMenuWindow.X + startMenuWindow.Width &&
                          mouseY >= startMenuWindow.Y && mouseY <= startMenuWindow.Y + startMenuWindow.Height))
                    {
                        isStartMenuOpen = false;
                        screenNeedsRedraw = true;
                    }
                }

                if (!isStartMenuOpen && mouseX >= 0 && mouseX <= 80 && mouseY >= ScreenHeight - TaskbarHeight && mouseY <= ScreenHeight)
                {
                    isStartMenuOpen = true;
                    screenNeedsRedraw = true;
                    mouseDownHandled = true;
                    return;
                }

                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    Window window = windows[i];
                    if (mouseX >= window.X && mouseX <= window.X + window.Width && mouseY >= window.Y && mouseY <= window.Y + window.Height)
                    {
                        if (isStartMenuOpen)
                        {
                            isStartMenuOpen = false;
                            screenNeedsRedraw = true;
                        }

                        windows.Remove(window); windows.Add(window);

                        if (window.IsPointInCloseButton(mouseX, mouseY))
                        {
                            windows.Remove(window);
                            screenNeedsRedraw = true;
                            mouseDownHandled = true;
                            return;
                        }

                        if (window.IsPointInTitleBar(mouseX, mouseY))
                        {
                            isDragging = true;
                            draggedWindow = window;
                            dragOffsetX = mouseX - window.X;
                            dragOffsetY = mouseY - window.Y;
                            mouseDownHandled = true;
                            focusedWindow = window;
                            return;
                        }

                        if (window is FileExplorerApp fileExplorer)
                        {
                            fileExplorer.HandleMouseClick(mouseX, mouseY);
                            screenNeedsRedraw = true;
                            mouseDownHandled = true;
                            focusedWindow = window;
                            return;
                        }

                        if(window is NotePathApp notePath)
                        {
                            notePath.HandleMouseClick(mouseX, mouseY);
                            screenNeedsRedraw = true;
                            mouseDownHandled = true;
                            focusedWindow = window;
                            return;
                        }

                        foreach (var control in window.Controls)
                        {
                            if (control.IsPointInside(mouseX, mouseY))
                            {
                                if (control is Button button) { button.OnClick?.Invoke(); }
                                mouseDownHandled = true;
                                focusedWindow = window;
                                return;
                            }
                        }

                        mouseDownHandled = true;
                        return;
                    }
                }
            }
            else if (!currentMouseState)
            {
                if (mouseDownHandled) { mouseDownHandled = false; }
                if (isDragging) { isDragging = false; draggedWindow = null; }
            }

            if (isDragging && currentMouseState)
            {
                draggedWindow.X = mouseX - dragOffsetX;
                draggedWindow.Y = mouseY - dragOffsetY;
                draggedWindow.X = Math.Max(0, Math.Min(draggedWindow.X, ScreenWidth - draggedWindow.Width));
                draggedWindow.Y = Math.Max(0, Math.Min(draggedWindow.Y, ScreenHeight - draggedWindow.Height - TaskbarHeight));
            }
        }

        private void HandleKeyboard()
        {
            if (KeyboardManager.KeyAvailable)
            {
                KeyEvent keyEvent = KeyboardManager.ReadKey();

                foreach (var window in windows)
                {
                    if (window is TerminalApp terminal && window == focusedWindow)
                    {
                        terminal.HandleKey(keyEvent);
                        screenNeedsRedraw = true;
                        return;
                    }

                    if(window is FileExplorerApp fileExplorer && window == focusedWindow)
                    {
                        if (keyEvent.Key == ConsoleKeyEx.Backspace)
                        {
                            fileExplorer.GoUp();
                            screenNeedsRedraw = true;
                            return;
                        }
                    }

                    if(window is NotePathApp notePath && window == focusedWindow)
                    {
                        notePath.HandleKey(keyEvent);
                        screenNeedsRedraw = true;
                        return;
                    }
                }
            }
        }

        private void DrawDesktop()
        {
            for (int y = 0; y < ScreenHeight - TaskbarHeight; y++)
            {
                float ratio = (float)y / (ScreenHeight - TaskbarHeight);
                int r = (int)(DesktopColorTop.R * (1 - ratio) + DesktopColorBottom.R * ratio);
                int g = (int)(DesktopColorTop.G * (1 - ratio) + DesktopColorBottom.G * ratio);
                int b = (int)(DesktopColorTop.B * (1 - ratio) + DesktopColorBottom.B * ratio);
                Color lineColor = Color.FromArgb(r, g, b);
                canvas.DrawFilledRectangle(lineColor, 0, y, ScreenWidth, 1);
            }

            canvas.DrawFilledRectangle(TaskbarColor, 0, ScreenHeight - TaskbarHeight, ScreenWidth, TaskbarHeight);

            canvas.DrawFilledRectangle(StartButtonColor, 5, ScreenHeight - TaskbarHeight + 5, 80, TaskbarHeight - 10);
            canvas.DrawString("Start", defaultFont, Color.White, 20, ScreenHeight - TaskbarHeight + 12);

            foreach (var window in windows)
            {
                window.Draw(canvas, defaultFont);
            }

            if (isStartMenuOpen)
            {
                startMenuWindow.Draw(canvas, defaultFont);
            }

            int mouseX = (int)MouseManager.X; int mouseY = (int)MouseManager.Y;
            canvas.DrawRectangle(Color.White, mouseX - 8, mouseY, 16, 1);
            canvas.DrawRectangle(Color.White, mouseX, mouseY - 8, 1, 16);

            canvas.DrawString($"FPS: {FPSCounter.GetFPS()}", defaultFont, Color.Black, 20, 20);

            canvas.Display();
        }
    }
}