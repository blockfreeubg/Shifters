// =====================================================================================
//  SHIFTERS - L9CoinRiver.cs   [LEVEL 9 - THE GETAWAY]
//  The COIN RIVER under the tram track (a FoamPit variant): anyone whose feet sink below SurfaceY inside Area is
//  bounced back onto the tram deck ("SPLASH! (OF COINS)"). Never Lost. Polled at 10 Hz (no trigger layers needed).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L9CoinRiver : MonoBehaviour
    {
        [Tooltip("World-space volume of the river (x/z used; y = below SurfaceY counts).")] public Bounds Area = new Bounds(Vector3.zero, new Vector3(30f, 8f, 140f));
        public float SurfaceY = -0.8f;
        public ChaseTrack Track;
        [Tooltip("Used when there is no tram (tests).")] public Vector3 FallbackPoint = new Vector3(0f, 0.2f, -4f);
        public int Splashes { get; private set; }
        float _poll;

        void FixedUpdate()
        {
            _poll -= Time.fixedDeltaTime;
            if (_poll > 0f) return;
            _poll = 0.1f;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || c.State == CharacterState.Lost || c.State == CharacterState.Captured) continue;
                var f = c.Feet;
                if (f.y > SurfaceY) continue;
                if (f.x < Area.min.x || f.x > Area.max.x || f.z < Area.min.z || f.z > Area.max.z) continue;
                Bounce(c, i);
            }
        }

        void Bounce(ShifterCharacter c, int slot)
        {
            Vector3 p; Quaternion r;
            var body = Track != null && Track.Tram != null && Track.Tram.Body != null ? Track.Tram.Body.transform : null;
            if (body != null)
            {
                p = body.TransformPoint(new Vector3((slot - 1) * 0.9f, 0.25f, -1.6f));
                r = body.rotation;
            }
            else { p = FallbackPoint; r = Quaternion.identity; }
            var splash = c.Feet; splash.y = SurfaceY - 0.3f;
            if (c.Motor != null) c.Motor.Teleport(p, r); else c.transform.SetPositionAndRotation(p, r);
            Splashes++;
            Fx.CoinShower(splash + Vector3.up * 0.4f, 36, 1.2f);
            Fx.Poof(p + Vector3.up, c.Color, 1.1f);
            Sfx.At(SfxId.Splash, splash, 1f);
            Sfx.At(SfxId.CoinShower, p, 0.7f);
            GameEvents.RaiseToast("SPLASH! (OF COINS)", UIColors.AccentYellow);
        }
    }
}
