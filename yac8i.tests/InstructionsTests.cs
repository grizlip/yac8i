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
            vm.instructions.Single(instruction => instruction.Opcode == 0x00E0).Execute(0);
            Assert.That(vm.Surface[0, 0], Is.EqualTo(false));
        }

        [Test]
        public void TestRETException()
        {
            Chip8VM vm = new();
            Assert.Throws<ArgumentException>(
                delegate
                {
                    vm.instructions.Single(instruction => instruction.Opcode == 0x00EE).Execute(100);
                });
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
    }
}