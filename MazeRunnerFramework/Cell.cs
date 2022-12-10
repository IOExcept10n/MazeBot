using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using System.Drawing;
using System.Globalization;

namespace MazeRunner
{
    public abstract class Cell
    {
        protected static Random choiceRandom = new Random();

        private static readonly Color[] VisitColors =
        {
            Color.Gray,
            FromHex("#FF00FA9A"),
            FromHex("#FF00FF7F"),
            FromHex("#FF00FF00"),
            FromHex("#FF66FF00"),
            FromHex("#FF7CFC00"),
            FromHex("#FFADFF2F"),
            FromHex("#FFCEFF1D"),
            FromHex("#FFEDFF21"),
            FromHex("#FFFDE910"),
            FromHex("#FFFFA812"),
        };

        public bool IsLocked { get; set; }

        public int TimesVisited { get; set; }

        public IEnumerable<string> AvailableMoves { get; set; }

        public static Cell CreateCell(IEnumerable<string> moves)
        {
            Cell res;
            if (moves.Count() == 1)
            {
                res = new DeadEnd();
            }
            else res = new CrossroadCell();
            res.AvailableMoves = moves;
            return res;
        }

        public abstract string MoveNext(Cell[,] maze, NavigationState state, Cell previous);

        public Cell GetRelativeCell(Cell[,] maze, string move, int x, int y)
        {
            switch (move)
            {
                case "right":
                    return maze[x, y + 1];
                case "down":
                    return maze[x + 1, y];
                case "left":
                    return maze[x, y - 1];
                case "up":
                    return maze[x - 1, y];
                default:
                    return null;
            }
        }

        public static void ExportMaze(string path, Cell[,] maze, NavigationState navState)
        {
            int lenX = maze.GetLength(0);
            int lenY = maze.GetLength(1);
            Bitmap map = new Bitmap(lenX * 2 + 2, lenY * 2 + 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Rectangle border = new Rectangle(0, 0, map.Width - 1, map.Height - 1);
            using (Graphics g = Graphics.FromImage(map))
            {
                g.Clear(Color.Black);
                for (int y = 0, ly = 1; y < lenY; y++, ly+=2)
                {
                    for (int x = 0, lx = 1; x < lenX; x++, lx+=2)
                    {
                        if (x == navState.X && y == navState.Y)
                        {
                            g.FillRectangle(new SolidBrush(Color.Blue), lx - 1, ly - 1, 3, 3);
                        }
                        if (x == navState.TargetX && y == navState.TargetY)
                        {
                            g.FillRectangle(new SolidBrush(Color.Violet), lx - 1, ly - 1, 3, 3);
                        }
                        else if (x == NavigationState.ArtificalTarget.X && y == NavigationState.ArtificalTarget.Y)
                        {
                            g.FillRectangle(new SolidBrush(Color.Lime), lx - 1, ly - 1, 3, 3);
                        }
                        if (x == NavigationState.StartPosition.X && y == NavigationState.StartPosition.Y)
                        {
                            g.FillRectangle(new SolidBrush(Color.White), lx - 1, ly - 1, 3, 3);
                        }
                        maze[x, y]?.DrawCell(g, lx, ly);
                    }
                }
                g.DrawString($"pos: {{{navState.X};{navState.Y}}}. Target: {{{navState.TargetX};{navState.TargetY}}}. Artificial target: {{{NavigationState.ArtificalTarget.X};{NavigationState.ArtificalTarget.Y}}}", new Font("Segoe UI", 16), Brushes.White, 5, lenY * 2 - 35);
            }
            map.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        public void DrawCell(Graphics graphics, int x, int y)
        {
            Color visitColor = TimesVisited > 9 ? VisitColors.Last() : VisitColors[TimesVisited];
            var wall = new SolidBrush(Color.FromArgb(0x7FFFFFFF));
            graphics.FillRectangle(wall, x - 1, y - 1, 1, 1);
            graphics.FillRectangle(wall, x + 1, y - 1, 1, 1);
            graphics.FillRectangle(wall, x - 1, y + 1, 1, 1);
            graphics.FillRectangle(wall, x + 1, y + 1, 1, 1);
            if (IsLocked) graphics.FillRectangle(new SolidBrush(FromHex("#FFFF2B2B")), x, y, 1, 1);
            else graphics.FillRectangle(new SolidBrush(visitColor), x, y, 1, 1);
            Vector2 position = new Vector2(x, y);
            Vector2 up = position + NavigationState.UpDelta;
            Vector2 right = position + NavigationState.RightDelta;
            Vector2 down = position - NavigationState.UpDelta;
            Vector2 left = position - NavigationState.RightDelta;
            if (!AvailableMoves.Contains("up")) graphics.FillRectangle(wall, up.X, up.Y, 1, 1);
            if (!AvailableMoves.Contains("down")) graphics.FillRectangle(wall, down.X, down.Y, 1, 1);
            if (!AvailableMoves.Contains("right")) graphics.FillRectangle(wall, right.X, right.Y, 1, 1);
            if (!AvailableMoves.Contains("left")) graphics.FillRectangle(wall, left.X, left.Y, 1, 1);
        }

        private static Color FromHex(string color)
        {
            var hex = color.Replace("#", string.Empty);
            var h = NumberStyles.HexNumber;

            var a = int.Parse(hex.Substring(0, 2), h);
            var r = int.Parse(hex.Substring(2, 2), h);
            var g = int.Parse(hex.Substring(4, 2), h);
            var b = int.Parse(hex.Substring(6, 2), h);

            return Color.FromArgb(a, r, g, b);
        }
    }
}