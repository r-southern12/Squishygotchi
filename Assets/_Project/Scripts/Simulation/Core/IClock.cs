using System;

namespace Squishy.Simulation.Core
{
    /// <summary>
    /// The only source of "now" for the simulation. Needs drain, offline progress and
    /// timers all read from here, so a server-time clock can replace the device clock later.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow { get { return DateTime.UtcNow; } }
    }

    /// <summary>A clock tests and fast-forward tools can move by hand.</summary>
    public sealed class ManualClock : IClock
    {
        private DateTime _now;

        public ManualClock(DateTime startUtc)
        {
            _now = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get { return _now; } }

        public void Advance(TimeSpan by)
        {
            _now = _now + by;
        }
    }
}
