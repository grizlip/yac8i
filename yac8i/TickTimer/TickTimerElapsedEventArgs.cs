using System;

namespace yac8i.TickTimer
{
    public class TickTimerElapsedEventArgs(double delay) : EventArgs
    {
        /// <summary>/// Real timer delay in [ms]/// </summary>
        public double Delay { get; } = delay;
    }
}