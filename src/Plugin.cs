using System.Reflection;
using HarmonyLib;
using MelonLoader;
using SDModKit;
using SDModKit.Api;
using SDModKit.Configuration;
using SDModKit.Patching;
using Ships;
using Ships.Interface.Settings;
using Ships.Parts.FluidValve;
using Ships.Parts.MathGate;

[assembly: MelonInfo(typeof(SignalForge.Plugin), "StellarDrive SignalForge", SignalForge.Plugin.Version, SignalForge.Plugin.Author)]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("SDModKit")]
[assembly: HarmonyDontPatchAll]
[assembly: StellarMod("signalforge", "SignalForge",
    Description = "Signal lamps, logic gates, push buttons, switch, circuit breaker and signal door.")]

namespace SignalForge
{
    public class Plugin : MelonMod
    {
        public const string Guid = "stellardrive.signalforge";
        public const string Version = "1.4.0";
        public const string Author = "leterrible20";

        public override void OnInitializeMelon()
        {
            ModKit.Register(new SignalForgeMod(), ConfigFile.For(Guid));
        }
    }

    public class SignalForgeMod : SDMod
    {
        public override string Id { get { return "signalforge"; } }
        public override string Name { get { return "SignalForge"; } }

        public override void OnRegistered()
        {
            Parts.RegisterAll();
            Patch(ModPatches.For(Plugin.Guid));
            MelonLogger.Msg("[SignalForge] " + Plugin.Version + " ready");
        }

        private static void Patch(HarmonyLib.Harmony harmony)
        {
            System.Type ship = typeof(TrackedShipServer);

            // Host side: gates, breaker handle, buttons, switch and doors.
            harmony.Patch(AccessTools.Method(ship, "UpdatePartState"),
                prefix: new HarmonyMethod(typeof(Server), "BeforeUpdatePartState"));
            harmony.Patch(AccessTools.Method(ship, "StartInteracting"),
                prefix: new HarmonyMethod(typeof(Server), "BeforeStartInteracting"));
            harmony.Patch(AccessTools.Method(ship, "UpdateParts"),
                postfix: new HarmonyMethod(typeof(Server), "AfterUpdateParts"));

            // Breaker power transfer: Pass early among the producer hooks, Settle once they all ran.
            MethodInfo producers = AccessTools.Method(ship, "PreparePowerProducers");
            HarmonyMethod pass = new HarmonyMethod(typeof(Breaker), "Pass");
            pass.priority = 590;
            harmony.Patch(producers, postfix: pass);
            HarmonyMethod settle = new HarmonyMethod(typeof(Breaker), "Settle");
            settle.priority = 0;
            harmony.Patch(producers, postfix: settle);

            // Client side: gate labels and breaker status light.
            harmony.Patch(AccessTools.Method(typeof(MathGateVisuals), "SetNetworkState"),
                postfix: new HarmonyMethod(typeof(Visuals), "AfterMathGateState"));
            harmony.Patch(AccessTools.Method(typeof(FluidValveVisual), "UpdateValveHandle"),
                postfix: new HarmonyMethod(typeof(Visuals), "AfterValveHandle"));
            harmony.Patch(AccessTools.Method(typeof(Ships.Parts.Common.PartInteraction), "Interact"),
                postfix: new HarmonyMethod(typeof(HoldToPress), "AfterInteract"));

            PatchByName(harmony, "Ships.Cables.SignalConnectionGroupValues:GetSocketSignal",
                typeof(Server), "AfterGetSocketSignal", "normally closed push button");
            harmony.Patch(AccessTools.Method(typeof(UI.Guide.GuideCanvas), "InstantiateButtons"),
                postfix: new HarmonyMethod(typeof(Guide), "AfterInstantiateButtons"));
            harmony.Patch(AccessTools.PropertyGetter(typeof(Ships.Interface.Model.Parts.State.LeverState), "EmissionSockets"),
                postfix: new HarmonyMethod(typeof(Server), "AfterLeverEmissionSockets"));
            // The build menu only lists parts unlocked by a finished research; ours have none.
            PatchByName(harmony, "Research.ResearchClientProxy:IsPartUnlocked",
                typeof(SignalForgeMod), "AfterIsPartUnlocked", "build menu");
        }

        private static void PatchByName(HarmonyLib.Harmony harmony, string target, System.Type owner, string postfix, string feature)
        {
            MethodInfo method = AccessTools.Method(target);
            if (method != null)
                harmony.Patch(method, postfix: new HarmonyMethod(owner, postfix));
            else
                MelonLogger.Warning("[SignalForge] " + target + " not found, " + feature + " disabled");
        }

        public static void AfterIsPartUnlocked(PartSettings part, ref bool __result)
        {
            if (!__result && part != null && Parts.IsOurs(part.id))
                __result = true;
        }
    }
}
