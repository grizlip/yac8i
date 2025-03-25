using System;
using System.Linq;
using NUnit.Framework;

namespace yac8i.tests
{
    public class InstructionsTests
    {
        private static int StartOfFreeMemory => Chip8VM.font.Length;

        [Test]
        public void TestCLS()
        {
            Chip8VM vm = new();
            vm.Surface[0, 0] = true;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x00E0, 0);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.Surface[0, 0], Is.EqualTo(false));
                Assert.That(shouldIncrementPC, Is.True);
            }
        }

        [Test]
        public void TestRETException()
        {
            Chip8VM vm = new();
            Assert.Throws<ArgumentException>(
                delegate
                {
                    ExecuteSingleInstruction(vm, 0x00EE, 100);
                });
        }

        [Test]
        public void TestRETCorrect()
        {
            Chip8VM vm = new();
            vm.stack.Push(0xFFFF);
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x00EE, 100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.ProgramCounter, Is.EqualTo(0xFFFF));
                Assert.That(shouldIncrementPC, Is.False);
            }
        }

        [Test]
        public void TestJP()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x1000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.ProgramCounter, Is.EqualTo(0x0FFF));
                Assert.That(shouldIncrementPC, Is.False);
            }
        }

        [Test]
        public void TestCALL()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x2000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.ProgramCounter, Is.EqualTo(0x0FFF));
                Assert.That(vm.stack.Peek(), Is.EqualTo(514));
                Assert.That(shouldIncrementPC, Is.False);
            }
        }

        [Test]
        public void TestSENoJump()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x3000, 0xFFFF);
            Assert.That(shouldIncrementPC, Is.True);
        }

        [Test]
        public void TestSEJump()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0xFF;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x3000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSNENoJump()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x4000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }

        }

        [Test]
        public void TestSNEJump()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0xFF;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x4000, 0xFFFF);
            Assert.That(shouldIncrementPC, Is.True);
        }


        [Test]
        public void TestSERegisterNoJump()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 1;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x5000, 0xFFAF);
            Assert.That(shouldIncrementPC, Is.True);
        }

        [Test]
        public void TestSERegisterJump()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0xFF;
            vm.registers[0xA] = 0xFF;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x5000, 0xFFAF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestLD()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x6000, 0xFFAF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0xAF));
            }
        }

        [Test]
        public void TestLDRegister()
        {
            Chip8VM vm = new();
            vm.registers[0] = 100;
            vm.registers[0xF] = 200;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8000, 0xFF0F);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(100));
                Assert.That(vm.registers[0], Is.EqualTo(100));
            }
        }

        [Test]
        public void TestADDNoOverflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 10;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x7000, 0x0F01);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(11));
            }
        }

        [Test]
        public void TestADDOverflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = byte.MaxValue;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x7000, 0x0F02);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
            }
        }

        [Test]
        public void TestOR()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11110000;
            vm.registers[1] = 0b00001111;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8001, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0b11111111));
                Assert.That(vm.registers[1], Is.EqualTo(0b00001111));
            }
        }

        [Test]
        public void TestAND()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11111000;
            vm.registers[1] = 0b00011111;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8002, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0b00011000));
                Assert.That(vm.registers[1], Is.EqualTo(0b00011111));
            }
        }

        [Test]
        public void TestXOR()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11111000;
            vm.registers[1] = 0b00011111;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8003, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0b11100111));
                Assert.That(vm.registers[1], Is.EqualTo(0b00011111));
            }
        }


        [Test]
        public void TestADDRegistersFRegisterNoOverflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8004, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestADDRegistersFRegisterOverflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 255;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8004, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestADDRegistersOtherRegisterOverflow()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 255;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8004, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[0xA], Is.EqualTo(49));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestADDRegistersOtherRegisterNoOverflow()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8004, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[0xA], Is.EqualTo(150));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestSUBRegistersFRegisterNoUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 100;
            vm.registers[1] = 10;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8005, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[1], Is.EqualTo(10));
            }
        }

        [Test]
        public void TestSUBRegistersFRegisterUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 20;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8005, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestSUBRegistersOtherRegisterUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 20;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8005, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[0xA], Is.EqualTo(226));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestSUBRegistersOtherRegisterNoUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8005, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[0xA], Is.EqualTo(50));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestSHR()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8006, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[0xA], Is.EqualTo(1));
                Assert.That(vm.registers[1], Is.EqualTo(0b00000010));
            }
        }

        [Test]
        public void TestSHRCarry()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b00000001;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8006, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[0xA], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(0b00000001));
            }
        }

        [Test]
        public void TestSHRRegisterFCarry()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b00000001;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8006, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[1], Is.EqualTo(0b00000001));
            }
        }

        [Test]
        public void TestSHRRegisterF()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8006, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(0b00000010));
            }
        }

        [Test]
        public void TestSUBNRegistersFRegisterNoUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 10;
            vm.registers[1] = 100;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8007, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[1], Is.EqualTo(100));
            }
        }

        [Test]
        public void TestSUBNRegistersFRegisterUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 50;
            vm.registers[1] = 20;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8007, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(20));
            }
        }

        [Test]
        public void TestSUBNRegistersOtherRegisterNoUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 20;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8007, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[0xA], Is.EqualTo(30));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestSUBNRegistersOtherRegisterUnderflow()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x8007, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[0xA], Is.EqualTo(206));
                Assert.That(vm.registers[1], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestSHL()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x800E, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[0xA], Is.EqualTo(0b00000100));
                Assert.That(vm.registers[1], Is.EqualTo(0b00000010));
            }
        }

        [Test]
        public void TestSHLCarry()
        {
            Chip8VM vm = new();
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b10000000;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x800E, 0x0A10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[0xA], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(0b10000000));
            }
        }

        [Test]
        public void TestSHLRegisterFCarry()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b10000000;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x800E, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
                Assert.That(vm.registers[1], Is.EqualTo(0b10000000));
            }
        }

        [Test]
        public void TestSHLRegisterF()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x800E, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
                Assert.That(vm.registers[1], Is.EqualTo(0b00000010));
            }
        }

        [Test]
        public void TestSNERegistersJump()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0xAA;
            vm.registers[1] = 0xBB;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x9000, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.registers[0xF], Is.EqualTo(0xAA));
                Assert.That(vm.registers[1], Is.EqualTo(0xBB));
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSNERegistersNoJump()
        {
            Chip8VM vm = new();
            vm.registers[0xF] = 0xAA;
            vm.registers[1] = 0xAA;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0x9000, 0x0F10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0xAA));
                Assert.That(vm.registers[1], Is.EqualTo(0xAA));
                Assert.That(vm.ProgramCounter, Is.EqualTo(512));
            }
        }

        [Test]
        public void TestLDI()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xA000, 0x0ABC);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.IRegister, Is.EqualTo(0x0ABC));
            }
        }

        [Test]
        public void TestJPOffset()
        {
            Chip8VM vm = new();
            vm.registers[0] = 0x000F;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xB000, 0x0ABC);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(0x0ACB));
            }
        }

        [Test]
        [Repeat(255)]
        public void TestRND()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xC000, 0x010F);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[1], Is.AtMost(0x000F));
            }
        }

        [Test]
        [Repeat(255)]
        public void TestRNDMask()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xC000, 0x01FF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[1], Is.AtMost(0x00FF));
            }
        }

        [Test]
        public void TestDRWSimple()
        {
            Chip8VM vm = new();
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xD000, 0x01FF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                for (int spriteBitIndex = 0; spriteBitIndex < 8; spriteBitIndex++)
                {
                    Assert.That(vm.Surface[spriteBitIndex, 0], Is.EqualTo(true));
                }
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
            }
        }

        [Test]
        public void TestDRWXOR()
        {
            Chip8VM vm = new();
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            vm.Surface[0, 0] = true;
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xD000, 0x01FF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.Surface[0, 0], Is.EqualTo(false));
                for (int spriteBitIndex = 1; spriteBitIndex < 8; spriteBitIndex++)
                {
                    Assert.That(vm.Surface[spriteBitIndex, 0], Is.EqualTo(true));
                }
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
            }
        }

        [Test]
        public void TestDRWSpriteOverflow()
        {
            Chip8VM vm = new();
            vm.registers[1] = 64;
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xD000, 0x01FF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                for (int spriteBitIndex = 0; spriteBitIndex < 8; spriteBitIndex++)
                {
                    Assert.That(vm.Surface[spriteBitIndex, 0], Is.EqualTo(true));
                }
                Assert.That(vm.registers[0xF], Is.EqualTo(0));
            }
        }

        [Test]
        public void TestSKPNoPress()
        {
            Chip8VM vm = new();
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xE09E, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(514));
            }
        }

        [Test]
        public void TestSKPPress()
        {
            Chip8VM vm = new();
            vm.registers[1] = 1;
            vm.UpdateKeyState(1,true);
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xE09E, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSKPThrows()
        {
            Chip8VM vm = new();
            vm.registers[1] = 100;
            Assert.Throws<ArgumentException>(() => ExecuteSingleInstruction(vm, 0xE09E, 0x0100));
        }


        [Test]
        public void TestDRWSpriteClipped()
        {
            Chip8VM vm = new();
            vm.registers[1] = 62;
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            bool shouldIncrementPC = ExecuteSingleInstruction(vm, 0xD000, 0x01FF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                for (int spriteBitIndex = 0; spriteBitIndex < 8; spriteBitIndex++)
                {
                    Assert.That(vm.Surface[spriteBitIndex, 0], Is.EqualTo(false));
                }
                for (int spriteBitIndex = 62; spriteBitIndex < 64; spriteBitIndex++)
                {
                    Assert.That(vm.Surface[spriteBitIndex, 0], Is.EqualTo(true));
                }

                Assert.That(vm.registers[0xF], Is.EqualTo(0));
            }
        }


        [Test]
        public void TestX()
        {
            byte result = Instruction.X(0xABCD);
            Assert.That(result, Is.EqualTo(0x0B));
        }

        [Test]
        public void TestY()
        {
            byte result = Instruction.Y(0xABCD);
            Assert.That(result, Is.EqualTo(0x0C));
        }

        [Test]
        public void TestN()
        {
            byte result = Instruction.N(0xABCD);
            Assert.That(result, Is.EqualTo(0x0D));
        }

        [Test]
        public void TestNN()
        {
            byte result = Instruction.NN(0xABCD);
            Assert.That(result, Is.EqualTo(0xCD));
        }

        [Test]
        public void TestNNN()
        {
            ushort result = Instruction.NNN(0xABCD);
            Assert.That(result, Is.EqualTo(0x0BCD));
        }

        private static bool ExecuteSingleInstruction(Chip8VM vm, ushort opcode, ushort args) => vm.instructions.Single(instruction => instruction.Opcode == opcode).Execute(args);

    }
}
