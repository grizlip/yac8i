using System.Collections.Generic;
using System;
using SDL2;
using System.Threading;

namespace yac8i.gui.sdl
{
    public class SDLDraw
    {
        private readonly AutoResetEvent DoFrameAutoResetEvent = new(false);
        private readonly int pitch;
        private readonly IntPtr windowTexturePtr;
        private readonly IntPtr rendererPtr;
        private static readonly Dictionary<SDL.SDL_Keycode, ushort> keysMapping = new()
            {
                {SDL.SDL_Keycode.SDLK_1,0x1},
                {SDL.SDL_Keycode.SDLK_2,0x2},
                {SDL.SDL_Keycode.SDLK_3,0x3},
                {SDL.SDL_Keycode.SDLK_4,0xC},
                {SDL.SDL_Keycode.SDLK_q,0x4},
                {SDL.SDL_Keycode.SDLK_w,0x5},
                {SDL.SDL_Keycode.SDLK_e,0x6},
                {SDL.SDL_Keycode.SDLK_r,0xD},
                {SDL.SDL_Keycode.SDLK_a,0x7},
                {SDL.SDL_Keycode.SDLK_s,0x8},
                {SDL.SDL_Keycode.SDLK_d,0x9},
                {SDL.SDL_Keycode.SDLK_f,0xE},
                {SDL.SDL_Keycode.SDLK_z,0xA},
                {SDL.SDL_Keycode.SDLK_x,0x0},
                {SDL.SDL_Keycode.SDLK_c,0xB},
                {SDL.SDL_Keycode.SDLK_v,0xF},
            };
        private readonly Chip8VM vm;
        private bool running;
        public SDLDraw(int pitch, IntPtr windowTexturePtr, IntPtr rendererPtr, Chip8VM vm)
        {
            this.pitch = pitch;
            this.windowTexturePtr = windowTexturePtr;
            this.rendererPtr = rendererPtr;
            this.running = false;
            this.vm = vm;
        }

        public void DoFrame()
        {
            DoFrameAutoResetEvent.Set();
        }

        public void Run()
        {
            running = true;
            var surface = new byte[512 * 4096];

            while (running)
            {
                // Check to see if there are any events and continue to do so until the queue is empty.
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) > 0)
                {
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT:
                            running = false;
                            break;
                        case SDL.SDL_EventType.SDL_KEYDOWN:
                            {
                                if (keysMapping.TryGetValue(e.key.keysym.sym, out ushort keyValue))
                                {
                                    vm.UpdateKeyState(keyValue, true);
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_KEYUP:
                            {
                                if (keysMapping.TryGetValue(e.key.keysym.sym, out ushort keyValue))
                                {
                                    vm.UpdateKeyState(keyValue, false);
                                }
                                break;
                            }
                    }
                }

                for (int i = 0; i < vm.Surface.GetLength(0); i++)
                {
                    for (int j = 0; j < vm.Surface.GetLength(1); j++)
                    {
                        if (!TryUpdatePixel(surface, i, j, vm.Surface[i, j]))
                        {
                            running = false;
                            Console.WriteLine($"Failed to update pixel {i} {j}");
                        }
                    }
                }


                //Update window.
                unsafe
                {
                    fixed (byte* surfacePtr = surface)
                    {
                        SDL.SDL_UpdateTexture(windowTexturePtr, IntPtr.Zero, (IntPtr)surfacePtr, pitch);
                        SDL.SDL_RenderCopy(rendererPtr, windowTexturePtr, IntPtr.Zero, IntPtr.Zero);
                        SDL.SDL_RenderPresent(rendererPtr);
                    }
                }

                DoFrameAutoResetEvent.WaitOne(200);
            }
        }

        public void Stop()
        {
            running = false;
            DoFrameAutoResetEvent.Set();
        }

        private bool TryUpdatePixel(byte[] surface, int x, int y, bool set = false)
        {
            bool result = true;
            var index = y * PixelConfig.PixelSize * pitch + x * PixelConfig.PixelSize * 4;
            if ((index + PixelConfig.PixelSize) >= surface.Length)
            {
                result = false;
            }
            else
            {
                for (int a = 0; a < PixelConfig.PixelSize; a++)
                {
                    int currentLineIndex = index + (a * pitch);
                    for (int b = 0; b < PixelConfig.PixelSize; b++)
                    {
                        int currentIndex = currentLineIndex + (b * 4);
                        surface[currentIndex] = 0; //blue
                        surface[currentIndex + 1] = 0;  //green
                        if (set)
                        {
                            surface[currentIndex + 2] = (byte)0xff;//red
                            surface[currentIndex + 3] = 0; //alpha
                        }
                        else
                        {
                            if (surface[currentIndex + 3] > 0)
                            {
                                surface[currentIndex + 3] = 0;
                                surface[currentIndex + 2] = 0;
                            }
                            else
                            {
                                surface[currentIndex + 3] = (byte)0x7f;
                            }
                        }

                    }
                }
            }
            return result;
        }
    }
}