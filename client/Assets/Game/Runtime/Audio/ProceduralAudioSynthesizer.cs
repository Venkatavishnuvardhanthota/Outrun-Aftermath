using System;
using UnityEngine;

namespace OutrunAftermath.Client.Runtime.Audio
{
    /// <summary>
    /// High-performance mathematical PCM audio synthesizer for OUTRUN: AFTERMATH.
    /// Generates spatialized, authentic tactical sound effects, weapon cracks, hit markers,
    /// vehicle engine harmonics, explosions, and environmental ambiance at runtime without external asset dependencies.
    /// </summary>
    public static class ProceduralAudioSynthesizer
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// Generates a realistic gunshot audio clip with transient crack, barrel expansion, and tail.
        /// </summary>
        public static AudioClip CreateGunshotClip(string weaponType)
        {
            weaponType = (weaponType ?? "rifle").ToLowerInvariant();
            float duration;
            float startFreq;
            float endFreq;
            float noiseRatio;

            switch (weaponType)
            {
                case "smg":
                    duration = 0.10f;
                    startFreq = 340f;
                    endFreq = 80f;
                    noiseRatio = 0.55f;
                    break;
                case "sniper":
                    duration = 0.45f;
                    startFreq = 180f;
                    endFreq = 36f;
                    noiseRatio = 0.70f;
                    break;
                case "shotgun":
                    duration = 0.32f;
                    startFreq = 220f;
                    endFreq = 45f;
                    noiseRatio = 0.85f;
                    break;
                case "turret":
                    duration = 0.16f;
                    startFreq = 260f;
                    endFreq = 50f;
                    noiseRatio = 0.60f;
                    break;
                case "pistol":
                    duration = 0.11f;
                    startFreq = 300f;
                    endFreq = 95f;
                    noiseRatio = 0.50f;
                    break;
                case "rifle":
                default:
                    duration = 0.18f;
                    startFreq = 260f;
                    endFreq = 55f;
                    noiseRatio = 0.65f;
                    break;
            }

            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random(weaponType.GetHashCode());

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;

                // Exponential decay envelope
                float env = Mathf.Exp(-progress * 9f);

                // Instant attack crack (first 4ms)
                float crack = 0f;
                if (progress < 0.05f)
                {
                    crack = ((float)rng.NextDouble() * 2f - 1f) * 1.5f;
                }

                // Pitch drop sine
                float currentFreq = Mathf.Lerp(startFreq, endFreq, progress);
                float tone = Mathf.Sin(2f * Mathf.PI * currentFreq * t);

                // Noise burst
                float noise = ((float)rng.NextDouble() * 2f - 1f);

                // Combine layers
                float sample = ((1f - noiseRatio) * tone + noiseRatio * noise) * env + crack * env;
                samples[i] = Mathf.Clamp(sample * 0.95f, -1f, 1f);
            }

            var clip = AudioClip.Create($"Gunshot_{weaponType}", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates hit confirmation audio cues (flesh thud, armor clink, or headshot ding).
        /// </summary>
        public static AudioClip CreateHitMarkerClip(bool headshot, bool armor)
        {
            float duration = headshot ? 0.15f : (armor ? 0.08f : 0.05f);
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;
                float env = Mathf.Exp(-progress * (headshot ? 7f : 16f));

                float val;
                if (headshot)
                {
                    // Harmonic bell chime (3200Hz + 4800Hz)
                    val = 0.6f * Mathf.Sin(2f * Mathf.PI * 3200f * t) +
                          0.4f * Mathf.Sin(2f * Mathf.PI * 4800f * t);
                }
                else if (armor)
                {
                    // Metallic ping (2400Hz + slight dissonance)
                    val = 0.7f * Mathf.Sin(2f * Mathf.PI * 2400f * t) +
                          0.3f * Mathf.Sin(2f * Mathf.PI * 3650f * t);
                }
                else
                {
                    // Organic body hit thud (650Hz -> 200Hz drop)
                    float freq = Mathf.Lerp(650f, 200f, progress);
                    val = Mathf.Sin(2f * Mathf.PI * freq * t);
                }

                samples[i] = Mathf.Clamp(val * env * 0.9f, -1f, 1f);
            }

            string name = headshot ? "Hit_Headshot" : (armor ? "Hit_Armor" : "Hit_Body");
            var clip = AudioClip.Create(name, numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates an explosive blast wave audio clip with sub-bass punch and reverberant rumble.
        /// </summary>
        public static AudioClip CreateExplosionClip()
        {
            float duration = 0.85f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random(42);

            float lastNoise = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;
                float env = Mathf.Exp(-progress * 4.5f);

                // Sub-bass thump (55Hz -> 25Hz)
                float subFreq = Mathf.Lerp(55f, 25f, progress);
                float sub = Mathf.Sin(2f * Mathf.PI * subFreq * t);

                // Low-pass filtered chaotic noise
                float white = (float)rng.NextDouble() * 2f - 1f;
                lastNoise = Mathf.Lerp(lastNoise, white, 0.18f);

                float sample = (sub * 0.55f + lastNoise * 0.45f) * env;
                samples[i] = Mathf.Clamp(sample * 1.1f, -1f, 1f);
            }

            var clip = AudioClip.Create("Explosion_Blast", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates tactical weapon reload mechanical clicks (mag out, mag in, slide rack).
        /// </summary>
        public static AudioClip CreateReloadClip(int part)
        {
            float duration = 0.08f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];

            float freq = part == 0 ? 1100f : (part == 1 ? 1650f : 2100f);
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;
                float env = Mathf.Exp(-progress * 25f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                samples[i] = Mathf.Clamp(tone * env * 0.85f, -1f, 1f);
            }

            var clip = AudioClip.Create($"Reload_Part_{part}", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates footstep sound impulse for tactical movement.
        /// </summary>
        public static AudioClip CreateFootstepClip(string surface = "pavement")
        {
            float duration = 0.06f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random();

            float last = 0f;
            float filter = surface == "metal" ? 0.4f : (surface == "dirt" ? 0.1f : 0.22f);
            for (int i = 0; i < numSamples; i++)
            {
                float progress = (float)i / numSamples;
                float env = Mathf.Exp(-progress * 30f);
                float white = (float)rng.NextDouble() * 2f - 1f;
                last = Mathf.Lerp(last, white, filter);
                samples[i] = Mathf.Clamp(last * env * 0.8f, -1f, 1f);
            }

            var clip = AudioClip.Create($"Footstep_{surface}", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates seamless multi-cylinder combustion pulse loop for vehicle engines.
        /// Pitch is modulated at runtime proportional to vehicle speed.
        /// </summary>
        public static AudioClip CreateVehicleEngineLoopClip()
        {
            float duration = 0.50f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random(1337);

            float baseFreq = 42f; // ~1260 RPM idle
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;

                // Cylinder firing pulses (harmonics 1x, 2x, 4x)
                float pulse = 0.55f * Mathf.Sin(2f * Mathf.PI * baseFreq * t) +
                              0.30f * Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t) +
                              0.15f * Mathf.Sin(2f * Mathf.PI * baseFreq * 4f * t);

                // Subtle mechanical exhaust rasp
                float rasp = ((float)rng.NextDouble() * 2f - 1f) * 0.12f;

                samples[i] = Mathf.Clamp((pulse + rasp) * 0.7f, -1f, 1f);
            }

            var clip = AudioClip.Create("Vehicle_Engine_Loop", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates high-pressure nitro boost hiss and turbine roar loop.
        /// </summary>
        public static AudioClip CreateNitroBoostClip()
        {
            float duration = 0.60f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random(77);

            float last = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                last = Mathf.Lerp(last, white, 0.4f);

                // High turbine whine
                float whine = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.25f;

                samples[i] = Mathf.Clamp((last * 0.75f + whine) * 0.85f, -1f, 1f);
            }

            var clip = AudioClip.Create("Nitro_Boost_Loop", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates tire screech friction noise during handbrake drifts.
        /// </summary>
        public static AudioClip CreateTireScreechClip()
        {
            float duration = 0.45f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random(99);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;
                float env = Mathf.Sin(progress * Mathf.PI);

                // Friction noise modulated by 950Hz squeal
                float squeal = Mathf.Sin(2f * Mathf.PI * 980f * t + Mathf.Sin(2f * Mathf.PI * 45f * t) * 3f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.4f;

                samples[i] = Mathf.Clamp((squeal * 0.6f + noise) * env * 0.75f, -1f, 1f);
            }

            var clip = AudioClip.Create("Tire_Screech", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates down-state heartbeat pulse ("lub-dub").
        /// </summary>
        public static AudioClip CreateHeartbeatClip()
        {
            float duration = 0.40f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float val = 0f;

                // First pulse at t=0
                if (t < 0.15f)
                {
                    float p = t / 0.15f;
                    val += Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-p * 12f);
                }
                // Second pulse at t=0.18
                else if (t >= 0.18f && t < 0.35f)
                {
                    float t2 = t - 0.18f;
                    float p = t2 / 0.17f;
                    val += Mathf.Sin(2f * Mathf.PI * 52f * t2) * Mathf.Exp(-p * 12f) * 0.85f;
                }

                samples[i] = Mathf.Clamp(val * 0.9f, -1f, 1f);
            }

            var clip = AudioClip.Create("Heartbeat_Pulse", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates match end victory/defeat musical chord stingers.
        /// </summary>
        public static AudioClip CreateStingerClip(bool victory)
        {
            float duration = victory ? 0.70f : 0.85f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];

            // Triad chord frequencies
            float f1 = victory ? 523.25f : 440f;    // C5 or A4
            float f2 = victory ? 659.25f : 523.25f; // E5 or C5
            float f3 = victory ? 783.99f : 622.25f; // G5 or Eb5

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;
                float env = Mathf.Exp(-progress * 3.5f);

                float chord = (Mathf.Sin(2f * Mathf.PI * f1 * t) +
                               Mathf.Sin(2f * Mathf.PI * f2 * t) +
                               Mathf.Sin(2f * Mathf.PI * f3 * t)) / 3f;

                samples[i] = Mathf.Clamp(chord * env * 0.85f, -1f, 1f);
            }

            var clip = AudioClip.Create(victory ? "Stinger_Victory" : "Stinger_Defeat", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates environmental wind and atmospheric ambiance loop.
        /// </summary>
        public static AudioClip CreateAmbianceLoopClip(string weatherType)
        {
            float duration = 1.8f;
            int numSamples = (int)(SampleRate * duration);
            float[] samples = new float[numSamples];
            var rng = new System.Random(555);

            float last = 0f;
            float filter = weatherType == "sandstorm" ? 0.25f : (weatherType == "toxicrain" ? 0.35f : 0.08f);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                last = Mathf.Lerp(last, white, filter);

                // Low-frequency atmospheric swell
                float swell = (Mathf.Sin(2f * Mathf.PI * 0.4f * t) + 1f) * 0.5f;

                samples[i] = Mathf.Clamp(last * (0.35f + 0.65f * swell) * 0.5f, -1f, 1f);
            }

            var clip = AudioClip.Create($"Ambiance_{weatherType}", numSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
