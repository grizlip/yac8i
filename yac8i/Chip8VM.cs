using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using yac8i.TickTimer;

namespace yac8i
{
    public class Chip8VM : IChip8VM
    {
        public event EventHandler<int> ProgramLoaded;

        public bool[,] Surface => state.Surface;

        public event EventHandler<string> NewMessage;

        public event EventHandler<bool> BeepStatus;

        public event EventHandler Tick;

        public IReadOnlyCollection<byte> Memory => state.Memory;

        public IReadOnlyCollection<byte> Registers => state.Registers;

        public ushort IRegister => state.IRegister;

        public ushort ProgramCounter => state.ProgramCounter;

        internal readonly List<Instruction> instructions;

        internal static readonly byte[] font = [
                                    0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
                                    0x20, 0x60, 0x20, 0x20, 0x70, // 1
                                    0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
                                    0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
                                    0x90, 0x90, 0xF0, 0x10, 0x10, // 4
                                    0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
                                    0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
                                    0xF0, 0x10, 0x20, 0x40, 0x40, // 7
                                    0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
                                    0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
                                    0xF0, 0x90, 0xF0, 0x90, 0x90, // A
                                    0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
                                    0xF0, 0x80, 0x80, 0x80, 0xF0, // C
                                    0xE0, 0x90, 0x90, 0x90, 0xE0, // D
                                    0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
                                    0xF0, 0x80, 0xF0, 0x80, 0x80  // F
                                    ];
        
        internal int instructionsToExecuteInFrame = 0;

        internal readonly ITickTimer tickTimer;
        internal readonly VmState state = new();
        private readonly ConcurrentDictionary<ushort, BreakpointInfo> breakpoints = [];

        private byte[] loadedProgram = null;

        private bool beepStatus = false;

        private bool loaded = false;

        private CancellationToken? ct;

        private int instructionsPerFrame = 7;

        private int programBytesCount = 0;

        public Chip8VM(ITickTimer tickTimer = null)
        {
            if (tickTimer == null)
            {
                this.tickTimer = new HighResolutionTimer(1000f / 60f); //60 times per second 
            }
            else
            {
                this.tickTimer = tickTimer;
            }
            instructions = InstructionsFactory.GetInstructions(this.state);
            if (instructions.Count != instructions.DistinctBy(item => item.Opcode).Count())
            {
                throw new InvalidOperationException("Duplicates found in instruction set.");
            }

            StopAndReset();
        }

        public bool TryAddBreakpoint(ushort address, out BreakpointInfo breakpointInfo)
        {
            bool result = address >= 512 && (address - 512) < programBytesCount && address % 2 == 0;
            breakpointInfo = null;
            if (result)
            {
                breakpointInfo = new BreakpointInfo();
                result = breakpoints.TryAdd(address, breakpointInfo);
            }
            return result;
        }

        public bool TryRemoveBreakpoint(ushort address, out BreakpointInfo breakpointInfo)
        {
            return breakpoints.TryRemove(address, out breakpointInfo);
        }

        public bool TryRestore(string fileName)
        {
            bool result = false;
            bool startTimer = tickTimer.IsRunning;
            tickTimer.Stop();
            try
            {
                VmSerializableState.Restore(fileName, out var restoredState);
                if (restoredState != null)
                {
                    result = loadedProgram != null && restoredState.LoadedProgram.SequenceEqual(loadedProgram);
                    if (result)
                    {
                        beepStatus = restoredState.BeepStatus;
                        state.DelayTimer = restoredState.DelayTimer;
                        instructionsPerFrame = restoredState.InstructionsPerFrame;
                        instructionsToExecuteInFrame = restoredState.InstructionsToExecuteInFrame;
                        state.IRegister = restoredState.IRegister;
                        state.Memory = (byte[])restoredState.Memory.Clone();
                        programBytesCount = restoredState.ProgramBytesCount;
                        state.ProgramCounter = restoredState.ProgramCounter;
                        state.Registers = (byte[])restoredState.Registers.Clone();
                        state.Stack = new Stack<ushort>(restoredState.Stack.Reverse());
                        state.SoundTimer = restoredState.SoundTimer;
                        state.Surface = (bool[,])restoredState.Surface.Clone();
                    }
                    else
                    {
                        OnNewMessage("Tried loading state from different program.");
                    }
                }
            }
            catch (Exception ex)
            {
                OnNewMessage($"Error while storing state: {ex}");
            }

            if (startTimer)
            {
                tickTimer.Start();
            }
            return result;
        }

        public bool TryStore(string fileName)
        {
            bool startTimer = tickTimer.IsRunning;
            tickTimer.Stop();

            VmSerializableState state = new()
            {
                BeepStatus = beepStatus,
                DelayTimer = this.state.DelayTimer,
                InstructionsPerFrame = instructionsPerFrame,
                InstructionsToExecuteInFrame = instructionsToExecuteInFrame,
                IRegister = this.state.IRegister,
                Memory = (byte[])this.state.Memory.Clone(),
                LoadedProgram = (byte[])loadedProgram.Clone(),
                ProgramBytesCount = programBytesCount,
                ProgramCounter = ProgramCounter,
                Registers = (byte[])this.state.Registers.Clone(),
                Stack = new Stack<ushort>(this.state.Stack.Reverse()),
                SoundTimer = this.state.SoundTimer,
                Surface = (bool[,])this.state.Surface.Clone(),
            };

            if (startTimer)
            {
                tickTimer.Start();
            }

            var result = true;

            try
            {
                VmSerializableState.Store(fileName, state);
            }
            catch (Exception ex)
            {
                OnNewMessage($"Error while storing state {ex}");
                result = false;
            }

            return result;
        }

        public async Task LoadAsync(Stream program)
        {
            programBytesCount = await program.ReadAsync(state.Memory.AsMemory(512, state.Memory.Length - 512));
            loadedProgram = (byte[])state.Memory.Clone();

            loaded = true;
            ProgramLoaded?.Invoke(this, programBytesCount);
        }

        public bool Load(string programSourceFilePath)
        {
            try
            {
                using (FileStream programSourceStreamReader = new(programSourceFilePath, FileMode.Open))
                {
                    programBytesCount = programSourceStreamReader.Read(state.Memory, 512, state.Memory.Length - 512);
                    loadedProgram = (byte[])state.Memory.Clone();
                }
                loaded = true;
                ProgramLoaded?.Invoke(this, programBytesCount);
            }
            catch (Exception ex)
            {
                OnNewMessage($"File read error: {ex.Message}");
            }
            return loaded;
        }

        public async Task StartAsync(CancellationToken cancelToken)
        {
            if (ct.HasValue)
            {
                StopAndReset();
            }
            ct = cancelToken;

            if (loaded)
            {

                try
                {
                    tickTimer.Elapsed += OnTick;
                    tickTimer.Start();

                    while (!ct.Value.IsCancellationRequested)
                    {
                        await Task.Delay(200, cancelToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    //TODO: Add some more information about place in which program failed.
                    OnNewMessage(ex.Message);
                }
                finally
                {
                    StopAndReset();
                }
            }
        }

        public void Pause()
        {
            tickTimer.Stop();
        }

        public void Go()
        {
            tickTimer.Start();
        }

        public void StopAndReset()
        {
            tickTimer.Stop();
            tickTimer.Elapsed -= OnTick;
            programBytesCount = 0;
            state.Clear();
            Array.Copy(font, state.Memory, font.Length);
            ct = null;
        }

        public void Step()
        {
            if (!tickTimer.IsRunning)
            {
                if (instructionsToExecuteInFrame > 0)
                {
                    ExecuteInstruction();
                    instructionsToExecuteInFrame--;
                }
                else if (instructionsToExecuteInFrame == 0)
                {
                    try
                    {
                        if (state.SoundTimer < 1)
                        {
                            state.SoundTimer = 0;
                            OnBeepStatus(false);
                        }
                        else
                        {
                            state.SoundTimer = (byte)(state.SoundTimer - 1);
                            OnBeepStatus(true);
                        }

                        if (state.DelayTimer > 0)
                        {
                            state.DelayTimer = (byte)(state.DelayTimer - 1);
                        }
                    }
                    finally
                    {
                        Tick?.Invoke(this, EventArgs.Empty);
                    }
                    instructionsToExecuteInFrame = instructionsPerFrame;
                }
            }
        }

        public void UpdateKeyState(ushort key, bool pressed)
        {
            ushort keyBitValue = (ushort)(1 << key);

            if (pressed)
            {
                state.LastPressedKey = null;
                state.PressedKeys |= keyBitValue;
            }
            else
            {
                state.LastPressedKey = key;
                state.PressedKeys &= (ushort)~keyBitValue;
            }
        }

        public string GetMnemonic(ushort instructionValue)
        {
            string result = "unknown";
            var instruction = instructions.Find(item => (instructionValue & item.Mask) == item.Opcode);
            if (instruction != null)
            {
                ushort argsMask = (ushort)(instruction.Mask ^ 0xFFFF);
                ushort args = (ushort)(instructionValue & argsMask);
                if (instruction.Emit != null)
                {
                    result = instruction.Emit(args);
                }
                else
                {
                    result = "none";
                }
            }
            return result;
        }

        public ushort GetOpcode(uint instructionAddress)
        {
            if (state.Memory.Length < instructionAddress + 1)
            {
                throw new ArgumentException("Instruction address out of bounds", $"{nameof(instructionAddress)}");
            }

            return (ushort)(state.Memory[instructionAddress] << 8 | state.Memory[instructionAddress + 1]);
        }

        private bool ExecuteInstruction()
        {
            bool executed = false;
            try
            {
                if (ProgramCounter < state.Memory.Length)
                {
                    if (breakpoints.TryGetValue(ProgramCounter, out var breakpointInfo))
                    {
                        if (!breakpointInfo.IsActive)
                        {
                            Pause();
                            breakpointInfo.IsActive = true;
                            breakpointInfo.OnHit();
                        }
                        else
                        {
                            breakpointInfo.IsActive = false;
                        }

                    }
                    if (!breakpointInfo?.IsActive ?? true)
                    {
                        byte[] instructionRaw = [state.Memory[ProgramCounter], state.Memory[ProgramCounter + 1]];

                        ushort instructionValue = (ushort)(instructionRaw[0] << 8 | instructionRaw[1]);

                        var instruction = instructions.Find(item => (instructionValue & item.Mask) == item.Opcode);
                        if (instruction != null && !(ct.HasValue && ct.Value.IsCancellationRequested))
                        {
                            ushort argsMask = (ushort)(instruction.Mask ^ 0xFFFF);
                            ushort args = (ushort)(instructionValue & argsMask);

                            if (instruction.Execute != null)
                            {
                                if (instruction.Execute(args))
                                {
                                    state.ProgramCounter += 2;
                                }
                                executed = true;
                            }
                        }
                        else
                        {
                            tickTimer.Stop();
                        }
                    }
                }
                else
                {
                    tickTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                OnNewMessage(ex.Message);
            }
            return executed;
        }

        private void OnTick(object sender, TickTimerElapsedEventArgs args)
        {
            int delayedInstructions = (int)Math.Floor(instructionsPerFrame * (args.Delay / tickTimer.Interval));

            for (instructionsToExecuteInFrame = instructionsPerFrame + delayedInstructions; 0 < instructionsToExecuteInFrame; instructionsToExecuteInFrame--)
            {
                if (!ExecuteInstruction())
                {
                    break;
                }
            }

            if (instructionsToExecuteInFrame == 0)
            {
                try
                {
                    if (state.SoundTimer < 1)
                    {
                        state.SoundTimer = 0;
                        OnBeepStatus(false);
                    }
                    else
                    {
                        state.SoundTimer = (byte)(state.SoundTimer - 1);
                        OnBeepStatus(true);
                    }

                    if (state.DelayTimer > 0)
                    {
                        state.DelayTimer = (byte)(state.DelayTimer - 1);
                    }
                }
                finally
                {
                    Tick?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void OnNewMessage(string msg)
        {
            NewMessage?.Invoke(this, msg);
        }

        private void OnBeepStatus(bool status)
        {
            if (status != beepStatus)
            {
                beepStatus = status;
                Task.Run(() => BeepStatus?.Invoke(this, status));
            }
        }
    }
}
