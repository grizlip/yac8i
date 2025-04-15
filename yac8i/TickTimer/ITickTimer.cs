using System;

namespace yac8i.TickTimer
{
    public interface ITickTimer
    {
        event EventHandler<TickTimerElapsedEventArgs> Elapsed;
        float Interval { get; set; }
        bool IsRunning { get; }
        void Start();
        void Stop(bool joinThread = true);
    }    
}