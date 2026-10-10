namespace Roadlife
{
    internal static class CoordinateBounds
    {
        internal static bool Contains(int x, int y, int width, int height)
            => x >= 0 && y >= 0 && x < width && y < height;
    }
}
