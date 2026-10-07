using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Cave.Diagnostics
{
    /// <summary>Bounded, registration-based counters. They observe runtime work; no counter is gameplay authority.</summary>
    public enum RuntimeWorkCategory { Projectile = 0, Vfx = 1, Impact = 2, TemporaryObject = 3 }

    public readonly struct RuntimeWorkTelemetry
    {
        public RuntimeWorkTelemetry(int active, int available, int misses, int expansions, int peakActive, int suppressedCosmetic)
        { Active = active; Available = available; Misses = misses; Expansions = expansions; PeakActive = peakActive; SuppressedCosmetic = suppressedCosmetic; }
        public int Active { get; } public int Available { get; } public int Misses { get; }
        public int Expansions { get; } public int PeakActive { get; } public int SuppressedCosmetic { get; }
    }

    public static class RuntimeTelemetry
    {
        private sealed class Counter
        {
            public int Active; public int Available; public int Misses; public int Expansions; public int PeakActive; public int SuppressedCosmetic;
        }

        private static readonly Counter[] Counters = { new Counter(), new Counter(), new Counter(), new Counter() };
        private static int activeMobs;
        private static int activeFormations;
        private static int ctcEvaluations;
        private static int resolverEvaluations;
        private static double ctcMilliseconds;
        private static double resolverMilliseconds;

        public static int ActiveMobs => activeMobs;
        public static int ActiveFormations => activeFormations;
        public static int CtcEvaluations => ctcEvaluations;
        public static int ResolverEvaluations => resolverEvaluations;
        public static float AverageCtcMilliseconds => ctcEvaluations == 0 ? 0f : (float)(ctcMilliseconds / ctcEvaluations);
        public static float AverageResolverMilliseconds => resolverEvaluations == 0 ? 0f : (float)(resolverMilliseconds / resolverEvaluations);

        public static void RegisterMob(bool active) { activeMobs = Mathf.Max(0, activeMobs + (active ? 1 : -1)); }
        public static void RegisterFormation(bool active) { activeFormations = Mathf.Max(0, activeFormations + (active ? 1 : -1)); }
        public static void RecordCtcEvaluation(float milliseconds) { ctcEvaluations++; ctcMilliseconds += Math.Max(0d, milliseconds); }
        public static void RecordResolverEvaluation(float milliseconds) { resolverEvaluations++; resolverMilliseconds += Math.Max(0d, milliseconds); }

        public static void Acquired(RuntimeWorkCategory category)
        {
            Counter counter = Counters[(int)category]; counter.Active++; counter.PeakActive = Math.Max(counter.PeakActive, counter.Active);
        }
        public static void Returned(RuntimeWorkCategory category) { Counters[(int)category].Active = Mathf.Max(0, Counters[(int)category].Active - 1); }
        public static void SetAvailable(RuntimeWorkCategory category, int value) { Counters[(int)category].Available = Mathf.Max(0, value); }
        public static void RecordPoolMiss(RuntimeWorkCategory category) { Counters[(int)category].Misses++; }
        public static void RecordPoolExpansion(RuntimeWorkCategory category) { Counters[(int)category].Expansions++; }
        public static void RecordSuppressedCosmetic(RuntimeWorkCategory category) { Counters[(int)category].SuppressedCosmetic++; }
        public static RuntimeWorkTelemetry Get(RuntimeWorkCategory category)
        {
            Counter counter = Counters[(int)category];
            return new RuntimeWorkTelemetry(counter.Active, counter.Available, counter.Misses, counter.Expansions, counter.PeakActive, counter.SuppressedCosmetic);
        }
    }

    /// <summary>Optional typed pool contract. Gameplay callers must use a fallback if Acquire returns false.</summary>
    public interface IRuntimePoolTelemetry
    {
        RuntimeWorkCategory Category { get; }
        RuntimeWorkTelemetry Telemetry { get; }
    }

    /// <summary>
    /// Small ownership-neutral pool foundation. It knows only acquire/reset/return;
    /// callers retain gameplay fallback responsibility when a required object is needed.
    /// </summary>
    public sealed class RuntimeObjectPool<T> : IRuntimePoolTelemetry where T : class
    {
        private readonly List<T> available = new List<T>();
        private readonly Func<T> create;
        private readonly Action<T> reset;
        public RuntimeObjectPool(RuntimeWorkCategory category, Func<T> create, Action<T> reset = null)
        { Category = category; this.create = create; this.reset = reset; RuntimeTelemetry.SetAvailable(category, 0); }
        public RuntimeWorkCategory Category { get; }
        public RuntimeWorkTelemetry Telemetry => RuntimeTelemetry.Get(Category);
        public bool TryAcquire(out T value)
        {
            if (available.Count > 0)
            {
                int index=available.Count-1; value=available[index]; available.RemoveAt(index); RuntimeTelemetry.SetAvailable(Category,available.Count); RuntimeTelemetry.Acquired(Category); return value!=null;
            }
            RuntimeTelemetry.RecordPoolMiss(Category); value=create!=null?create():null;
            if(value==null)return false;
            RuntimeTelemetry.RecordPoolExpansion(Category); RuntimeTelemetry.Acquired(Category); return true;
        }
        public void Return(T value)
        {
            if(value==null)return; reset?.Invoke(value); available.Add(value); RuntimeTelemetry.Returned(Category); RuntimeTelemetry.SetAvailable(Category,available.Count);
        }
    }
}
