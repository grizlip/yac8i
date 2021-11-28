using System;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.Versioning;
using System.Collections.Generic;
using NLog;

namespace yac8i;

public partial class MainForm : Form
{

    //We will use keypad layout as stated here: https://tobiasvl.github.io/blog/write-a-chip-8-emulator/#keypad
    private static Dictionary<Keys, ushort> keysMapping = new Dictionary<Keys, ushort>()
        {
            {Keys.D1, 0x1},
            {Keys.D2, 0x2},
            {Keys.D3, 0x3},
            {Keys.D4, 0xC},
            {Keys.Q, 0x4},
            {Keys.W, 0x5},
            {Keys.E, 0x6},
            {Keys.R, 0xD},
            {Keys.A, 0x7},
            {Keys.S, 0x8},
            {Keys.D, 0x9},
            {Keys.F, 0xE},
            {Keys.Z, 0xA},
            {Keys.X, 0x0},
            {Keys.C, 0xB},
            {Keys.V, 0xF},
       };
    Logger log = LogManager.GetCurrentClassLogger();
    private readonly Chip8VM chip8VM;
    private int x = 0;
    private int y = 0;

    private ScreenRefreshEventArgs currentRefreshEventArgs = null;

    [SupportedOSPlatform("windows")]
    private SolidBrush fillBrush = new SolidBrush(Color.Black);
    private const int pixelSize = 16;
    public MainForm(Chip8VM chip8VM)
    {
        this.KeyDown += OnKeyDown;
        this.KeyUp += OnKeyUp;
        this.chip8VM = chip8VM;
        InitializeComponent();
        this.chip8VM.NewMessage += OnNewMessage;
        this.chip8VM.ScreenRefresh += OnScreenRefresh;
    }


    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (keysMapping.ContainsKey(e.KeyCode))
        {
            chip8VM.UpdateKeyUp(keysMapping[e.KeyCode]);
        }
    }
    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (keysMapping.ContainsKey(e.KeyCode))
        {
            chip8VM.UpdateKeyDown(keysMapping[e.KeyCode]);
        }
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
        log.Info(msg);
    }
}

