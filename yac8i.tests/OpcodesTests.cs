using System;
using System.Linq;
using NUnit.Framework;

namespace yac8i.tests
{
    public class OpcodesTests
    {
        private static int StartOfFreeMemory => Chip8VM.font.Length;
        public required Chip8VM vm;
        [SetUp]
        public void CreateVM()
        {
            vm = new();
        }

        [Test]
        public void TestCLS()
        {
            vm.Surface[0, 0] = true;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x00E0, 0);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.Surface[0, 0], Is.EqualTo(false));
                Assert.That(shouldIncrementPC, Is.True);
            }
        }

        [Test]
        public void TestRETException()
        {
            Assert.Throws<ArgumentException>(
                delegate
                {
                    ExecuteSingleInstruction(0x00EE, 100);
                });
        }

        [Test]
        public void TestRETCorrect()
        {
            vm.stack.Push(0xFFFF);
            bool shouldIncrementPC = ExecuteSingleInstruction(0x00EE, 100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.ProgramCounter, Is.EqualTo(0xFFFF));
                Assert.That(shouldIncrementPC, Is.False);
            }
        }

        [Test]
        public void TestJP()
        {
            bool shouldIncrementPC = ExecuteSingleInstruction(0x1000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.ProgramCounter, Is.EqualTo(0x0FFF));
                Assert.That(shouldIncrementPC, Is.False);
            }
        }

        [Test]
        public void TestCALL()
        {
            bool shouldIncrementPC = ExecuteSingleInstruction(0x2000, 0xFFFF);
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
            bool shouldIncrementPC = ExecuteSingleInstruction(0x3000, 0xFFFF);
            Assert.That(shouldIncrementPC, Is.True);
        }

        [Test]
        public void TestSEJump()
        {
            vm.registers[0xF] = 0xFF;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x3000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSNENoJump()
        {
            bool shouldIncrementPC = ExecuteSingleInstruction(0x4000, 0xFFFF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSNEJump()
        {
            vm.registers[0xF] = 0xFF;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x4000, 0xFFFF);
            Assert.That(shouldIncrementPC, Is.True);
        }

        [Test]
        public void TestSERegisterNoJump()
        {
            vm.registers[0xF] = 1;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x5000, 0xFFAF);
            Assert.That(shouldIncrementPC, Is.True);
        }

        [Test]
        public void TestSERegisterJump()
        {
            vm.registers[0xF] = 0xFF;
            vm.registers[0xA] = 0xFF;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x5000, 0xFFAF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestLD()
        {
            bool shouldIncrementPC = ExecuteSingleInstruction(0x6000, 0xFFAF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(0xAF));
            }
        }

        [Test]
        public void TestLDRegister()
        {
            vm.registers[0] = 100;
            vm.registers[0xF] = 200;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8000, 0xFF0F);
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
            vm.registers[0xF] = 10;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x7000, 0x0F01);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(11));
            }
        }

        [Test]
        public void TestADDOverflow()
        {
            vm.registers[0xF] = byte.MaxValue;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x7000, 0x0F02);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[0xF], Is.EqualTo(1));
            }
        }

        [Test]
        public void TestOR()
        {
            vm.registers[0xF] = 0b11110000;
            vm.registers[1] = 0b00001111;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8001, 0x0F10);
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
            vm.registers[0xF] = 0b11111000;
            vm.registers[1] = 0b00011111;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8002, 0x0F10);
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
            vm.registers[0xF] = 0b11111000;
            vm.registers[1] = 0b00011111;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8003, 0x0F10);
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
            vm.registers[0xF] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8004, 0x0F10);
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
            vm.registers[0xF] = 255;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8004, 0x0F10);
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
            vm.registers[0xA] = 255;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8004, 0x0A10);
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
            vm.registers[0xA] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8004, 0x0A10);
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
            vm.registers[0xF] = 100;
            vm.registers[1] = 10;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8005, 0x0F10);
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
            vm.registers[0xF] = 20;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8005, 0x0F10);
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
            vm.registers[0xA] = 20;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8005, 0x0A10);
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
            vm.registers[0xA] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8005, 0x0A10);
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
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8006, 0x0A10);
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
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b00000001;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8006, 0x0A10);
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
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b00000001;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8006, 0x0F10);
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
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8006, 0x0F10);
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
            vm.registers[0xF] = 10;
            vm.registers[1] = 100;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8007, 0x0F10);
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
            vm.registers[0xF] = 50;
            vm.registers[1] = 20;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8007, 0x0F10);
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
            vm.registers[0xA] = 20;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8007, 0x0A10);
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
            vm.registers[0xA] = 100;
            vm.registers[1] = 50;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x8007, 0x0A10);
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
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x800E, 0x0A10);
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
            vm.registers[0xA] = 0b11111111;
            vm.registers[1] = 0b10000000;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x800E, 0x0A10);
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
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b10000000;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x800E, 0x0F10);
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
            vm.registers[0xF] = 0b11111111;
            vm.registers[1] = 0b00000010;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x800E, 0x0F10);
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
            vm.registers[0xF] = 0xAA;
            vm.registers[1] = 0xBB;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x9000, 0x0F10);
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
            vm.registers[0xF] = 0xAA;
            vm.registers[1] = 0xAA;
            bool shouldIncrementPC = ExecuteSingleInstruction(0x9000, 0x0F10);
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
            bool shouldIncrementPC = ExecuteSingleInstruction(0xA000, 0x0ABC);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.IRegister, Is.EqualTo(0x0ABC));
            }
        }

        [Test]
        public void TestJPOffset()
        {
            vm.registers[0] = 0x000F;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xB000, 0x0ABC);
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
            bool shouldIncrementPC = ExecuteSingleInstruction(0xC000, 0x010F);
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
            bool shouldIncrementPC = ExecuteSingleInstruction(0xC000, 0x01FF);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[1], Is.AtMost(0x00FF));
            }
        }

        [Test]
        public void TestDRWSimple()
        {
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            bool shouldIncrementPC = ExecuteSingleInstruction(0xD000, 0x01FF);
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
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            vm.Surface[0, 0] = true;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xD000, 0x01FF);
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
            vm.registers[1] = 64;
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            bool shouldIncrementPC = ExecuteSingleInstruction(0xD000, 0x01FF);
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
        public void TestDRWSpriteClipped()
        {
            vm.registers[1] = 62;
            vm.memory[StartOfFreeMemory + 1] = 0xff;
            vm.IRegister = (ushort)(StartOfFreeMemory + 1);
            bool shouldIncrementPC = ExecuteSingleInstruction(0xD000, 0x01FF);
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
        public void TestSKPNoPress()
        {
            bool shouldIncrementPC = ExecuteSingleInstruction(0xE09E, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(514));
            }
        }

        [Test]
        public void TestSKPPress()
        {
            vm.registers[1] = 1;
            vm.UpdateKeyState(1, true);
            bool shouldIncrementPC = ExecuteSingleInstruction(0xE09E, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSKPThrows()
        {
            vm.registers[1] = 100;
            Assert.Throws<ArgumentException>(() => ExecuteSingleInstruction(0xE09E, 0x0100));
        }

        [Test]
        public void TestSKPNNoPress()
        {
            bool shouldIncrementPC = ExecuteSingleInstruction(0xE0A1, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(516));
            }
        }

        [Test]
        public void TestSKPNPress()
        {
            vm.registers[1] = 1;
            vm.UpdateKeyState(1, true);
            bool shouldIncrementPC = ExecuteSingleInstruction(0xE0A1, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo(514));
            }
        }

        [Test]
        public void TestSKPNThrows()
        {
            vm.registers[1] = 100;
            Assert.Throws<ArgumentException>(() => ExecuteSingleInstruction(0xE0A1, 0x0100));
        }

        [Test]
        public void TestLDTimerRead()
        {
            vm.delayTimer = 100;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF007, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.True);
                Assert.That(vm.registers[1], Is.EqualTo(100));
            }
        }

        [Test]
        public void TestLDKeyNoPress()
        {
            vm.registers[1] = 200;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF00A, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.EqualTo(false));
                Assert.That(vm.registers[1], Is.EqualTo(200));
                Assert.That(vm.ProgramCounter, Is.EqualTo(512));
            }
        }

        [Test]
        public void TestLDKeyPress()
        {
            vm.registers[1] = 200;
            vm.UpdateKeyState(2, true);
            vm.UpdateKeyState(2, false);
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF00A, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.EqualTo(false));
                Assert.That(vm.registers[1], Is.EqualTo(2));
                Assert.That(vm.ProgramCounter, Is.EqualTo(514));
            }
        }

        [Test]
        public void TestLDDelayTimer()
        {
            vm.registers[1] = 200;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF015, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.EqualTo(true));
                Assert.That(vm.registers[1], Is.EqualTo(200));
                Assert.That(vm.delayTimer, Is.EqualTo(200));
            }
        }

        [Test]
        public void TestLDSoundTimer()
        {
            vm.registers[1] = 200;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF018, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.EqualTo(true));
                Assert.That(vm.registers[1], Is.EqualTo(200));
                Assert.That(vm.soundTimer, Is.EqualTo(200));
            }
        }
        [TestCase((byte)0xF, (ushort)0xF, false, (ushort)0x1E, Description = "No overflow")]
        [TestCase((byte)0xFF, (ushort)0x0FFF, true, (ushort)0x10FE, Description = "Overflow")]
        public void TestADDI(byte regValue, ushort iRegisterValue, bool overflow, ushort result)
        {
            vm.registers[1] = regValue;
            vm.IRegister = iRegisterValue;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF01E, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.EqualTo(true));
                Assert.That(vm.IRegister, Is.EqualTo(result));
                Assert.That(vm.registers[0xF], overflow ? Is.EqualTo(1) : Is.EqualTo(0));
            }
        }

        [Test]
        public void TestLDFont()
        {
            vm.registers[1] = 2;
            bool shouldIncrementPC = ExecuteSingleInstruction(0xF029, 0x0100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(shouldIncrementPC, Is.EqualTo(true));
                Assert.That(vm.IRegister, Is.EqualTo(10));
            }
        }
        private bool ExecuteSingleInstruction(ushort opcode, ushort args) => vm.instructions.Single(instruction => instruction.Opcode == opcode).Execute(args);
    }
}