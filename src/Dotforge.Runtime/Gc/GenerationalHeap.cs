using System.Reflection.Metadata;

namespace Dotforge.Runtime.Gc;

internal sealed class GenerationalHeap
{
    private readonly HashSet<DotObject> _young = [];
    private readonly HashSet<DotObject> _old = [];
    private readonly HashSet<DotObject> _rememberedOld = [];

    public DotObject AllocateObject(TypeDefinitionHandle typeHandle, string typeName, IEnumerable<string> fields)
    {
        var obj = new DotObject(typeHandle, typeName, fields, this);
        _young.Add(obj);
        return obj;
    }

    public void WriteBarrier(DotObject owner, object? assignedValue)
    {
        if (owner.Generation != 1)
        {
            return;
        }

        if (assignedValue is DotObject child && child.Generation == 0)
        {
            _rememberedOld.Add(owner);
        }
    }

    public void CollectMinor(IEnumerable<DotObject> roots)
    {
        var markStack = new Stack<DotObject>();
        foreach (var root in roots)
        {
            MarkYoungReachable(root, markStack);
        }

        foreach (var oldRef in _rememberedOld)
        {
            foreach (var child in oldRef.EnumerateReferences())
            {
                MarkYoungReachable(child, markStack);
            }
        }

        foreach (var obj in _young.ToArray())
        {
            if (!obj.Marked)
            {
                _young.Remove(obj);
                continue;
            }

            obj.ClearMark();
            obj.PromoteToOld();
            _young.Remove(obj);
            _old.Add(obj);
        }

        _rememberedOld.RemoveWhere(static x => x is null);
    }

    public void CollectMajor(IEnumerable<DotObject> roots)
    {
        var markStack = new Stack<DotObject>();
        foreach (var root in roots)
        {
            MarkAllReachable(root, markStack);
        }

        SweepAndCompact(_young);
        SweepAndCompact(_old);
        _rememberedOld.RemoveWhere(oldObj => !_old.Contains(oldObj));
    }

    private static void MarkYoungReachable(DotObject obj, Stack<DotObject> stack)
    {
        if (obj.Generation != 0 || obj.Marked)
        {
            return;
        }

        obj.Mark();
        stack.Push(obj);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var child in current.EnumerateReferences())
            {
                if (child.Generation != 0 || child.Marked)
                {
                    continue;
                }

                child.Mark();
                stack.Push(child);
            }
        }
    }

    private static void MarkAllReachable(DotObject obj, Stack<DotObject> stack)
    {
        if (obj.Marked)
        {
            return;
        }

        obj.Mark();
        stack.Push(obj);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var child in current.EnumerateReferences())
            {
                if (child.Marked)
                {
                    continue;
                }

                child.Mark();
                stack.Push(child);
            }
        }
    }

    private static void SweepAndCompact(HashSet<DotObject> generationSet)
    {
        var survivors = new List<DotObject>(generationSet.Count);
        foreach (var obj in generationSet)
        {
            if (!obj.Marked)
            {
                continue;
            }

            obj.ClearMark();
            survivors.Add(obj);
        }

        generationSet.Clear();
        foreach (var obj in survivors)
        {
            generationSet.Add(obj);
        }
    }
}
