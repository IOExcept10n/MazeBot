using System.Linq;
using System.Diagnostics;

namespace MazeRunner
{
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
}