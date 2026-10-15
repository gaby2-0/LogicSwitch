using System;
using HarmonyLib;
using SDModKit.Game;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using Ships.Parts.FluidValve;
using Ships.Parts.MathGate;
using TMPro;
using UnityEngine;

namespace LogicSwitch
{
    // Client-side looks: gate labels, lamp colours and text, breaker status light.
    internal static class Visuals
    {
        private static readonly System.Reflection.FieldInfo TextField =
            AccessTools.Field(typeof(MathGateVisuals), "textField");

        public static void AfterMathGateState(MathGateVisuals __instance, IStatefulPartState state)
        {
            try
            {
                MathGateState gate = state as MathGateState;
                GateDisplay display = __instance.GetComponent<GateDisplay>();
                if (gate != null && display != null)
                    display.Apply(TextField.GetValue(__instance) as TMP_Text, gate);
            }
            catch (Exception e)
            {
                Log.Once("MathGateVisuals", e);
            }
        }

        public static void AfterValveHandle(FluidValveVisual __instance, FluidValveState state)
        {
            try
            {
                BreakerDisplay display = __instance.GetComponent<BreakerDisplay>();
                if (display != null && state != null)
                    display.Apply(state.get_ValveOpenedRate() >= 0.5f);
            }
            catch (Exception e)
            {
                Log.Once("FluidValveVisual", e);
            }
        }
    }

    // Writes the gate's function on its screen, always in the blue of the game's screens.
    public class GateDisplay : MonoBehaviour
    {
        private static readonly string[] Names = { "AND", "OR", "XOR", "NAND", "NOR", "XNOR", "NOT", "SR", "T", "BUF" };
        private static readonly Color Blue = new Color(0.2f, 0.72f, 1f);

        protected virtual string Label(int operation)
        {
            return operation >= 0 && operation < Names.Length ? Names[operation] : "?";
        }

        public void Apply(TMP_Text text, MathGateState state)
        {
            if (text == null) return;
            text.text = Label((int)state.get_Operation());
            text.color = Blue;
        }
    }

    public class AndGateDisplay : GateDisplay
    {
        protected override string Label(int operation) { return Lang.T("ET", "AND"); }
    }

    public class OrGateDisplay : GateDisplay
    {
        protected override string Label(int operation) { return Lang.T("OU", "OR"); }
    }

    public class XorGateDisplay : GateDisplay
    {
        protected override string Label(int operation) { return "XOR"; }
    }

    // Lamp look. The colour comes from the signal on plug 0, read here from the cable network
    // (the game does not send a transmitter's value to clients); the screen text comes from the
    // part state, where the game keeps what the player wrote.
    public class LampVisual : DefaultShipPartVisuals, IPartVisualWritable, IContextAwarePart
    {
        private const float PollSeconds = 0.1f;
        public const float Intensity = 2.5f;

        private static readonly Color Pink = new Color(1f, 0.3f, 0.75f);
        private static readonly Color Green = new Color(0.15f, 1f, 0.25f);
        private static readonly Color Yellow = new Color(1f, 0.88f, 0.1f);
        private static readonly Color Red = new Color(1f, 0.1f, 0.06f);
        public static readonly Color Unlit = new Color(0.16f, 0.2f, 0.24f);
        private static readonly Color UnlitText = new Color(0.45f, 0.55f, 0.62f);

        private Renderer _lens;
        private Light _light;
        private TMP_Text _text;
        private TMP_InputField _field;
        private bool _resolved;

        private PartContext _part;
        private bool _hasPart;
        private float _nextPoll;
        private double _signal;
        private string _written;
        private string _shown;

        // Bands: under 0.25 pink, under 0.5 green, under 0.75 yellow, then red; 0 or less is off.
        public static bool Band(double value, out Color color, out string name)
        {
            if (value <= 0.0) { color = Unlit; name = Lang.T("ÉTEINT", "OFF"); return false; }
            if (value < 0.25) { color = Pink; name = Lang.T("ROSE", "PINK"); }
            else if (value < 0.5) { color = Green; name = Lang.T("VERT", "GREEN"); }
            else if (value < 0.75) { color = Yellow; name = Lang.T("JAUNE", "YELLOW"); }
            else { color = Red; name = Lang.T("ROUGE", "RED"); }
            return true;
        }

        public TMP_InputField InputField
        {
            get { Resolve(); return _field; }
        }

        public void SetContext(ShipPartContext context)
        {
            _part = context.Part;
            _hasPart = true;
            _nextPoll = 0f;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            ReadText(state);
            Render();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            ReadText(change.NewState);
            Render();
        }

        private void Update()
        {
            if (!_hasPart || Time.time < _nextPoll) return;
            _nextPoll = Time.time + PollSeconds;
            double signal = ReadSignal();
            if (signal == _signal) return;
            _signal = signal;
            Render();
        }

        private void ReadText(IStatefulPartState state)
        {
            SignalTransmitterState transmitter = state as SignalTransmitterState;
            _written = transmitter != null ? transmitter.get_Channel() : null;
        }

        // The host has the authoritative tracker; a joining player only has the client one.
        private double ReadSignal()
        {
            ConnectionsTracker connections = GameRefs.Service<ConnectionsServerTracker>();
            if (connections == null) connections = GameRefs.Service<ConnectionsClientTracker>();
            if (connections == null) return 0;
            return connections.GetSocketSignalValue(new EntityPartSocket(_part.ShipId, _part.PartId, 0));
        }

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            LampLens lens = GetComponentInChildren<LampLens>(true);
            _lens = lens != null ? lens.GetComponent<Renderer>() : null;
            Transform glow = transform.Find(Lamps.LightNode);
            _light = glow != null ? glow.GetComponent<Light>() : null;
            _field = GetComponentInChildren<TMP_InputField>(true);
            _text = _field != null ? _field.textComponent : GetComponentInChildren<TMP_Text>(true);
        }

        private void Render()
        {
            Resolve();
            Color color;
            string name;
            bool on = Band(_signal, out color, out name);

            if (_lens != null)
                foreach (Material m in _lens.materials)
                    Paint.SetColor(m, color, on ? color * 2.2f : Color.black);
            if (_light != null)
            {
                _light.color = color;
                _light.intensity = on ? Intensity : 0f;
                _light.enabled = on;
            }

            // The Gas Sensor screen is an input field that writes its own text back, so go through
            // it, and leave it alone while the player is typing in it.
            string label = string.IsNullOrEmpty(_written) ? name : _written;
            if (_field != null)
            {
                if (!_field.isFocused && label != _shown)
                {
                    _field.SetTextWithoutNotify(label);
                    _shown = label;
                }
            }
            else if (_text != null)
            {
                _text.text = label;
            }
            if (_text != null) _text.color = on ? color : UnlitText;
        }
    }

    // Breaker status light: green while power flows, red while cut.
    public class BreakerDisplay : MonoBehaviour
    {
        public const string LedNode = "SF_Led";
        private static readonly Color Closed = new Color(0.15f, 1f, 0.25f);
        private static readonly Color Open = new Color(1f, 0.12f, 0.08f);

        private Renderer _led;
        private bool _resolved;

        public void Apply(bool closed)
        {
            if (!_resolved)
            {
                _resolved = true;
                Transform led = transform.Find(LedNode);
                _led = led != null ? led.GetComponent<Renderer>() : null;
            }
            if (_led == null) return;
            Color c = closed ? Closed : Open;
            Paint.SetColor(_led.material, c, c * 2f);
        }
    }
}
