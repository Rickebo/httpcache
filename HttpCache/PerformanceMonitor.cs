using System.Diagnostics;

namespace HttpCache;

public class PerformanceMonitor
{
    private Dictionary<string, Stopwatch> _stopwatches = new();

    public PerformanceMonitor()
    {
    }

    private Stopwatch GetStopwatch(string name) =>
        _stopwatches.TryGetValue(name, out var foundStopwatch)
            ? foundStopwatch
            : _stopwatches[name] = new Stopwatch();

    public void ResetAll()
    {
        foreach (var sw in _stopwatches.Values)
        {
            sw.Stop();
            sw.Reset();
        }
    }

    public async Task RunTimedAsync(string name, Func<Task> task)
    {
        var sw = GetStopwatch(name);

        sw.Start();
        await task();
        sw.Stop();
    }

    public async Task<T> RunTimedAsync<T>(string name, Func<Task<T>> func)
    {
        var sw = GetStopwatch(name);

        sw.Start();
        var result = await func();
        sw.Stop();
        return result;
    }
    
    public void RunTimed(string name, Action action)
    {
        var sw = GetStopwatch(name);

        sw.Start();
        action();
        sw.Stop();
    }
    
    public void Start(string name, bool fromStart = false)
    {
        var sw = GetStopwatch(name);

        if (fromStart)
            sw.Reset();

        sw.Start();
    }

    public void Reset(string name) => 
        GetStopwatch(name).Reset();
    
    public void Stop(string name) =>
        GetStopwatch(name).Stop();

    public TimeSpan GetElapsed(string name) => 
        GetStopwatch(name).Elapsed;
}