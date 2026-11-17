// =====================================================================================
//  SHIFTERS - AudioTestTools.cs   [owned by the AUDIO agent - test helpers only]
//  16-bit WAV writer, autocorrelation pitch detector, seam/click metrics and a waveform +
//  log-frequency spectrogram PNG plotter used by AudioTests to sanity-check the synths.
// =====================================================================================
#if UNITY_EDITOR   // DevPlay test code: editor only (uses System.IO), never shipped in player/WebGL builds
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shifters
{
    public static class AudioTestTools
    {
        // ------------------------------------------------------------------ files

        /// <summary>Preview folder: $SP/audio_preview when running in a scratchpad clone, else &lt;project&gt;/Temp/audio_preview.</summary>
        public static string PreviewDir()
        {
            string data = Application.dataPath.Replace('\\', '/');
            int k = data.IndexOf("/clones/", StringComparison.Ordinal);
            string root = k > 0 ? Path.Combine(data.Substring(0, k), "audio_preview") : Path.Combine(data, "../Temp/audio_preview");
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>Writes interleaved float samples as a 16-bit PCM WAV (44.1 kHz).</summary>
        public static void WriteWav(string path, float[] data, int channels, int repeatToFrames = 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            int frames = data.Length / Math.Max(1, channels);
            int outFrames = repeatToFrames > frames ? repeatToFrames : frames;
            int bytes = outFrames * channels * 2;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + bytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(Synth.SampleRate);
                w.Write(Synth.SampleRate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(bytes);
                var buf = new byte[outFrames * channels * 2];
                int n = data.Length, o = 0;
                for (int i = 0; i < outFrames * channels; i++)
                {
                    float v = data[i % n];
                    int s = (int)Math.Round(Mathf.Clamp(v, -1f, 1f) * 32767f);
                    buf[o++] = (byte)(s & 0xFF); buf[o++] = (byte)((s >> 8) & 0xFF);
                }
                w.Write(buf);
            }
        }

        // ------------------------------------------------------------------ metrics

        public static float Mean(float[] b) { double s = 0; for (int i = 0; i < b.Length; i++) s += b[i]; return b.Length > 0 ? (float)(s / b.Length) : 0f; }

        /// <summary>Peak of the last ms milliseconds (a clip that ends loud clicks).</summary>
        public static float TailPeak(float[] b, float ms, int channels = 1)
        {
            int n = Math.Min(b.Length, Synth.Frames(ms / 1000f) * channels);
            float p = 0f;
            for (int i = b.Length - n; i < b.Length; i++) { float a = Math.Abs(b[i]); if (a > p) p = a; }
            return p;
        }

        /// <summary>Largest jump between adjacent frames of one channel (interior) and across the loop seam.</summary>
        public static void Jumps(float[] b, int channels, int ch, out float interior, out float seam)
        {
            int frames = b.Length / channels;
            interior = 0f;
            for (int i = 1; i < frames; i++)
            {
                float d = Math.Abs(b[i * channels + ch] - b[(i - 1) * channels + ch]);
                if (d > interior) interior = d;
            }
            seam = Math.Abs(b[ch] - b[(frames - 1) * channels + ch]);
        }

        /// <summary>Fundamental frequency (Hz) around frame center by normalized autocorrelation; 0 if unvoiced.</summary>
        public static float PitchAt(float[] b, int center, float minHz = 70f, float maxHz = 900f, int win = 2048)
        {
            int start = Math.Max(0, center - win / 2);
            int minLag = (int)(Synth.SampleRate / maxHz), maxLag = (int)(Synth.SampleRate / minHz);
            if (start + win + maxLag >= b.Length) start = b.Length - win - maxLag - 1;
            if (start < 0) return 0f;
            double e0 = 0;
            for (int i = 0; i < win; i++) e0 += b[start + i] * b[start + i];
            if (e0 < 1e-4) return 0f;
            var r = new float[maxLag + 1];
            float best = 0f;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double acc = 0, e1 = 0;
                for (int i = 0; i < win; i++) { float x = b[start + i], y = b[start + i + lag]; acc += x * y; e1 += y * y; }
                r[lag] = (float)(acc / Math.Sqrt(e0 * e1 + 1e-12));
                if (r[lag] > best) best = r[lag];
            }
            if (best < 0.35f) return 0f;
            // first local peak that is close to the best one (avoids octave-down errors)
            for (int lag = minLag + 1; lag < maxLag; lag++)
                if (r[lag] >= best * 0.88f && r[lag] >= r[lag - 1] && r[lag] >= r[lag + 1])
                    return Synth.SampleRate / (float)lag;
            return 0f;
        }

        /// <summary>Pitch at a fraction of the clip's loud region (where the envelope is above 20% of peak).</summary>
        public static float PitchAtFraction(float[] b, float frac)
        {
            float peak = AudioFx.Peak(b);
            int first = 0, last = b.Length - 1, hop = 256;
            float th = peak * 0.2f;
            for (int i = 0; i < b.Length; i += hop) if (Math.Abs(b[i]) > th || WindowPeak(b, i, hop) > th) { first = i; break; }
            for (int i = b.Length - 1; i > 0; i -= hop) if (WindowPeak(b, Math.Max(0, i - hop), hop) > th) { last = i; break; }
            int c = first + (int)((last - first) * frac);
            return PitchAt(b, c);
        }

        static float WindowPeak(float[] b, int s, int n) { float p = 0f; for (int i = s; i < Math.Min(b.Length, s + n); i++) { float a = Math.Abs(b[i]); if (a > p) p = a; } return p; }

        /// <summary>Median of the voiced pitch estimates across the clip.</summary>
        public static float MedianPitch(float[] b)
        {
            var list = new List<float>();
            for (int c = 1024; c < b.Length - 3000; c += 1024) { float p = PitchAt(b, c); if (p > 0f) list.Add(p); }
            if (list.Count == 0) return 0f;
            list.Sort();
            return list[list.Count / 2];
        }

        // ------------------------------------------------------------------ plots

        public struct PlotItem { public string Label; public float[] Data; public int Channels; public float Seconds; }

        /// <summary>Grid of tiles: waveform (top) + log-frequency spectrogram 50 Hz..12 kHz (bottom). Label drawn as a color bar.</summary>
        public static void PlotGrid(string path, List<PlotItem> items, int cols = 4, int tileW = 300, int tileH = 140)
        {
            int rows = (items.Count + cols - 1) / cols;
            int W = cols * tileW, H = rows * tileH;
            var px = new Color32[W * H];
            var bg = new Color32(14, 17, 22, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            for (int k = 0; k < items.Count; k++)
            {
                int ox = (k % cols) * tileW, oy = H - (k / cols + 1) * tileH;   // texture origin is bottom-left
                DrawTile(px, W, ox, oy, tileW, tileH, items[k], k);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static void DrawTile(Color32[] px, int W, int ox, int oy, int tw, int th, PlotItem it, int index)
        {
            int ch = Math.Max(1, it.Channels);
            var mono = it.Data;
            if (ch > 1)
            {
                int fr = it.Data.Length / ch;
                mono = new float[fr];
                for (int i = 0; i < fr; i++) { float s = 0f; for (int c = 0; c < ch; c++) s += it.Data[i * ch + c]; mono[i] = s / ch; }
            }
            int frames = it.Seconds > 0f ? Math.Min(mono.Length, Synth.Frames(it.Seconds)) : mono.Length;
            int pad = 4, w = tw - pad * 2;
            int waveH = (th - pad * 3) / 3, specH = th - pad * 3 - waveH;
            int wy = oy + pad + specH + pad, sy = oy + pad;
            // label color bar (hue by index) so tiles can be matched to the log order
            var lab = Color.HSVToRGB((index * 0.13f) % 1f, 0.7f, 1f);
            for (int x = 0; x < 10; x++) for (int y = 0; y < 3; y++) Put(px, W, ox + pad + x, oy + th - 2 - y, lab);
            // waveform (min/max per column)
            var waveCol = new Color32(255, 160, 60, 255);
            int mid = wy + waveH / 2;
            for (int x = 0; x < w; x++)
            {
                int a = (int)((long)x * frames / w), b = (int)((long)(x + 1) * frames / w);
                float mn = 0f, mx = 0f;
                for (int i = a; i < Math.Max(a + 1, b) && i < frames; i++) { if (mono[i] < mn) mn = mono[i]; if (mono[i] > mx) mx = mono[i]; }
                int y0 = mid + (int)(mn * waveH * 0.5f), y1 = mid + (int)(mx * waveH * 0.5f);
                for (int y = y0; y <= y1; y++) Put(px, W, ox + pad + x, y, waveCol);
                Put(px, W, ox + pad + x, wy + (int)(waveH * 0.5f + 0.891f * waveH * 0.5f), new Color32(60, 70, 85, 255));
            }
            // spectrogram (Goertzel at log-spaced bins)
            const int N = 1024;
            var hann = new float[N];
            for (int i = 0; i < N; i++) hann[i] = 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * i / (N - 1));
            var coef = new float[specH];
            for (int k = 0; k < specH; k++)
            {
                double f = 50.0 * Math.Pow(12000.0 / 50.0, k / (double)(specH - 1));
                coef[k] = (float)(2.0 * Math.Cos(2.0 * Math.PI * f / Synth.SampleRate));
            }
            for (int x = 0; x < w; x++)
            {
                int c = (int)((long)x * frames / w);
                int s0 = c - N / 2;
                for (int k = 0; k < specH; k++)
                {
                    float q1 = 0f, q2 = 0f, cf = coef[k];
                    for (int i = 0; i < N; i++)
                    {
                        int j = s0 + i;
                        float v = j >= 0 && j < frames ? mono[j] * hann[i] : 0f;
                        float q0 = cf * q1 - q2 + v;
                        q2 = q1; q1 = q0;
                    }
                    float mag = (float)Math.Sqrt(Math.Max(0f, q1 * q1 + q2 * q2 - q1 * q2 * cf)) / (N * 0.25f);
                    float db = 20f * (float)Math.Log10(mag + 1e-6f);
                    float t = Mathf.Clamp01((db + 70f) / 70f);
                    Put(px, W, ox + pad + x, sy + k, Heat(t));
                }
            }
        }

        static Color32 Heat(float t)
        {
            // black -> indigo -> magenta -> orange -> yellow-white
            Color a = new Color(0.03f, 0.03f, 0.08f), b = new Color(0.25f, 0.1f, 0.5f), c = new Color(0.85f, 0.2f, 0.45f), d = new Color(1f, 0.6f, 0.15f), e = new Color(1f, 1f, 0.75f);
            Color r = t < 0.25f ? Color.Lerp(a, b, t / 0.25f) : t < 0.5f ? Color.Lerp(b, c, (t - 0.25f) / 0.25f) : t < 0.75f ? Color.Lerp(c, d, (t - 0.5f) / 0.25f) : Color.Lerp(d, e, (t - 0.75f) / 0.25f);
            return r;
        }

        static void Put(Color32[] px, int W, int x, int y, Color32 c)
        {
            int i = y * W + x;
            if (x >= 0 && x < W && i >= 0 && i < px.Length) px[i] = c;
        }
    }
}
#endif
