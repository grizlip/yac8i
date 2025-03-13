using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace yac8i
{
    public class Chip8VM
    {
        public event EventHandler<int> ProgramLoaded;

        public bool[,] Surface => surface;

        public EventHandler<string> NewMessage;

        public EventHandler<bool> BeepStatus;

        public event EventHandler Tick;

        public IReadOnlyCollection<byte> Memory => memory;

        public IReadOnlyCollection<byte> Registers => registers;

        public ushort IRegister { get; private set; } = 0;

        public ushort ProgramCounter { get; private set; } = 0x200;

        internal readonly List<Instruction> instructions;

        internal  Stack<ushort> stack = new();
        
        internal byte[] registers = new byte[16];

        private readonly byte[] font = [
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

        private readonly ConcurrentDictionary<ushort, BreakpointInfo> breakpoints = [];

        private readonly HighResolutionTimer tickTimer = new(1000f / 60f); //60 times per second

        private byte[] memory = new byte[4096];

        private byte[] loadedProgram = null;

        private int instructionsToExecuteInFrame = 0;

        private byte soundTimer = 0;

        private bool beepStatus = false;

        private byte delayTimer = 0;

        private ushort pressedKeys = 0;

        private ushort? lastPressedKey = null;

        private bool loaded = false;

        private bool[,] surface = new bool[64, 32];

        private CancellationToken? ct;

        private int instructionsPerFrame = 7;

        private int programBytesCount = 0;

        public Chip8VM()
        {
            instructions =
        [
            // TODO: This instruction collides with 0x00EF and 0x00E0
            //       It happens because all three instructions beings with 0
            //       and in case of 0x0000 we are checking only top most four bits
            //       if they are 0, then we assume we have a match
            //       Find a way to implement this better. 
            //new() { Opcode=0x0000,Mask=0xF000},
            new() { Opcode=0x00E0,Mask=0xFFFF,
            Execute = args =>
            {
                Array.Clear(surface);
                return true;
            },
            Emit = args =>
            {
                return "CLS";
            }},

            new() { Opcode=0x00EE,Mask=0xFFFF,
            Execute = args =>
            {
                if(this.stack.TryPop(out ushort returnAddress))
                {
                    this.ProgramCounter = returnAddress;
                }
                else
                {
                    throw new ArgumentException("Error. Stack empty while trying to return from subroutine");
                }
                return false;
            },
            Emit = args =>
            {
                return "RET";
            }},

            new() { Opcode=0x1000,Mask=0xF000,
            Execute = args =>
            {
                    this.ProgramCounter = Instruction.NNN(args);
                    return false;
            },
            Emit = args=>
            {
                return $"JP 0x{Instruction.NNN(args):X4}";
            }},

            new() { Opcode=0x2000,Mask=0xF000,
            Execute = args =>
            {
                this.stack.Push((ushort)(ProgramCounter+2));
                this.ProgramCounter = Instruction.NNN(args);

                return false;
            },
            Emit = args=>
            {
                return $"CALL 0x{Instruction.NNN(args):X4}";
            }
            },

            new() { Opcode=0x3000,Mask=0xF000,
            Execute = args =>
            {
                byte registerIndex = Instruction.X(args);
                CheckRegisterIndex(registerIndex);
                byte registerValue = registers[registerIndex];
                byte compareToValue = Instruction.NN(args);
                bool valuesEqual = registerValue == compareToValue;
                if(valuesEqual)
                {
                    ProgramCounter+= 4;
                }
                return !valuesEqual;
            },
            Emit = args =>
            {
                return $"SE V{Instruction.X(args)}, 0x{Instruction.NN(args):X4}";
            }
            },

            new() { Opcode=0x4000,Mask=0xF000,
            Execute = args =>
            {
                byte registerIndex = Instruction.X(args);
                CheckRegisterIndex(registerIndex);
                byte registerValue = registers[registerIndex];
                byte compareToValue = Instruction.NN(args);
                bool valuesEqual = registerValue == compareToValue;
                if(!valuesEqual)
                {
                    ProgramCounter+= 4;
                }
                return valuesEqual;
            },
            Emit = args =>
            {
                return $"SNE V{Instruction.X(args)}, 0x{Instruction.NN(args):X4}";
            }},

            new() { Opcode=0x5000,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                bool valuesEqual = registers[registerXIndex] == registers[registerYIndex];
                if(valuesEqual)
                {
                    ProgramCounter +=4;
                }
                return !valuesEqual;
            },
            Emit = args =>
            {
                return $"SE V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x6000,Mask=0xF000,
            Execute = args =>
            {
                byte registerIndex  = Instruction.X(args);
                CheckRegisterIndex(registerIndex);
                byte newRegisterValue =  Instruction.NN(args);
                registers[registerIndex] = newRegisterValue;
                return true;
            },
            Emit = args=>
            {
                return $"LD V{Instruction.X(args)}, 0x{Instruction.NN(args):X4}";

            }},

            new() { Opcode=0x7000,Mask=0xF000,
            Execute = args =>
            {
                int registerIndex  =  Instruction.X(args);
                CheckRegisterIndex(registerIndex);
                byte valueToAdd =  Instruction.NN(args);
                registers[registerIndex] += valueToAdd; //this will wrap if overflow happens. Not sure if that is correct behavior. 
                return true;

            },
            Emit = args=>
            {
                return $"ADD V{Instruction.X(args)}, 0x{Instruction.NN(args):X4}";
            }},

            new() { Opcode=0x8000,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = registers[registerYIndex];
                return true;
            },
            Emit = args=>
            {
                return $"LD V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8001,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = (byte)(registers[registerXIndex] | registers[registerYIndex]);
                registers[0xF] = 0;
                return true;
            },
            Emit = args =>
            {
                return $"OR V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8002,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = (byte)(registers[registerXIndex] & registers[registerYIndex]);
                registers[0xF] = 0;
                return true;

            },
            Emit = args =>
            {
                return $"AND V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8003,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = (byte)(registers[registerXIndex] ^ registers[registerYIndex]);
                registers[0xF] = 0;
                return true;

            },
            Emit= args =>
            {
                return $"XOR V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8004,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                byte xValue = registers[registerXIndex];
                byte yValue = registers[registerYIndex];
                registers[registerXIndex] = (byte)(xValue + yValue);
                
                //TODO: find better way to check and do this?
                if(xValue + yValue >255)
                {
                    registers[0xF] = 1;
                }
                else
                {
                    registers[0xF] = 0;
                }
                return true;
            },
            Emit = args =>
            {
                return $"ADD V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8005,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                byte xValue = registers[registerXIndex];
                byte yValue = registers[registerYIndex];
                //perform subtraction (no need to worry about underflow here)
                registers[registerXIndex] = (byte)(xValue - yValue);
                registers[0xF] = (byte)(xValue>yValue ? 1 : 0);
                return true;
            },
            Emit = args =>
            {
                return $"SUB V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8006,Mask=0xF00F,
            Execute = args =>
            {
                //TODO: Implement some kind of switch that will enable user to use either CHIP-48 and SUPER-CHIP version or original 
                //       COSMAC VIP version. Currently CHIP-48 and SUPER-CHIP version is implemented.
                //       More here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#8xy6-and-8xye-shift
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = registers[registerYIndex];
                byte xValue = registers[registerXIndex];
                
                //right shift Vx by one
                registers[registerXIndex] = (byte)(xValue >>1);
                //set VF register
                registers[0xF] =(byte)(xValue & 0x1);

                return true;
            },
            Emit = args =>
            {
                return $"SHR V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x8007,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);

                byte xValue = registers[registerXIndex];
                byte yValue = registers[registerYIndex];
                //perform substraction (no need to worry about underflow here)
                registers[registerXIndex] = (byte)(yValue - xValue);
                registers[0xF] = (byte)(xValue<yValue ? 1 : 0);
                return true;

            },
            Emit = args =>
            {
                return $"SUBN V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x800E,Mask=0xF00F,
            Execute = args =>
            {
                //TODO: Implement some kind of switch that will enable user to use either CHIP-48 and SUPER-CHIP version or original 
                //       COSMAC VIP version. Currently CHIP-48 and SUPER-CHIP version is implemented.
                //       More here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#8xy6-and-8xye-shift
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = registers[registerYIndex];
                byte xValue = registers[registerXIndex];
                
                //left shift Vx by one
                registers[registerXIndex] = (byte)(xValue <<1);
                registers[0xF] =(byte)((xValue & 0x80) >>7);
                return true;

            },
            Emit = args =>
            {
                return $"SHL V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0x9000,Mask=0xF00F,
            Execute = args =>
            {
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                bool valuesEqual = registers[registerXIndex] == registers[registerYIndex];
                if(!valuesEqual)
                {
                    ProgramCounter +=4;
                }
                return valuesEqual;

            },
            Emit = args =>
            {
                return $"SNE V{Instruction.X(args)}, V{Instruction.Y(args)}";
            }},

            new() { Opcode=0xA000,Mask=0xF000,
            Execute = args =>
            {
                IRegister = Instruction.NNN(args);
                return true;
            },
            Emit = args =>
            {
                return $"LD I, 0x{Instruction.NNN(args)}";
            }},

            new() { Opcode=0xB000,Mask=0xF000,
            Execute = args =>
            {

                //TODO: Implement some kind of switch that will enable user to use either CHIP-48 and SUPER-CHIP version or original 
                //       COSMAC VIP version. Currently CHIP-48 and SUPER-CHIP version is implemented.
                //       More here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#8xy6-and-8xye-shift
                ushort jumpOffset = registers[0];
                ushort jumpBase = Instruction.NNN(args);

                ProgramCounter = (ushort)(jumpBase + jumpOffset);

                return false;
            },
            Emit = args =>
            {
                return $"JP V0, 0x{Instruction.NNN(args)}";
            }},

            new() { Opcode=0xC000,Mask=0xF000,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                byte nnArgs = Instruction.NN(args);
                Random r = new(DateTime.Now.Second);
                byte[] random = new byte[1];
                r.NextBytes(random);
                registers[registerXIndex] = (byte)(random[0] & nnArgs);
                return true;
            },
            Emit = args =>
            {
                return $"RND V{Instruction.X(args)}, 0x{Instruction.NN(args)}";
            }},

            new() { Opcode=0xD000,Mask=0xF000,
            Execute = args =>
            {
                registers[0xF] = 0;
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                int spriteLength = Instruction.N(args);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                byte xPosition = registers[registerXIndex];
                byte yPosition = registers[registerYIndex];
                byte[] sprite = memory.Skip(IRegister)
                                      .Take(spriteLength)
                                      .ToArray();
                int rowBeginning = (xPosition % 64);
                int y = (yPosition % 32);
                int x = rowBeginning;

                for(int i = 0; i < sprite.Length;i++)
                {
                        BitArray spriteRow = new(new byte[] {sprite[i]});
                        if(y < surface.GetLength(1))
                        {
                            for(int rowBitIndex = spriteRow.Length - 1; rowBitIndex >=0 ; rowBitIndex--)
                            {
                                var bit = spriteRow[rowBitIndex];
                                if (x < surface.GetLength(0))
                                {
                                        if(surface[x,y] && bit)
                                        {
                                            surface[x,y] = false;
                                            registers[0xF] = 1;
                                        }
                                        else if(!surface[x,y] && bit)
                                        {
                                            surface[x,y] = true;
                                        }
                                     x += 1;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            x=rowBeginning;
                            y += 1;
                        }
                        else
                        {
                            break;
                        }

                }
               return true;
            },
            Emit= args =>
            {
                return $"DRW V{Instruction.X(args)}, V{Instruction.Y(args)}, 0x{Instruction.N(args)}";
            }},

            new() { Opcode=0xE09E,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                byte keyIndex = registers[registerXIndex];
                if(keyIndex > 0xF)
                {
                    throw new ArgumentException($"Max argument value exceeded: {keyIndex}");
                }

                if((pressedKeys & (ushort)(1 << keyIndex)) > 0)
                {
                    ProgramCounter += 4;
                }
                else
                {
                    ProgramCounter += 2;
                }
                return false;
            },
            Emit= args =>
            {
                return $"SKP V{Instruction.X(args)}";
            }},

            new() { Opcode=0xE0A1,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                byte keyIndex = registers[registerXIndex];
                if(keyIndex > 0xF)
                {
                    throw new ArgumentException($"Max argument value exceeded: {keyIndex}");
                }

                if((pressedKeys & (ushort)(1 << keyIndex)) == 0)
                {
                    ProgramCounter += 4;
                }
                else
                {
                    ProgramCounter += 2;
                }
                return false;

            },
            Emit = args =>
            {
                return $"SKNP V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF007,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                registers[registerXIndex] = delayTimer;
                return true;
            },
            Emit = args =>
            {
                return $"LD V{Instruction.X(args)}, DT";
            }},

            new() { Opcode=0xF00A,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);

                if(lastPressedKey.HasValue)
                {
                    registers[registerXIndex] = (byte)lastPressedKey.Value;
                    ProgramCounter+=2;
                    lastPressedKey = null;
                }

                return false;
            },
            Emit = args =>
            {
                return $"LD V{Instruction.X(args)}, K";
            }},

            new() { Opcode=0xF015,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                delayTimer = registers[registerXIndex] ;
                return true;
            },
            Emit = args =>
            {
                return $"LD DT, V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF018,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                soundTimer = registers[registerXIndex] ;
                return true;
            },
            Emit =args =>
            {
                return $"LD ST V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF01E,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                byte registerValue = registers[registerXIndex];
                IRegister += registerValue;

                //TODO: not original behavior of the instruction (at least not in relation to COSMAC VIP)
                //      might be a good idea to put some kind of switch to decide what should happen.
                //      more here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#fx1e-add-to-index
                if((IRegister & 0xF000) > 0)
                {
                    registers[0xF] = 1;
                }
                return true;
            },
            Emit = args=>
            {
                return $"ADD I, V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF029,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                byte registerValue = registers[registerXIndex];
                IRegister = (ushort)(registerValue * 5);
                return true;
            },
            Emit = args =>
            {
                return $"LD F, V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF033,Mask=0xF0FF,
            Execute = args =>
            {
                int registerXIndex = Instruction.X(args);
                CheckRegisterIndex(registerXIndex);
                byte registerValue = registers[registerXIndex];

                for(int i =2;i>=0;i--)
                {
                    CheckMemoryAdders(IRegister + i);
                    memory[IRegister + i]= (byte)(registerValue % 10);
                    registerValue = (byte)(registerValue / 10);
                }

                return true;
            },
            Emit = args =>
            {
                return $"BCD V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF055,Mask=0xF0FF,
            Execute = args =>
            {
                int lastRegisterIndex = Instruction.X(args);
                CheckRegisterIndex(lastRegisterIndex);

                for(int i=0;i<=lastRegisterIndex;i++)
                {
                    CheckMemoryAdders(IRegister + i);
                    memory[IRegister + i] = registers[i];
                }
                IRegister += (ushort)(lastRegisterIndex +1);
                return true;
            },
            Emit = args =>
            {
                return $"LD [I], V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF065,Mask=0xF0FF, Execute = args =>
            {
                int lastRegisterIndex = Instruction.X(args);
                CheckRegisterIndex(lastRegisterIndex);
                for(int i=0;i<=lastRegisterIndex;i++)
                {
                    CheckMemoryAdders(IRegister + i);
                    registers[i] = memory[IRegister + i];
                }
                IRegister += (ushort)(lastRegisterIndex +1);
                return true;
            },
            Emit = args =>
            {
                return $"LD V{Instruction.X(args)}, [I]";
            }},
        ];

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
                        delayTimer = restoredState.DelayTimer;
                        instructionsPerFrame = restoredState.InstructionsPerFrame;
                        instructionsToExecuteInFrame = restoredState.InstructionsToExecuteInFrame;
                        IRegister = restoredState.IRegister;
                        memory = (byte[])restoredState.Memory.Clone();
                        programBytesCount = restoredState.ProgramBytesCount;
                        ProgramCounter = restoredState.ProgramCounter;
                        registers = (byte[])restoredState.Registers.Clone();
                        stack = new Stack<ushort>(restoredState.Stack.Reverse());
                        soundTimer = restoredState.SoundTimer;
                        surface = (bool[,])restoredState.Surface.Clone();
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
                DelayTimer = delayTimer,
                InstructionsPerFrame = instructionsPerFrame,
                InstructionsToExecuteInFrame = instructionsToExecuteInFrame,
                IRegister = IRegister,
                Memory = (byte[])memory.Clone(),
                LoadedProgram = (byte[])loadedProgram.Clone(),
                ProgramBytesCount = programBytesCount,
                ProgramCounter = ProgramCounter,
                Registers = (byte[])registers.Clone(),
                Stack = new Stack<ushort>(stack.Reverse()),
                SoundTimer = soundTimer,
                Surface = (bool[,])surface.Clone(),
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

        public bool Load(string programSourceFilePath)
        {
            try
            {
                using (FileStream programSourceStreamReader = new(programSourceFilePath, FileMode.Open))
                {
                    programBytesCount = programSourceStreamReader.Read(memory, 512, memory.Length - 512);
                    loadedProgram = (byte[])memory.Clone();
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

            ProgramCounter = 0x200;
            IRegister = 0;
            soundTimer = 0;
            delayTimer = 0;
            stack.Clear();
            Array.Clear(surface);
            Array.Clear(memory);
            Array.Clear(registers);
            Array.Copy(font, memory, font.Length);
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
                        if (soundTimer < 1)
                        {
                            soundTimer = 0;
                            OnBeepStatus(false);
                        }
                        else
                        {
                            soundTimer = (byte)(soundTimer - 1);
                            OnBeepStatus(true);
                        }

                        if (delayTimer > 0)
                        {
                            delayTimer = (byte)(delayTimer - 1);
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
                lastPressedKey = null;
                pressedKeys |= keyBitValue;
            }
            else
            {
                lastPressedKey = key;
                pressedKeys &= (ushort)~keyBitValue;
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
            if (memory.Length < instructionAddress + 1)
            {
                throw new ArgumentException("Instruction address out of bounds", $"{nameof(instructionAddress)}");
            }

            return (ushort)(memory[instructionAddress] << 8 | memory[instructionAddress + 1]);
        }

        private bool ExecuteInstruction()
        {
            bool executed = false;
            try
            {
                if (ProgramCounter < memory.Length)
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
                        byte[] instructionRaw = [memory[ProgramCounter], memory[ProgramCounter + 1]];

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
                                    ProgramCounter += 2;
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

        private void OnTick(object sender, HighResolutionTimerElapsedEventArgs args)
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
                    if (soundTimer < 1)
                    {
                        soundTimer = 0;
                        OnBeepStatus(false);
                    }
                    else
                    {
                        soundTimer = (byte)(soundTimer - 1);
                        OnBeepStatus(true);
                    }

                    if (delayTimer > 0)
                    {
                        delayTimer = (byte)(delayTimer - 1);
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

        private void CheckMemoryAdders(int memoryAddress)
        {
            if (memory.Length < memoryAddress)
            {
                throw new ArgumentOutOfRangeException($"Address {memoryAddress} out of range.");
            }
        }

        private void CheckRegisterIndex(int registerIndex)
        {
            if (registerIndex > registers.Length)
            {
                throw new ArgumentOutOfRangeException($"Register V{registerIndex} does not exists.");
            }
        }
    }
}
