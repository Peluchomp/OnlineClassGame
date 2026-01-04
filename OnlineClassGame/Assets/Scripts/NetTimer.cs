using System.Diagnostics;

public static class NetTimer
{
    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public static double GetTime()
    {
        return _stopwatch.Elapsed.TotalSeconds;
    }
}