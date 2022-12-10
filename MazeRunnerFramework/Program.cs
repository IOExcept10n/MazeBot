using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Numerics;
using System.Drawing.Printing;
using System.Diagnostics;

namespace MazeRunner
{
    internal class Program
    {
        const string url = "wss://maze.web1.nto.sprush.rocks/ws";

        private enum SslProtocolsHack
        {
            Tls = 192,
            Tls11 = 768,
            Tls12 = 3072
        }

        static void Main(string[] args)
        {
            RunMaze().GetAwaiter().GetResult();
            Console.ReadKey();
        }

        static async Task RunMaze()
        {
            Console.Write("Print here the size of the maze: ");
            string inp = Console.ReadLine();
            string[] sizeStr = (string.IsNullOrWhiteSpace(inp) ? "1000 1000" : inp).Replace(",", " ").Replace(";", " ").Split(' ');
            if (sizeStr.Length < 2) sizeStr = new string[] { "1000", "1000" };
            Cell[,] maze = new Cell[int.Parse(sizeStr[0]), int.Parse(sizeStr[1])];

            Console.Write("Print here logging threshold: ");
            int logMoves;
            if (!int.TryParse(Console.ReadLine(), out logMoves))
            {
                logMoves = 50;
            }
            Console.Write("Print here image generation coefficient: ");
            int genCoefficient;
            if (!int.TryParse(Console.ReadLine(), out genCoefficient))
            {
                genCoefficient = 50;
            }
            int saveMoves = logMoves * genCoefficient;

            Console.Write("Print here maze URL: ");
            string url = Console.ReadLine();
            if (string.IsNullOrEmpty(url)) url = Program.url;
            string wsString = $"wss://{url}/ws";
            string httpString = $"https://{url}";

            string move = null;
            int lastX = 0;
            int lastY = 0;
            bool isRunning = true;
            bool freeze = false;
            Cell last = null;
            int moveNumber = 0;
            Guid session = Guid.NewGuid();
            Stack<string> route = new Stack<string>();

            using (var ws = new WebSocket(wsString))
            {
                ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                ws.OnMessage += (s, e) =>
                {
                    dynamic message = JsonConvert.DeserializeObject<JObject>(e.Data);
                    NavigationState navState = new NavigationState()
                    {
                        X = message.position[0],
                        Y = message.position[1],
                        TargetX = message.end[0],
                        TargetY = message.end[1],
                        Status = message.status
                    };
                    if (moveNumber != 0)
                    {
                        Vector2 delta = new Vector2(navState.X - lastX, navState.Y - lastY);
                        if (move == "right")
                        {
                            NavigationState.RightDelta = delta;
                        }
                        else if (move == "left")
                        {
                            NavigationState.RightDelta = -delta;
                        }
                        else if (move == "up")
                        {
                            NavigationState.UpDelta = delta;
                        }
                        else if (move == "down")
                        {
                            NavigationState.UpDelta = -delta;
                        }
                    }
                    else
                    {
                        NavigationState.StartPosition = new Vector2(navState.X, navState.Y);
                        NavigationState.Target = new Vector2(navState.TargetX, navState.TargetY);
                    }
                    moveNumber++;
                    if (moveNumber % logMoves == 0)
                    {
                        Console.WriteLine($"move {moveNumber}...");
                    }
                    if (moveNumber % saveMoves == 0)
                    {
                        //Console.ForegroundColor = ConsoleColor.Yellow;
                        //Console.Write($"Seems like the maze is too hard to complete it with current algorithm so quick (with {moveNumber} moves).\n\r Print here path to export the maze picture to analyze it: ");
                        //string exportPath = Console.ReadLine();
                        //Cell.ExportMaze(exportPath, maze, navState);
                        //Console.WriteLine("Exported successfully.");
                        //Console.ResetColor();

                        Directory.CreateDirectory($"{session}");
                        string exportPath = $"{session}/maze_{moveNumber}.png";
                        Cell.ExportMaze(exportPath, maze, navState);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Autosave exported successfully.");
                        Console.ResetColor();
                    }

                    if (navState.TargetX != NavigationState.Target.X && navState.TargetY != NavigationState.Target.Y)
                    {
                        using (StreamWriter writer = new StreamWriter($"{session}/maze_root_{moveNumber}.route"))
                        {
                            foreach (var mov in route)
                            {
                                writer.WriteLine(mov);
                            }
                        }
                        route.Clear();

                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.WriteLine("Changing location data....");
                        NavigationState.Target = new Vector2(navState.TargetX, navState.TargetY);
                        Array.Clear(maze, 0, maze.Length);
                        NavigationState.StartPosition = new Vector2(navState.X, navState.Y);
                        Console.ResetColor();
                    }

                    if (navState.Status != "ok" && navState.Status != "invalid move" && navState.Status != "start")
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("My congratulations, you've got a cake!");
                        Console.WriteLine("========================");
                        Console.WriteLine(e.Data);
                        Console.WriteLine("========================");
                        Directory.CreateDirectory($"{session}");
                        string exportPath = $"{session}/maze_completed_{moveNumber}.png";
                        Cell.ExportMaze(exportPath, maze, navState);

                        using (StreamWriter writer = new StreamWriter($"{session}/maze_root_{moveNumber}.route"))
                        {
                            foreach (var mov in route)
                            {
                                writer.WriteLine(mov);
                            }
                        }
                        route.Clear();

                        Console.WriteLine("Exported successfully!");
                        Console.ResetColor();
                        return;
                    }
                    Cell currentCell = maze[navState.X, navState.Y];
                    if (currentCell == null)
                    {
                        currentCell = maze[navState.X, navState.Y] = Cell.CreateCell(ConvertJObject(message.possible_moves));
                    }
                    move = currentCell.MoveNext(maze, navState, last);

                    if (route.Count == 0)
                    {
                        route.Push(move);
                    }
                    else
                    {
                        string lastMove = route.Peek();
                        bool rem = false;
                        switch (lastMove)
                        {
                            case "up":
                                rem = move == "down";
                                break;
                            case "down":
                                rem = move == "up";
                                break;
                            case "right":
                                rem = move == "left";
                                break;
                            case "left":
                                rem = move == "right";
                                break;
                        }
                        if (rem) route.Pop();
                        else route.Push(move);
                    }

                    lastX = navState.X;
                    lastY = navState.Y;
                    last = currentCell;
                    freeze = true;
                };
                ws.OnError += (s, e) =>
                {
                    Console.WriteLine("Error: " + e.Message);
                };
                ws.EnableRedirection = true;

                ws.Connect();
                ws.Ping();
                while (isRunning)
                {
                    SpinWait.SpinUntil(() => freeze);
                    ws.SendAsync(move, null);
                    freeze = false;
                }
            }
        }

        private static List<string> ConvertJObject(dynamic obj)
        {
            List<string> list = new List<string>();
            foreach (var item in obj)
            {
                list.Add(item.ToString());
            }
            return list;
        }
    }

    public struct NavigationState
    {
        public int X { get; set; }

        public int Y { get; set; }

        public static Vector2 StartPosition { get; set; }

        public static Vector2 Target { get; set; }

        public static Vector2 RightDelta { get; set; }

        public static Vector2 UpDelta { get; set; }

        public int TargetX { get; set; }

        public int TargetY { get; set; }

        public string Status { get; set; }

        public Vector2 PerformMove(string move)
        {
            Vector2 position = new Vector2(X, Y);
            switch (move)
            {
                case "right":
                    position += RightDelta;
                    break;
                case "left":
                    position -= RightDelta;
                    break;
                case "up":
                    position += UpDelta;
                    break;
                case "down":
                    position -= UpDelta;
                    break;
            }
            return position;
        }
    }

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
                        if (x == NavigationState.StartPosition.X && y == NavigationState.StartPosition.Y)
                        {
                            g.FillRectangle(new SolidBrush(Color.White), lx - 1, ly - 1, 3, 3);
                        }
                        maze[x, y]?.DrawCell(g, lx, ly);
                    }
                }
                g.DrawString($"pos: {{{navState.X};{navState.Y}}}. Target: {{{navState.TargetX};{navState.TargetY}}}", new Font("Segoe UI", 16), Brushes.White, 5, lenY * 2 - 35);
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

    public class DeadEnd : Cell
    {
        public override string MoveNext(Cell[,] maze, NavigationState state, Cell previous)
        {
            if (state.X == NavigationState.Target.X && state.Y == NavigationState.Target.Y)
            {
                Debugger.Break();
            }
            TimesVisited++;
            IsLocked = true;
            return AvailableMoves.First();
        }
    }

    public class CrossroadCell : Cell
    {
        public override string MoveNext(Cell[,] maze, NavigationState state, Cell previous)
        {
            if (state.X == NavigationState.Target.X && state.Y == NavigationState.Target.Y)
            {
                Debugger.Break();
            }
            TimesVisited++;
            int availableRoutes = AvailableMoves.Count();
            int lockedRoutes = 0;
            List<Route> moves = new List<Route>();
            string cache = null;
            foreach (var move in AvailableMoves)
            {
                var cell = GetRelativeCell(maze, move, state.X, state.Y);
                if (cell?.IsLocked == true)
                {
                    lockedRoutes++;
                }
                else if (previous != cell)
                {
                    moves.Add(new Route(move, cell, state));
                }
                else
                {
                    cache = move;
                }
            }
            if (lockedRoutes == availableRoutes - 1) IsLocked = true;
            if (moves.Count == 0)
            {
                IsLocked = false;
                return cache;
            }
            return moves.OrderBy(x => x.ReversePriority).FirstOrDefault().Path;
        }

        private readonly struct Route
        {
            public string Path { get; }

            public double ReversePriority { get; }

            public Route(string move, Cell cell, NavigationState state)
            {
                Path = move;
                if (cell == null) ReversePriority = 1;
                else ReversePriority = cell.TimesVisited + (choiceRandom.NextDouble() - 0.5) * cell.TimesVisited;

                Vector2 performedMove = state.PerformMove(move);
                Vector2 target = new Vector2(state.TargetX, state.TargetY);
                Vector2 delta = (target - performedMove);
                Vector2 currentDelta = target - new Vector2(state.X, state.Y);
                float dist1 = delta.LengthSquared();
                float dist2 = currentDelta.LengthSquared();
                if (dist1 > dist2)
                {
                    ReversePriority *= 3.5;
                }
                else
                {
                    ReversePriority /= 3.5;
                }
                ReversePriority *= choiceRandom.NextDouble() * 0.5 + 0.5;
            }
        }
    }
}