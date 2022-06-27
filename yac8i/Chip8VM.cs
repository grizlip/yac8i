using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading;

namespace yac8i;

public class Chip8VM
{

    public EventHandler<ScreenRefreshEventArgs> ScreenRefresh;
    public EventHandler<string> NewMessage;
    public EventHandler<bool> BeepStatus;


    private readonly byte[] font = new byte[] {0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
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
                                    0xF0, 0x80, 0xF0, 0x80, 0x80 }; // F

    private byte[] memory = new byte[4096];
    private byte[] registers = new byte[16];
    private ushort iRegister = 0;
    private ushort programCounter = 0x200;
    private Stack<ushort> stack = new Stack<ushort>();
    private byte soundTimer = 0;
    private bool beepStatus = false;
    private byte delayTimer = 0;
    private List<Instruction> instructions;
    private ushort pressedKeys = 0;
    private ushort? lastPressedKey = null;
    private bool loaded = false;
    private bool[,] surface = new bool[64, 32];


    public Chip8VM()
    {
        instructions = new List<Instruction>()
        {
            // TODO: This instruction collides with 0x00EF and 0x00E0
            //       It happens because all three instructions beings with 0
            //       and in case of 0x0000 we are checking only top most four bits
            //       if they are 0, then we assume we have a match
            //       Find a way to implement this better. 
            //new Instruction() { Opcode=0x0000,Mask=0xF000},
            new Instruction() { Opcode=0x00E0,Mask=0xFFFF, Execute = args =>
            {
                ClearSurface();
                ScreenRefresh?.Invoke(this,new ScreenRefreshEventArgs(RefreshRequest.Clear));
                return true;
            }},
            new Instruction() { Opcode=0x00EE,Mask=0xFFFF, Execute = args =>
            {
                ushort returnAddress;
                if(this.stack.TryPop(out returnAddress))
                {
                    this.programCounter = returnAddress;
                }
                else
                {
                    throw new ArgumentException("Error. Stack empty while trying to return from subroutine");
                }
                return false;
            }},
            new Instruction() { Opcode=0x1000,Mask=0xF000, Execute = args =>
            {
                    this.programCounter = args;
                    return false;
            }},
            new Instruction() { Opcode=0x2000,Mask=0xF000, Execute = args =>
            {
                this.stack.Push((ushort)(programCounter+2));
                this.programCounter = args;

                return false;
            }},
            new Instruction() { Opcode=0x3000,Mask=0xF000, Execute = args =>
            {
                int registerIndex = (args & 0x0F00) >> 8;
                CheckRegisterIndex(registerIndex);
                byte registerValue = registers[registerIndex];
                byte compareToValue = (byte)(args & 0x00FF);
                bool valuesEqual = registerValue == compareToValue;
                if(valuesEqual)
                {
                    programCounter+= 4;
                }
                return !valuesEqual;
            }},
            new Instruction() { Opcode=0x4000,Mask=0xF000, Execute = args =>
            {
                int registerIndex = (args & 0x0F00) >> 8;
                CheckRegisterIndex(registerIndex);
                byte registerValue = registers[registerIndex];
                byte compareToValue = (byte)(args & 0x00FF);
                bool valuesEqual = registerValue == compareToValue;
                if(!valuesEqual)
                {
                    programCounter+= 4;
                }
                return valuesEqual;
            }},
            new Instruction() { Opcode=0x5000,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                bool valuesEqual = registers[registerXIndex] == registers[registerYIndex];
                if(valuesEqual)
                {
                    programCounter +=4;
                }
                return !valuesEqual;
            }},
            new Instruction() { Opcode=0x6000,Mask=0xF000, Execute = args =>
            {
                int registerIndex  = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerIndex);
                byte newRegisterValue = (byte)(args & 0x00FF);
                registers[registerIndex] = newRegisterValue;
                return true;
            }},
            new Instruction() { Opcode=0x7000,Mask=0xF000, Execute = args =>
            {
                int registerIndex  = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerIndex);
                byte valueToAdd = (byte)(args & 0x00FF);
                registers[registerIndex] += valueToAdd; //this will wrap if overflow happens. Not sure if that is correct behavior. 
                return true;

            }},
            new Instruction() { Opcode=0x8000,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = registers[registerYIndex];
                return true;
            }},
            new Instruction() { Opcode=0x8001,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = (byte)(registers[registerXIndex] | registers[registerYIndex]);
                return true;
            }},
            new Instruction() { Opcode=0x8002,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = (byte)(registers[registerXIndex] & registers[registerYIndex]);
                return true;

            }},
            new Instruction() { Opcode=0x8003,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                registers[registerXIndex] = (byte)(registers[registerXIndex] ^ registers[registerYIndex]);
                return true;

            }},
            new Instruction() { Opcode=0x8004,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                //if overflow happens, set flag
                //TODO: find better way to check and do this?
                if(registers[registerXIndex] + registers[registerYIndex] >255)
                {
                    registers[0xF] = 1;
                }
                registers[registerXIndex] = (byte)(registers[registerXIndex] + registers[registerYIndex]);

                return true;
            }},
            new Instruction() { Opcode=0x8005,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                //set F register
                registers[0xF] = (byte)(registers[registerXIndex]> registers[registerYIndex] ? 1 : 0);
                //perform substraction (no need to worry about underflow here)
                registers[registerXIndex] = (byte)(registers[registerXIndex] - registers[registerYIndex]);
                return true;
            }},
            new Instruction() { Opcode=0x8006,Mask=0xF00F, Execute = args =>
            {
                //TODO: Implement some kind of switch that will enable user to use either CHIP-48 and SUPER-CHIP version or original 
                //       COSMAC VIP version. Currently CHIP-48 and SUPER-CHIP version is implemented.
                //       More here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#8xy6-and-8xye-shift
                int registerXIndex = (args & 0x0F00)>>8;
                //int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                //CheckRegisterIndex(registerYIndex);
                //set VF register
                registers[0xF] =(byte)( registers[registerXIndex] & 0x1);
                //right shift Vx by one
                registers[registerXIndex] = (byte)(registers[registerXIndex] >>1);

                return true;
            }},
            new Instruction() { Opcode=0x8007,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                //set F register
                registers[0xF] = (byte)(registers[registerXIndex]< registers[registerYIndex] ? 1 : 0);
                //perform substraction (no need to worry about underflow here)
                registers[registerXIndex] = (byte)(registers[registerYIndex] - registers[registerXIndex]);
                return true;

            }},
            new Instruction() { Opcode=0x800E,Mask=0xF00F, Execute = args =>
            {
                //TODO: Implement some kind of switch that will enable user to use either CHIP-48 and SUPER-CHIP version or original 
                //       COSMAC VIP version. Currently CHIP-48 and SUPER-CHIP version is implemented.
                //       More here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#8xy6-and-8xye-shift
                int registerXIndex = (args & 0x0F00)>>8;
                //int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                //CheckRegisterIndex(registerYIndex);
                //set VF register
                registers[0xF] =(byte)( registers[registerXIndex] & 0x8000);
                //right shift Vx by one
                registers[registerXIndex] = (byte)(registers[registerXIndex] <<1);

                return true;

            }},
            new Instruction() { Opcode=0x9000,Mask=0xF00F, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                bool valuesEqual = registers[registerXIndex] == registers[registerYIndex];
                if(!valuesEqual)
                {
                    programCounter +=4;
                }
                return valuesEqual;

            }},
            new Instruction() { Opcode=0xA000,Mask=0xF000, Execute = args =>
            {
                iRegister = (ushort)(args & 0x0FFF);
                return true;
            }},
            new Instruction() { Opcode=0xB000,Mask=0xF000, Execute = args =>
            {

                //TODO: Implement some kind of switch that will enable user to use either CHIP-48 and SUPER-CHIP version or original 
                //       COSMAC VIP version. Currently CHIP-48 and SUPER-CHIP version is implemented.
                //       More here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#8xy6-and-8xye-shift
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                ushort jumpOffset = registers[registerXIndex];
                ushort jumpBase = (ushort)(args & 0x0FFF);

                programCounter += (ushort)(jumpBase + jumpOffset);

                return false;
            }},
            new Instruction() { Opcode=0xC000,Mask=0xF000, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                byte nnArgs = (byte)(args & 0x00FF);
                Random r = new Random(DateTime.Now.Second);
                byte[] random = new byte[1];
                r.NextBytes(random);
                registers[registerXIndex] = (byte)(random[0] & nnArgs);
                return true;
            }},
            new Instruction() { Opcode=0xD000,Mask=0xF000, Execute = args =>
            {
                registers[0xF] = 0;
                int registerXIndex = (args & 0x0F00)>>8;
                int registerYIndex = (args & 0x00F0)>>4;
                int spriteLength = (args & 0x000F);
                CheckRegisterIndex(registerXIndex);
                CheckRegisterIndex(registerYIndex);
                byte xPosition = registers[registerXIndex];
                byte yPosition = registers[registerYIndex];
                byte[] sprite = memory.Skip(iRegister)
                                      .Take(spriteLength)
                                      .ToArray();
                int rowBeginning = (xPosition % 64);
                int y = (yPosition % 32);

                int x = rowBeginning;

                for(int i = 0; i < sprite.Length;i++)
                {
                        BitArray spriteRow = new BitArray(new byte[] {sprite[i]});
                        if(y < surface.GetLength(1))
                        {
                            for(int k = spriteRow.Length - 1; k >=0 ; k--)
                            {
                                var bit = spriteRow[k];
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


                this.ScreenRefresh?.Invoke(this,new ScreenRefreshEventArgs(RefreshRequest.Draw, surface));
                return true;
            }},
            new Instruction() { Opcode=0xE09E,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                byte keyIndex = registers[registerXIndex];
                if(keyIndex > 0xF)
                {
                    throw new ArgumentException($"Max argument value exceeded: {keyIndex}");
                }

                if((pressedKeys & (ushort)(1 << keyIndex)) > 0)
                {
                    programCounter += 4;
                }
                else
                {
                    programCounter += 2;
                }
                return false;
            }},
            new Instruction() { Opcode=0xE0A1,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                byte keyIndex = registers[registerXIndex];
                if(keyIndex > 0xF)
                {
                    throw new ArgumentException($"Max argument value exceeded: {keyIndex}");
                }

                if((pressedKeys & (ushort)(1 << keyIndex)) == 0)
                {
                    programCounter += 4;
                }
                else
                {
                    programCounter += 2;
                }
                return false;

            }
            },
            new Instruction() { Opcode=0xF007,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                registers[registerXIndex] = delayTimer;
                return true;
            }},
            new Instruction() { Opcode=0xF00A,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);

                if(lastPressedKey.HasValue)
                {
                    registers[registerXIndex] = (byte)lastPressedKey.Value;
                    programCounter+=2;
                }

                return false;
            }},
            new Instruction() { Opcode=0xF015,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                delayTimer = registers[registerXIndex] ;
                return true;
            }},
            new Instruction() { Opcode=0xF018,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                soundTimer = registers[registerXIndex] ;
                return true;
            }},
            new Instruction() { Opcode=0xF01E,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                byte registerValue = registers[registerXIndex];
                iRegister += registerValue;

                //TODO: not original behavior of the instruction (at least not in relation to COSMAC VIP)
                //      might be a good idea to put some kind of switch to decide what should happen.
                //      more here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#fx1e-add-to-index
                if((iRegister & 0xF000) > 0)
                {
                    registers[0xF] = 1;
                }
                return true;
            }},
            new Instruction() { Opcode=0xF029,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                byte registerValue = registers[registerXIndex];
                iRegister = (ushort)(registerValue * 5);
                return true;
            }},
            new Instruction() { Opcode=0xF033,Mask=0xF0FF, Execute = args =>
            {
                int registerXIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(registerXIndex);
                byte registerValue = registers[registerXIndex];

                for(int i =2;i>=0;i--)
                {
                    CheckMemoryAdders(iRegister + i);
                    memory[iRegister + i]= (byte)(registerValue % 10);
                    registerValue = (byte)(registerValue / 10);
                }

                return true;
            }},
            new Instruction() { Opcode=0xF055,Mask=0xF0FF, Execute = args =>
            {
                int lastRegisterIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(lastRegisterIndex);

                for(int i=0;i<=lastRegisterIndex;i++)
                {
                    CheckMemoryAdders(iRegister + i);
                    memory[iRegister + i] = registers[i];
                }
                return true;
            }},
            new Instruction() { Opcode=0xF065,Mask=0xF0FF, Execute = args =>
            {
                int lastRegisterIndex = (args & 0x0F00)>>8;
                CheckRegisterIndex(lastRegisterIndex);
                for(int i=0;i<=lastRegisterIndex;i++)
                {
                    CheckMemoryAdders(iRegister + i);
                    registers[i] = memory[iRegister + i];
                }
                return true;
            }},
        };
        Reset();
    }

    public bool Load(string programSourceFilePath)
    {
        try
        {
            using (FileStream programSourceStreamReader = new FileStream(programSourceFilePath, FileMode.Open))
            {
                programSourceStreamReader.Read(memory, 512, memory.Length - 512);
            }
            loaded = true;
        }
        catch (Exception ex)
        {
            OnNewMessage($"File read error: {ex.Message}");
        }
        return loaded;
    }

    public void Start(CancellationToken? cancelToken = null)
    {
        if (cancelToken.HasValue && cancelToken.Value.IsCancellationRequested)
        {
            return;
        }

        if (loaded)
        {
            HighResolutionTimer timersHandler = null;
            try
            {
                timersHandler = new HighResolutionTimer(1000f / 60f); //60 times per second
                timersHandler.Elapsed += HandleTimers;
                timersHandler.Start();
                while (programCounter < memory.Length)
                {
                    byte[] instructionRaw = new byte[] { memory[programCounter], memory[programCounter + 1] };

                    ushort instructionValue = (ushort)(instructionRaw[0] << 8 | instructionRaw[1]);

                    var instruction = instructions.SingleOrDefault(item => (instructionValue & item.Mask) == item.Opcode);
                    bool increaseProgramCounter = false;
                    if (instruction != null)
                    {
                        ushort argsMask = (ushort)(instruction.Mask ^ 0xFFFF);
                        ushort args = (ushort)(instructionValue & argsMask);

                        if (instruction.Execute != null)
                        {
                            increaseProgramCounter = instruction.Execute(args);
                            //this takes around 15 - 16 ms. Check if we can do something else to control how fast
                            //execution goes
                            System.Threading.Thread.Sleep(1);
                        }

                        //OnNewMessage(string.Format("Instruction: 0x{0:X4}, Args: 0x{1:X4}, Program counter: 0x{2:X4}", instructionValue, args, programCounter));

                        if (increaseProgramCounter)
                        {
                            programCounter += 2;
                        }
                    }
                    else
                    {
                        break;
                    }
                    if (cancelToken.HasValue && cancelToken.Value.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                //TODO: Add some more information about place in which program failed.
                OnNewMessage(ex.Message);
            }
            finally
            {
                if (timersHandler != null)
                {
                    if (timersHandler.IsRunning)
                    {
                        timersHandler.Stop();
                    }
                    timersHandler.Elapsed -= HandleTimers;

                }
            }
        }
    }

    public void Reset()
    {
        programCounter = 0x200;
        iRegister = 0;
        soundTimer = 0;
        delayTimer = 0;
        stack.Clear();
        Array.Clear(surface, 0, surface.Length);
        Array.Clear(memory, 0, memory.Length);
        Array.Clear(registers, 0, registers.Length);
        Array.Copy(font, memory, font.Length);
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

    private void HandleTimers(object sender, HighResolutionTimerElapsedEventArgs args)
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

    private void OnNewMessage(string msg)
    {
        NewMessage?.Invoke(this, msg);
    }

    private void OnBeepStatus(bool status)
    {
        if (status != beepStatus)
        {
            beepStatus = status;
            BeepStatus?.Invoke(this, status);
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

    private void ClearSurface()
    {
        unsafe
        {
            fixed (bool* surfacePointer = &surface[0, 0])
            {
                var surfaceSpan = new Span<bool>(surfacePointer, 64 * 32);
                surfaceSpan.Fill(false);
            }
        }
    }
}
