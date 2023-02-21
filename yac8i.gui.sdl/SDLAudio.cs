using System;
using SDL2;
using System.Collections.Generic;

namespace yac8i.gui.sdl
{
    public class SDLAudio : IDisposable
    {
        private static double samplingIndex = 0;
        private const double AMPLITUDE = 28000d;
        private const double SOUND_FREQUENCY = 261.63d;
        private ushort samples;
        private int frequency;
        private byte channels;
        private uint soundDeviceId;
        private Chip8VM vm;

        public SDLAudio(Chip8VM vm)
        {
            this.vm = vm;
        }

        public List<string> GetAudioDevices()
        {
            List<string> audioDevices = new List<string>();
            if (SDL.SDL_WasInit(0) > 0)
            {
                int audioDevicesCount = SDL.SDL_GetNumAudioDevices(0);
                for (int i = 0; i < audioDevicesCount; i++)
                {
                    audioDevices.Add(SDL.SDL_GetAudioDeviceName(i, 0));
                }
            }
            return audioDevices;
        }

        public void SetupAudio(string deviceName)
        {

            if (SDL.SDL_WasInit(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) > 0)
            {
                SDL.SDL_AudioSpec want = new SDL.SDL_AudioSpec();
                want.freq = 44100;
                want.format = SDL.AUDIO_S16SYS;
                want.channels = 1;
                want.samples = 512;
                want.callback = AudioCallback;

                soundDeviceId = SDL.SDL_OpenAudioDevice(deviceName, 0, ref want, out SDL.SDL_AudioSpec have, 0);

                if (soundDeviceId == 0)
                {
                    Console.WriteLine($"Failed to open audio: {SDL.SDL_GetError()}");
                }
                else if (want.format != have.format)
                {
                    Console.WriteLine($"Failed to get the desired AudioSpec. Instead got:");
                    Console.WriteLine($"frequency: {have.freq}");
                    Console.WriteLine($"format: {have.format}");
                    Console.WriteLine($"channels: {have.channels}");
                    Console.WriteLine($"samples: {have.samples}");
                    Console.WriteLine($"sizes: {have.size}");
                }
                else
                {
                    samples = have.samples;
                    channels = have.channels;
                    frequency = have.freq;
                    vm.BeepStatus += OnBeepStatusChanged;
                }
            }
        }

        public void Dispose()
        {
            vm.BeepStatus -= OnBeepStatusChanged;
            SDL.SDL_CloseAudioDevice(soundDeviceId);
        }

        private void OnBeepStatusChanged(object? sender, bool status)
        {
            SDL.SDL_PauseAudioDevice(soundDeviceId, status ? 0 : 1);
        }

        private void AudioCallback(IntPtr userdata, IntPtr stream, int lenInBytes)
        {
            Span<short> streamSpan;
            unsafe
            {
                short* buffer = (short*)stream.ToPointer();
                streamSpan = new Span<short>(buffer, lenInBytes / sizeof(short));
            }
            for (int sample = 0; sample < samples; sample++)
            {
                short data = GetData(sample);

                for (int channelId = 0; channelId < channels; channelId++)
                {
                    int offset = (sample * channels) + channelId;
                    streamSpan[offset] = data;
                }
            }


            short GetData(int sample)
            {
                short result = (short)(AMPLITUDE * Math.Sin(samplingIndex));
                samplingIndex += (2.0d * Math.PI * SOUND_FREQUENCY) / frequency;
                //we want to wrap around after we make full circle
                if (samplingIndex >= (2.0d * Math.PI))
                {
                    samplingIndex -= 2.0d * Math.PI;
                }
                return result;

            }
        }
    }
}
