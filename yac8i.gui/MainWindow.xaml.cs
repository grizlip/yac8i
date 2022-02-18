using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;

namespace yac8i.gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Dictionary<Key, ushort> keysMapping = new Dictionary<Key, ushort>()
        {
            {Key.D1,0x1},
            {Key.D2,0x2},
            {Key.D3,0x3},
            {Key.D4,0xC},
            {Key.Q,0x4},
            {Key.W,0x5},
            {Key.E,0x6},
            {Key.R,0xD},
            {Key.A,0x7},
            {Key.S,0x8},
            {Key.D,0x9},
            {Key.F,0xE},
            {Key.Z,0xA},
            {Key.X,0x0},
            {Key.C,0xB},
            {Key.V,0xF},
        };
        private const int PixelSize = 16;
        private WriteableBitmap writeableBitmap;
        private bool[,] surface = new bool[64, 32];
        private Chip8VM vm;
        public MainWindow()
        {
            InitializeComponent();
            var args = Environment.GetCommandLineArgs();
            if(args.Length != 2)
            {
                MessageBox.Show("Please pass path to the rom as an argument in the command line.");
                this.Close();
            }
            RenderOptions.SetBitmapScalingMode(this.Surface, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this.Surface, EdgeMode.Aliased);

            writeableBitmap = new WriteableBitmap(
                1024,
                512,
                96,
                96,
                PixelFormats.BlackWhite,
                null);
            this.Surface.Source = writeableBitmap;
            DrawSurface();
            vm = new Chip8VM();
            vm.ScreenRefresh += OnScreenRefresh;
            vm.Load(args[1]);
            Task.Factory.StartNew(() =>
                  {
                      vm.Start();
                  }, TaskCreationOptions.LongRunning);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (keysMapping.TryGetValue(e.Key, out ushort keyValue))
            {
                vm.UpdateKeyState(keyValue, false);
            }
            base.OnKeyUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (keysMapping.TryGetValue(e.Key, out ushort keyValue))
            {
                vm.UpdateKeyState(keyValue, true);
            }
            base.OnKeyDown(e);
        }
        private void OnScreenRefresh(object? sender, ScreenRefreshEventArgs args)
        {
            switch (args.RequestType)
            {
                case RefreshRequest.Clear:
                    Array.Clear(surface, 0, surface.Length);
                    break;
                case RefreshRequest.Draw:
                    surface = args.Surface;
                    break;
            }
            this.Dispatcher.BeginInvoke(() =>
              {
                  DrawSurface();
              });
        }

        private void DrawSurface()
        {
            for (int i = 0; i < surface.GetLength(0); i++)
            {
                for (int j = 0; j < surface.GetLength(1); j++)
                {
                    byte[] pixelBrush = new byte[32];
                    if (surface[i, j])
                    {
                        for (int k = 0; k < pixelBrush.Length; k++)
                        {
                            pixelBrush[k] = 0xFF;
                        }
                    }

                    Int32Rect rect = new Int32Rect(i * PixelSize, j * PixelSize, PixelSize, PixelSize);
                    writeableBitmap.WritePixels(rect, pixelBrush, (rect.Width * writeableBitmap.Format.BitsPerPixel + 7) / 8, 0);
                }
            }
        }
    }
}
