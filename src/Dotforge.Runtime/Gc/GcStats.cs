namespace Dotforge.Runtime.Gc;

internal readonly record struct GcStats(
    long MinorCollections,
    long MajorCollections,
    int Gen0Count,
    int Gen1Count,
    int LohCount,
    int LastCollected,
    int LastPromoted);
