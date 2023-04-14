using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace yac8i.gui.sdl.MVVM
{
    public class Model : IDisposable
    {
        public event EventHandler ProgramLoaded;
        public IReadOnlyCollection<ushort> Opcodes => opcodes;

        public IReadOnlyCollection<byte> Registers => registers;

        public ushort ProgramCounter { get; private set; }

        private readonly List<ushort> opcodes = new List<ushort>();
        private readonly List<byte> registers = new List<byte>();
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task? vmTask = null;
        private readonly Chip8VM vm;

        public Model(Chip8VM vm)
        {
            this.vm = vm;
            this.vm.ProgramLoaded += OnProgramLoaded;
            UpdateOpcodes();
            UpdateRegisters();

        }

        public void UpdateOpcodes(int bytesCount = 0)
        {
            opcodes.Clear();
            int bytesCountAdjusted = (bytesCount + 512);
            for (int i = 512; i < bytesCountAdjusted; i += 2)
            {
                byte[] instructionRaw = new byte[] { vm.Memory[i], vm.Memory[i + 1] };
                ushort opcode = (ushort)(instructionRaw[0] << 8 | instructionRaw[1]);
                opcodes.Add(opcode);
            }
        }

        public void UpdateRegisters()
        {
            registers.Clear();
            registers.AddRange(vm.Registers);
        }

        public void LoadAndExecute(string file)
        {
            if (!vmTask?.IsCompleted ?? false)
            {
                cancellationTokenSource.Cancel();
                vmTask?.Wait();
            }
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            vm.StopAndReset();
            vm.Load(file);
            vmTask = vm.StartAsync(cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            if (!vmTask?.IsCompleted ?? false)
            {
                cancellationTokenSource.Cancel();
                vmTask?.Wait();
            }
            cancellationTokenSource.Dispose();
            vmTask?.Dispose();
        }

        private void OnProgramLoaded(object sender, int bytesCount)
        {
            UpdateOpcodes(bytesCount);
            ProgramLoaded?.Invoke(this, EventArgs.Empty);
        }
    }
}