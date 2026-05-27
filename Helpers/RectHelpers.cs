using Windows.Foundation;

namespace AutoSales.Helpers
{
    /// <summary>
    /// Windows.Foundation.Rect doesn't have Empty / Union / IsEmpty like System.Windows.Rect does.
    /// These helpers fill the gap so the dashboard VM can keep its original shape.
    /// </summary>
    public static class RectHelpers
    {
        public static Rect Empty => new Rect(double.PositiveInfinity, double.PositiveInfinity, 0, 0);

        public static bool IsEmpty(this Rect r) =>
            double.IsPositiveInfinity(r.X) && double.IsPositiveInfinity(r.Y) && r.Width == 0 && r.Height == 0;

        public static Rect Union(this Rect a, Rect b)
        {
            if (a.IsEmpty()) return b;
            if (b.IsEmpty()) return a;

            var minX = System.Math.Min(a.X, b.X);
            var minY = System.Math.Min(a.Y, b.Y);
            var maxX = System.Math.Max(a.X + a.Width, b.X + b.Width);
            var maxY = System.Math.Max(a.Y + a.Height, b.Y + b.Height);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
