// Assets/Scripts/Core/BoardGeometry.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct BoardGeometry
{
    // Optional (debug/math): axial coordinates for each cell id
    // length = cellCount; coordById[id] = (q,r)
    public readonly (short q, short r)[] coordById;

    // Fast neighbor table: neighborsById[id][d] for d=0..5, or -1 if off-board
    public readonly int[][] neighborsById;

    // Rings: ringsByRadius[k] = all ids at hex distance k from center; k=0..radius
    public readonly int[][] ringsByRadius;

    // Distance from center (0,0) for each id (or replace with distToVP if you prefer)
    public readonly short[] distFromCenter;

    // NEW: axial -> id map for fast lookups (used by LOS / ray marching, etc.)
    public readonly Dictionary<(short q, short r), int> idByAxial;

    public BoardGeometry(
        (short q, short r)[] coordById,
        int[][] neighborsById,
        int[][] ringsByRadius,
        short[] distFromCenter,
        Dictionary<(short q, short r), int> idByAxial)
    {
        this.coordById      = coordById;
        this.neighborsById  = neighborsById;
        this.ringsByRadius  = ringsByRadius;
        this.distFromCenter = distFromCenter;
        this.idByAxial      = idByAxial;
    }

    // Convenience helper: returns invalidId if not found
    public int AxialToId(short q, short r, int invalidId)
        => idByAxial.TryGetValue((q, r), out var id) ? id : invalidId;
}
