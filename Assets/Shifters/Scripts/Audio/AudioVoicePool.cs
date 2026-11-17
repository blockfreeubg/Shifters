// =====================================================================================
//  SHIFTERS - AudioVoicePool.cs   [owned by the AUDIO agent]
//  Pooled AudioSources: 3D world voices (custom rolloff, min 2 m / max 40 m, no doppler)
//  and 2D interface voices (ignore listener pause). Voice stealing by importance then age,
//  follow-a-transform voices, fades, pause-aware lifetimes. No per-frame allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>One pooled playback slot.</summary>
    public sealed class AudioVoice
    {
        public AudioSource Src;
        public Transform Tr;
        public bool Is3D, Busy, Loop, UI;
        public int Id;                    // (int)SfxId, or -1 for voices
        public int Importance;
        public float Start, End;
        public float BaseVolume, BasePitch;
        public Transform Follow;
        public Vector3 FollowOffset;
        public object Owner;              // CharacterProfile for voice lines
        public int OwnerPriority;
        public VoiceLine Line;
        public int Handle;
        public float Fade = 1f, FadeFrom, FadeTo, FadeT, FadeDur;
        public bool StopAfterFade;
    }

    public sealed class AudioVoicePool
    {
        public const float MinDistance = 2f, MaxDistance = 40f;

        public readonly AudioVoice[] Voices3D;
        public readonly AudioVoice[] Voices2D;
        public int Stolen { get; private set; }
        public int MaxActive3D { get; private set; }
        public int MaxActive2D { get; private set; }
        int _handle;
        readonly AnimationCurve _rolloff;

        public AudioVoicePool(Transform parent, int count3D, int count2D)
        {
            // normalized distance (d / MaxDistance) -> gain: flat to 2 m, gentle falloff, silent at 40 m
            _rolloff = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(MinDistance / MaxDistance, 1f), new Keyframe(0.15f, 0.8f),
                new Keyframe(0.3f, 0.5f), new Keyframe(0.5f, 0.25f), new Keyframe(0.75f, 0.08f), new Keyframe(1f, 0f));
            Voices3D = new AudioVoice[count3D];
            Voices2D = new AudioVoice[count2D];
            for (int i = 0; i < count3D; i++) Voices3D[i] = Make(parent, "Voice3D_" + i, true);
            for (int i = 0; i < count2D; i++) Voices2D[i] = Make(parent, "Voice2D_" + i, false);
        }

        AudioVoice Make(Transform parent, string name, bool is3D)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.dopplerLevel = 0f;
            if (is3D)
            {
                src.spatialBlend = 1f;
                src.minDistance = MinDistance;
                src.maxDistance = MaxDistance;
                if (Application.platform == RuntimePlatform.WebGLPlayer) src.rolloffMode = AudioRolloffMode.Logarithmic;   // WebGL panner: no custom curves
                else
                {
                    src.rolloffMode = AudioRolloffMode.Custom;
                    src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _rolloff);
                }
                src.spread = 40f;
            }
            else
            {
                src.spatialBlend = 0f;
                src.ignoreListenerPause = true;
                src.bypassReverbZones = true;
            }
            return new AudioVoice { Src = src, Tr = go.transform, Is3D = is3D };
        }

        public int NewHandle() { _handle++; if (_handle <= 0) _handle = 1; return _handle; }

        /// <summary>Free slot, or steals the least important / oldest one not more important than importance.</summary>
        public AudioVoice Acquire(bool is3D, int importance)
        {
            var arr = is3D ? Voices3D : Voices2D;
            AudioVoice best = null;
            for (int i = 0; i < arr.Length; i++)
            {
                var v = arr[i];
                if (!v.Busy) return v;
                if (v.Importance > importance) continue;
                if (best == null || v.Importance < best.Importance || (v.Importance == best.Importance && v.Start < best.Start)) best = v;
            }
            if (best != null) { Release(best); Stolen++; }
            return best;
        }

        public int CountActive(int id)
        {
            int n = 0;
            for (int i = 0; i < Voices3D.Length; i++) if (Voices3D[i].Busy && Voices3D[i].Id == id) n++;
            for (int i = 0; i < Voices2D.Length; i++) if (Voices2D[i].Busy && Voices2D[i].Id == id) n++;
            return n;
        }

        public int CountBusy(bool is3D)
        {
            var arr = is3D ? Voices3D : Voices2D;
            int n = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i].Busy) n++;
            return n;
        }

        /// <summary>Newest (newest=true) or oldest busy voice of an id.</summary>
        public AudioVoice FindById(int id, bool newest)
        {
            AudioVoice r = null;
            for (int k = 0; k < 2; k++)
            {
                var arr = k == 0 ? Voices3D : Voices2D;
                for (int i = 0; i < arr.Length; i++)
                {
                    var v = arr[i];
                    if (!v.Busy || v.Id != id) continue;
                    if (r == null || (newest ? v.Start > r.Start : v.Start < r.Start)) r = v;
                }
            }
            return r;
        }

        public AudioVoice FindOwner(object owner)
        {
            if (owner == null) return null;
            for (int i = 0; i < Voices3D.Length; i++) if (Voices3D[i].Busy && ReferenceEquals(Voices3D[i].Owner, owner)) return Voices3D[i];
            for (int i = 0; i < Voices2D.Length; i++) if (Voices2D[i].Busy && ReferenceEquals(Voices2D[i].Owner, owner)) return Voices2D[i];
            return null;
        }

        public AudioVoice FindHandle(int handle)
        {
            if (handle <= 0) return null;
            for (int i = 0; i < Voices3D.Length; i++) if (Voices3D[i].Busy && Voices3D[i].Handle == handle) return Voices3D[i];
            for (int i = 0; i < Voices2D.Length; i++) if (Voices2D[i].Busy && Voices2D[i].Handle == handle) return Voices2D[i];
            return null;
        }

        public void Release(AudioVoice v)
        {
            if (v == null) return;
            if (v.Src != null) { v.Src.Stop(); v.Src.clip = null; }
            v.Busy = false; v.Loop = false; v.Follow = null; v.Owner = null; v.Handle = 0; v.FadeDur = 0f; v.StopAfterFade = false;
        }

        public void FadeOut(AudioVoice v, float seconds)
        {
            if (v == null || !v.Busy) return;
            if (seconds <= 0f) { Release(v); return; }
            v.FadeFrom = v.Fade; v.FadeTo = 0f; v.FadeT = 0f; v.FadeDur = seconds; v.StopAfterFade = true;
        }

        public void StopAll()
        {
            for (int i = 0; i < Voices3D.Length; i++) if (Voices3D[i].Busy) Release(Voices3D[i]);
            for (int i = 0; i < Voices2D.Length; i++) if (Voices2D[i].Busy) Release(Voices2D[i]);
        }

        /// <summary>Per-frame update (unscaled time).</summary>
        public void Tick(float now, float dt, bool paused, float worldPitch, float sfxGain)
        {
            int b3 = Tick(Voices3D, now, dt, paused, worldPitch, sfxGain);
            int b2 = Tick(Voices2D, now, dt, paused, worldPitch, sfxGain);
            if (b3 > MaxActive3D) MaxActive3D = b3;
            if (b2 > MaxActive2D) MaxActive2D = b2;
        }

        int Tick(AudioVoice[] arr, float now, float dt, bool paused, float worldPitch, float sfxGain)
        {
            int busy = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                var v = arr[i];
                if (!v.Busy) continue;
                if (v.Src == null) { v.Busy = false; continue; }
                if (v.Follow != null)
                {
                    if (v.Follow) v.Tr.position = v.Follow.position + v.FollowOffset;
                    else v.Follow = null;
                }
                if (v.FadeDur > 0f)
                {
                    v.FadeT += dt;
                    float u = Mathf.Clamp01(v.FadeT / v.FadeDur);
                    v.Fade = Mathf.Lerp(v.FadeFrom, v.FadeTo, u);
                    if (u >= 1f)
                    {
                        v.FadeDur = 0f;
                        if (v.StopAfterFade) { Release(v); continue; }
                    }
                }
                if (paused && v.Is3D && !v.UI) v.End += dt;
                if (!v.Loop && now >= v.End) { Release(v); continue; }
                v.Src.volume = v.BaseVolume * v.Fade * sfxGain;
                if (v.Is3D && !v.UI) v.Src.pitch = v.BasePitch * worldPitch;
                busy++;
            }
            return busy;
        }
    }
}
