using Moq;
using Microsoft.FluentUI.AspNetCore.Components;
using yac8i.blazorwasm.Pages;
using yac8i.TickTimer;
using System.Linq;

namespace yac8i.blazorwasm.tests
{
    public class HomeTests : BunitContext
    {
        [Test]
        public void HomeRender_Twice_draw_getReference_Once()
        {
            var vmMock = new Mock<IChip8VM>();
            var tickTimerMock = new Mock<ITickTimer>();
            
            using var ctx = new BunitContext();

            ctx.Services.AddSingleton(vmMock.Object);
            ctx.Services.AddSingleton(tickTimerMock.Object);
            ctx.Services.AddFluentUIComponents();

            var getReferenceResult = ctx.JSInterop.SetupVoid("getReference", _ => true).SetVoidResult();
            var drawResult = ctx.JSInterop.SetupVoid("draw", _ => true).SetVoidResult();

            var webAudioModule = ctx.JSInterop.SetupModule(@"./_content/KristofferStrube.Blazor.WebAudio/KristofferStrube.Blazor.WebAudio.js");
            webAudioModule.Mode = JSRuntimeMode.Loose;
            var domModule = ctx.JSInterop.SetupModule(@"./_content/KristofferStrube.Blazor.DOM/KristofferStrube.Blazor.DOM.js");
            domModule.Mode = JSRuntimeMode.Loose;

#pragma warning disable CA1416
            var cut = ctx.Render<Home>();
            cut.Render();
#pragma warning disable CA1416

            using (Assert.EnterMultipleScope())
            {
                Assert.That(drawResult.Invocations.Single().Identifier, Is.EqualTo("draw"));
                Assert.That(getReferenceResult.Invocations.Single().Identifier, Is.EqualTo("getReference"));
            }
        }

    }
}