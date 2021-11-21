using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace yac8i
{
    public class Chip8VM : IDisposable
    {
        private byte[] memory = new byte[4096];
        private byte[] registers = new byte[16];
        private ushort iRegister = 0;
        private ushort programCounter = 0x200;
        private Stack<ushort> stack = new Stack<ushort>();
        //TODO: registers for sound and timer
        public EventHandler<ScreenRefreshEventArgs> ScreenRefresh;
        private List<Instruction> instructions;
        private bool loaded = false;
        private FileStream programSourceStreamReader;

        public event EventHandler<string> NewMessage;
        public Chip8VM()
        {
            instructions = new List<Instruction>()
        {
            // TODO: This instruction colides with 0x00EF and 0x00E0
            //       It happens because all three instructions beings with 0
            //       and in case of 0x0000 we are checking only top most four bits
            //       if they are 0, then we assume we have a match
            //       Find a way to implement this better. 
            //new Instruction() { Opcode=0x0000,Mask=0xF000},
            new Instruction() { Opcode=0x00E0,Mask=0xFFFF, Execute = args =>
            {
                this.ScreenRefresh?.Invoke(this,new ScreenRefreshEventArgs(RefreshRequest.Clear));
                return true;
            }},
            new Instruction() { Opcode=0x00EE,Mask=0xFFFF,Execute = args =>
            {
                ushort returnAddress;
                if(this.stack.TryPop(out returnAddress))
                {
                    OnNewMessage(string.Format("Pop 0x{0:X4}",returnAddress));
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
                OnNewMessage(string.Format("Push 0x{0:X4}",programCounter+2));
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
                OnNewMessage($"Comparing register V{registerIndex} with value {registerValue} to {compareToValue}. Result is {valuesEqual}. Args are {string.Format("0x{0:X4}",args)}");
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
                OnNewMessage($"Comparing register V{registerIndex} with value {registerValue} to {compareToValue}. Result is {valuesEqual}. Args are {string.Format("0x{0:X4}",args)}");
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
            new Instruction() { Opcode=0x7000,Mask=0xF000,Execute = args =>
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
            new Instruction() { Opcode=0x8007,Mask=0xF00F, Execute  =args =>
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
            new Instruction() { Opcode=0xB000,Mask=0xF000},
            new Instruction() { Opcode=0xC000,Mask=0xF000},
            new Instruction() { Opcode=0xD000,Mask=0xF000, Execute = args =>
            {
                //TODO: Implement actual drawing.
                this.ScreenRefresh?.Invoke(this,new ScreenRefreshEventArgs(RefreshRequest.Draw));
                return true;
            }},
            new Instruction() { Opcode=0xE09E,Mask=0xF0FF},
            new Instruction() { Opcode=0xE0A1,Mask=0xF0FF},
            new Instruction() { Opcode=0xF007,Mask=0xF0FF},
            new Instruction() { Opcode=0xF00A,Mask=0xF0FF},
            new Instruction() { Opcode=0xF015,Mask=0xF0FF},
            new Instruction() { Opcode=0xF018,Mask=0xF0FF},
            new Instruction() { Opcode=0xF01E,Mask=0xF0FF},
            new Instruction() { Opcode=0xF029,Mask=0xF0FF},
            new Instruction() { Opcode=0xF033,Mask=0xF0FF},
            new Instruction() { Opcode=0xF055,Mask=0xF0FF},
            new Instruction() { Opcode=0xF065,Mask=0xF0FF},
        };

            Reset();
        }
        public bool Load(string programSourceFilePath)
        {
            try
            {
                programSourceStreamReader = new FileStream(programSourceFilePath, FileMode.Open);
                programSourceStreamReader.Read(memory, 512, memory.Length - 512);
                loaded = true;
            }
            catch (Exception ex)
            {
                OnNewMessage($"File read error: {ex.Message}");
            }
            return loaded;
        }

        public void Start()
        {
            if (loaded)
            {

                try
                {
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
                            }
                            OnNewMessage(string.Format("Instruction: 0x{0:X4}, Args: 0x{1:X4}, Program counter: 0x{2:X4}", instructionValue, args, programCounter));
                            if (increaseProgramCounter)
                            {
                                programCounter += 2;
                            }
                        }
                        else
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
            }
        }
        public void Reset()
        {
            programCounter = 0x200;
            iRegister = 0;
            stack.Clear();
            Array.Clear(memory, 0, memory.Length);
            Array.Clear(registers, 0, registers.Length);
        }
        #region IDisposable
        // To detect redundant calls
        private bool _disposedValue;

        public void Dispose() => Dispose(true);

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    programSourceStreamReader?.Dispose();
                }

                _disposedValue = true;
            }
        }
        #endregion

        private void OnNewMessage(string msg)
        {
            NewMessage?.Invoke(this, msg);
        }

        public void CheckRegisterIndex(int registerIndex)
        {
            if (registerIndex > registers.Length)
            {
                throw new ArgumentOutOfRangeException($"Register V{registerIndex} does not exists.");
            }

        }
    }
}
