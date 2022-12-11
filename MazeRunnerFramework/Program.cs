using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using System.Linq;
using Microsoft.Win32;

namespace MazeRunner
{
    internal static class Program
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
            Reader.TryReadLine(out _, 10000);
        }

        static async Task RunMaze()
        {
            Console.Write("Print here the size of the maze: ");
            string inp = Reader.ReadLine();
            string[] sizeStr = (string.IsNullOrWhiteSpace(inp) ? "1000 1000" : inp).Replace(",", " ").Replace(";", " ").Split(' ');
            if (sizeStr.Length < 2) sizeStr = new string[] { "1000", "1000" };
            Cell[,] maze = new Cell[int.Parse(sizeStr[0]), int.Parse(sizeStr[1])];

            Console.Write("Print here logging threshold: ");
            int logMoves;
            if (!int.TryParse(Reader.ReadLine(), out logMoves))
            {
                logMoves = 50;
            }
            Console.Write("Print here image generation coefficient: ");
            int genCoefficient;
            if (!int.TryParse(Reader.ReadLine(), out genCoefficient))
            {
                genCoefficient = 4;
            }
            int saveMoves = logMoves * genCoefficient;
            Console.Write("Print here navigation correction step coefficient: ");
            int corrCoefficient;
            if (!int.TryParse(Reader.ReadLine(), out corrCoefficient))
            {
                corrCoefficient = 5;
            }
            int correctionStep = saveMoves * corrCoefficient;

            Console.Write("Print here maze URL: ");
            string url = Reader.ReadLine();
            if (string.IsNullOrEmpty(url)) url = Program.url;
            string wsString = $"wss://{url}/ws";
            string httpString = $"https://{url}";

            int inputDelay = 15000;

            string move = null;
            int lastX = 0;
            int lastY = 0;
            bool isRunning = true;
            bool freeze = false;
            Cell last = null;
            int moveNumber = 0;
            Guid session = Guid.NewGuid();
            Stack<string> route = new Stack<string>();

            Console.Title = $"MazeRunner session {session}";

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
                        AskForAutoRoute(inputDelay);
                    }
                    moveNumber++;

                    bool rec = NavigationState.IsTracingRoute;

                    if (moveNumber % logMoves == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"move {moveNumber}...");
                        if (rec) Console.WriteLine("(Recorded)");
                    }
                    if (moveNumber % saveMoves == 0)
                    {

                        Directory.CreateDirectory($"{session}");
                        string exportPath = $"{session}/maze_{moveNumber}.png";
                        Cell.ExportMaze(exportPath, maze, navState);

                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("Autosave exported successfully.");
                        Console.ResetColor();
                    }

                    if (!rec && moveNumber % correctionStep == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Beep();
                        Console.WriteLine($"Current position: {navState.X};{navState.Y}");
                        Console.WriteLine("Please, check the image and give me the target: ");
                        Reader.TryReadLine(out string input, inputDelay);
                        if (!string.IsNullOrWhiteSpace(input))
                        {
                            if (input.Equals("default", StringComparison.OrdinalIgnoreCase))
                            {
                                NavigationState.ArtificalTarget = NavigationState.Target;
                            }
                            else if (input.StartsWith("/") && input.Length > 1)
                            {
                                var command = input.TrimStart('/').Split();
                                int val = 0;
                                bool commandCorrect = command.Length == 1 || int.TryParse(command[1], out val);
                                if (commandCorrect)
                                {
                                    if (command[0].StartsWith("imgen"))
                                    {
                                        genCoefficient = val;
                                        saveMoves = logMoves * genCoefficient;
                                    }
                                    else if (command[0].StartsWith("corstep"))
                                    {
                                        corrCoefficient = val;
                                        correctionStep = genCoefficient * corrCoefficient;
                                    }
                                    else if (command[0].StartsWith("delay"))
                                    {
                                        inputDelay = val;
                                    }
                                    else if (command[0].StartsWith("save"))
                                    {
                                        using (StreamWriter writer = new StreamWriter($"{session}/maze_root_{moveNumber}.route"))
                                        {
                                            foreach (var mov in route)
                                            {
                                                writer.WriteLine(mov);
                                            }
                                        }
                                        Console.WriteLine("Save success!");
                                    }
                                    else if (command[0].StartsWith("exit"))
                                    {
                                        using (StreamWriter writer = new StreamWriter($"{session}/maze_root_{moveNumber}.route"))
                                        {
                                            foreach (var mov in route)
                                            {
                                                writer.WriteLine(mov);
                                            }
                                        }
                                        Console.WriteLine("Saved file");
                                        Console.WriteLine("Goodbye!");
                                        Environment.Exit(0);
                                    }
                                }
                            }
                            else
                            {
                                string[] coords = input.Replace(",", " ").Replace(";", " ").Split(' ');
                                int x = 0, y = 0;
                                if (coords.Length > 1 && !int.TryParse(coords[0], out x))
                                {
                                    x = NavigationState.Target.X;
                                }
                                if (coords.Length > 1 && !int.TryParse(coords[1], out y))
                                {
                                    y = NavigationState.Target.Y;
                                }
                                NavigationState.ArtificalTarget = new Vector2(x, y);
                            }
                        }
                        Console.ResetColor();
                    }

                    if (navState.X == NavigationState.ArtificalTarget.X && navState.Y == NavigationState.ArtificalTarget.Y)
                    {
                        Console.WriteLine("Target reached, changing to default");
                        NavigationState.ArtificalTarget = NavigationState.Target;
                    }

                    if (navState.TargetX != NavigationState.Target.X || navState.TargetY != NavigationState.Target.Y)
                    {
                        using (StreamWriter writer = new StreamWriter($"{session}/maze_t_{NavigationState.Target.X}_{NavigationState.Target.Y}_s_{NavigationState.StartPosition.X}_{NavigationState.StartPosition.Y}_{moveNumber}.route"))
                        {
                            foreach (var mov in route)
                            {
                                writer.WriteLine(mov);
                            }
                        }
                        route.Clear();

                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.WriteLine("Changing location data....");
                        Console.WriteLine("==================");
                        Console.WriteLine(e.Data);
                        Console.WriteLine("==================");
                        NavigationState.Target = new Vector2(navState.TargetX, navState.TargetY);
                        Array.Clear(maze, 0, maze.Length);
                        NavigationState.StartPosition = new Vector2(navState.X, navState.Y);

                        AskForAutoRoute(inputDelay);
                    }

                    if (!navState.Status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("INFO: Status is not equal to zero. Message to receive: ");
                        Console.WriteLine(navState.Status);
                        Console.WriteLine("+++++++++++++++");
                        Console.ResetColor();
                    }

                    if (navState.X == navState.TargetX && navState.Y == navState.TargetY)
                    {
                        Console.Beep();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("My congratulations, you've got a cake!");
                        Console.WriteLine("========================");
                        Console.WriteLine(e.Data);
                        Console.WriteLine("========================");
                        Directory.CreateDirectory($"{session}");
                        string exportPath = $"{session}/maze_completed_{moveNumber}.png";
                        Cell.ExportMaze(exportPath, maze, navState);

                        using (StreamWriter writer = new StreamWriter($"{session}/maze_t_{NavigationState.Target.X}_{NavigationState.Target.Y}_s_{NavigationState.StartPosition.X}_{NavigationState.StartPosition.Y}_{moveNumber}.route"))
                        {
                            foreach (var mov in route)
                            {
                                writer.WriteLine(mov);
                            }
                        }
                        route.Clear();

                        using (StreamWriter writer = new StreamWriter($"{session}/completeData_{moveNumber}.json"))
                        {
                            writer.WriteLine(e.Data);
                        }

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

                    if (rec && !NavigationState.IsTracingRoute)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("End of record. Continue moving with autopilot...");
                        Console.ResetColor();
                    }

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

        private static void AskForAutoRoute(int inputDelay)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Current targeting data: {NavigationState.Target.X}, {NavigationState.Target.Y}; Start: {NavigationState.StartPosition.X}, {NavigationState.StartPosition.Y}");
            Console.Write("Use loaded file preset? [Y/N]: ");
            if (Reader.TryReadLine(out string input, inputDelay)
                && !string.IsNullOrWhiteSpace(input)
                && input.StartsWith("Y", StringComparison.OrdinalIgnoreCase))
            {
                var foundFile = Directory.EnumerateFiles("routes/").Where(x =>
                {
                    try
                    {
                        var lst = x.Split('_');
                        int ind = Array.FindIndex(lst, y => y.StartsWith("t", StringComparison.OrdinalIgnoreCase));
                        if (ind != -1 && lst.Length > ind + 1)
                        {
                            int targetX = int.Parse(lst[ind + 1]);
                            int targetY = int.Parse(lst[ind + 2]);
                            if (targetX != NavigationState.Target.X || targetY != NavigationState.Target.Y) return false;
                        }
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }).OrderBy(x =>
                {
                    var lst = x.Split('_');
                    int ind = Array.FindIndex(lst, y => y.StartsWith("s", StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"Debug index = {ind} for file {x}");
                    if (ind != -1 && lst.Length > ind + 1)
                    {
                        int startX = int.Parse(lst[ind + 1]);
                        int startY = int.Parse(lst[ind + 2]);
                        int dist = (NavigationState.StartPosition - new Vector2(startX, startY)).LengthSquared();
                        Console.WriteLine($"For vector with start position {startX}, {startY} distance is {dist}");
                        return dist;
                    }
                    return 1000000;
                }).FirstOrDefault();
                if (foundFile != null)
                {
                    using (StreamReader reader = new StreamReader(foundFile))
                    {
                        NavigationState.RecordedRoute.Clear();
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            NavigationState.RecordedRoute.Push(line);
                        }
                    }
                    Console.WriteLine("Route has been successfully loaded! Starting tracing...");
                }
                else
                {
                    Console.WriteLine("Route not found, use default moving...");
                }
            }
            Console.ResetColor();
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

    static class Reader
    {
        private static Thread inputThread;
        private static AutoResetEvent getInput, gotInput;
        private static string input;

        static Reader()
        {
            getInput = new AutoResetEvent(false);
            gotInput = new AutoResetEvent(false);
            inputThread = new Thread(reader);
            inputThread.IsBackground = true;
            inputThread.Start();
        }

        private static void reader()
        {
            while (true)
            {
                getInput.WaitOne();
                input = Console.ReadLine();
                gotInput.Set();
            }
        }

        // omit the parameter to read a line without a timeout
        public static string ReadLine(int timeOutMillisecs = Timeout.Infinite)
        {
            getInput.Set();
            bool success = gotInput.WaitOne(timeOutMillisecs);
            if (success)
                return input;
            else
                throw new TimeoutException("User did not provide input within the time limit.");
        }

        public static bool TryReadLine(out string line, int timeOutMillisecs = Timeout.Infinite)
        {
            getInput.Set();
            bool success = gotInput.WaitOne(timeOutMillisecs);
            if (success)
                line = input;
            else
                line = null;
            return success;
        }
    }
}