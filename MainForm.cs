using System.Windows.Forms;
using System.Drawing;
using System.Runtime.Versioning;

namespace yac8i
{
    public partial class MainForm : Form
    {
        private readonly Chip8VM chip8VM;
        public MainForm(Chip8VM chip8VM)
        {
            this.chip8VM = chip8VM;
            InitializeComponent();
            this.chip8VM.NewMessage += OnNewMessage;
        }

        [SupportedOSPlatform("windows")]
        protected override void OnPaint(PaintEventArgs e)
        {
            SolidBrush sb = new SolidBrush(Color.Black);
            e.Graphics.FillRectangle(sb, 1, 1, 10, 10);
            base.OnPaint(e);
        }

        private void OnNewMessage(object sender, string msg)
        {
            //TODO: Implement showing of messages (maybe some list?)
        }
    }
}