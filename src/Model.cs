using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using UnityEngine;

namespace LogicSwitch
{
    // Turning a donor's fluid plug into a power plug, as the Resistor of Functional Batteries does.
    internal static class Sockets
    {
        private static readonly System.Reflection.FieldInfo TypeField = AccessTools.Field(typeof(SocketObject), "type");
        private static readonly System.Reflection.FieldInfo RelayField = AccessTools.Field(typeof(SocketObject), "relayedSockets");

        // A vanilla power plug, whose visual and interaction objects the converted plug borrows.
        public static SocketObject PowerTemplate()
        {
            foreach (PartSettings part in Resources.FindObjectsOfTypeAll<PartSettings>())
            {
                if (part == null || part.part == null || Parts.IsOurs(part.id)) continue;
                foreach (SocketObject s in part.part.GetComponentsInChildren<SocketObject>(true))
                    if (s.Type == CableType.Power && s.visualObject != null && s.interactive != null)
                        return s;
            }
            return null;
        }

        public static void MakePower(SocketObject socket, SocketObject template)
        {
            TypeField.SetValue(socket, CableType.Power);
            ClearRelays(socket);
            socket.shouldCreateFluidBufferWhenTouchingAnotherSocket = false;
            if (template == null)
            {
                MelonLogger.Warning("[Logic&Switch] no vanilla power plug found, plug " + socket.Id + " keeps its fluid look");
                return;
            }
            socket.visualObject = template.visualObject;
            socket.interactive = template.interactive;
        }

        public static void ClearRelays(SocketObject socket)
        {
            RelayField.SetValue(socket, new SocketObject[0]);
        }
    }

    // Materials, meshes and bounds for the parts' extra geometry.
    internal static class Paint
    {
        private static Mesh _sphere;
        private static Mesh _cylinder;

        public static Mesh SphereMesh()
        {
            if (_sphere == null) _sphere = Primitive(PrimitiveType.Sphere);
            return _sphere;
        }

        // Unity's cylinder: 1 wide, 2 high, centred on its origin.
        public static Mesh CylinderMesh()
        {
            if (_cylinder == null) _cylinder = Primitive(PrimitiveType.Cylinder);
            return _cylinder;
        }

        private static Mesh Primitive(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        public static Material FirstMaterial(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (Material m in r.sharedMaterials)
                    if (m != null) return m;
            }
            return null;
        }

        public static Material Clone(Material source, string name)
        {
            Material m = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return m;
        }

        // Same shader as the source (so the game's outline style stays) but no texture: part
        // textures are atlases laid out for their own mesh and turn to noise on our primitives.
        public static Material Solid(Material source, string name, Color color)
        {
            Material m = Clone(source, name);
            foreach (string texture in m.GetTexturePropertyNames())
                m.SetTexture(texture, null);
            SetColor(m, color, Color.black);
            return m;
        }

        public static Color BaseColor(Material m)
        {
            if (m == null) return Color.gray;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.gray;
        }

        public static void SetColor(Material m, Color body, Color emission)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", body);
            if (m.HasProperty("_Color")) m.SetColor("_Color", body);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
        }

        // Multiplies (or replaces) the base colour of every material under root,
        // leaving the cable plugs and the optional excluded branch alone.
        public static void Tint(GameObject root, Color color, string tag, bool replace, Transform exclude)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<SocketObject>() != null) continue;
                if (exclude != null && r.transform.IsChildOf(exclude)) continue;
                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Material m = Clone(materials[i], materials[i].name + " (" + tag + ")");
                    Color c = replace ? color : BaseColor(m) * color;
                    c.a = BaseColor(materials[i]).a;
                    SetColor(m, c, Color.black);
                    materials[i] = m;
                }
                r.sharedMaterials = materials;
            }
        }

        public static void SetWorldScale(Transform t, Vector3 world)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(
                world.x / Mathf.Max(1e-4f, Mathf.Abs(parent.x)),
                world.y / Mathf.Max(1e-4f, Mathf.Abs(parent.y)),
                world.z / Mathf.Max(1e-4f, Mathf.Abs(parent.z)));
        }

        // Mesh bounds of everything under root, in the space of `space`
        // (renderer.bounds is empty on the inactive prefabs SDModKit hands us).
        public static bool TryBounds(GameObject root, Transform space, out Bounds result)
        {
            result = new Bounds();
            bool any = false;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds b;
                if (!MeshBounds(mf, space, out b)) continue;
                if (!any) { result = b; any = true; }
                else result.Encapsulate(b);
            }
            return any;
        }

        public static bool MeshBounds(MeshFilter mf, Transform space, out Bounds result)
        {
            result = new Bounds();
            if (mf == null || mf.sharedMesh == null) return false;
            Bounds local = mf.sharedMesh.bounds;
            Matrix4x4 m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Vector3 c = local.center, e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (i == 0) result = new Bounds(p, Vector3.zero);
                else result.Encapsulate(p);
            }
            return true;
        }
    }

    // Bulb icon for the build menu tab.
    internal static class Icon
    {
        public static Texture2D Make()
        {
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "Logic&Switch icon";
            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - 31.5f, dy = y - 38f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool bulb = r < 18f && r > 13f;
                    bool filament = Mathf.Abs(dx) < 2f && y > 30 && y < 44;
                    bool screw = Mathf.Abs(dx) < 8f && y > 10 && y < 22 && y % 4 != 0;
                    t.SetPixel(x, y, bulb || filament || screw ? Color.white : clear);
                }
            t.Apply();
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }
    }
}
