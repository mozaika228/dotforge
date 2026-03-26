namespace Dotforge.Runtime.Gc;

internal interface IHeapObject
{
    int Generation { get; }
    bool Marked { get; }
    int EstimatedSizeBytes { get; }

    void SetGeneration(int generation);
    void Mark();
    void ClearMark();
    IEnumerable<IHeapObject> EnumerateReferences();
}
