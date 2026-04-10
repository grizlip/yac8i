using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace yac8i
{
    internal class InstructionsFactory
    {
        private static readonly Random random = new(DateTime.Now.Second);
        public static List<Instruction> GetInstructions(VmState state)
        {
            List<Instruction> result =
            [
            new() { Opcode=0x00E0,Mask=0xFFFF,
            Execute = args =>
                {
                    Array.Clear(state.Surface);
                    return true;
                },
                Emit = args =>
                {
                    return "CLS";
                }
            },

            new() { Opcode=0x00EE,Mask=0xFFFF,
            Execute = args =>
            {
                if(state.Stack.TryPop(out ushort returnAddress))
                {
                    state.ProgramCounter = returnAddress;
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
                    state.ProgramCounter = Instruction.NNN(args);
                    return false;
            },
            Emit = args=>
            {
                return $"JP 0x{Instruction.NNN(args):X4}";
            }},

            new() { Opcode=0x2000,Mask=0xF000,
            Execute = args =>
            {
                state.Stack.Push((ushort)(state.ProgramCounter+2));
                state.ProgramCounter = Instruction.NNN(args);

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
                CheckRegisterIndex(registerIndex, state.Registers);
                byte registerValue = state.Registers[registerIndex];
                byte compareToValue = Instruction.NN(args);
                bool valuesEqual = registerValue == compareToValue;
                if(valuesEqual)
                {
                    state.ProgramCounter+= 4;
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
                CheckRegisterIndex(registerIndex, state.Registers);
                byte registerValue = state.Registers[registerIndex];
                byte compareToValue = Instruction.NN(args);
                bool valuesEqual = registerValue == compareToValue;
                if(!valuesEqual)
                {
                    state.ProgramCounter+= 4;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                bool valuesEqual = state.Registers[registerXIndex] == state.Registers[registerYIndex];
                if(valuesEqual)
                {
                    state.ProgramCounter +=4;
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
                CheckRegisterIndex(registerIndex, state.Registers);
                byte newRegisterValue =  Instruction.NN(args);
                state.Registers[registerIndex] = newRegisterValue;
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
                CheckRegisterIndex(registerIndex, state.Registers);
                byte valueToAdd =  Instruction.NN(args);
                state.Registers[registerIndex] += valueToAdd; //this will wrap if overflow happens. Not sure if that is correct behavior. 
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                state.Registers[registerXIndex] = state.Registers[registerYIndex];
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                state.Registers[registerXIndex] = (byte)(state.Registers[registerXIndex] | state.Registers[registerYIndex]);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                state.Registers[registerXIndex] = (byte)(state.Registers[registerXIndex] & state.Registers[registerYIndex]);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                state.Registers[registerXIndex] = (byte)(state.Registers[registerXIndex] ^ state.Registers[registerYIndex]);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                byte xValue = state.Registers[registerXIndex];
                byte yValue = state.Registers[registerYIndex];
                var result = xValue + yValue;
                state.Registers[registerXIndex] = (byte)result;
                
                //TODO: find better way to check and do this?
                if(result > 255)
                {
                    state.Registers[0xF] = 1;
                }
                else
                {
                    state.Registers[0xF] = 0;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                byte xValue = state.Registers[registerXIndex];
                byte yValue = state.Registers[registerYIndex];
                //perform subtraction (no need to worry about underflow here)
                state.Registers[registerXIndex] = (byte)(xValue - yValue);
                state.Registers[0xF] = (byte)(xValue>yValue ? 1 : 0);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                state.Registers[registerXIndex] = state.Registers[registerYIndex];
                byte xValue = state.Registers[registerXIndex];
                
                //right shift Vx by one
                state.Registers[registerXIndex] = (byte)(xValue >>1);
                //set VF register
                state.Registers[0xF] =(byte)(xValue & 0x1);

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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);

                byte xValue = state.Registers[registerXIndex];
                byte yValue = state.Registers[registerYIndex];
                //perform substraction (no need to worry about underflow here)
                state.Registers[registerXIndex] = (byte)(yValue - xValue);
                state.Registers[0xF] = (byte)(xValue<yValue ? 1 : 0);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                state.Registers[registerXIndex] = state.Registers[registerYIndex];
                byte xValue = state.Registers[registerXIndex];
                
                //left shift Vx by one
                state.Registers[registerXIndex] = (byte)(xValue <<1);
                state.Registers[0xF] =(byte)((xValue & 0x80) >>7);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                bool valuesEqual = state.Registers[registerXIndex] == state.Registers[registerYIndex];
                if(!valuesEqual)
                {
                    state.ProgramCounter +=4;
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
                state.IRegister = Instruction.NNN(args);
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
                ushort jumpOffset = state.Registers[0];
                ushort jumpBase = Instruction.NNN(args);

                state.ProgramCounter = (ushort)(jumpBase + jumpOffset);

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
                CheckRegisterIndex(registerXIndex, state.Registers);
                byte nnArgs = Instruction.NN(args);
                byte[] randomByteArray = new byte[1];
                random.NextBytes(randomByteArray);
                state.Registers[registerXIndex] = (byte)(randomByteArray[0] & nnArgs);
                return true;
            },
            Emit = args =>
            {
                return $"RND V{Instruction.X(args)}, 0x{Instruction.NN(args)}";
            }},

            new() { Opcode=0xD000,Mask=0xF000,
            Execute = args =>
            {
                state.Registers[0xF] = 0;
                byte registerXIndex = Instruction.X(args);
                byte registerYIndex = Instruction.Y(args);
                int spriteLength = Instruction.N(args);
                CheckRegisterIndex(registerXIndex, state.Registers);
                CheckRegisterIndex(registerYIndex, state.Registers);
                byte xPosition = state.Registers[registerXIndex];
                byte yPosition = state.Registers[registerYIndex];
                byte[] sprite = [.. state.Memory.Skip(state.IRegister).Take(spriteLength)];

                int rowBeginning = xPosition % 64;
                int y = yPosition % 32;
                int x = rowBeginning;

                for(int i = 0; i < sprite.Length;i++)
                {
                        BitArray spriteRow = new(new byte[] {sprite[i]});
                        if(y < state.Surface.GetLength(1))
                        {
                            for(int rowBitIndex = spriteRow.Length - 1; rowBitIndex >=0 ; rowBitIndex--)
                            {
                                var bit = spriteRow[rowBitIndex];
                                if (x <state. Surface.GetLength(0))
                                {
                                        if(state.Surface[x,y] && bit)
                                        {
                                            state.Surface[x,y] = false;
                                            state.Registers[0xF] = 1;
                                        }
                                        else if(!state.Surface[x,y] && bit)
                                        {
                                            state.Surface[x,y] = true;
                                        }
                                     x += 1;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            x = rowBeginning;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                byte keyIndex = state.Registers[registerXIndex];
                if(keyIndex > 0xF)
                {
                    throw new ArgumentException($"Max argument value exceeded: {keyIndex}");
                }

                if((state.PressedKeys & (ushort)(1 << keyIndex)) > 0)
                {
                    state.ProgramCounter += 4;
                }
                else
                {
                    state.ProgramCounter += 2;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                byte keyIndex = state.Registers[registerXIndex];
                if(keyIndex > 0xF)
                {
                    throw new ArgumentException($"Max argument value exceeded: {keyIndex}");
                }

                if((state.PressedKeys & (ushort)(1 << keyIndex)) == 0)
                {
                    state.ProgramCounter += 4;
                }
                else
                {
                    state.ProgramCounter += 2;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                state.Registers[registerXIndex] = state.DelayTimer;
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
                CheckRegisterIndex(registerXIndex, state.Registers);

                if(state.LastPressedKey.HasValue)
                {
                    state.Registers[registerXIndex] = (byte)state.LastPressedKey.Value;
                    state.ProgramCounter+=2;
                    state.LastPressedKey = null;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                state.DelayTimer = state.Registers[registerXIndex] ;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                state.SoundTimer = state.Registers[registerXIndex] ;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                byte registerValue = state.Registers[registerXIndex];
                state.IRegister += registerValue;

                //TODO: not original behavior of the instruction (at least not in relation to COSMAC VIP)
                //      might be a good idea to put some kind of switch to decide what should happen.
                //      more here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#fx1e-add-to-index
                if((state.IRegister & 0xF000) > 0)
                {
                    state.Registers[0xF] = 1;
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                byte registerValue = state.Registers[registerXIndex];
                state.IRegister = (ushort)(registerValue * 5);
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
                CheckRegisterIndex(registerXIndex, state.Registers);
                byte registerValue = state.Registers[registerXIndex];

                for(int i =2;i>=0;i--)
                {
                    CheckMemoryAdders(state.IRegister + i, state.Memory);
                    state.Memory[state.IRegister + i]= (byte)(registerValue % 10);
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
                CheckRegisterIndex(lastRegisterIndex, state.Registers);

                for(int i=0;i<=lastRegisterIndex;i++)
                {
                    CheckMemoryAdders(state.IRegister + i, state.Memory);
                    state.Memory[state.IRegister + i] = state.Registers[i];
                }
                state.IRegister += (ushort)(lastRegisterIndex +1);
                return true;
            },
            Emit = args =>
            {
                return $"LD [I], V{Instruction.X(args)}";
            }},

            new() { Opcode=0xF065,Mask=0xF0FF, Execute = args =>
            {
                int lastRegisterIndex = Instruction.X(args);
                CheckRegisterIndex(lastRegisterIndex, state.Registers);
                for(int i=0;i<=lastRegisterIndex;i++)
                {
                    CheckMemoryAdders(state.IRegister + i, state.Memory);
                    state.Registers[i] = state.Memory[state.IRegister + i];
                }
                state.IRegister += (ushort)(lastRegisterIndex +1);
                return true;
            },
            Emit = args =>
            {
                return $"LD V{Instruction.X(args)}, [I]";
            }},
        ];

            return result;
        }

        private static void CheckRegisterIndex(int registerIndex, byte[] registers)
        {
            if (registerIndex > registers.Length)
            {
                throw new ArgumentOutOfRangeException($"Register V{registerIndex} does not exists.");
            }
        }

        private static void CheckMemoryAdders(int memoryAddress, byte[] memory)
        {
            if (memory.Length < memoryAddress)
            {
                throw new ArgumentOutOfRangeException($"Address {memoryAddress} out of range.");
            }
        }
    }
}