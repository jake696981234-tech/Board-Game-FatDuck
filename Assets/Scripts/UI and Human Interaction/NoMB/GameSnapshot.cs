// Data-only snapshot of everything the view needs.
// Immutable once composed.
using UnityEngine;

public sealed class GameSnapshot
{
    // --- Board layout ---
    public Vector3[] worldPosById;
    public int victoryPointCellId;
    public int[] coreCellIdByPlayer;

    // --- Pieces ---
    public int pieceCount;
    public int[] pieceCellId;
    public int[] pieceOwner;
    public byte[] pieceType;
    public short[] pieceHP;
    public byte?[] connector;

    // --- Match counters ---
    public int centerVP;
    public int[] coreHPByPlayer;
    public int[] vpByPlayer;

    // --- Type metadata (for visuals/UI) ---
    public string[] spritePathByType;
    public string[] displayNameByType;

    // --- Optional: player palette / tint ---
    public Color[] ownerTintByPlayer;

    // --- Versioning for dirty checking (optional) ---
    public uint version;

    public CurrentEndRoundPayOut[] PerEndRoundPayOut;
}
