using System;
using System.Linq;
using NUnit.Framework;

namespace yac8i.tests
{
    public class InstructionsTests
    {

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