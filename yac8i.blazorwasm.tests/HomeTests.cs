using Moq;
using Microsoft.FluentUI.AspNetCore.Components;
using yac8i.blazorwasm.Pages;
using yac8i.TickTimer;
using System.Linq;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace yac8i.blazorwasm.tests
{
    public class CompareStreamContentsWithString
    {
        private Mock<IChip8VM> vmMock;
        private Mock<ITickTimer> tickTimerMock;
        private BunitContext testContext;
        private JSRuntimeInvocationHandler getReferenceResult;
        private JSRuntimeInvocationHandler drawResult;
        private BunitJSModuleInterop webAudioModule;
        private BunitJSModuleInterop domModule;

        [SetUp]
        public void SetUp()
        {
            vmMock = new Mock<IChip8VM>();
            tickTimerMock = new Mock<ITickTimer>();
            testContext = new BunitContext();

            testContext.Services.AddSingleton(vmMock.Object);
            testContext.Services.AddSingleton(tickTimerMock.Object);
            testContext.Services.AddFluentUIComponents();

            getReferenceResult = testContext.JSInterop.SetupVoid("getReference", _ => true).SetVoidResult();
            drawResult = testContext.JSInterop.SetupVoid("draw", _ => true).SetVoidResult();

            webAudioModule = testContext.JSInterop.SetupModule(@"./_content/KristofferStrube.Blazor.WebAudio/KristofferStrube.Blazor.WebAudio.js");
            webAudioModule.Mode = JSRuntimeMode.Loose;
            domModule = testContext.JSInterop.SetupModule(@"./_content/KristofferStrube.Blazor.DOM/KristofferStrube.Blazor.DOM.js");
            domModule.Mode = JSRuntimeMode.Loose;
        }

        [TearDown]
        public void TearDown()
        {
            getReferenceResult.Dispose();
            drawResult.Dispose();
            testContext.Dispose();
        }

        [TestCase(true, true, true, false, true, false)]
        [TestCase(false, false, true, true, false, false)]
        [TestCase(true, false, true, false, true, true)]
        public void Buttons_Test(bool started, bool running, bool loaded, bool startEnabled, bool pauseGoEnabled, bool stepEnabled)
        {
            bool startClicked = false;
            bool pauseGoClicked = false;
            bool stepClicked = false;
#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416
            cut.Instance.started = started;
            cut.Instance.running = running;
            cut.Instance.loaded = loaded;
            cut.Render();

            var buttons = cut.FindComponents<FluentButton>();
            var startButton = buttons.Single(item => item.Instance.Id == "startButton");
            startButton.Render(parameters =>
            {
                parameters.Add(p => p.OnClick, (e) => { startClicked = true; });
            });
            var pauseGoButton = buttons.Single(item => item.Instance.Id == "pauseGoButton");
            pauseGoButton.Render(parameters =>
            {
                parameters.Add(p => p.OnClick, (e) => { pauseGoClicked = true; });
            });
            var stepButton = buttons.Single(item => item.Instance.Id == "stepButton");
            stepButton.Render(parameters =>
            {
                parameters.Add(p => p.OnClick, (e) => { stepClicked = true; });
            });

            startButton.Find("fluent-button").Click();
            pauseGoButton.Find("fluent-button").Click();
            stepButton.Find("fluent-button").Click();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(startClicked, Is.EqualTo(startEnabled));
                Assert.That(pauseGoClicked, Is.EqualTo(pauseGoEnabled));
                Assert.That(stepClicked, Is.EqualTo(stepEnabled));
            }
        }

        [Test]
        public void ProgramLoaded_StartButtonClicked_ProgramStarts()
        {
#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416

            cut.Instance.loaded = true;
            cut.Render();

            var startButton = cut.FindComponents<FluentButton>().Single(item => item.Instance.Id == "startButton").Find("fluent-button");
            startButton.Click();

            vmMock.Verify(m => m.StartAsync(CancellationToken.None), Times.Once);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(cut.Instance.loaded, Is.True);
                Assert.That(cut.Instance.running, Is.True);
                Assert.That(cut.Instance.started, Is.True);
            }
        }

        [Test]
        public void HomeRender_Twice_draw_getReference_Once()
        {
#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416
            cut.Render();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(drawResult.Invocations.Single().Identifier, Is.EqualTo("draw"));
                Assert.That(getReferenceResult.Invocations.Single().Identifier, Is.EqualTo("getReference"));
            }
        }

        [Test]
        public void PressLoadButton_Vm_LoadAsync_Called_Event_Subscribed()
        {
            bool programLoadedEventCalled = false;
            vmMock.Setup(m => m.LoadAsync(It.IsAny<Stream>()))
                  .Returns(Task.CompletedTask)
                  .Raises(e => e.ProgramLoaded += null, this, default(int));

            vmMock.Object.ProgramLoaded += (s, count) => { programLoadedEventCalled = true; };

#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416

            var inputFileComponent = cut.FindComponent<InputFile>();
            string dummyProgram = "Very dummy program...";
            var fileToUpload = InputFileContent.CreateFromText(dummyProgram, "test.txt");
            inputFileComponent.UploadFiles(fileToUpload);

            Assert.That(programLoadedEventCalled, Is.True);
        }

        [Test]
        public void PressLoadButton_Program_Passed_To_VM()
        {
#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416

            var inputFileComponent = cut.FindComponent<InputFile>();
            string dummyProgram = "Very dummy program...";
            var fileToUpload = InputFileContent.CreateFromText(dummyProgram, "test.txt");
            inputFileComponent.UploadFiles(fileToUpload);

            vmMock.Verify(m => m.StopAndReset(), Times.Once);
            vmMock.Verify(m => m.LoadAsync(It.Is<Stream>(s => CompareStreamContentsToString(s, dummyProgram))), Times.Once);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(cut.Instance.loaded, Is.True);
                Assert.That(cut.Instance.running, Is.False);
                Assert.That(cut.Instance.started, Is.False);
            }
        }

        [Test]
        public void ProgramLoadedEvent_ProperInstructionsView()
        {
            int instructionsId = 0;
            vmMock.Setup(m => m.GetMnemonic(It.IsAny<ushort>()))
                  .Returns(() => $"{instructionsId++}");

            vmMock.Setup(m => m.LoadAsync(It.IsAny<Stream>()))
                  .Returns(Task.CompletedTask)
                  .Raises(e => e.ProgramLoaded += null, this, 6);

#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416

            var inputFileComponent = cut.FindComponent<InputFile>();
            string dummyProgram = "Very dummy program...";
            var fileToUpload = InputFileContent.CreateFromText(dummyProgram, "test.txt");
            inputFileComponent.UploadFiles(fileToUpload);

            var instructions = cut.FindComponents<FluentAccordionItem>()
                                  .Single(item => item.Instance.Heading == "Source")
                                  .FindAll("div")
                                  .Where(item => item?.Id?.StartsWith("instruction-") ?? false)
                                  .Select(item => (item.Id, item.InnerHtml))
                                  .ToArray();


            Assert.That(instructions, Has.Length.EqualTo(3));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(instructions[0].Id, Is.EqualTo("instruction-512"));
                Assert.That(instructions[1].Id, Is.EqualTo("instruction-514"));
                Assert.That(instructions[2].Id, Is.EqualTo("instruction-516"));

                Assert.That(instructions[0].InnerHtml, Is.EqualTo("512 :: 0"));
                Assert.That(instructions[1].InnerHtml, Is.EqualTo("514 :: 1"));
                Assert.That(instructions[2].InnerHtml, Is.EqualTo("516 :: 2"));
            }
        }

        [Test]
        public void RegistersChanged_RegisterRendered()
        {
            vmMock.Setup(m => m.Registers).Returns(() => new byte[] { 0xAB, 0xCD, 0xEF });
            vmMock.Setup(m => m.IRegister).Returns(() => 0xFFFF);
#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416

            var registers = cut.FindComponents<FluentAccordionItem>()
                                              .Single(item => item.Instance.Heading == "Registers")
                                              .FindAll("p")
                                              .ToArray();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(registers, Has.Length.EqualTo(4));
                Assert.That(registers.Count(r => r.InnerHtml.Contains("0xab")), Is.EqualTo(1));
                Assert.That(registers.Count(r => r.InnerHtml.Contains("0xcd")), Is.EqualTo(1));
                Assert.That(registers.Count(r => r.InnerHtml.Contains("0xef")), Is.EqualTo(1));
                Assert.That(registers.Count(r => r.InnerHtml.Contains("0xffff")), Is.EqualTo(1));
            }
        }

        private static bool CompareStreamContentsToString(Stream streamValue, string stringValue)
        {
            using StreamReader sr = new(streamValue);
            var str = sr.ReadToEnd();
            return str.Equals(stringValue);
        }

    }
}