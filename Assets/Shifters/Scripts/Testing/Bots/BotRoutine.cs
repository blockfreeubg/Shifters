// =====================================================================================
//  SHIFTERS - BotRoutine.cs   [BOTS module]
//  Exception-safe stepper for nested coroutines. Unity silently kills a coroutine chain when a
//  nested IEnumerator throws; BotRoutine steps the chain itself so TeamBot can turn exceptions
//  into a clean, diagnosed failure. Supports yielding: null (one frame), IEnumerator (nested),
//  CustomYieldInstruction and AsyncOperation. Any other yield value waits one frame
//  (use bot.Wait()/ctx.Wait() instead of WaitForSeconds).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class BotRoutine
    {
        readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        readonly Func<bool> _shouldStop;

        public bool Done { get; private set; }
        public Exception Error { get; private set; }

        /// <param name="shouldStop">Checked before every resume: true => abort right away (e.g. the bot failed), so
        /// plain code after a failed action never runs.</param>
        public BotRoutine(IEnumerator root, Func<bool> shouldStop = null)
        {
            _shouldStop = shouldStop;
            if (root != null) _stack.Push(root); else Done = true;
        }

        /// <summary>Advance by one frame. Returns false once finished (or failed: see Error).</summary>
        public bool Step()
        {
            if (Done) return false;
            while (_stack.Count > 0)
            {
                if (_shouldStop != null && _shouldStop()) { Abort(); return false; }
                var top = _stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (Exception e) { Error = e; Abort(); return false; }
                if (!moved) { _stack.Pop(); continue; }
                var cur = top.Current;
                if (cur is IEnumerator nested) { _stack.Push(nested); continue; }
                if (cur is CustomYieldInstruction cyi) { _stack.Push(WaitCustom(cyi)); continue; }
                if (cur is AsyncOperation op) { _stack.Push(WaitAsync(op)); continue; }
                return true; // null / anything else: one frame
            }
            Done = true;
            return false;
        }

        /// <summary>Stop now. Disposes the pending enumerators so their finally blocks run.</summary>
        public void Abort()
        {
            while (_stack.Count > 0)
            {
                var e = _stack.Pop();
                try { (e as IDisposable)?.Dispose(); } catch (Exception) { /* already failing */ }
            }
            Done = true;
        }

        static IEnumerator WaitCustom(CustomYieldInstruction y) { while (y.keepWaiting) yield return null; }
        static IEnumerator WaitAsync(AsyncOperation op) { while (!op.isDone) yield return null; }
    }
}
