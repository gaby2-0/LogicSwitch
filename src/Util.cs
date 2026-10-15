using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using SDModKit.Game;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using UnityEngine;

namespace LogicSwitch
{
    // The game's part states are C# records with init-only setters, which this
    // compiler cannot call, so states are cloned and written through reflection.
    internal static class States
    {
        private static readonly Dictionary<Type, MethodInfo> Clones = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();

        public static T Clone<T>(T state) where T : class
        {
            Type type = state.GetType();
            MethodInfo clone;
            if (!Clones.TryGetValue(type, out clone))
            {
                clone = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Clones[type] = clone;
            }
            return (T)clone.Invoke(state, null);
        }

        public static void Set(object state, string property, object value)
        {
            Type type = state.GetType();
            string key = type.FullName + "." + property;
            FieldInfo field;
            if (!Fields.TryGetValue(key, out field))
            {
                field = AccessTools.Field(type, "<" + property + ">k__BackingField");
                Fields[key] = field;
            }
            field.SetValue(state, value);
        }
    }

    internal static class Net
    {
        public static ConnectionsServerTracker Connections
        {
            get { return GameRefs.Service<ConnectionsServerTracker>(); }
        }

        public static PowerServerTracker Power
        {
            get { return GameRefs.Service<PowerServerTracker>(); }
        }

        public static bool IsWired(ConnectionsServerTracker connections, uint shipId, ushort partId, byte socket)
        {
            if (connections == null) return false;
            SocketConnection cable;
            return connections.TryGetSocketCable(new EntityPartSocket(shipId, partId, socket), CableType.Signal, out cable);
        }

        public static double Signal(ConnectionsServerTracker connections, uint shipId, ushort partId, byte socket)
        {
            if (connections == null) return 0;
            return connections.GetSocketSignalValue(new EntityPartSocket(shipId, partId, socket));
        }

        public static bool TryGroup(PowerServerTracker power, uint shipId, ushort partId, byte socket, out PowerGroupServerState group)
        {
            group = null;
            if (power == null) return false;
            return power.TryGetGroupFromSocket(new EntityPartSocket(shipId, partId, socket), out group);
        }
    }

    internal static class Log
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();

        public static void Once(string context, Exception error)
        {
            if (Seen.Add(context))
                MelonLogger.Error("[Logic&Switch] " + context + " : " + error);
        }
    }
}
