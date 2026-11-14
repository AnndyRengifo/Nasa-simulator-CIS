using System;
using UnityEngine;

namespace Sol3.Game.Audio
{
    /// <summary>
    /// Sintetizador de la música y los efectos del juego (todo original, generado por código).
    /// La música se compone de pads, bajo y arpegios sobre progresiones de acordes, en bucle sin cortes.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>Tema en bucle. chords: notas MIDI por acorde; cada acorde dura barSeconds.</summary>
        public static AudioClip Music(string name, int[][] chords, float barSeconds, float arpStep, float brightness, float volume, int seed)
        {
            int length = Mathf.RoundToInt(chords.Length * barSeconds * Rate);
            var buffer = new float[length];
            var rng = new System.Random(seed);

            for (int c = 0; c < chords.Length; c++)
            {
                int start = Mathf.RoundToInt(c * barSeconds * Rate);
                int[] chord = chords[c];

                // Pad: tres voces ligeramente desafinadas con ataque y relajación largos (se solapan en el bucle).
                foreach (int note in chord)
                {
                    float f = Midi(note);
                    int dur = Mathf.RoundToInt((barSeconds + 1.6f) * Rate);
                    for (int i = 0; i < dur; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Clamp01(t / 1.4f) * Mathf.Clamp01((barSeconds + 1.6f - t) / 1.6f);
                        float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * f * 1.004f * t + 1.3f)
                                  + brightness * 0.25f * Mathf.Sin(4f * Mathf.PI * f * t);
                        buffer[(start + i) % length] += s * env * 0.045f;
                    }
                }

                // Bajo: fundamental una octava abajo, dos golpes suaves por compás.
                float bassF = Midi(chord[0] - 12);
                for (int hit = 0; hit < 2; hit++)
                {
                    int bStart = start + Mathf.RoundToInt(hit * barSeconds * 0.5f * Rate);
                    int dur = Mathf.RoundToInt(barSeconds * 0.5f * Rate);
                    for (int i = 0; i < dur; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 1.6f);
                        buffer[(bStart + i) % length] += Mathf.Sin(2f * Mathf.PI * bassF * t) * env * 0.11f;
                    }
                }

                // Arpegio pulsado (campanillas suaves).
                int steps = Mathf.RoundToInt(barSeconds / arpStep);
                for (int k = 0; k < steps; k++)
                {
                    if (rng.NextDouble() < 0.18)
                        continue;
                    int note = chord[k % chord.Length] + 12 * (k % 4 == 3 ? 2 : 1);
                    float f = Midi(note);
                    int aStart = start + Mathf.RoundToInt(k * arpStep * Rate);
                    int dur = Mathf.RoundToInt(1.4f * Rate);
                    for (int i = 0; i < dur; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 3.2f);
                        float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) * Mathf.Exp(-t * 6f);
                        buffer[(aStart + i) % length] += s * env * 0.05f * brightness;
                    }
                }
            }

            Normalize(buffer, volume);
            var clip = AudioClip.Create(name, length, 1, Rate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        public static AudioClip DayTheme() => Music("SOL3_Day", new[]
        {
            new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55, 60 }, new[] { 55, 59, 62 },
            new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 50, 53, 57 }, new[] { 52, 56, 59 },
        }, 4f, 0.5f, 1f, 0.55f, 7);

        public static AudioClip NightTheme() => Music("SOL3_Night", new[]
        {
            new[] { 50, 53, 57 }, new[] { 46, 50, 53 }, new[] { 41, 45, 48 }, new[] { 45, 49, 52 },
            new[] { 50, 53, 57 }, new[] { 43, 46, 50 }, new[] { 46, 50, 53 }, new[] { 45, 49, 52 },
        }, 5f, 1f, 0.6f, 0.45f, 13);

        /// <summary>Ambiente de estación: zumbido grave de máquinas y ventilación.</summary>
        public static AudioClip StationHum()
        {
            int length = Rate * 4;
            var buffer = new float[length];
            var rng = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.02f;
                buffer[i] = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.12f + lp * 0.9f;
            }
            Normalize(buffer, 0.35f);
            return Clip("SOL3_Hum", buffer);
        }

        public static AudioClip Tones(string name, float[] frequencies, float noteSeconds, float decay, float volume)
        {
            int noteLength = Mathf.RoundToInt(noteSeconds * Rate);
            var buffer = new float[noteLength * frequencies.Length + Rate / 4];
            for (int n = 0; n < frequencies.Length; n++)
            {
                for (int i = 0; i < noteLength + Rate / 4; i++)
                {
                    int index = n * noteLength + i;
                    if (index >= buffer.Length)
                        break;
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t * decay);
                    buffer[index] += (Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * frequencies[n] * t)) * env;
                }
            }
            Normalize(buffer, volume);
            return Clip(name, buffer);
        }

        public static AudioClip Noise(string name, float seconds, float lowpass, float decay, float volume, int seed)
        {
            var buffer = new float[Mathf.RoundToInt(seconds * Rate)];
            var rng = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * lowpass;
                buffer[i] = lp * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * decay);
            }
            Normalize(buffer, volume);
            return Clip(name, buffer);
        }

        public static AudioClip Sweep(string name, float from, float to, float seconds, float volume)
        {
            var buffer = new float[Mathf.RoundToInt(seconds * Rate)];
            float phase = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)buffer.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, t * t) / Rate;
                buffer[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t);
            }
            Normalize(buffer, volume);
            return Clip(name, buffer);
        }

        static AudioClip Clip(string name, float[] buffer)
        {
            var clip = AudioClip.Create(name, buffer.Length, 1, Rate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        static void Normalize(float[] buffer, float peak)
        {
            float max = 0.0001f;
            foreach (float v in buffer)
                max = Math.Max(max, Math.Abs(v));
            float gain = peak / max;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= gain;
        }
    }
}
