// =====================================================================================
//  SHIFTERS - L13BumpButton.cs   [LEVEL 13 - P2 BUMP RUN button]
//  The BUMP RUN TimedButton that records WHO pressed it (spec: L13BumpButton.Press(who)).
//  L13BumpRun reads Presser / PressCount; a script press (who == null) leaves Presser null and
//  L13BumpRun falls back to the nearest intern.
//  Built by L13Builder.MakeBumpButton (replaces the PuzzleKit TimedButton component, same parts).
// =====================================================================================
namespace Shifters
{
    public sealed class L13BumpButton : TimedButton
    {
        public ShifterCharacter Presser { get; private set; }
        public int PressCount { get; private set; }

        protected override void OnPressed(ShifterCharacter who)
        {
            Presser = who;
            PressCount++;
            base.OnPressed(who);
        }

        public override void ResetState() { Presser = null; base.ResetState(); }
    }
}
