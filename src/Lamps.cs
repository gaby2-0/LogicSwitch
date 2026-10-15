using MelonLoader;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.SignalTransmitter;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalForge
{
    // Signal lamps. Both light a lens shaped like the Gas Sensor's round window:
    //  - the screen lamp keeps the whole Gas Sensor and lights its window; its screen shows a text the player writes;
    //  - the porthole is just that window, on the Signal Splitter's footprint and hitbox.
    // They need no power and hold no logic: LampVisual reads the signal on plug 0 and picks the colour.
    internal static class Lamps
    {
        public const string LightNode = "SF_Light";

        // The Gas Sensor's window: 0.21 wide, standing 0.13 out of a 0.27 collar.
        private const float LensDiameter = 0.21f;
        private const float LensHeight = 0.13f;
        private const float CollarDiameter = 0.27f;
        private const float CollarHeight = 0.03f;
        // Hull blue-grey, as the Gas Sensor's casing around its window.
        private static readonly Color Casing = new Color(0.33f, 0.43f, 0.5f);

        private static string Bands
        {
            get
            {
                return Lang.T("Couleur selon le signal : sous 0,25 rose, sous 0,5 vert, sous 0,75 jaune, au-dessus rouge. " +
                              "À 0 (ou sans câble) elle est éteinte. Pas besoin de courant.",
                              "Colour follows the signal: below 0.25 pink, below 0.5 green, below 0.75 yellow, above that red. " +
                              "At 0 (or with no cable) it is off. No power needed.");
            }
        }

        public static void BuildScreen(PartSettings settings, GameObject prefab, bool writable)
        {
            if (writable)
            {
                Parts.Describe(settings, Lang.T("Lampe à écran", "Screen Lamp"), Lang.T(
                    "Le hublot s'allume selon le signal branché sur la prise au bout. Appuie sur F pour écrire ton propre texte " +
                    "sur l'écran ; il prend la couleur de la lampe. Sans texte, l'écran affiche le nom de la couleur. " +
                    "Le texte sert aussi de canal : un Signal Receiver réglé sur le même nom reçoit la valeur de la lampe. ",
                    "The window lights up from the signal plugged into the socket at the end. Press F to write your own text " +
                    "on the screen; it takes the lamp's colour. With no text the screen shows the colour name. " +
                    "The text is also a channel: a Signal Receiver set to the same name gets the lamp's value. ") + Bands);
                // The Signal Transmitter state holds a text the player can edit; the game syncs and saves it.
                UseState(settings, prefab, "SignalTransmitterState");
                AddWritingInteraction(prefab);
            }
            else
            {
                Parts.Describe(settings, Lang.T("Lampe à écran (ancienne)", "Screen Lamp (old)"),
                    Lang.T("Ancienne version, remplacée par la lampe à écran à texte. ", "Old version, replaced by the screen lamp with text. ") + Bands);
                UseState(settings, prefab, "MathGateState");
            }

            // The Gas Sensor is one mesh, 0.30 wide, 0.23 high, z -0.22 to 0.44; its window
            // fills the far end, so its axis stands half a width before that end.
            Transform root = prefab.transform;
            Bounds body;
            if (!Paint.TryBounds(prefab, root, out body))
                body = new Bounds(new Vector3(0f, 0.115f, 0.11f), new Vector3(0.30f, 0.23f, 0.66f));
            float top = body.max.y + 0.004f;
            Vector3 axis = new Vector3(body.center.x, 0f, body.max.z - body.size.x * 0.5f);
            AddCylinder(root, "SF_Lens", axis + Vector3.up * (top - LensHeight * 0.5f), LensDiameter + 0.005f, LensHeight, Paint.FirstMaterial(prefab), true);
            AddLight(root, axis + Vector3.up * (top + 0.2f));
            prefab.AddComponent<LampVisual>();
        }

        public static void BuildPorthole(PartSettings settings, GameObject prefab)
        {
            Parts.Describe(settings, Lang.T("Hublot lumineux", "Light Porthole"),
                Lang.T("Hublot qui s'allume selon le signal branché sur son côté. ",
                       "Porthole that lights up from the signal plugged into its side. ") + Bands);
            UseState(settings, prefab, "MathGateState");
            Transform root = prefab.transform;

            // Keep the splitter's first plug (on its side), alone and not relayed.
            SocketObject keep = null;
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
                if (keep == null && socket.Id == 0) keep = socket;
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
                if (socket != keep) Object.DestroyImmediate(socket.gameObject);
            if (keep != null) Sockets.ClearRelays(keep);

            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                if (r.GetComponentInParent<SocketObject>() == null) r.enabled = false;

            Material source = GasSensorMaterial() ?? Paint.FirstMaterial(prefab);
            Bounds box = settings.Bounds;
            Vector3 floor = new Vector3(box.center.x, box.min.y, box.center.z);
            AddCylinder(root, "SF_Collar", floor + Vector3.up * (CollarHeight * 0.5f), CollarDiameter, CollarHeight, source, false);
            AddCylinder(root, "SF_Lens", floor + Vector3.up * (CollarHeight + LensHeight * 0.5f), LensDiameter, LensHeight, source, true);
            AddLight(root, floor + Vector3.up * (CollarHeight + LensHeight + 0.2f));
            prefab.AddComponent<LampVisual>();
        }

        // The donor's own visual expects the donor's state; ours replaces it.
        private static void UseState(PartSettings settings, GameObject prefab, string stateClass)
        {
            settings.internalStateClass = stateClass;
            foreach (DefaultShipPartVisuals old in prefab.GetComponentsInChildren<DefaultShipPartVisuals>(true))
                Object.DestroyImmediate(old);
        }

        // The Gas Sensor has nothing to press F on. Borrow the Signal Transmitter's interaction
        // (it starts the game's "write text" mode) and stretch its collider over the sensor body.
        private static void AddWritingInteraction(GameObject prefab)
        {
            PartInteraction source = null;
            foreach (PartSettings part in Resources.FindObjectsOfTypeAll<PartSettings>())
            {
                if (part == null || part.part == null || Parts.IsOurs(part.id)) continue;
                if (part.part.GetComponentInChildren<SignalTransmitterVisuals>(true) == null) continue;
                source = part.part.GetComponentInChildren<PartInteraction>(true);
                if (source != null) break;
            }
            if (source == null)
            {
                MelonLogger.Warning("[SignalForge] screen lamp: Signal Transmitter interaction not found, its text cannot be edited");
                return;
            }

            Transform holder = prefab.transform.Find("Interactions");
            if (holder == null) holder = prefab.transform;
            Bounds body;
            if (!Paint.TryBounds(prefab, holder, out body))
                body = new Bounds(new Vector3(0f, 0.115f, 0.11f), new Vector3(0.30f, 0.23f, 0.66f));

            GameObject copy = Object.Instantiate(source.gameObject, holder, false);
            copy.name = "SF_Write";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            foreach (Collider old in copy.GetComponents<Collider>())
                Object.DestroyImmediate(old);
            BoxCollider box = copy.AddComponent<BoxCollider>();
            box.center = body.center;
            box.size = body.size + Vector3.one * 0.02f;
        }

        private static Material GasSensorMaterial()
        {
            PartSettings sensor = SDModKit.Game.PartLookup.ByAssetName("GasSensor");
            return sensor != null && sensor.part != null ? Paint.FirstMaterial(sensor.part) : null;
        }

        private static void AddCylinder(Transform root, string name, Vector3 center, float diameter, float height, Material source, bool lens)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.AddComponent<MeshFilter>().sharedMesh = Paint.CylinderMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = lens
                ? Paint.Solid(source, "sf lamp lens", LampVisual.Unlit)
                : Paint.Solid(source, "sf lamp casing", Casing);
            if (lens) go.AddComponent<LampLens>();
        }

        private static void AddLight(Transform root, Vector3 position)
        {
            GameObject glow = new GameObject(LightNode);
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = position;
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
        }
    }

    // Marks the renderer LampVisual colours.
    public class LampLens : MonoBehaviour
    {
    }
}
