// =====================================================================================
//  SHIFTERS - GetawayTramModel.cs   [owned by VISUALS_BOSSES]
//  The CH08 coin-sorting getaway tram: dressing for the kit MovingPlatform deck (4 x 7 m). Built by BossModels.GetawayTram
//  as a CHILD of the deck with its root at the deck top centre, front +Z. Railings are child BoxColliders (LEVELS2 CH08)
//  with a boarding gap in the middle of each long side; the front-left corner is kept clear for the CONSOLE terminal.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class GetawayTramModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Cab", "Windshield", "Headlight", "RailL", "RailR", "RailBack", "Bogies", "CoinHopper", "ConsoleSpot" };
        public override string[] RequiredParts => PartNames;

        public Transform Cab, Headlight, ConsoleSpot, Bogies;
        public BoxCollider[] RailColliders = new BoxCollider[0];
        public Vector2 DeckSize = new Vector2(4f, 7f);
        public Renderer HeadlightRenderer;
        public Material HeadlightOff, HeadlightOn;

        /// <summary>Headlight glow (on while the tram is powered / moving).</summary>
        public void SetHeadlight(bool on)
        {
            if (HeadlightRenderer != null && HeadlightOn != null && HeadlightOff != null) HeadlightRenderer.sharedMaterial = on ? HeadlightOn : HeadlightOff;
        }

        /// <summary>Wheel spin (degrees) for the bogie wheels (call with distance travelled * 57 / wheelRadius).</summary>
        public void SetWheelAngle(float degrees)
        {
            if (Bogies == null) return;
            for (int i = 0; i < Bogies.childCount; i++) Bogies.GetChild(i).localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
