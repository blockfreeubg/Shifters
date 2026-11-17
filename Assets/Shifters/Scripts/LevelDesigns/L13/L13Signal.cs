// =====================================================================================
//  SHIFTERS - L13Signal.cs   [LEVEL 13]  plain script-driven signal (cell door, firewall down).
// =====================================================================================
namespace Shifters
{
    public class L13Signal : SignalSource, IResettable
    {
        public void Set(bool on) { if (IsOn != on) SetOn(on); }
        public void ResetState() => Set(false);
    }
}
