// =====================================================================================
//  SHIFTERS - BossModelsBank.cs   [owned by VISUALS_BOSSES]
//  BossModels.Porkchop (CH08 boss) and BossModels.GetawayTram (CH08 chase tram dressing).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class BossModels
    {
        internal static readonly Color PigPink = new Color(0.95f, 0.62f, 0.68f);

        /// <summary>PORKCHOP: 3.5 m armored robo-piggy-bank (body 3.1 m wide, 4.4 m long incl. snout). Root on the floor
        /// centre, snout +Z. No colliders (bodyCollider = true adds one BoxCollider on the root for a kinematic brain).
        /// Weak-point anchors: BellyPlug (-X flank, 1.2 m up, its +Z = outward normal), Snout (front), EarPort (right ear).</summary>
        public static PorkchopModel Porkchop(Transform parent, Vector3 pos, Quaternion? rot = null, bool bodyCollider = false)
        {
            var go = Root("Porkchop", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<PorkchopModel>();
            var pink = C(PigPink, 0.55f);
            var steel = C(Steel, 0.55f);
            var dark = C(Ink, 0.4f);

            var body = Node("Body", t, Vector3.zero);
            P("Hull", body, "pork_hull", b =>
            {
                b.AddSphere(new Vector3(0f, 2f, 0f), 1f, Quaternion.identity, W, 22, 14, true, new Vector3(1.55f, 1.35f, 2f));
            }, pink);
            P("Armor", body, "pork_armor", b =>
            {
                // spine plates following the back curve, side plates along both flanks, forehead helmet
                for (int i = 0; i < 5; i++)
                {
                    float z = -1.3f + i * 0.62f;
                    float k = Mathf.Sqrt(Mathf.Max(0.01f, 1f - (z / 2f) * (z / 2f)));
                    float y = 2f + 1.35f * k + 0.02f;
                    float slope = -1.35f * (z / 4f) / k;            // dy/dz of the top curve
                    b.AddBox(new Vector3(0f, y, z), new Vector3(1.9f * k + 0.2f, 0.16f, 0.66f), Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0f, 0f), W, 0.05f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = s * (1.55f * k * 0.93f);
                        b.AddBox(new Vector3(x, 2.55f, z), new Vector3(0.14f, 0.7f, 0.6f), Quaternion.Euler(0f, 0f, -s * 28f), W, 0.04f);
                    }
                }
                b.AddBox(new Vector3(0f, 2.95f, 1.45f), new Vector3(1.3f, 0.14f, 0.8f), Quaternion.Euler(28f, 0f, 0f), W, 0.05f);
            }, steel);
            P("Rivets", body, "pork_rivets", b =>
            {
                for (int i = 0; i < 5; i++)
                {
                    float z = -1.3f + i * 0.62f;
                    float k = Mathf.Sqrt(Mathf.Max(0.01f, 1f - (z / 2f) * (z / 2f)));
                    for (int s = -1; s <= 1; s += 2)
                        b.AddSphere(new Vector3(s * (0.75f * k + 0.1f), 2f + 1.35f * k + 0.1f, z), 0.06f, Quaternion.identity, W, 6, 4, true, null);
                }
            }, GoldMat);

            // coin slot on top
            var slot = Node("CoinSlot", body, new Vector3(0f, 3.36f, -0.2f));
            P("SlotRim", slot, "pork_slot_rim", b => b.AddBox(Vector3.zero, new Vector3(0.9f, 0.1f, 0.3f), Quaternion.identity, W, 0.03f), GoldMat);
            P("SlotHole", slot, "pork_slot_hole", b => b.AddBox(new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.04f, 0.1f), Quaternion.identity, W, 0f), dark);

            // snout (front), armored + cracked plate (STR: "CRACKED SNOUT - PUNCH")
            var snout = Node("Snout", body, new Vector3(0f, 1.95f, 1.98f));
            P("SnoutNose", snout, "pork_snout", b =>
            {
                b.AddCylinder(new Vector3(0f, 0f, 0.18f), 0.58f, 0.46f, AlongZ, W, 20, true);
            }, pink);
            P("SnoutPlate", snout, "pork_snout_plate", b =>
            {
                b.AddTorus(new Vector3(0f, 0f, 0.36f), 0.58f, 0.07f, AlongZ, W, 20, 6);
                b.AddBox(new Vector3(0f, 0.3f, 0.42f), new Vector3(0.9f, 0.32f, 0.06f), Quaternion.identity, W, 0.03f);
            }, steel);
            var crack = P("SnoutDetails", snout, "pork_snout_dark", b =>
            {
                b.AddCylinder(new Vector3(-0.2f, -0.08f, 0.42f), 0.11f, 0.04f, AlongZ, W, 10, true);    // nostrils
                b.AddCylinder(new Vector3(0.2f, -0.08f, 0.42f), 0.11f, 0.04f, AlongZ, W, 10, true);
                // crack zig-zag across the plate
                b.AddBox(new Vector3(-0.22f, 0.33f, 0.455f), new Vector3(0.24f, 0.03f, 0.02f), Quaternion.Euler(0f, 0f, -35f), W, 0f);
                b.AddBox(new Vector3(-0.03f, 0.3f, 0.455f), new Vector3(0.22f, 0.03f, 0.02f), Quaternion.Euler(0f, 0f, 40f), W, 0f);
                b.AddBox(new Vector3(0.15f, 0.33f, 0.455f), new Vector3(0.22f, 0.03f, 0.02f), Quaternion.Euler(0f, 0f, -30f), W, 0f);
                b.AddBox(new Vector3(0.3f, 0.38f, 0.455f), new Vector3(0.14f, 0.025f, 0.02f), Quaternion.Euler(0f, 0f, 55f), W, 0f);
            }, dark);
            m.SnoutCrack = crack.GetComponent<Renderer>();

            // eyes (normal: glossy black with glints; red: glowing)
            m.EyeNormal = C(new Color(0.05f, 0.05f, 0.07f), 0.9f);
            m.EyeRed = Glow(new Color(1f, 0.12f, 0.08f), 3f);
            var glint = Glow(Color.white, 1.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Node(s < 0 ? "EyeL" : "EyeR", body, new Vector3(s * 0.62f, 2.65f, 1.58f));
                var er = P("EyeBall", eye, "pork_eye", b => b.AddSphere(Vector3.zero, 0.2f, Quaternion.identity, W, 12, 8, true, null), m.EyeNormal);
                P("Glint", eye, "pork_glint", b => b.AddSphere(new Vector3(0.06f, 0.07f, 0.16f), 0.05f, Quaternion.identity, W, 6, 4, true, null), glint, default, null, false);
                P("Brow", eye, "pork_brow", b => b.AddBox(new Vector3(0f, 0.27f, 0.05f), new Vector3(0.42f, 0.08f, 0.12f), Quaternion.Euler(0f, 0f, 0f), W, 0.03f), steel, default, Quaternion.Euler(0f, 0f, -s * 14f));
                m.EyeRenderers[s < 0 ? 0 : 1] = er.GetComponent<Renderer>();
                if (s < 0) m.EyeL = eye; else m.EyeR = eye;
            }

            // ears (pivot at the base), right ear carries the hack port
            for (int s = -1; s <= 1; s += 2)
            {
                var ear = Node(s < 0 ? "EarL" : "EarR", body, new Vector3(s * 0.78f, 3.0f, 1.0f), Quaternion.Euler(-20f, 0f, -s * 25f));
                P("EarFlap", ear, "pork_ear", b => b.AddCone(Vector3.zero, 0.45f, 0.8f, Quaternion.identity, W, 14), pink, default, null);
                ear.GetChild(0).localScale = new Vector3(1f, 1f, 0.38f);
                if (s < 0) m.EarL = ear; else m.EarR = ear;
            }
            var port = Node("EarPort", m.EarR, new Vector3(0f, 0.26f, 0.13f), Quaternion.Euler(-10f, 0f, 0f));
            P("PortBox", port, "pork_port", b =>
            {
                b.AddBox(Vector3.zero, new Vector3(0.3f, 0.22f, 0.1f), Quaternion.identity, W, 0.02f);
                b.AddBox(new Vector3(0f, -0.02f, 0.05f), new Vector3(0.16f, 0.06f, 0.02f), Quaternion.identity, W, 0f);
            }, dark);
            P("PortLed", port, "pork_port_led", b => b.AddSphere(new Vector3(0.09f, 0.06f, 0.055f), 0.025f, Quaternion.identity, W, 6, 4, true, null), Glow(new Color(0.3f, 1f, 0.45f), 2.5f), default, null, false);

            // belly plug on the -X flank (rubber + gold rim), +Z of the node = outward normal
            var plug = Node("BellyPlug", body, new Vector3(-1.24f, 1.2f, 0.3f), Face(new Vector3(-0.76f, -0.65f, 0f).normalized));
            P("Plug", plug, "pork_plug", b =>
            {
                b.AddCylinder(new Vector3(0f, 0f, 0.06f), 0.42f, 0.2f, AlongZ, W, 18, true);
                b.AddCylinder(new Vector3(0f, 0f, 0.2f), 0.12f, 0.12f, AlongZ, W, 10, true);    // pull knob
            }, C(new Color(0.12f, 0.12f, 0.14f), 0.2f));
            P("PlugRim", plug, "pork_plug_rim", b => b.AddTorus(new Vector3(0f, 0f, 0.08f), 0.44f, 0.06f, AlongZ, W, 18, 5), GoldMat);

            // legs (pivot at the hip) with armored paws
            string[] ln = { "FL", "FR", "BL", "BR" };
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.88f : 0.88f, z = i < 2 ? 1.15f : -1.15f;
                var leg = Node("Leg" + ln[i], t, new Vector3(x, 1.15f, z));
                P("Thigh", leg, "pork_leg", b => b.AddCylinder(new Vector3(0f, -0.42f, 0f), 0.4f, 0.95f, Quaternion.identity, W, 14, true), pink);
                var paw = Node("Paw" + ln[i], leg, new Vector3(0f, -1.0f, 0f));
                P("Hoof", paw, "pork_paw", b =>
                {
                    b.AddCylinder(new Vector3(0f, 0.01f, 0.03f), 0.45f, 0.3f, Quaternion.identity, W, 14, true);
                    b.AddBox(new Vector3(0f, 0.01f, 0.4f), new Vector3(0.08f, 0.3f, 0.16f), Quaternion.identity, W, 0.02f);   // split hoof
                }, steel);
                m.Legs[i] = leg;
            }

            // curly tail
            var tail = Node("Tail", body, new Vector3(0f, 2.35f, -2.0f), Quaternion.Euler(0f, 90f, 0f));
            P("Curl", tail, "pork_tail", b =>
            {
                b.AddTorus(new Vector3(0f, 0f, 0f), 0.18f, 0.06f, AlongX, W, 12, 5);
                b.AddTorus(new Vector3(-0.12f, 0.14f, 0f), 0.13f, 0.05f, AlongX, W, 12, 5);
            }, pink);

            // name plates on both flanks
            for (int s = -1; s <= 1; s += 2)
            {
                var plate = Node(s < 0 ? "NameL" : "NameR", body, new Vector3(s * 1.47f, 2.3f, -0.3f), Face(new Vector3(s, 0f, 0f)));
                P("Plate", plate, "pork_nameplate", b => b.AddBox(Vector3.zero, new Vector3(1.4f, 0.36f, 0.05f), Quaternion.identity, W, 0.03f), dark);
                EnvKit.AddText(plate, "PORKCHOP", new Vector3(0f, 0f, 0.03f), new Vector2(1.3f, 0.3f), UIColors.Hex(0xF2B632), ShiftersFonts.Display, 0.28f);
            }

            if (bodyCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.9f, 0.2f);
                col.size = new Vector3(3.0f, 2.9f, 4.4f);
            }

            m.Body = body; m.Snout = snout; m.BellyPlug = plug; m.EarPort = port; m.Tail = tail; m.CoinSlot = slot;
            m.SetEyesRed(false);
            m.SetEars(0f);
            return Finish(m, "Porkchop");
        }

        /// <summary>Getaway tram dressing for a deck of deckSize (x = width, y = length; LEVELS2: 4 x 7 m). Parent it to the
        /// MovingPlatform's moving transform with pos = the deck top centre. Railings (1.0 m, BoxColliders when
        /// railColliders) leave a 2.2 m boarding gap mid each long side and a 1.6 m gap mid back; the cab sits front-right,
        /// the front-left corner is free (ConsoleSpot marks it). wheelY: bogie axle height below the deck top.</summary>
        public static GetawayTramModel GetawayTram(Transform parent, Vector3 pos, Quaternion? rot = null, Vector2? deckSize = null, bool railColliders = true, float wheelY = -0.55f)
        {
            var go = Root("GetawayTram", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<GetawayTramModel>();
            var ds = deckSize ?? new Vector2(4f, 7f);
            m.DeckSize = ds;
            float hw = ds.x * 0.5f, hl = ds.y * 0.5f;
            var steel = C(Steel, 0.5f);
            var dark = C(SteelDark, 0.45f);
            string dk = ds.x.ToString("0.0") + "x" + ds.y.ToString("0.0");

            // ---- cab (front right): dash + windshield + headlight
            var cab = Node("Cab", t, new Vector3(0.6f * (hw / 2f), 0f, hl - 0.5f));
            P("Dash", cab, "tram_dash", b =>
            {
                b.AddBox(new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1.0f, 0.9f), Quaternion.identity, W, 0.08f);
                b.AddBox(new Vector3(0f, 1.02f, -0.1f), new Vector3(2.4f, 0.08f, 0.6f), Quaternion.Euler(-12f, 0f, 0f), W, 0.02f);
            }, steel);
            P("CabTrim", cab, "tram_trim", b =>
            {
                b.AddBox(new Vector3(0f, 0.08f, 0.46f), new Vector3(2.7f, 0.14f, 0.06f), Quaternion.identity, W, 0.02f);
                b.AddBox(new Vector3(0f, 0.92f, 0.46f), new Vector3(2.7f, 0.06f, 0.06f), Quaternion.identity, W, 0.02f);
                b.AddCylinder(new Vector3(-1.2f, 1.45f, 0.3f), 0.04f, 0.9f, Quaternion.identity, W, 8, true);   // windshield posts
                b.AddCylinder(new Vector3(1.2f, 1.45f, 0.3f), 0.04f, 0.9f, Quaternion.identity, W, 8, true);
                b.AddBox(new Vector3(0f, 1.9f, 0.3f), new Vector3(2.5f, 0.06f, 0.08f), Quaternion.identity, W, 0.02f);
            }, GoldMat);
            P("Windshield", cab, "tram_glass", b => b.AddBox(new Vector3(0f, 1.45f, 0.3f), new Vector3(2.36f, 0.86f, 0.03f), Quaternion.Euler(-10f, 0f, 0f), W, 0f), GlassMat, default, null, false);
            m.HeadlightOff = C(new Color(0.75f, 0.72f, 0.6f), 0.8f);
            m.HeadlightOn = Glow(new Color(1f, 0.92f, 0.6f), 3f);
            var head = Node("Headlight", cab, new Vector3(0.75f, 0.55f, 0.47f));
            var hlr = P("Lamp", head, "tram_lamp", b => b.AddCylinder(Vector3.zero, 0.17f, 0.06f, AlongZ, W, 14, true), m.HeadlightOn, default, null, false);
            m.HeadlightRenderer = hlr.GetComponent<Renderer>();
            Label(cab, "CabSign", "COIN SORTING TRAM 3", new Vector3(0f, 0.6f, -0.46f), Face(Vector3.back), new Vector2(2.3f, 0.3f), Paper, 0.24f);
            Label(cab, "CabFront", "EXPRESS\n<size=60%>NO PIGS</size>", new Vector3(-0.35f, 0.5f, 0.47f), Face(Vector3.forward), new Vector2(1.2f, 0.6f), Paper, 0.3f);
            // gold cowcatcher in front of the deck
            P("Cowcatcher", t, "tram_cow_" + dk, b =>
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = -hw + 0.3f + i * (ds.x - 0.6f) / 6f;
                    b.AddBox(new Vector3(x, -0.2f, hl + 0.28f), new Vector3(0.08f, 0.5f, 0.5f), Quaternion.Euler(-40f, 0f, 0f), W, 0.02f);
                }
                b.AddBox(new Vector3(0f, 0.02f, hl + 0.08f), new Vector3(ds.x, 0.12f, 0.12f), Quaternion.identity, W, 0.03f);
            }, GoldMat);

            // ---- railings (posts + rail + mid rail), colliders per segment
            var cols = new System.Collections.Generic.List<BoxCollider>();
            float gapHalf = 1.1f, rearGap = 0.8f, rh = 1.0f;
            Transform Rail(string name) => Node(name, t, Vector3.zero);
            void Segment(Transform railNode, string key, Vector3 a, Vector3 c2)
            {
                var d = c2 - a; float len = d.magnitude;
                var mid = (a + c2) * 0.5f;
                var q = Quaternion.LookRotation(d.normalized, Vector3.up);
                P("Seg_" + key, railNode, "tram_rail_" + key + "_" + dk, b =>
                {
                    b.AddCylinder(mid + new Vector3(0f, rh, 0f), 0.045f, len, q * AlongZ, W, 8, true);
                    b.AddCylinder(mid + new Vector3(0f, rh * 0.5f, 0f), 0.03f, len, q * AlongZ, W, 8, true);
                    int posts = Mathf.Max(2, Mathf.RoundToInt(len / 1.1f) + 1);
                    for (int i = 0; i < posts; i++)
                    {
                        var p = Vector3.Lerp(a, c2, i / (float)(posts - 1));
                        b.AddCylinder(p + new Vector3(0f, rh * 0.5f, 0f), 0.04f, rh, Quaternion.identity, W, 8, true);
                    }
                }, GoldMat);
                if (railColliders)
                {
                    var cgo = Node("Col_" + key, railNode, mid + new Vector3(0f, rh * 0.5f, 0f), q);
                    var col = cgo.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.1f, rh, len);
                    cols.Add(col);
                }
            }
            float sx = hw - 0.06f, frontEnd = hl - 1.0f, backEnd = -hl + 0.06f;
            var railL = Rail("RailL");
            Segment(railL, "LA", new Vector3(-sx, 0f, backEnd), new Vector3(-sx, 0f, -gapHalf));
            Segment(railL, "LB", new Vector3(-sx, 0f, gapHalf), new Vector3(-sx, 0f, hl - 0.06f));
            var railR = Rail("RailR");
            Segment(railR, "RA", new Vector3(sx, 0f, backEnd), new Vector3(sx, 0f, -gapHalf));
            Segment(railR, "RB", new Vector3(sx, 0f, gapHalf), new Vector3(sx, 0f, frontEnd));
            var railB = Rail("RailBack");
            Segment(railB, "BA", new Vector3(-sx, 0f, backEnd), new Vector3(-rearGap, 0f, backEnd));
            Segment(railB, "BB", new Vector3(rearGap, 0f, backEnd), new Vector3(sx, 0f, backEnd));
            m.RailColliders = cols.ToArray();

            // ---- bogies (wheel pairs spin about X)
            var bogies = Node("Bogies", t, new Vector3(0f, wheelY, 0f));
            float[] zs = { hl - 1.0f, hl - 2.0f, -hl + 2.0f, -hl + 1.0f };
            for (int i = 0; i < zs.Length; i++)
            {
                var w = Node("Wheels" + i, bogies, new Vector3(0f, 0f, zs[i]));
                P("Pair", w, "tram_wheels_" + dk, b =>
                {
                    b.AddCylinder(new Vector3(-hw + 0.45f, 0f, 0f), 0.32f, 0.16f, AlongX, W, 14, true);
                    b.AddCylinder(new Vector3(hw - 0.45f, 0f, 0f), 0.32f, 0.16f, AlongX, W, 14, true);
                    b.AddCylinder(Vector3.zero, 0.06f, ds.x - 0.9f, AlongX, W, 8, true);
                    b.AddBox(new Vector3(-hw + 0.45f, 0f, 0f), new Vector3(0.05f, 0.5f, 0.1f), Quaternion.identity, W, 0f);   // spoke (shows the spin)
                    b.AddBox(new Vector3(hw - 0.45f, 0f, 0f), new Vector3(0.05f, 0.5f, 0.1f), Quaternion.identity, W, 0f);
                }, dark);
            }
            m.Bogies = bogies;

            // ---- coin hopper bolted to the back (outside the deck)
            var hop = Node("CoinHopper", t, new Vector3(0f, 0f, -hl - 0.38f));
            P("Bin", hop, "tram_bin_" + dk, b =>
            {
                b.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(ds.x - 0.8f, 0.7f, 0.6f), Quaternion.identity, W, 0.05f);
            }, steel);
            P("Coins", hop, "tram_coins_" + dk, b =>
            {
                b.AddBox(new Vector3(0f, 0.51f, 0f), new Vector3(ds.x - 0.9f, 0.04f, 0.5f), Quaternion.identity, W, 0.02f);
                for (int i = 0; i < 14; i++)
                {
                    float x = -hw + 0.6f + (i * 0.37f) % (ds.x - 1.2f);
                    b.AddCylinder(new Vector3(x, 0.56f + (i % 3) * 0.03f, -0.15f + (i % 4) * 0.1f), 0.09f, 0.025f, Quaternion.Euler(i * 23f % 40f, 0f, i * 17f % 30f), W, 10, true);
                }
            }, GoldMat);
            Label(hop, "BackSign", "HOW'S MY DRIVING?\n<size=70%>CALL 1-800-OINK</size>", new Vector3(0f, 0.15f, -0.31f), Face(Vector3.back), new Vector2(ds.x - 1.0f, 0.55f), Paper, 0.24f);

            var spot = Node("ConsoleSpot", t, new Vector3(-hw + 0.55f, 0f, hl - 0.55f));
            m.Cab = cab; m.Headlight = head; m.ConsoleSpot = spot;
            m.SetHeadlight(true);
            return Finish(m, "GetawayTram");
        }
    }
}
