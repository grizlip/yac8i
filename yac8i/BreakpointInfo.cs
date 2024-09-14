using System;
using System.Threading;

namespace yac8i
{
    public class BreakpointInfo
    {
        public int HitCount { get => hitCount; }
        private int hitCount = 0;
        public event EventHandler BreakpointHit;

        public bool IsActive { get; internal set; }

        internal void OnHit()
        {
            Interlocked.Increment(ref hitCount);
            BreakpointHit?.Invoke(this, EventArgs.Empty);
        }
    }
}