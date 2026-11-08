using System.Runtime.InteropServices;

namespace LoqLighting.Core;

public static class TimerResolution
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    public static IDisposable Request(uint periodMilliseconds = 1)
    {
        return new ResolutionScope(periodMilliseconds);
    }

    private sealed class ResolutionScope : IDisposable
    {
        private readonly uint _period;
        private bool _disposed;

        public ResolutionScope(uint period)
        {
            _period = period;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    TimeBeginPeriod(_period);
                }
            }
            catch
            {
                // Fallback gracefully if winmm is unavailable
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        TimeEndPeriod(_period);
                    }
                }
                catch
                {
                    // Suppress cleanup errors
                }
            }
        }
    }
}
