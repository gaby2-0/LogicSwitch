using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Ships;
using Ships.Cables;
using Ships.Cables.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using UnityEngine;

namespace LogicSwitch
{
    // Host-side behaviour of the gates, buttons, switch, door and breaker handle.
    internal static class Server
    {
        public const float PressSeconds = 0.4f;

        private static bool _writing;
        private static readonly Dictionary<long, bool> PreviousA = new Dictionary<long, bool>();
        private static readonly Dictionary<long, float> Releases = new Dictionary<long, float>();
        private static readonly List<long> Due = new List<long>();
        private static readonly List<StatefulPart> Doors = new List<StatefulPart>();

        private static long Key(uint shipId, ushort partId)
        {
            return ((long)shipId << 16) | partId;
        }

        public static ushort IdOf(StatefulPart part)
        {
            if (part == null || part.Settings == null) return 0;
            return part.Settings.id;
        }

        // ------------------------------------------------------------------ state rewrites

        // Every gate evaluation of the vanilla signal solver and every valve update ends here,
        // so our result replaces the vanilla one before anything else reads it.
        public static void BeforeUpdatePartState(TrackedShipServer __instance, ushort partId, ref IStatefulPartState state)
        {
            if (_writing) return;
            MathGateState gate = state as MathGateState;
            FluidValveState valve = state as FluidValveState;
            if (gate == null && valve == null) return;
            try
            {
                StatefulPart part;
                if (!__instance.TryGetStatefulPart(partId, out part)) return;
                ushort id = IdOf(part);
                if (gate != null && Parts.IsGate(id))
                    state = EvaluateGate(__instance, part, gate, Parts.GateOperation(id, (int)gate.get_Operation()));
                else if (valve != null && id == Parts.BreakerId)
                    state = BreakerHandle(valve, Breaker.IsClosed(Net.Connections, __instance.Id, part.Id));
            }
            catch (Exception e)
            {
                Log.Once("UpdatePartState", e);
            }
        }

        private static MathGateState EvaluateGate(TrackedShipServer ship, StatefulPart part, MathGateState incoming, int operation)
        {
            ConnectionsServerTracker connections = Net.Connections;
            uint shipId = ship.Id;
            bool a = Net.Signal(connections, shipId, part.Id, 0) >= 0.5;
            bool b = Net.Signal(connections, shipId, part.Id, 1) >= 0.5;
            MathGateState current = part.State as MathGateState;
            bool previous = current != null && current.get_Output() >= 0.5;

            bool result;
            switch (operation)
            {
                case 0: result = a && b; break;                       // AND
                case 1: result = a || b; break;                       // OR
                case 2: result = a != b; break;                       // XOR
                case 3: result = !(a && b); break;                    // NAND
                case 4: result = !(a || b); break;                    // NOR
                case 5: result = a == b; break;                       // XNOR
                case 6: result = !a; break;                           // NOT
                case 7: result = !b && (a || previous); break;        // SR latch, reset wins
                case 8: result = !b && (RisingA(shipId, part.Id, a) ? !previous : previous); break; // T flip-flop
                default: result = a; break;                           // BUF
            }

            MathGateState next = States.Clone(incoming);
            States.Set(next, "Output", result ? 1.0 : 0.0);
            return next;
        }

        // The first reading after a load only records A, so a held signal does not flip the output.
        private static bool RisingA(uint shipId, ushort partId, bool a)
        {
            long key = Key(shipId, partId);
            bool last;
            bool known = PreviousA.TryGetValue(key, out last);
            PreviousA[key] = a;
            return known && a && !last;
        }

        // The vanilla handle turns with ValveOpenedRate: open = power flows.
        private static FluidValveState BreakerHandle(FluidValveState state, bool closed)
        {
            FluidValveState next = States.Clone(state);
            States.Set(next, "ValveOpenedRate", closed ? 1f : 0f);
            States.Set(next, "CurrentTransferRate", 0f);
            return next;
        }

        // ------------------------------------------------------------------ buttons and switch

        // Buttons pulse the lever signal and the switch flips it, instead of handing the player the lever.
        public static bool BeforeStartInteracting(TrackedShipServer __instance, ushort partId, ref bool __result)
        {
            try
            {
                StatefulPart part;
                if (!__instance.TryGetStatefulPart(partId, out part)) return true;
                ushort id = IdOf(part);
                if (!Parts.IsLever(id)) return true;
                if (Parts.IsButton(id))
                {
                    __instance.SetLeverHeight(partId, 1f);
                    Releases[Key(__instance.Id, partId)] = Time.time + PressSeconds;
                }
                else
                {
                    LeverState lever = part.State as LeverState;
                    bool on = lever != null && lever.get_LeverHeight() >= 0.5f;
                    __instance.SetLeverHeight(partId, on ? 0f : 1f);
                }
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                Log.Once("StartInteracting", e);
                return true;
            }
        }

        public static void AfterUpdateParts(TrackedShipServer __instance, float deltaTime)
        {
            try
            {
                ReleaseButtons(__instance);
                DriveDoors(__instance);
            }
            catch (Exception e)
            {
                Log.Once("UpdateParts", e);
            }
        }

        private static void ReleaseButtons(TrackedShipServer ship)
        {
            if (Releases.Count == 0) return;
            uint shipId = ship.Id;
            float now = Time.time;
            Due.Clear();
            foreach (KeyValuePair<long, float> entry in Releases)
                if ((uint)(entry.Key >> 16) == shipId && now >= entry.Value)
                    Due.Add(entry.Key);
            for (int i = 0; i < Due.Count; i++)
            {
                Releases.Remove(Due[i]);
                ship.SetLeverHeight((ushort)(Due[i] & 0xFFFF), 0f);
            }
        }

        // The normally closed push button is a two-way contact. Each plug sends out what the cable
        // network on the other plug carries, leaving out what the button itself sends into it (or
        // the signal would loop and stay on); pressed, both plugs send 0. Runs on host and clients.
        private static FieldInfo _shipsField;
        private static FieldInfo _groupMapField;
        private static MethodInfo _socketSignal;
        private static readonly HashSet<long> Visiting = new HashSet<long>();

        // Lever states only emit on plug 0; the contact also needs plug 1 (no other lever has one).
        private static readonly byte[] BothPlugs = { 0, 1 };

        public static void AfterLeverEmissionSockets(ref byte[] __result)
        {
            __result = BothPlugs;
        }

        public static void AfterGetSocketSignal(SignalConnectionGroupValues __instance, EntityPartSocket entityPartSocket, ref double __result)
        {
            byte plug = entityPartSocket.Socket.SocketId;
            if (plug > 1) return;
            long key = ((long)entityPartSocket.EntityId << 24) | ((long)entityPartSocket.Socket.PartId << 8) | plug;
            if (!Visiting.Add(key))
            {
                __result = 0;   // a loop of contacts: stop here
                return;
            }
            try
            {
                if (_shipsField == null)
                {
                    _shipsField = AccessTools.Field(typeof(SignalConnectionGroupValues), "_ships");
                    _groupMapField = AccessTools.Field(typeof(SignalConnectionGroupValues), "_groupMap");
                    _socketSignal = AccessTools.Method(typeof(SignalConnectionGroupValues), "GetSocketSignal");
                }
                IShipsProvider ships = _shipsField.GetValue(__instance) as IShipsProvider;
                IShipStateRead ship;
                StatefulPart part;
                if (ships == null || !ships.TryGetShip(entityPartSocket.EntityId, out ship)) return;
                if (!ship.TryGetStatefulPart(entityPartSocket.Socket.PartId, out part)) return;
                if (IdOf(part) != Parts.ButtonNcId) return;

                LeverState lever = part.State as LeverState;
                if (lever != null && lever.get_LeverHeight() >= 0.5f)
                {
                    __result = 0;
                    return;
                }
                __result = OtherSide(__instance, new EntityPartSocket(entityPartSocket.EntityId, part.Id, (byte)(1 - plug)));
            }
            catch (Exception e)
            {
                Log.Once("GetSocketSignal", e);
            }
            finally
            {
                Visiting.Remove(key);
            }
        }

        // The value of the cable group on `plug`, from every source in it but `plug` itself.
        private static double OtherSide(SignalConnectionGroupValues values, EntityPartSocket plug)
        {
            ConnectionGroupMap map = _groupMapField.GetValue(values) as ConnectionGroupMap;
            ConnectionGroup group;
            if (map == null || !map.TryGetConnectionGroupWithSocket(plug, out group)) return 0;
            List<EntityPartSocket> inputs, outputs;
            values.GetGroupInputsAndOutputs(group.Id, out inputs, out outputs);
            double value = 0;
            foreach (EntityPartSocket input in inputs)
            {
                if (input == plug) continue;
                double signal = (double)_socketSignal.Invoke(values, new object[] { input });
                value = SignalCablesUtils.ResolveConflictingSignal(value, signal);
            }
            return value;
        }
        // ------------------------------------------------------------------ door

        // A wired signal door follows its signal; unwired, it stays a manual door.
        private static void DriveDoors(TrackedShipServer ship)
        {
            Doors.Clear();
            foreach (StatefulPart part in ship.StatefulParts)
                if (IdOf(part) == Parts.DoorId)
                    Doors.Add(part);
            if (Doors.Count == 0) return;

            ConnectionsServerTracker connections = Net.Connections;
            uint shipId = ship.Id;
            for (int i = 0; i < Doors.Count; i++)
            {
                DoorState door = Doors[i].State as DoorState;
                if (door == null || !Net.IsWired(connections, shipId, Doors[i].Id, Parts.DoorSignalSocket)) continue;
                bool open = Net.Signal(connections, shipId, Doors[i].Id, Parts.DoorSignalSocket) >= 0.5;
                if (door.get_IsOpen() == open) continue;
                DoorState next = States.Clone(door);
                States.Set(next, "IsOpen", open);
                _writing = true;
                try { ship.UpdatePartState(Doors[i].Id, next); }
                finally { _writing = false; }
            }
        }
    }
}
