using System;

namespace yac8i.TickTimer
{
    public class TickTimerElapsedEventArgs : EventArgs
    {
        /// <summary>/// Real timer delay in [ms]/// </summary>
        public double Delay { get; }

        internal TickTimerElapsedEventArgs(double delay)
        {
            Delay = delay;
        }
    }
}