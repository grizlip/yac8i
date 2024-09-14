using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace yac8i.gui.sdl.MVVM
{
    public class BreakpointViewModel : ObservableObject, IDisposable
    {
        public ushort Address
        {
            get;
        }

        public int Count
        {
            get => breakpointInfo.HitCount;
        }

        private readonly BreakpointInfo breakpointInfo;
        
        public BreakpointViewModel(BreakpointInfo breakpointInfo, ushort address)
        {
            Address = address;
            this.breakpointInfo = breakpointInfo;
            this.breakpointInfo.BreakpointHit += OnBreakpointHit;
        }

        public void Dispose()
        {
            breakpointInfo.BreakpointHit -= OnBreakpointHit;
        }

        private void OnBreakpointHit(object? sender, EventArgs args)
        {
            OnPropertyChanged(nameof(Count));
        }
    }
}