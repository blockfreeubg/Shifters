// =====================================================================================
//  SHIFTERS - TombstoneRowView.cs   [owned by STORY_KIT]   (StoryKit.TombstoneRow runtime)
//  Start: fills the slots from CampaignDirector.LostLog (late-bound; empty -> "HERE LIES NOBODY" + "RESERVED FOR YOU").
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public sealed class TombstoneRowView : MonoBehaviour
    {
        public TextMeshPro[] Texts;
        /// <summary>false = keep the built texts (a level fills the row itself with Fill).</summary>
        public bool AutoFillFromLostLog = true;
        public int FilledFromLog { get; private set; }

        void Start() { if (AutoFillFromLostLog) FillFromLostLog(); }

        public void FillFromLostLog() => FillFrom(StoryKitLate.LostLog());

        public void FillFrom(IList<LostEntry> log)
        {
            if (Texts == null) return;
            var list = new List<string>();
            if (log != null)
                for (int i = log.Count - 1; i >= 0 && list.Count < Texts.Length; i--) list.Add(Format(log[i]));   // newest first
            FilledFromLog = list.Count;
            Fill(list);
        }

        /// <summary>Sets slot texts in order; missing slots get the fallbacks.</summary>
        public void Fill(IList<string> texts)
        {
            if (Texts == null) return;
            int n = texts != null ? texts.Count : 0;
            for (int i = 0; i < Texts.Length; i++)
            {
                if (Texts[i] == null) continue;
                Texts[i].text = i < n ? texts[i] : (n == 0 && i == 0 ? StoryKitLines.TombNobody : StoryKitLines.TombReserved);
            }
        }

        public static string Format(LostEntry e)
        {
            string nn = "??";
            if (!string.IsNullOrEmpty(e.ChapterId))
            {
                var digits = new System.Text.StringBuilder();
                foreach (char ch in e.ChapterId) if (char.IsDigit(ch)) digits.Append(ch);
                if (digits.Length > 0) nn = digits.ToString().PadLeft(2, '0');
            }
            return StoryKitLines.TombTemplate
                .Replace("{NAME}", string.IsNullOrEmpty(e.Name) ? "AN INTERN" : e.Name.ToUpperInvariant())
                .Replace("{CAUSE}", string.IsNullOrEmpty(e.Cause) ? "LOST IN THE LINE OF DUTY" : e.Cause.ToUpperInvariant().TrimEnd('.'))
                .Replace("{NN}", nn)
                .Replace("{CLONE}", string.IsNullOrEmpty(e.CloneName) ? "A CLONE" : e.CloneName.ToUpperInvariant());
        }
    }
}
