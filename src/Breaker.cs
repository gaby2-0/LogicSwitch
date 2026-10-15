using System;
using System.Collections.Generic;
using Ships;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace LogicSwitch
{
    // Circuit breaker: while closed, moves power between the groups on its two power plugs,
    // from the side with spare production to the side that is short (host only).
    internal static class Breaker
    {
        public const float MaxWatts = 100000f;

        private struct Transfer
        {
            public PowerGroupServerState From;
            public PowerGroupServerState To;
            public float Amount;
        }

        private static readonly List<Transfer> Passed = new List<Transfer>();

        // Either signal plug (0 or 1) at 0.5 or more closes it; with no signal cable it stays closed.
        public static bool IsClosed(ConnectionsServerTracker connections, uint shipId, ushort partId)
        {
            bool wired = false;
            for (byte socket = 0; socket <= 1; socket++)
            {
                if (!Net.IsWired(connections, shipId, partId, socket)) continue;
                wired = true;
                if (Net.Signal(connections, shipId, partId, socket) >= 0.5) return true;
            }
            return !wired;
        }

        public static void Pass(TrackedShipServer __instance, float deltaTime)
        {
            Passed.Clear();
            try
            {
                PowerServerTracker power = Net.Power;
                ConnectionsServerTracker connections = Net.Connections;
                if (power == null || connections == null || deltaTime <= 0f) return;
                uint shipId = __instance.Id;
                foreach (StatefulPart part in __instance.StatefulParts)
                {
                    if (Server.IdOf(part) != Parts.BreakerId) continue;
                    if (!IsClosed(connections, shipId, part.Id)) continue;
                    PowerGroupServerState a, b;
                    if (!Net.TryGroup(power, shipId, part.Id, Parts.BreakerSideA, out a)) continue;
                    if (!Net.TryGroup(power, shipId, part.Id, Parts.BreakerSideB, out b)) continue;
                    if (a == b) continue;
                    float limit = MaxWatts * deltaTime;
                    float shortB = b.TotalPowerNeeded - b.TotalPowerProductionCapacity;
                    float shortA = a.TotalPowerNeeded - a.TotalPowerProductionCapacity;
                    if (shortB > 0f) Move(a, b, Mathf.Min(shortB, limit));
                    else if (shortA > 0f) Move(b, a, Mathf.Min(shortA, limit));
                }
            }
            catch (Exception e)
            {
                Log.Once("Breaker.Pass", e);
            }
        }

        // The receiving side gets the power as production, the giving side as extra demand.
        private static void Move(PowerGroupServerState from, PowerGroupServerState to, float amount)
        {
            from.TotalPowerNeeded += amount;
            to.TotalPowerProductionCapacity += amount;
            Transfer t;
            t.From = from;
            t.To = to;
            t.Amount = amount;
            Passed.Add(t);
        }

        // Runs once every producer has reported: a starved source only delivers its share.
        public static void Settle()
        {
            for (int i = 0; i < Passed.Count; i++)
            {
                Transfer t = Passed[i];
                if (t.From.TotalPowerNeeded <= 0f) continue;
                if (t.From.TotalPowerProductionCapacity >= t.From.TotalPowerNeeded) continue;
                float delivered = t.Amount * Mathf.Clamp01(t.From.TotalPowerProductionCapacity / t.From.TotalPowerNeeded);
                t.To.TotalPowerProductionCapacity -= t.Amount - delivered;
            }
            Passed.Clear();
        }
    }
}
