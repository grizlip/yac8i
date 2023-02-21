//based on https://jsayers.dev/category/c-sdl-tutorial-series/
using System.Collections.Generic;
using System;
using SDL2;

namespace yac8i.gui.sdl
{
    public class SDLFront : IDisposable
    {
        private Chip8VM vm;

        private SDLAudio sdlAudio;
        private SDLVideo sdlVideo;

        public SDLFront(Chip8VM vm)
        {
            this.vm = vm;
            sdlAudio = new SDLAudio(vm);
        }

        public bool Initialize()
        {
            if (SDL.SDL_WasInit(0) == 0)
            {
                // Initializes SDL.
                if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) <0)
                {
                    Console.WriteLine($"There was an issue initializing SDL. {SDL.SDL_GetError()}");
                    return false;
                }

                // Enable drop of file            
                SDL.SDL_EventState(SDL.SDL_EventType.SDL_DROPFILE, SDL.SDL_ENABLE);
                // Create a new window given a title, size, and passes it a flag indicating it should be shown.
                IntPtr windowPtr = SDL.SDL_CreateWindow("Yet another Chip8 Interpreter",
                                                        SDL.SDL_WINDOWPOS_UNDEFINED,
                                                        SDL.SDL_WINDOWPOS_UNDEFINED,
                                                        64 * SDLVideo.PIXEL_SIZE, 32 * SDLVideo.PIXEL_SIZE,
                                                        SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

                if (windowPtr == IntPtr.Zero)
                {
                    Console.WriteLine($"There was an issue creating the window. {SDL.SDL_GetError()}");
                    return false;
                }

                // Creates a new SDL hardware renderer. 
                var rendererPtr = SDL.SDL_CreateRenderer(windowPtr,
                                                      -1,
                                                      SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

                if (rendererPtr == IntPtr.Zero)
                {
                    Console.WriteLine($"There was an issue creating the renderer. {SDL.SDL_GetError()}");
                    return false;
                }
                sdlVideo = new SDLVideo(vm, windowPtr, rendererPtr);
                return sdlVideo.Initialize();
            }
            return false;
        }

        public List<string> GetAudioDevices()
        {
            return sdlAudio.GetAudioDevices();
        }

        public void ChooseAudioDevice(string name)
        {
            sdlAudio.SetupAudio(name);
        }

        public void Start()
        {
            sdlVideo.MainLoop();
        }

        public void Dispose()
        {
            sdlAudio.Dispose();
            sdlVideo.Dispose();
            SDL.SDL_Quit();
        }
    }
}