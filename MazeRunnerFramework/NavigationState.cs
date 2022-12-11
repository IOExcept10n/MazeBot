using System.Collections;
using System.Collections.Generic;

namespace MazeRunner
{
    public struct NavigationState
    {
        private static Vector2 target;

        public int X { get; set; }

        public int Y { get; set; }

        public static Vector2 StartPosition { get; set; }

        public static Vector2 Target
        {
            get => target;
            set
            {
                target = value;
                ArtificalTarget = value;
            }
        }

        public static Vector2 RightDelta { get; set; }

        public static Vector2 UpDelta { get; set; }

        public static Vector2 ArtificalTarget { get; set; }

        public static Stack<string> RecordedRoute { get; set; } = new Stack<string>();

        public static bool IsTracingRoute => RecordedRoute.Count > 0;

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
}