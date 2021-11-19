using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace yac8i
{

    public enum RefreshRequest
    {
        Clear = 0,
        Draw
    }
    public class ScreenRefreshEventArgs : EventArgs
    {
        public RefreshRequest RequestType { get; private set; }
        public ScreenRefreshEventArgs(RefreshRequest refreshRequest)
        {
            this.RequestType = refreshRequest;
        }

    }
}