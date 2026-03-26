using System.Reflection.Metadata;

namespace Dotforge.Runtime.Gc;

internal sealed class GenerationalHeap
{
    private readonly HashSet<IHeapObject> _gen0 = [];
    private readonly HashSet<IHeapObject> _gen1 = [];
    private readonly HashSet<IHeapObject> _loh = [];
    private readonly HashSet<IHeapObject> _remembered = [];
    private readonly Dictionary<int, HandleEntry> _handles = [];
    private readonly Dictionary<IHeapObject, Action<IHeapObject>> _finalizers = [];
    private readonly Queue<IHeapObject> _finalizationQueue = [];

    private long _minorCollections;
    private long _majorCollections;
    private int _lastCollected;
    private int _lastPromoted;
    private int _nextHandleId = 1;

    public int Gen0BudgetBytes { get; set; } = 512 * 1024;
    public int Gen1BudgetBytes { get; set; } = 4 * 1024 * 1024;
    public int LohThresholdBytes { get; set; } = 85 * 1024;
    public Action<string>? Logger { get; set; }

    public DotObject AllocateObject(TypeDefinitionHandle typeHandle, string typeName, IEnumerable<string> fields)
    {
        var obj = new DotObject(typeHandle, typeName, fields, this);
        TrackAllocation(obj);
        return obj;
    }

    public DotArray AllocateArray(string elementTypeName, int length)
    {
        var array = new DotArray(elementTypeName, length, this);
        TrackAllocation(array);
        return array;
    }

    public int CreateHandle(IHeapObject target, GcHandleKind kind)
    {
        var id = _nextHandleId++;
        _handles[id] = new HandleEntry(id, kind, target);
        return id;
    }

    public IHeapObject? ResolveHandle(int handleId)
    {
        if (!_handles.TryGetValue(handleId, out var entry))
        {
            return null;
        }

        return entry.Kind == GcHandleKind.Strong
            ? entry.StrongRef
            : (entry.WeakRef is null ? null : (entry.WeakRef.TryGetTarget(out var target) ? target : null));
    }

    public void FreeHandle(int handleId)
    {
        _handles.Remove(handleId);
    }

    public void RegisterFinalizer(IHeapObject target, Action<IHeapObject> callback)
    {
        _finalizers[target] = callback;
    }

    public void WriteBarrier(IHeapObject owner, object? assignedValue)
    {
        if (owner.Generation != 1)
        {
            return;
        }

        if (assignedValue is IHeapObject child && child.Generation == 0)
        {
            _remembered.Add(owner);
        }
    }

    public void MaybeCollect(IEnumerable<IHeapObject> roots)
    {
        if (CurrentGen0Bytes() > Gen0BudgetBytes)
        {
            CollectMinor(roots);
        }

        if (CurrentGen1Bytes() > Gen1BudgetBytes)
        {
            CollectMajor(roots);
        }
    }

    public void CollectMinor(IEnumerable<IHeapObject> roots)
    {
        _minorCollections++;
        var before = _gen0.Count;
        var promoted = 0;

        MarkFromRoots(roots, onlyGen0: true);
        foreach (var remembered in _remembered)
        {
            MarkReachableFrom(remembered, onlyGen0: true);
        }

        foreach (var obj in _gen0.ToArray())
        {
            if (!obj.Marked)
            {
                QueueFinalizerIfNeeded(obj);
                _gen0.Remove(obj);
                continue;
            }

            obj.ClearMark();
            obj.SetGeneration(1);
            _gen0.Remove(obj);
            _gen1.Add(obj);
            promoted++;
        }

        _remembered.RemoveWhere(x => !_gen1.Contains(x));
        DrainFinalizationQueue();

        _lastCollected = before - _gen0.Count;
        _lastPromoted = promoted;
        Logger?.Invoke($"[GC] minor #{_minorCollections}: collected={_lastCollected}, promoted={promoted}, gen0={_gen0.Count}, gen1={_gen1.Count}, loh={_loh.Count}");
    }

    public void CollectMajor(IEnumerable<IHeapObject> roots)
    {
        _majorCollections++;
        var before = _gen0.Count + _gen1.Count + _loh.Count;

        MarkFromRoots(roots, onlyGen0: false);
        SweepGeneration(_gen0);
        SweepGeneration(_gen1);
        SweepGeneration(_loh);

        _remembered.RemoveWhere(x => !_gen1.Contains(x));
        CompactGeneration(_gen1);
        CompactGeneration(_loh);
        DrainFinalizationQueue();

        var after = _gen0.Count + _gen1.Count + _loh.Count;
        _lastCollected = before - after;
        _lastPromoted = 0;
        Logger?.Invoke($"[GC] major #{_majorCollections}: collected={_lastCollected}, gen0={_gen0.Count}, gen1={_gen1.Count}, loh={_loh.Count}");
    }

    public GcStats GetStats()
    {
        return new GcStats(
            MinorCollections: _minorCollections,
            MajorCollections: _majorCollections,
            Gen0Count: _gen0.Count,
            Gen1Count: _gen1.Count,
            LohCount: _loh.Count,
            LastCollected: _lastCollected,
            LastPromoted: _lastPromoted);
    }

    private void TrackAllocation(IHeapObject obj)
    {
        if (obj.EstimatedSizeBytes >= LohThresholdBytes)
        {
            obj.SetGeneration(1);
            _loh.Add(obj);
        }
        else
        {
            obj.SetGeneration(0);
            _gen0.Add(obj);
        }
    }

    private int CurrentGen0Bytes() => _gen0.Sum(static x => x.EstimatedSizeBytes);
    private int CurrentGen1Bytes() => _gen1.Sum(static x => x.EstimatedSizeBytes);

    private void MarkFromRoots(IEnumerable<IHeapObject> roots, bool onlyGen0)
    {
        foreach (var root in roots)
        {
            MarkReachableFrom(root, onlyGen0);
        }

        foreach (var handle in _handles.Values)
        {
            if (handle.Kind != GcHandleKind.Strong || handle.StrongRef is null)
            {
                continue;
            }

            MarkReachableFrom(handle.StrongRef, onlyGen0);
        }
    }

    private static void MarkReachableFrom(IHeapObject root, bool onlyGen0)
    {
        var stack = new Stack<IHeapObject>();
        if ((!onlyGen0 || root.Generation == 0) && !root.Marked)
        {
            root.Mark();
            stack.Push(root);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var child in current.EnumerateReferences())
            {
                if (onlyGen0 && child.Generation != 0)
                {
                    continue;
                }

                if (child.Marked)
                {
                    continue;
                }

                child.Mark();
                stack.Push(child);
            }
        }
    }

    private void SweepGeneration(HashSet<IHeapObject> generation)
    {
        foreach (var obj in generation.ToArray())
        {
            if (obj.Marked)
            {
                obj.ClearMark();
                continue;
            }

            QueueFinalizerIfNeeded(obj);
            generation.Remove(obj);
        }
    }

    private void CompactGeneration(HashSet<IHeapObject> generation)
    {
        // Simulation for runtime model: preserve order-independent survivors,
        // but keep a dedicated compaction phase for future relocation maps.
        var survivors = generation.ToArray();
        generation.Clear();
        foreach (var survivor in survivors)
        {
            generation.Add(survivor);
        }
    }

    private void QueueFinalizerIfNeeded(IHeapObject obj)
    {
        if (_finalizers.ContainsKey(obj))
        {
            _finalizationQueue.Enqueue(obj);
        }
    }

    private void DrainFinalizationQueue()
    {
        while (_finalizationQueue.Count > 0)
        {
            var obj = _finalizationQueue.Dequeue();
            if (_finalizers.TryGetValue(obj, out var callback))
            {
                callback(obj);
                _finalizers.Remove(obj);
            }
        }

        foreach (var weakEntry in _handles.Values.Where(static h => h.Kind == GcHandleKind.Weak).ToArray())
        {
            if (weakEntry.WeakRef is null)
            {
                continue;
            }

            if (!weakEntry.WeakRef.TryGetTarget(out var target) ||
                (!IsTracked(target)))
            {
                _handles[weakEntry.Id] = weakEntry with { WeakRef = null };
            }
        }
    }

    private bool IsTracked(IHeapObject target)
    {
        return _gen0.Contains(target) || _gen1.Contains(target) || _loh.Contains(target);
    }

    private sealed record HandleEntry(
        int Id,
        GcHandleKind Kind,
        IHeapObject? StrongRef,
        WeakReference<IHeapObject>? WeakRef = null)
    {
        public HandleEntry(int id, GcHandleKind kind, IHeapObject target)
            : this(
                id,
                kind,
                kind == GcHandleKind.Strong ? target : null,
                kind == GcHandleKind.Weak ? new WeakReference<IHeapObject>(target) : null)
        {
        }
    }
}
