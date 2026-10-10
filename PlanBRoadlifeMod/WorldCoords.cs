namespace Roadlife
{
    internal static class WorldCoords
    {
        // GHexes.IsHex indexes the flag array directly, including for sentinel coordinates.
        internal static bool IsHex(int2 coords)
            => CoordinateBounds.Contains(coords.x, coords.y, GWorld.size.x, GWorld.size.y)
                && GHexes.IsHex(coords);
    }
}
