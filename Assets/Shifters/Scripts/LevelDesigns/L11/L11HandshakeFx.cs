using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10: the ghost hands (Spikes reskin) shout "FIRM HANDSHAKE!" over whoever they just greeted.</summary>
    public sealed class L11HandshakeFx : MonoBehaviour
    {
        public Spikes[] Hands = new Spikes[0];
        int[] _hits;

        void Update()
        {
            if (Hands == null) return;
            if (_hits == null || _hits.Length != Hands.Length) _hits = new int[Hands.Length];
            for (int i = 0; i < Hands.Length; i++)
            {
                var h = Hands[i];
                if (h == null || h.Hits == _hits[i]) continue;
                _hits[i] = h.Hits;
                Fx.FloatingText(h.transform.position + Vector3.up * 2.4f, "FIRM HANDSHAKE!", L11Kit.GhostBlue, 1.1f);
                Sfx.At(SfxId.Bonk, h.transform.position, 0.8f, 1.2f);
            }
        }
    }
}
