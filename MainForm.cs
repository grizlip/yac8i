using System;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.ComponentModel;

namespace yac8i
{
    public partial class MainForm : Form
    {
        private readonly Chip8VM chip8VM;
        private int x = 0;
        private int y = 0;

        private ScreenRefreshEventArgs currentRefreshEventArgs = null;

        [SupportedOSPlatform("windows")]
        private SolidBrush fillBrush = new SolidBrush(Color.Black);
        private const int pixelSize = 16;
        public MainForm(Chip8VM chip8VM)
        {
            this.chip8VM = chip8VM;
            InitializeComponent();
            this.chip8VM.NewMessage += OnNewMessage;
            this.chip8VM.ScreenRefresh += OnScreenRefresh;
        }

        private void OnScreenRefresh(object sender, ScreenRefreshEventArgs args)
        {
            this.currentRefreshEventArgs = args;
            this.BeginInvoke(() => this.Refresh());
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            System.Threading.Thread.Sleep(500);
            this.chip8VM.Start();
        }

        [SupportedOSPlatform("windows")]
        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.currentRefreshEventArgs != null)
            {
                switch (this.currentRefreshEventArgs.RequestType)
                {
                    case RefreshRequest.Clear:
                        e.Graphics.Clear(Color.Transparent);
                        break;
                    case RefreshRequest.Draw:
                        e.Graphics.FillRectangle(fillBrush, x, y, pixelSize, pixelSize);
                        x += 1 % this.Width;
                        y += 1 % this.Height;
                        break;
                    default:
                        this.OnNewMessage(this, $"Error. Unknown screen refresh request type: {this.currentRefreshEventArgs.RequestType}");
                        break;
                }
            }
            else
            {
                this.OnNewMessage(this, "Error. Trying to refresh without refresh events set.");
            }
            base.OnPaint(e);
        }
        private void OnNewMessage(object sender, string msg)
        {
            //TODO: Implement showing of messages (maybe some list?)

            System.Console.WriteLine(msg);
        }
    }
}