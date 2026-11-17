using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10 GUN bonus: the hidden bell on the mausoleum roof. Each hit makes every ghost wave and sing a
    /// barbershop chord (three Boo samples a major triad apart) + a floating lyric. Purely a bonus, nothing gates on it.</summary>
    public sealed class L11BellChord : MonoBehaviour
    {
        public ShootTarget Bell;
        public GhostTrio[] Trios = new GhostTrio[0];
        public int Rings { get; private set; }
        int _lastHits;
        float _next;
        static readonly string[] Lyrics = { "~ TEEEAM-WOOORK ~", "~ WE'RE A QUAAARTET ~", "~ NOBODY SINGS ALOOONE ~" };

        void Update()
        {
            if (Bell == null) return;
            if (Bell.Hits == _lastHits) return;
            _lastHits = Bell.Hits;
            if (Time.time < _next) return;
            _next = Time.time + 2.5f;
            Vector3 p = Bell.transform.position;
            Sfx.At(SfxId.Cymbal, p, 0.7f, 1.4f);
            Sfx.At(SfxId.Boo, p, 0.8f, 1.0f);
            Sfx.At(SfxId.Boo, p, 0.7f, 1.26f);
            Sfx.At(SfxId.Boo, p, 0.7f, 1.5f);
            foreach (var t in Trios) if (t != null) t.Wave();
            Fx.FloatingText(p + Vector3.up * 0.8f, Lyrics[Rings % Lyrics.Length], L11Kit.GhostBlue, 1.4f);
            if (Rings == 0) GameEvents.RaiseToast("BONUS: THE GHOSTS OF TEAMS PAST SING IN FOUR-PART HARMONY.");
            Rings++;
        }
    }
}
