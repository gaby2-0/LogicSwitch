using System;
using Players.Interface.Model;
using Ships.Parts.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalForge
{
    // Holding F on a push button keeps it pressed. The game only reports the key going down, so
    // while F stays held the player's client presses the button again every RepeatSeconds; each
    // press pushes the host's release time back (Server.BeforeStartInteracting). Uses the game's
    // own interaction requests, so it works the same for the host and for joining players.
    public class HoldToPress : MonoBehaviour
    {
        private const float RepeatSeconds = 0.1f;

        private PartInteraction _interaction;
        private bool _holding;
        private bool _repeating;
        private float _next;

        public static void AfterInteract(PartInteraction __instance, InteractionType type, bool __result)
        {
            try
            {
                if (!__result || type != InteractionType.Interact) return;
                HoldToPress hold = __instance.GetComponent<HoldToPress>();
                if (hold != null && !hold._repeating) hold.Begin(__instance);
            }
            catch (Exception e)
            {
                Log.Once("HoldToPress", e);
            }
        }

        private void Begin(PartInteraction interaction)
        {
            _interaction = interaction;
            _holding = true;
            _next = Time.time + RepeatSeconds;
        }

        private void Update()
        {
            if (!_holding) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[Key.F].isPressed || _interaction == null)
            {
                _holding = false;
                return;
            }
            if (Time.time < _next) return;
            _next = Time.time + RepeatSeconds;
            _repeating = true;
            try { _interaction.Interact(InteractionType.Interact); }
            finally { _repeating = false; }
        }
    }
}
