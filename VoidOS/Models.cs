using Cosmos.HAL;
using Cosmos.System.FileSystem.Listing;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace VoidOS.Models
{
    public abstract class Control
    {
        public int X, Y, Width, Height;
        public Window ParentWindow;

        protected Control(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public virtual bool IsPointInside(int x, int y)
        {
            return x >= ParentWindow.X + X && x <= ParentWindow.X + X + Width &&
                   y >= ParentWindow.Y + Y && y <= ParentWindow.Y + Y + Height;
        }

        public abstract void Draw(Canvas canvas, Font font);
    }
    public class Label : Control
    {
        public string Text;
        public Color TextColor;

        public Label(int x, int y, string text, Color textColor) : base(x, y, 100, 20)
        {
            Text = text; TextColor = textColor;
        }

        public override void Draw(Canvas canvas, Font font)
        {
            canvas.DrawString(Text, font, TextColor, ParentWindow.X + X, ParentWindow.Y + Y);
        }
    }
    public class Button : Control
    {
        public string Text;
        public Color BackgroundColor;
        public Color TextColor;
        public Action OnClick;

        public Button(int x, int y, int width, int height, string text, Color bgColor, Color textColor) : base(x, y, width, height)
        {
            Text = text; BackgroundColor = bgColor; TextColor = textColor;
        }

        public override void Draw(Canvas canvas, Font font)
        {
            int absX = ParentWindow.X + X;
            int absY = ParentWindow.Y + Y;

            canvas.DrawFilledRectangle(BackgroundColor, absX, absY, Width, Height);
            canvas.DrawRectangle(Color.Black, absX, absY, Width, Height);

            int textX = absX + (Width / 2) - (Text.Length * (font.Width / 2));
            int textY = absY + (Height / 2) - (font.Height / 2);
            canvas.DrawString(Text, font, TextColor, textX, textY);
        }
    }
    public class Window
    {
        public int X, Y, Width, Height;
        public string Title;
        public Color Color;
        public AppState AppState;
        public List<Control> Controls = new List<Control>();
        public const int TitleBarHeight = 25;
        public const int CloseButtonSize = 16;

        public Window(int x, int y, int width, int height, string title, Color color)
        {
            X = x; Y = y; Width = width; Height = height; Title = title; Color = color;
        }

        public bool IsPointInTitleBar(int x, int y)
        {
            return x >= X && x <= X + Width && y >= Y && y <= Y + TitleBarHeight;
        }

        public bool IsPointInCloseButton(int x, int y)
        {
            int closeButtonX = X + Width - CloseButtonSize - 2;
            int closeButtonY = Y + 2;

            return x >= closeButtonX && x <= closeButtonX + CloseButtonSize &&
                   y >= closeButtonY && y <= closeButtonY + CloseButtonSize;
        }

        public virtual void Draw(Canvas canvas, Font font)
        {
            canvas.DrawFilledRectangle(Color, X, Y + TitleBarHeight, Width, Height - TitleBarHeight);

            Color titleBarColor = Color.DarkGray;
            canvas.DrawFilledRectangle(titleBarColor, X, Y, Width, TitleBarHeight);
            canvas.DrawRectangle(Color.Black, X, Y, Width, Height);
            canvas.DrawString(Title, font, Color.White, X + 5, Y + 5);

            int closeButtonX = X + Width - CloseButtonSize - 2;
            int closeButtonY = Y + 2;
            Color closeButtonColor = Color.Red;
            canvas.DrawFilledRectangle(closeButtonColor, closeButtonX, closeButtonY, CloseButtonSize, CloseButtonSize);

            Color crossColor = Color.White;
            canvas.DrawLine(crossColor, closeButtonX + 3, closeButtonY + 3, closeButtonX + CloseButtonSize - 3, closeButtonY + CloseButtonSize - 3);
            canvas.DrawLine(crossColor, closeButtonX + CloseButtonSize - 3, closeButtonY + 3, closeButtonX + 3, closeButtonY + CloseButtonSize - 3);

            foreach (var control in Controls)
            {
                control.ParentWindow = this;
                control.Draw(canvas, font);
            }
        }
    }
    public static class FPSCounter
    {
        private static int _frameCount = 0;
        private static int _lastSecond = -1;
        private static int _currentFPS = 0;

        public static void Update()
        {
            _frameCount++;
            int currentSecond = RTC.Second;

            if (currentSecond != _lastSecond)
            {
                _currentFPS = _frameCount;
                _frameCount = 0;
                _lastSecond = currentSecond;
            }
        }

        public static int GetFPS()
        {
            return _currentFPS;
        }
    }
    public class IOEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }


        public IOEntry(DirectoryEntry entry)
        {
            Name = entry.mName;
            FullPath = entry.mFullPath;
            IsDirectory = entry.mEntryType == DirectoryEntryTypeEnum.Directory ? true : false;
        }
        public IOEntry(string name, string fullPath, bool isDirectory)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
        }
    }
    public static class VoidPath
    {
        public static string Combine(string Path_1, string Path_2, string Path_3, string Path_4)
        {
            return Combine(Path_1, Path_2, Combine(Path_3, Path_4));
        }
        public static string Combine(string Path_1, string Path_2, string Path_3)
        {
            return Combine(Path_1, Combine(Path_2, Path_3));
        }
        public static string Combine(string Path_1, string Path_2)
        {
            if (string.IsNullOrEmpty(Path_1) || string.IsNullOrEmpty(Path_2)) return "";

            if (!Path_1.EndsWith("\\"))
                Path_1 += "\\";
            if (!Path_2.EndsWith("\\"))
                Path_2 += "\\";

            if (Path_2.StartsWith("\\"))
                Path_2.Remove(0);

            return Path_1 + Path_2;
        }
    }
    public class Picture : Image
    {
        public int posX, posY;

        public Picture(int x, int y, uint width, uint height, ColorDepth color, int[] rawData) : base(width, height, color)
        {
            Width = width;
            Height = height;

            posX = x;
            posY = y;

            Depth = color;
            RawData = rawData;
        }

        public void Draw(Canvas canvas)
        {
            canvas.DrawImage(this, posX, posY);
        }
    }
    public enum AppState
    {
        Created,
        Running,
        Minimized,
        Suspended,
        Closed
    }

}
