
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
    string phase;
    string results;
    float time;
    public Timer()
    {
        time = Time.realtimeSinceStartup;
        results = "";
    }
    private void EndPhase()
    {
        if (phase != null)
        {
            results += $"{phase}: {(Time.realtimeSinceStartup - time) * 1000} ms\n";
            time = Time.realtimeSinceStartup;
        }
        phase = null;
    }
    public void Phase(string name)
    {
        EndPhase();
        phase = name;
    }
    public void End()
    {
        EndPhase();
        Debug.Log(results);
    }
}