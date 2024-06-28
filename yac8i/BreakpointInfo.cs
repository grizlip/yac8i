using System.Threading;

namespace yac8i
{
    public class BreakpointInfo
    {
        public int HitCount { get => hitCount; }
        private int hitCount = 0;

        internal void OnHit()
        {
            Interlocked.Increment(ref hitCount);
        }
    }
}