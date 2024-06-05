using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace yac8i.gui.sdl.MVVM
{
    public class Model : IDisposable
    {
        public event EventHandler? ProgramLoaded;
        public event EventHandler? Tick
        {
            add { vm.Tick += value; }
            remove { vm.Tick -= value; }
        }

        public IReadOnlyCollection<ushort> Opcodes => opcodes;

        public IReadOnlyCollection<byte> Registers => registers;
        public ushort IRegister => vm.IRegister;

        public ushort ProgramCounter => vm.ProgramCounter;

        private readonly List<ushort> opcodes = [];
        private readonly List<byte> registers = [];
        private CancellationTokenSource cancellationTokenSource = new();
        private Task? vmTask = null;

        private string lastRomFile;
        private readonly Chip8VM vm;

        private readonly SDLFront sdlFront;
        public Model(Chip8VM vm)
        {
            this.vm = vm;
            sdlFront = new SDLFront(vm);

            //TODO: long running
            Task.Run(() => sdlFront.InitializeAndStart());

            this.vm.ProgramLoaded += OnProgramLoaded;
            this.lastRomFile = string.Empty;
            UpdateOpcodes();
            UpdateRegisters();
        }

        public string GetMnemonic(ushort instruction)
        {
            return vm.GetMnemonic(instruction);
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
        }

        public void Pause()
        {
            vm.Pause();
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
            cancellationTokenSource.Dispose();
            vmTask?.Dispose();
            sdlFront.Stop();
        }


        private void UpdateOpcodes(int bytesCount = 0)
        {
            opcodes.Clear();
            int bytesCountAdjusted = bytesCount + 512;
            for (int i = 512; i < bytesCountAdjusted; i += 2)
            {
                byte[] instructionRaw = [vm.Memory.ElementAt(i), vm.Memory.ElementAt(i + 1)];
                ushort opcode = (ushort)(instructionRaw[0] << 8 | instructionRaw[1]);
                opcodes.Add(opcode);
            }
        }

        private void OnProgramLoaded(object? sender, int bytesCount)
        {
            UpdateOpcodes(bytesCount);
            ProgramLoaded?.Invoke(this, EventArgs.Empty);
        }
    }
}
