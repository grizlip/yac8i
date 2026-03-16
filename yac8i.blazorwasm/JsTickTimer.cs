using yac8i.TickTimer;


namespace yac8i.blazorwasm
{
    public class JsTickTimer : ITickTimer
    {
        public event EventHandler<TickTimerElapsedEventArgs>? Elapsed;
        public float Interval { get; set; } = 1;
        public bool IsRunning { get; private set; }
        public void Start()
        {
            IsRunning = true;
        }
        public void Stop(bool joinThread = true)
        {
            IsRunning = false;
        }

        public void Tick()
        {
            if (IsRunning)
            {
                Elapsed?.Invoke(this, new TickTimerElapsedEventArgs(0));
            }


        }
    }
}