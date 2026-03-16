
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace yac8i
{
    public interface IChip8VM
    {
        bool[,] Surface { get; }
        IReadOnlyCollection<byte> Memory { get; }
        IReadOnlyCollection<byte> Registers { get; }
        ushort IRegister { get; }
        ushort ProgramCounter { get; }

        event EventHandler<int> ProgramLoaded;
        event EventHandler<string> NewMessage;
        event EventHandler<bool> BeepStatus;
        event EventHandler Tick;
        string GetMnemonic(ushort instructionValue);
        ushort GetOpcode(uint instructionAddress);
        void Go();
        bool Load(string programSourceFilePath);
        Task LoadAsync(Stream program);
        void Pause();
        Task StartAsync(CancellationToken cancelToken);
        void Step();
        void StopAndReset();
        bool TryAddBreakpoint(ushort address, out BreakpointInfo breakpointInfo);
        bool TryRemoveBreakpoint(ushort address, out BreakpointInfo breakpointInfo);
        bool TryRestore(string fileName);
        bool TryStore(string fileName);
        void UpdateKeyState(ushort key, bool pressed);
    }
}