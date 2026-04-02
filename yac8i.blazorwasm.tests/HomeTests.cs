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

        [Test]
        public void ProgramLoaded_StartButtonClicked_ProgramStarts()
        {
#pragma warning disable CA1416
            var cut = testContext.Render<Home>();
#pragma warning disable CA1416

            cut.Instance.loaded = true;
            cut.Render();

            var buttons = cut.FindComponents<FluentButton>();

            var domButton = buttons[0].Find("fluent-button");
            domButton.Click();

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

        private static bool CompareStreamContentsToString(Stream streamValue, string stringValue)
        {
            using StreamReader sr = new(streamValue);
            var str = sr.ReadToEnd();
            return str.Equals(stringValue);
        }

    }
}