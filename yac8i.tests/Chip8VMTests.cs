using NUnit.Framework;
using System.Threading.Tasks;
using yac8i.TickTimer;
using System;
using System.IO;
using System.Linq;

namespace yac8i.tests
{
    public class Chip8VMTests
    {
        private Chip8VM vm;

        [SetUp]
        public void Setup()
        {
            // inject fake timer so we can inspect IsRunning and avoid real threads
            vm = new Chip8VM(new FakeTimer());
        }

        private class FakeTimer : ITickTimer
        {
            public event EventHandler<TickTimerElapsedEventArgs> Elapsed = delegate { };
            public float Interval { get; set; } = 1f;
            public bool IsRunning { get; private set; }
            public void Start() => IsRunning = true;
            public void Stop(bool joinThread = true) => IsRunning = false;
        }

        [Test]
        public void GetMnemonic_KnownAndUnknown()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.GetMnemonic(0x00E0), Is.EqualTo("CLS"));
                // choose a value that doesn't match any opcode mask
                Assert.That(vm.GetMnemonic(0xFFFF), Is.EqualTo("unknown"));
            }
        }

        [Test]
        public void GetOpcode_ValidAndInvalid()
        {
            // put some bytes in memory at address 0
            vm.memory[0] = 0x12;
            vm.memory[1] = 0x34;
            ushort code = vm.GetOpcode(0);
            Assert.That(code, Is.EqualTo(0x1234));

            Assert.Throws<ArgumentException>(() => vm.GetOpcode((uint)vm.memory.Length));
        }

        [Test]
        public void TryAddAndRemoveBreakpoint_Behavior()
        {
            // no program loaded yet -> programBytesCount == 0 -> always false
            Assert.That(vm.TryAddBreakpoint(512, out _), Is.False);

            // load a small program to allow breakpoints
            var data = new byte[10];
            using var ms = new MemoryStream(data);
            vm.LoadAsync(ms).Wait();
            using (Assert.EnterMultipleScope())
            {
                // first valid address must be 512
                Assert.That(vm.TryAddBreakpoint(512, out var bp), Is.True);
                Assert.That(bp, Is.Not.Null);
                Assert.That(vm.TryRemoveBreakpoint(512, out var removed), Is.True);
                Assert.That(removed, Is.SameAs(bp));
                // removing again returns false
                Assert.That(vm.TryRemoveBreakpoint(512, out _), Is.False);
            }
        }

        [Test]
        public async Task StoreAndRestore_PreservesState()
        {
            // load program
            byte[] program = [0xAA, 0xBB, 0xCC];
            using var ms = new MemoryStream(program);
            await vm.LoadAsync(ms);

            // modify some state
            vm.registers[1] = 0x42;
            vm.IRegister = 0x123;

            string file = Path.GetTempFileName();
            try
            {
                Assert.That(vm.TryStore(file), Is.True);

                // change state
                vm.registers[1] = 0;
                vm.IRegister = 0;

                using (Assert.EnterMultipleScope())
                {
                    // restore from same program file - should succeed
                    Assert.That(vm.TryRestore(file), Is.True);
                    Assert.That(vm.registers[1], Is.EqualTo(0x42));
                    Assert.That(vm.IRegister, Is.EqualTo(0x123));
                }

                // load different program then try to restore again -> mismatch
                using var ms2 = new MemoryStream([1, 2, 3, 4]);
                await vm.LoadAsync(ms2);
                Assert.That(vm.TryRestore(file), Is.False);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void Load_FilePathsAndEvents()
        {
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(file, [0x10, 0x20, 0x30]);
                int loadedBytes = -1;
                vm.ProgramLoaded += (_, cnt) => loadedBytes = cnt;

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(vm.Load(file), Is.True);
                    Assert.That(loadedBytes, Is.EqualTo(3));
                }

                // create fresh VM instance to clear loaded flag
                vm = new Chip8VM(new FakeTimer());
                string message = string.Empty;
                vm.NewMessage += (_, msg) => message = msg;
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(vm.Load(file + ".doesnotexist"), Is.False);
                    Assert.That(message, Does.Contain("File read error"));
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public async Task LoadAsync_Streams()
        {
            byte[] program = [0x01, 0x02];
            using var ms = new MemoryStream(program);
            int loadedCount = -1;
            vm.ProgramLoaded += (_, cnt) => loadedCount = cnt;
            await vm.LoadAsync(ms);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(loadedCount, Is.EqualTo(2));
                // verify that memory copy occurred (first byte after 512)
                Assert.That(vm.memory[512], Is.EqualTo(0x01));
            }
        }

        [Test]
        [Repeat(20)]
        public void Step_ExecutesPendingInstructionAndUpdatesTimers()
        {
            // make CLS at program counter
            vm.memory[0x200] = 0x00;
            vm.memory[0x201] = 0xE0;
            // set pending count to 1
            vm.instructionsToExecuteInFrame = 1;
            vm.Step();
            Assert.That(vm.ProgramCounter, Is.EqualTo((ushort)0x202));

            // set timers and check beep event
            vm.delayTimer = 2;
            vm.soundTimer = 1;
            bool beeped = false;
            var beepSignal = new System.Threading.ManualResetEventSlim(false);
            vm.BeepStatus += (_, status) => { beeped = status; beepSignal.Set(); };
            // ensure no instructions pending so timer path is used
            vm.instructionsToExecuteInFrame = 0;
            vm.Step();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(beepSignal.Wait(200), Is.True, "beep event not received");
                Assert.That(vm.delayTimer, Is.EqualTo((byte)1));
                Assert.That(vm.soundTimer, Is.Zero);
                Assert.That(beeped, Is.True);
            }
        }

        [Test]
        public void UpdateKeyState_ChangesInternalFlags()
        {
            vm.UpdateKeyState(3, true);
            Assert.That(vm.pressedKeys & (1 << 3), Is.Not.Zero);

            vm.UpdateKeyState(3, false);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.pressedKeys & (1 << 3), Is.Zero);
                Assert.That(vm.lastPressedKey, Is.EqualTo((ushort)3));
            }
        }

        [Test]
        public void PauseGoStopAndReset_AffectTimerAndState()
        {
            vm.Go();
            Assert.That(vm.tickTimer.IsRunning, Is.True);
            vm.Pause();
            Assert.That(vm.tickTimer.IsRunning, Is.False);

            // modify memory/registers and reset
            vm.registers[0] = 99;
            vm.Surface[1, 1] = true;
            vm.StopAndReset();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(vm.registers.All(r => r == 0), Is.True);
                Assert.That(vm.Surface[1, 1], Is.False);
                Assert.That(vm.ProgramCounter, Is.EqualTo((ushort)0x200));
            }
        }
    }
}
