using System.Linq;
using System.Diagnostics;

namespace MazeRunner
{
    public class DeadEnd : Cell
    {
        public override string MoveNext(Cell[,] maze, NavigationState state, Cell previous)
        {
            TimesVisited++;
            IsLocked = true;
            if (NavigationState.RecordedRoute.Count > 0)
            {
                return NavigationState.RecordedRoute.Pop();
            }
            return AvailableMoves.First();
        }
    }
}