using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using yac8i.TickTimer;

namespace yac8i.blazorwasm.Pages
{
    [System.Runtime.Versioning.SupportedOSPlatform("browser")]
    public partial class Home : IDisposable
    {
        private readonly byte[] surface = new byte[64 * 32 * 4];//8192
        private readonly JsTickTimer jsTickTimer;
        private readonly Chip8VM vm;
        private DotNetObjectReference<Home>? objRef;

        [Inject]
        public IJSRuntime? JSInterop { get; set; }

        public Home()
        {
            jsTickTimer = new();
            vm = new(jsTickTimer);
        }

        public async Task SendArrayBufferToJavaScript(byte[] data)
        {
            string base64String = Convert.ToBase64String(data);
            await JSInterop!.InvokeVoidAsync("receiveArrayBuffer", base64String);
        }

        [JSInvokable]
        public async Task OnTick()
        {
            jsTickTimer.Tick();

            for (int i = 0; i < vm.Surface.GetLength(0); i++)
            {
                for (int j = 0; j < vm.Surface.GetLength(1); j++)
                {
                    int blueIndex = j * (64 * 4) + i * 4 + 2;
                    int alphaIndex = j * (64 * 4) + i * 4 + 3;
                    if (vm.Surface[i, j])
                    {
                        surface[blueIndex] = 0xff;
                        surface[alphaIndex] = 0xff;
                    }
                    else
                    {
                        surface[blueIndex] = 0;
                        surface[alphaIndex] = 0;
                    }
                }
            }

            await SendArrayBufferToJavaScript(surface);
        }

        [JSInvokable]
        public void OnKeyDown(string  arg)
        {
            Console.WriteLine(arg);
            
        }

        [JSInvokable]
        public void OnKeyUp(string arg)
        {
            Console.WriteLine(arg);
        }

        public void Dispose()
        {
            objRef?.Dispose();
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                vm.NewMessage += (sender, message) =>
                  {
                      Console.WriteLine(message);
                  };
                objRef = DotNetObjectReference.Create(this);
                await JSInterop!.InvokeVoidAsync("getReference", objRef);
                //start animation, when blazor loads
                await JSInterop!.InvokeVoidAsync("draw");
            }
        }

        private async Task LoadFiles(InputFileChangeEventArgs e)
        {
            Console.WriteLine(e.File.Name);
            vm.StopAndReset();
            await vm.LoadAsync(e.File.OpenReadStream());
            await vm.StartAsync(CancellationToken.None);
        }


        private class JsTickTimer : ITickTimer
        {
            public event EventHandler<TickTimerElapsedEventArgs>? Elapsed;
            public float Interval { get; set; } = 1;
            public bool IsRunning { get; private set; }
            public void Start()
            {
                IsRunning = true;
            }
            public void Stop(bool joinThread = true)
            {
                IsRunning = false;
            }

            public void Tick()
            {
                if (IsRunning)
                {
                    Elapsed?.Invoke(this, new TickTimerElapsedEventArgs(0));
                }
            }
        }
    }
}