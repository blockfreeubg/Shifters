// =====================================================================================
//  SHIFTERS - BotDiagnostics.cs   [BOTS module]
//  State dumps (team, signals, recent steps) and diagnostic cameras (focus / overview shots)
//  so a failed solution explains itself.
// =====================================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public static class BotDiagnostics
    {
        /// <summary>Multi-line text dump of everything a solution author needs to debug a failure.</summary>
        public static string Dump(TeamBot bot)
        {
            var sb = new StringBuilder();
            var cm = CharacterManager.Instance;
            sb.Append("=== BOT DUMP '").Append(bot != null ? bot.Label : "-").Append("' t=").Append(bot != null ? bot.Now.ToString("0.00") : "-")
              .Append(" active=").Append(cm != null ? BotFmt.Name(cm.Active) : "-").Append(" AI=").Append(cm != null ? cm.AIMode.ToString() : "-").Append('\n');
            var team = bot != null ? bot.Team : new List<ShifterCharacter>(ShifterCharacter.All);
            foreach (var c in team) sb.Append(DescribeCharacter(c)).Append('\n');
            AppendSignals(sb);
            if (bot != null)
            {
                int from = Mathf.Max(0, bot.Steps.Count - 8);
                sb.Append("last steps:\n");
                for (int i = from; i < bot.Steps.Count; i++)
                {
                    var s = bot.Steps[i];
                    sb.Append("  ").Append(s.Outcome).Append(' ').Append(s.Title).Append(' ').Append(s.Seconds.ToString("0.00")).Append("s ").Append(s.Note).Append('\n');
                }
            }
            return sb.ToString();
        }

        public static string DescribeCharacter(ShifterCharacter c)
        {
            if (c == null) return "  (null character)";
            var m = c.Motor;
            var sb = new StringBuilder("  [");
            sb.Append(c.Slot).Append("] ").Append(c.DisplayName).Append(' ').Append(c.Archetype);
            if (c.Stats != null) sb.Append(" STR").Append(c.Stats.Strength).Append(" SPD").Append(c.Stats.Speed).Append(" IQ").Append(c.Stats.IQ);
            sb.Append(' ').Append(c.Mass.ToString("0")).Append("kg pos ").Append(BotFmt.V(c.Feet))
              .Append(' ').Append(c.State).Append('/').Append(c.Condition)
              .Append(" hp ").Append(c.Health.ToString("0"));
            if (m != null)
                sb.Append(" grounded=").Append(m.IsGrounded ? "Y" : "N")
                  .Append(" ground=").Append(m.GroundCollider != null ? m.GroundCollider.name : "-")
                  .Append(" on=").Append(m.StandingOn != null ? m.StandingOn.DisplayName : "-")
                  .Append(" v=").Append(BotFmt.V(m.Velocity))
                  .Append(m.IsTumbling ? " TUMBLING" : "");
            if (c.Grabber != null && c.Grabber.IsHolding) sb.Append(" holding=").Append(c.Grabber.Held.name);
            if (c.Bridge != null && c.Bridge.IsBridging) sb.Append(" BRIDGE load ").Append(c.Bridge.CurrentLoad.ToString("0")).Append('/').Append(c.Bridge.Capacity.ToString("0"));
            if (c.Weapon != null && c.Weapon.HasWeapon) sb.Append(" GUN");
            if (c.IsActive) sb.Append(" ACTIVE");
            if (c.ExternalControl) sb.Append(" ext");
            if (!string.IsNullOrEmpty(c.LastCause)) sb.Append(" cause='").Append(c.LastCause).Append('\'');
            return sb.ToString();
        }

        static void AppendSignals(StringBuilder sb)
        {
            var receivers = Object.FindObjectsByType<SignalReceiver>();
            var sources = Object.FindObjectsByType<SignalSource>();
            sb.Append("receivers (").Append(receivers.Length).Append("):");
            int n = 0;
            foreach (var r in receivers)
            {
                if (n++ >= 30) { sb.Append(" ..."); break; }
                sb.Append(' ').Append(r.name).Append('=').Append(r.Powered ? "ON" : "off");
            }
            sb.Append("\nsources (").Append(sources.Length).Append("):");
            n = 0;
            foreach (var s in sources)
            {
                if (n++ >= 30) { sb.Append(" ..."); break; }
                sb.Append(' ').Append(s.name).Append('=').Append(s.IsOn ? "ON" : "off");
            }
            sb.Append('\n');
        }

        /// <summary>Screenshot from a temporary camera looking at a point from behind/above.</summary>
        public static string FocusShot(DevPlayContext ctx, string name, Vector3 focus, Vector3 forward, float distance = 6f, float height = 3f)
        {
            if (ctx == null) return null;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Vector3 pos = focus - forward.normalized * distance + Vector3.up * height;
            return ShotFrom(ctx, name, pos, focus);
        }

        /// <summary>Screenshot from high above the team's centroid.</summary>
        public static string OverviewShot(DevPlayContext ctx, string name, List<ShifterCharacter> team)
        {
            if (ctx == null || team == null || team.Count == 0) return null;
            Vector3 c = Vector3.zero;
            foreach (var t in team) c += t.Center;
            c /= team.Count;
            return ShotFrom(ctx, name, c + new Vector3(0f, 16f, -14f), c);
        }

        public static string ShotFrom(DevPlayContext ctx, string name, Vector3 pos, Vector3 lookAt)
        {
            var go = new GameObject("[BotDiagCam]");
            try
            {
                var cam = go.AddComponent<Camera>();
                var main = Camera.main;
                if (main != null) { cam.CopyFrom(main); cam.targetTexture = null; }
                cam.enabled = false;
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 500f;
                go.transform.position = pos;
                go.transform.LookAt(lookAt);
                return ctx.Shot(name, cam);
            }
            finally { Object.Destroy(go); }
        }
    }
}
