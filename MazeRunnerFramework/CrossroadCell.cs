using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace MazeRunner
{
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
                Vector2 target = new Vector2(NavigationState.ArtificalTarget.X, NavigationState.ArtificalTarget.Y);
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