namespace TerrainPatcher;

internal static class Utils
{
    internal static int DivFloor(int a, int b)
    {
        if (((a < 0) ^ (b < 0)) && (a % b != 0)) return a / b - 1;
        else return a / b;
    }

    internal static int RemFloor(int a, int b)
    {
        if (((a < 0) ^ (b < 0)) && (a % b != 0)) return a % b + b;
        else return a % b;
    }
}
