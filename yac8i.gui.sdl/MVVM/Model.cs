using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace yac8i.gui.sdl.MVVM
{
    public class Model : IDisposable
    {
        public event EventHandler? ProgramLoaded;
        public event EventHandler? Tick;
        public IReadOnlyCollection<ushort> Opcodes => opcodes;

        public IReadOnlyCollection<byte> Registers => registers;
        public ushort IRegister
        {
            get
            {
                return vm.IRegister;
            }
        }

        public ushort ProgramCounter
        {
            get
            {
                return vm.ProgramCounter;
            }
        }

        private readonly List<ushort> opcodes = new List<ushort>();
        private readonly List<byte> registers = new List<byte>();
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task? vmTask = null;
        private Task? tickTask = null;
        private string lastRomFile;
        private readonly Chip8VM vm;

        private readonly SDLFront sdlFront;
        public Model(Chip8VM vm)
        {
            this.vm = vm;
            sdlFront = new SDLFront(vm);
            using (ExecutionContext.SuppressFlow())
            {
                Task.Run(() => sdlFront.InitializeAndStart());
            }
            this.vm.ProgramLoaded += OnProgramLoaded;
            this.lastRomFile = string.Empty;
            UpdateOpcodes();
            UpdateRegisters();
        }

        private void OnVmTickAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (vm.TickAutoResetEvent.WaitOne(1))
                {
                    Tick?.Invoke(this, EventArgs.Empty);
                    sdlFront?.DoFrameAutoResetEvent.Set();
                }
            }
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

        public void Load(string file)
        {
            lastRomFile = file;
            cancellationTokenSource.Cancel();

            if ((!vmTask?.IsCompleted) ?? false)
            {
                vmTask?.Wait();
            }
            if ((!tickTask?.IsCompleted) ?? false)
            {
                tickTask?.Wait();
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            vm.StopAndReset();
            vm.Load(file);
        }

        public void Start()
        {
            if (!vmTask?.IsCompleted ?? false)
            {
                return;
            }
            var token = cancellationTokenSource.Token;
            vmTask = vm.StartAsync(token);

            using (ExecutionContext.SuppressFlow())
            {
                tickTask = Task.Run(() => OnVmTickAsync(token));
            }

        }

        public void Pause()
        {
            vm.Puase();
        }

        public void Go()
        {
            vm.Go();
        }

        public void Reset()
        {
            Load(lastRomFile);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            if (!vmTask?.IsCompleted ?? false)
            {
                vmTask?.Wait();
            }
            if (!tickTask?.IsCompleted ?? false)
            {
                tickTask?.Wait();
            }
            cancellationTokenSource.Dispose();
            vmTask?.Dispose();
            tickTask?.Dispose();
        }

        private void OnProgramLoaded(object? sender, int bytesCount)
        {
            UpdateOpcodes(bytesCount);
            ProgramLoaded?.Invoke(this, EventArgs.Empty);
        }
    }
}
