// =====================================================================================
//  SHIFTERS - TallyBoardView.cs   [owned by STORY_KIT]   (StoryKit.TallyBoard runtime)
//  StoryBeat listener: ch14.vote -> AYE 0 / NAY 1 "1-0. I WIN. - ME"; ch14.impeached -> AYE 3 / NAY 1 "MOTION CARRIES.
//  MAXIMILIAN ME IS HEREBY... IMPEACHED."; ch14.exhibitA/B/C -> caption; ch14.tally.aye / ch14.tally.nay (+1),
//  ch14.tally.reset, ch14.tally.set.<aye>.<nay>. Counter changes pulse the digits.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public sealed class TallyBoardView : MonoBehaviour
    {
        public TextMeshPro AyeText, NayText, Caption;
        public int Aye { get; private set; }
        public int Nay { get; private set; }
        float _pulse;

        void OnEnable() { GameEvents.StoryBeat += OnBeat; }
        void OnDisable() { GameEvents.StoryBeat -= OnBeat; }

        public void SetTally(int aye, int nay, string caption = null)
        {
            Aye = Mathf.Max(0, aye); Nay = Mathf.Max(0, nay);
            if (AyeText != null) AyeText.text = Aye.ToString();
            if (NayText != null) NayText.text = Nay.ToString();
            if (caption != null && Caption != null) Caption.text = caption;
            _pulse = 0.4f;
            Sfx.Play(SfxId.Gavel, 0.6f);
        }

        public void OnBeat(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("ch14.")) return;
            switch (id)
            {
                case "ch14.vote": SetTally(Aye, Mathf.Max(1, Nay), "IT'S 1-0. I WIN. - ME"); return;
                case "ch14.impeached": SetTally(3, 1, "MOTION CARRIES. MAXIMILIAN ME IS HEREBY... IMPEACHED."); return;
                case "ch14.exhibitA": if (Caption != null) Caption.text = "EXHIBIT A: THE GREY MEMO"; return;
                case "ch14.exhibitB": if (Caption != null) Caption.text = "EXHIBIT B: THE BOOKS"; return;
                case "ch14.exhibitC": if (Caption != null) Caption.text = "EXHIBIT C: THE BEDSHEET"; return;
                case "ch14.defense": if (Caption != null) Caption.text = "THE DEFENSE CALLS... ME."; return;
                case "ch14.filibuster": if (Caption != null) Caption.text = "FILIBUSTER IN PROGRESS (EST. 9 HOURS)"; return;
                case "ch14.muted": if (Caption != null) Caption.text = "THE DEFENDANT HAS BEEN MUTED."; return;
                case "ch14.tally.aye": SetTally(Aye + 1, Nay); return;
                case "ch14.tally.nay": SetTally(Aye, Nay + 1); return;
                case "ch14.tally.reset": SetTally(0, 0, "THE HEARING IS IN SESSION"); return;
            }
            if (id.StartsWith("ch14.tally.set."))
            {
                var parts = id.Substring(15).Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int n)) SetTally(a, n);
            }
        }

        void Update()
        {
            if (_pulse <= 0f) return;
            _pulse -= Time.deltaTime;
            float s = 1f + Mathf.Max(0f, _pulse) * 0.6f;
            if (AyeText != null) AyeText.transform.localScale = Vector3.one * s;
            if (NayText != null) NayText.transform.localScale = Vector3.one * s;
        }
    }
}
