using System;

namespace yac8i;
public enum RefreshRequest
{
    Clear = 0,
    Draw
}
public class ScreenRefreshEventArgs : EventArgs
{
    public bool[,] Surface { get; private set; }
    public RefreshRequest RequestType { get; private set; }
    public ScreenRefreshEventArgs(RefreshRequest refreshRequest, bool[,] surface = null)
    {
        Surface = surface;
        RequestType = refreshRequest;
    }
}
