using HarmonyLib;
using MelonLoader;
using SDModKit.Content;
using SDModKit.Game;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.FluidValve;
using Ships.Parts.Lever;
using Ships.Parts.MathGate;
using UnityEngine;

namespace SignalForge
{
    // Part ids, build menu registration and the model work for every part but the lamps (Lamps.cs).
    // Ids are saved in worlds: never reuse or renumber one.
    internal static class Parts
    {
        public const ushort OldScreenLampId = 4801;   // first screen lamp, fixed text: hidden, kept for old worlds
        public const ushort GateId = 4802;
        public const ushort ButtonId = 4803;
        public const ushort BreakerId = 4804;
        public const ushort AndId = 4805;
        public const ushort OrId = 4806;
        public const ushort XorId = 4807;
        public const ushort PortholeId = 4808;
        public const ushort ButtonNcId = 4809;
        public const ushort SwitchId = 4810;
        public const ushort OldSwitchNcId = 4811;     // removed normally closed switch: hidden, kept for old worlds
        public const ushort DoorId = 4812;
        public const ushort ScreenLampId = 4813;

        private const ushort FirstId = 4801;
        private const ushort LastId = 4813;

        public const string Tab = "SignalForge";

        // Sockets. The breaker is a Fluid Valve whose two fluid ends became power plugs, as the
        // Resistor of Functional Batteries does; its two signal plugs (0 and 1) both drive it.
        public const byte BreakerSideA = 2;
        public const byte BreakerSideB = 3;
        public const byte DoorSignalSocket = 0;
        // The normally closed push button is a two-way contact between its plugs 0 and 1.
        public const byte ContactInSocket = 1;

        public static bool IsOurs(ushort id) { return id >= FirstId && id <= LastId; }
        public static bool IsGate(ushort id) { return id == GateId || id == AndId || id == OrId || id == XorId; }
        public static bool IsButton(ushort id) { return id == ButtonId || id == ButtonNcId; }
        public static bool IsLever(ushort id) { return IsButton(id) || id == SwitchId || id == OldSwitchNcId; }

        // Fixed gates ignore the operation picked with F; the multi-function gate uses it.
        public static int GateOperation(ushort id, int selected)
        {
            if (id == AndId) return 0;
            if (id == OrId) return 1;
            if (id == XorId) return 2;
            return selected;
        }

        public static void RegisterAll()
        {
            BuildTabs.Declare(Tab, 50, Icon.Make());

            // Row 0: lamps and gates.
            Register(ScreenLampId, "ELK Lampe ecran texte", "GasSensor", 0, (s, p) => Lamps.BuildScreen(s, p, true));
            Register(PortholeId, "ELK Hublot lumineux", "SignalSplitter", 0, Lamps.BuildPorthole);
            Register(GateId, "ELK Porte logique", "MathGate", 0, (s, p) => ConfigureGate(s, p, GateId));
            Register(AndId, "ELK Porte ET", "MathGate", 0, (s, p) => ConfigureGate(s, p, AndId));
            Register(OrId, "ELK Porte OU", "MathGate", 0, (s, p) => ConfigureGate(s, p, OrId));
            Register(XorId, "ELK Porte XOR", "MathGate", 0, (s, p) => ConfigureGate(s, p, XorId));

            // Row 1: controls and power.
            Register(ButtonId, "ELK Bouton NO", "Lever", 1, (s, p) => ConfigureLever(s, p, ButtonId));
            Register(ButtonNcId, "ELK Bouton NF", "Lever", 1, (s, p) => ConfigureLever(s, p, ButtonNcId));
            Register(SwitchId, "ELK Interrupteur", "Lever", 1, (s, p) => ConfigureLever(s, p, SwitchId));
            Register(BreakerId, "ELK Sectionneur", "FluidValve", 1, ConfigureBreaker);
            Register(DoorId, "ELK Porte a signal", "Door", 1, ConfigureDoor);

            // Retired parts: still built so saved worlds load, but not offered in the menu.
            Register(OldScreenLampId, "ELK Lampe ecran", "GasSensor", 0, (s, p) => Lamps.BuildScreen(s, p, false));
            Register(OldSwitchNcId, "ELK Interrupteur NF", "Lever", 1, (s, p) => ConfigureLever(s, p, SwitchId));
            BuildTabs.Hide(OldScreenLampId, () => true);
            BuildTabs.Hide(OldSwitchNcId, () => true);
        }

        // The name is the internal asset name; it stays as first published so saved worlds keep matching.
        private static void Register(ushort id, string name, string donor, int row, System.Action<PartSettings, GameObject> configure)
        {
            CustomPartDefinition def = new CustomPartDefinition();
            def.Id = id;
            def.Name = name;
            def.Donor = donor;
            def.BuildTab = Tab;
            def.BuildRow = row;
            def.AutoFinish = false;
            def.Configure = (settings, prefab) =>
            {
                try { configure(settings, prefab); }
                catch (System.Exception e) { MelonLogger.Error("[SignalForge] " + name + ": " + e); }
            };
            CustomParts.Register(def);
        }

        public static void Describe(PartSettings settings, string label, string description)
        {
            settings.fullLabel = label;
            settings.description = description;
            settings.localizedDescription = null;
        }

        // ------------------------------------------------------------------ gates

        private static void ConfigureGate(PartSettings settings, GameObject prefab, ushort id)
        {
            string truth = Lang.T(
                " Un signal de 0,5 ou plus compte comme 1 ; la sortie vaut 1 ou 0.",
                " A signal of 0.5 or more counts as 1; the output is 1 or 0.");
            if (id == AndId)
                Describe(settings, Lang.T("Porte ET", "AND Gate"),
                    Lang.T("Sortie à 1 quand A ET B sont à 1.", "Output is 1 when both A AND B are 1.") + truth);
            else if (id == OrId)
                Describe(settings, Lang.T("Porte OU", "OR Gate"),
                    Lang.T("Sortie à 1 quand A OU B (ou les deux) est à 1.", "Output is 1 when A OR B (or both) is 1.") + truth);
            else if (id == XorId)
                Describe(settings, Lang.T("Porte XOR", "XOR Gate"),
                    Lang.T("Sortie à 1 quand une seule des deux entrées est à 1.", "Output is 1 when exactly one of the two inputs is 1.") + truth);
            else
                Describe(settings, Lang.T("Porte logique", "Logic Gate"), Lang.T(
                    "Porte logique à deux entrées (A et B). F change de fonction : AND, OR, XOR, NAND, NOR, XNOR, " +
                    "NOT (inverse A), SR (A = Set, B = Reset, garde l'état), T (chaque appui sur A inverse la sortie, B = Reset), " +
                    "BUF (recopie A).",
                    "Two-input logic gate (A and B). F cycles the function: AND, OR, XOR, NAND, NOR, XNOR, " +
                    "NOT (inverts A), SR (A = Set, B = Reset, keeps its state), T (each press on A flips the output, B = Reset), " +
                    "BUF (copies A).") + truth);

            MathGateVisuals visuals = prefab.GetComponentInChildren<MathGateVisuals>(true);
            if (visuals == null)
            {
                MelonLogger.Warning("[SignalForge] gate: Signal Combinator visual not found");
                return;
            }
            GameObject host = visuals.gameObject;
            if (id == AndId) host.AddComponent<AndGateDisplay>();
            else if (id == OrId) host.AddComponent<OrGateDisplay>();
            else if (id == XorId) host.AddComponent<XorGateDisplay>();
            else host.AddComponent<GateDisplay>();
        }

        // ------------------------------------------------------------------ buttons and switch

        private static void ConfigureLever(PartSettings settings, GameObject prefab, ushort id)
        {
            string pulse = Server.PressSeconds.ToString("0.0");
            Color tint;
            float angle;
            if (id == ButtonId)
            {
                Describe(settings, Lang.T("Bouton poussoir NO", "Push Button NO"), Lang.T(
                    "Normalement ouvert : envoie 0 au repos et 1 quand tu appuies avec F, au moins " + pulse + " s, et tant que tu gardes F enfoncé.",
                    "Normally open: sends 0 at rest and 1 when you press it with F, for at least " + pulse + " s and as long as you hold F."));
                tint = new Color(0.2f, 0.85f, 0.25f);
                angle = 14f;
            }
            else if (id == ButtonNcId)
            {
                Describe(settings, Lang.T("Bouton poussoir NF", "Push Button NC"), Lang.T(
                    "Contact normalement fermé, comme un vrai, dans les deux sens : le signal qui arrive sur une prise ressort par l'autre. " +
                    "Au repos il laisse passer le signal tel quel ; pendant l'appui (F, au moins " + pulse + " s, et tant que F reste enfoncé) la sortie tombe à 0. " +
                    "Sans signal d'un côté, l'autre reste à 0. Idéal comme arrêt d'urgence.",
                    "Normally closed contact, like a real one, both ways: the signal reaching one plug comes out of the other. " +
                    "At rest it passes the signal through unchanged; while pressed (F, at least " + pulse + " s and as long as F is held) the output drops to 0. " +
                    "With no signal on one side, the other stays at 0. Ideal as an emergency stop."));
                tint = new Color(0.95f, 0.15f, 0.1f);
                angle = 14f;
                // Input on the end opposite the vanilla output plug, facing into the part like it.
                ContentBuilder.GraftSocket(prefab, ContactInSocket, CableType.Signal,
                    new Vector3(0f, 0.07f, 0.25f), Quaternion.LookRotation(Vector3.back));
            }
            else
            {
                Describe(settings, Lang.T("Interrupteur", "Toggle Switch"), Lang.T(
                    "Interrupteur à bascule : F passe de 0 à 1 puis de 1 à 0. Garde sa position.",
                    "Toggle switch: F flips it from 0 to 1 and back. Keeps its position."));
                tint = new Color(0.2f, 0.55f, 1f);
                angle = 35f;
            }

            LeverVisuals lever = prefab.GetComponentInChildren<LeverVisuals>(true);
            if (lever == null)
            {
                MelonLogger.Warning("[SignalForge] " + settings.fullLabel + ": lever visual not found");
                return;
            }
            AccessTools.Field(typeof(LeverVisuals), "maxAngle").SetValue(lever, angle);
            if (IsButton(id))
            {
                PartInteraction press = prefab.GetComponentInChildren<PartInteraction>(true);
                if (press != null) press.gameObject.AddComponent<HoldToPress>();
            }
            Transform handle = AccessTools.Field(typeof(LeverVisuals), "leverObject").GetValue(lever) as Transform;
            if (handle != null)
                Paint.Tint(handle.gameObject, tint, "sf lever", true, null);
        }

        // ------------------------------------------------------------------ breaker

        private static void ConfigureBreaker(PartSettings settings, GameObject prefab)
        {
            string kw = (Breaker.MaxWatts / 1000f).ToString("0");
            Describe(settings, Lang.T("Sectionneur", "Circuit Breaker"), Lang.T(
                "Coupe ou laisse passer le courant entre ses deux prises électriques (jusqu'à " + kw + " kW). " +
                "Le signal se branche sur l'un ou l'autre côté. Signal de 0,5 ou plus : le courant passe, " +
                "la poignée tourne et le voyant passe au vert. Signal sous 0,5 : circuit coupé, voyant rouge. " +
                "Sans câble de signal, le courant passe.",
                "Cuts or passes power between its two power plugs (up to " + kw + " kW). " +
                "Plug the signal on either side. Signal of 0.5 or more: power flows, the handle turns and the light goes green. " +
                "Signal below 0.5: circuit open, red light. With no signal cable, power flows."));

            SocketObject template = Sockets.PowerTemplate();
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
                if (socket.Type == CableType.Fluid)
                    Sockets.MakePower(socket, template);

            // Electrical cabinet look: darker body, hazard yellow handle, a status light beside it.
            FluidValveVisual visual = prefab.GetComponentInChildren<FluidValveVisual>(true);
            Transform handle = visual != null
                ? AccessTools.Field(typeof(FluidValveVisual), "valveHandle").GetValue(visual) as Transform
                : null;
            Paint.Tint(prefab, new Color(0.5f, 0.52f, 0.58f), "sf breaker body", false, handle);
            if (handle == null)
            {
                MelonLogger.Warning("[SignalForge] breaker: valve handle not found");
                return;
            }
            Paint.Tint(handle.gameObject, new Color(1f, 0.78f, 0.05f), "sf breaker handle", true, null);

            Bounds hb;
            if (!Paint.TryBounds(handle.gameObject, visual.transform, out hb))
                hb = new Bounds(handle.localPosition, Vector3.one * 0.2f);
            float size = Mathf.Clamp(Mathf.Max(hb.size.x, hb.size.z) * 0.3f, 0.05f, 0.12f);
            GameObject led = new GameObject(BreakerDisplay.LedNode);
            led.transform.SetParent(visual.transform, false);
            led.transform.localPosition = hb.center + new Vector3(hb.extents.x + size, hb.extents.y * 0.5f, 0f);
            Paint.SetWorldScale(led.transform, Vector3.one * size);
            led.AddComponent<MeshFilter>().sharedMesh = Paint.SphereMesh();
            led.AddComponent<MeshRenderer>().sharedMaterial = Paint.Clone(Paint.FirstMaterial(handle.gameObject), "sf breaker led");
            visual.gameObject.AddComponent<BreakerDisplay>();
        }

        // ------------------------------------------------------------------ door

        private static void ConfigureDoor(PartSettings settings, GameObject prefab)
        {
            Describe(settings, Lang.T("Porte à signal", "Signal Door"), Lang.T(
                "Porte pilotable à distance. Branche un signal sur la prise du cadre : 0,5 ou plus ouvre la porte, " +
                "en dessous elle se ferme. Sans câble, elle s'ouvre à la main comme une porte normale.",
                "Remote-controlled door. Plug a signal into the socket on the frame: 0.5 or more opens it, " +
                "below that it closes. With no cable it opens by hand like a normal door."));

            // On the face of the right post of the frame (frame x -0.5..1.5, opening 0..1, face z 0.11),
            // standing off by 3 cm and facing into the part like the vanilla plugs.
            Bounds frame;
            if (!Paint.TryBounds(prefab, prefab.transform, out frame))
                frame = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(2f, 2f, 0.22f));
            Vector3 local = new Vector3(frame.max.x - 0.25f, frame.center.y, frame.max.z + 0.03f);
            ContentBuilder.GraftSocket(prefab, DoorSignalSocket, CableType.Signal, local, Quaternion.LookRotation(Vector3.back));
        }
    }
}
