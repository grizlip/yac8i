using System;

namespace yac8i;
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
