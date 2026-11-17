// =====================================================================================
//  SHIFTERS - BossTestSignal.cs   [BOSSES module - tests]  A SignalSource the test flips by code
//  (ChaseTrack station "Clear", door wiring checks).
// =====================================================================================
namespace Shifters
{
    public sealed class BossTestSignal : SignalSource
    {
        public void Set(bool on) => SetOn(on);
    }
}
