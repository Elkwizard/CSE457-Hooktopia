
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
class IdentityEqualityComparer<T> : IEqualityComparer<T> where T : class
{
    public bool Equals(T v1, T v2)
    {
        return ReferenceEquals(v1, v2);
    }
    public int GetHashCode(T v)
    {
        return RuntimeHelpers.GetHashCode(v);
    }
}


public static class Util
{
    public static Dictionary<K, V> MakeMap<K, V>() where K : class
    {
        return new(new IdentityEqualityComparer<K>());
    }
    public static HashSet<T> MakeSet<T>() where T : class
    {
        return new(new IdentityEqualityComparer<T>());
    }
}
public class Timer
{
    private readonly string title;
    private readonly float startTime;
    private string currentPhase;
    private Dictionary<string, (float total, int count)> phases = new();
    private float phaseStartTime;
    public Timer(string _title)
    {
        startTime = phaseStartTime = Time.realtimeSinceStartup;
        title = _title;
    }
    private string Format(float seconds)
    {
        return $"{seconds * 1000} ms";
    }
    private void EndPhase()
    {
        if (currentPhase != null)
        {
            float duration = Time.realtimeSinceStartup - phaseStartTime;
            var (total, count) = phases.GetValueOrDefault(currentPhase, (0, 0));
            phases[currentPhase] = (total + duration, count + 1);
            phaseStartTime = Time.realtimeSinceStartup;
        }
        currentPhase = null;
    }
    public void Phase(string name)
    {
        EndPhase();
        currentPhase = name;
    }
    public void End()
    {
        EndPhase();
        float duration = Time.realtimeSinceStartup - startTime;
        string log = $"{title}: {Format(duration)}";

        foreach (var (name, (total, count)) in phases)
        {
            log += $"\n{name}: {Format(total)}";
        }
        
        Debug.Log(log);
    }
}