namespace MazeRunner
{
    public struct Vector2
    {
        public int X { get; set; }

        public int Y { get; set; }

        public Vector2(int x, int y)
        {
            X = x;
            Y = y;
        }


        public static Vector2 operator+(Vector2 a, Vector2 b)
        {
            return new Vector2(a.X + b.X, a.Y + b.Y);
        }

        public static Vector2 operator-(Vector2 a, Vector2 b)
        {
            return new Vector2(a.X - b.X, a.Y - b.Y);
        }

        public static Vector2 operator -(Vector2 vector)
        {
            return new Vector2(-vector.X, -vector.Y);
        }

        public int LengthSquared()
        {
            return (X * X) + (Y * Y);
        }
    }
}