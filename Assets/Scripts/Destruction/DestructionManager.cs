using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

abstract class PriorityScheduler<T> where T : class
{
    private class Task
    {
        public readonly T owner;
        public readonly Action complete;
        public float priority = 0;

        public Task(T _owner, Action _complete)
        {
            owner = _owner;
            complete = _complete;
        }
    }

    private LinkedList<Task> tasks;
    private float averageTaskTime;
    private int completedTasks;

    public PriorityScheduler()
    {
        tasks = new();
        completedTasks = 0;
        averageTaskTime = 0;
    }
    public void AddTask(T owner, Action task)
    {
        tasks.AddLast(new Task(owner, task));
    }
    public void Complete(T owner)
    {
        List<LinkedListNode<Task>> toComplete = new();
        for (LinkedListNode<Task> current = tasks.First; current != null; current = current.Next)
        {
            if (current.Value.owner == owner)
            {

            toComplete.Add(current);
            }
        }

        foreach (var node in toComplete)
        {
            tasks.Remove(node);
            node.Value.complete();
        }
    }
    protected abstract float GetPriority(T owner);
    private Action ExtractNextTask()
    {
        float maxPriority = float.NegativeInfinity;
        LinkedListNode<Task> best = null;

        for (LinkedListNode<Task> current = tasks.First; current != null; current = current.Next)
        {
            var priority = current.Value.priority;
            if (priority >= maxPriority)
            {
                maxPriority = priority;
                best = current;
            }
        }

        tasks.Remove(best);

        return best.Value.complete;
    }
    public virtual void Run(float timeBudget)
    {
        if (tasks.Count == 0 || timeBudget <= 0) return;

        // compute priorities for this run
        var ownerToPriority = Util.MakeMap<T, float>();
        foreach (var task in tasks)
        {
            if (!ownerToPriority.ContainsKey(task.owner))
                ownerToPriority[task.owner] = GetPriority(task.owner);
            task.priority = ownerToPriority[task.owner];
        }

        // run as many tasks as we reasonably can
        do
        {
            float startTime = Time.realtimeSinceStartup;

            ExtractNextTask()();

            // incorporate performance data for future frames
            float duration = Time.realtimeSinceStartup - startTime;
            averageTaskTime = (averageTaskTime * completedTasks + duration) / (completedTasks + 1);
            completedTasks++;
            timeBudget -= duration;
        } while (timeBudget > averageTaskTime);
    }
}

class BVH<T>
{
    private readonly List<(T, Bounds)> items;

    public int Count { get => items.Count; }

    private BVH(List<(T, Bounds)> _items)
    {
        items = _items;
    }

    public List<T> Query(Bounds bounds)
    {
        return items
                .Where(t => t.Item2.Intersects(bounds))
                .Select(t => t.Item1)
                .ToList();
    }

    public static BVH<T> Build(IEnumerable<(T, Bounds)> bounds)
    {
        return new(bounds.ToList());
    }
}

class DestructibleSetupScheduler : PriorityScheduler<Destructible>
{
    private List<GameObject> hazards = new();
    protected override float GetPriority(Destructible obj)
    {
        return hazards
            .Select(hazard => (obj.gameObject.transform.position - hazard.transform.position).sqrMagnitude)
            .Aggregate(float.PositiveInfinity, Mathf.Min);
    }

    public override void Run(float timeBudget)
    {
        hazards = hazards.Where(hazard => hazard != null).ToList();
        base.Run(timeBudget);
    }

    public void AddHazard(GameObject hazard)
    {
        hazards.Add(hazard);
    }

}

public class DestructionManager : MonoBehaviour
{
    private static readonly float TIME_BUDGET = 0.01f; // seconds
    private static DestructionManager instanceCache;
    public static DestructionManager GetInstance()
    {
        if (instanceCache == null)
        {
            var manager = new GameObject("destruction manager");
            instanceCache = manager.AddComponent<DestructionManager>();
        }
        return instanceCache;
    }
    private BVH<Destructible> bvh;
    private readonly List<(Destructible, Bounds)> walls = new();
    private readonly Queue<(Destructible wall, Sphere hitSphere)> breakRequests = new();
    private readonly DestructibleSetupScheduler setupScheduler = new();

    void Update()
    {
        float startTime = Time.realtimeSinceStartup;
        if (breakRequests.Count > 0)
        {
            var (wall, hitSphere) = breakRequests.Dequeue();
            setupScheduler.Complete(wall);
            wall.Break(hitSphere);
        }
        float remainingTime = TIME_BUDGET - (Time.realtimeSinceStartup - startTime);
        setupScheduler.Run(remainingTime);
    }

    public void AddHazard(GameObject hazard)
    {
        setupScheduler.AddHazard(hazard);
    }

    public void AddDestructible(Destructible wall, Action setup)
    {
        walls.Add((
            wall,
            wall.gameObject.GetComponent<MeshCollider>().bounds
        ));
        setupScheduler.AddTask(wall, setup);
    }

    public void Break(Sphere sphere)
    {
        if (bvh == null || walls.Count > bvh.Count)
        {
            bvh = BVH<Destructible>.Build(walls);
        }
        foreach (var wall in bvh.Query(sphere.Bounds))
        {
            breakRequests.Enqueue((wall, sphere));
        }
    }
}