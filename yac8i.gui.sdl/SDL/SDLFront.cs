//based on https://jsayers.dev/category/c-sdl-tutorial-series/
using System;
using SDL2;

namespace yac8i.gui.sdl
{
    public class SDLFront
    {
        private readonly Chip8VM vm;
        private SDLSound? sdlSound;
        private SDLDraw? sdlDraw;

        public SDLFront(Chip8VM vm)
        {
            this.vm = vm;
        }

        public void InitializeAndStart()
        {
            // Initilizes SDL.
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) < 0)
            {
                throw new Exception($"There was an issue initializing SDL. {SDL.SDL_GetError()}");
            }

            sdlSound = new SDLSound(vm);

            // Create a new window given a title, size, and passes it a flag indicating it should be shown.
            IntPtr windowPtr = SDL.SDL_CreateWindow("Yet another Chip8 Interpreter",
                                                SDL.SDL_WINDOWPOS_UNDEFINED,
                                                SDL.SDL_WINDOWPOS_UNDEFINED,
                                                PixelConfig.PixelWidth * PixelConfig.PixelSize, PixelConfig.PixelHeight * PixelConfig.PixelSize,
                                                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (windowPtr == IntPtr.Zero)
            {
                throw new Exception($"There was an issue creating the window. {SDL.SDL_GetError()}");
            }

            int pitch = -1;
            IntPtr windowTexturePtr = IntPtr.Zero;

            // Creates a new SDL hardware renderer. 
            IntPtr rendererPtr = SDL.SDL_CreateRenderer(windowPtr,
                                                  -1,
                                                  SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            if (rendererPtr == IntPtr.Zero)
            {
                throw new Exception($"There was an issue creating the renderer. {SDL.SDL_GetError()}");
            }
            uint pixelFormat = SDL.SDL_GetWindowPixelFormat(windowPtr);
            // Get window texture, so we can draw on it
            windowTexturePtr = SDL.SDL_CreateTexture(rendererPtr,
                                                    pixelFormat,
                                                     (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                                                     PixelConfig.PixelWidth * PixelConfig.PixelSize, PixelConfig.PixelHeight * PixelConfig.PixelSize);
            if (windowTexturePtr == IntPtr.Zero)
            {
                throw new Exception($"There was an issue reading window texture.  {SDL.SDL_GetError()}");
            }

            pitch = SDL.SDL_BYTESPERPIXEL(pixelFormat) * PixelConfig.PixelWidth * PixelConfig.PixelSize;

            if (pitch < 0)
            {
                throw new Exception($"There was an issue reading pitch.  {SDL.SDL_GetError()}");
            }

            vm.Tick += OnTick;
            sdlDraw = new SDLDraw(pitch, windowTexturePtr, rendererPtr, vm);
            // Start main drawing loop
            sdlDraw.Run();


            // Clean up the resources that were created.
            sdlSound.Dispose();
            vm.Tick -= OnTick;
            SDL.SDL_DestroyRenderer(rendererPtr);
            SDL.SDL_DestroyWindow(windowPtr);
            SDL.SDL_Quit();
        }

        public void Stop()
        {
            vm.Tick -= OnTick;
            sdlDraw?.Stop();
        }

        private void OnTick(object? sender, EventArgs ags)
        {
            sdlDraw?.DoFrame();
        }
    }
}
