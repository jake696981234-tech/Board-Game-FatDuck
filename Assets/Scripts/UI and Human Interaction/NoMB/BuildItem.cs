// Assets/Scripts/UI/HIC/HICDtos.cs
using UnityEngine;

public readonly struct BuildItem
{
    public readonly byte pieceType;
    public readonly string name;
    public readonly string spritePath;
    public readonly int cost;
    public readonly bool legal;
    public readonly ushort Auxiliary;

    public BuildItem(byte pieceType, string name, string spritePath, int cost, bool legal, ushort auxiliary)
    { this.pieceType = pieceType; this.name = name; this.spritePath = spritePath; this.cost = cost; this.legal = legal; this.Auxiliary = auxiliary; }
}

public readonly struct PieceContext
{
    public readonly int pieceId;
    public readonly byte type;
    public readonly int owner;
    public readonly short hp;

    public PieceContext(int pieceId, byte type, int owner, short hp)
    { this.pieceId = pieceId; this.type = type; this.owner = owner; this.hp = hp; }
}

public readonly struct ActionItem
{
    public readonly string id;     // internal tag
    public readonly string name;   // UI text
    public readonly int cost;
    public readonly bool legal;
    public readonly int[] legalTargets; // cellIds
    public readonly int kind;

    public ActionItem(string id, string name, int cost, bool legal, int[] legalTargets, int kind)
    { this.id = id; this.name = name; this.cost = cost; this.legal = legal; this.legalTargets = legalTargets; this.kind = kind; }
}

//Total across all piece type, Payout at end of round per player
public readonly struct CurrentEndRoundPayOut
{
    public readonly int[] pieceType;     // internal tag
    public readonly bool[] IsGroup;
    public readonly float[] PieceTypePayOut;
    public readonly float BonusForVP;
    public readonly float BonusForCoreDamage;

    public CurrentEndRoundPayOut(int[] pieceType, bool[] IsGroup, float[] PieceTypePayOut, float BonusForVP, float BonusForCoreDamage)
    { this.pieceType = pieceType; this.IsGroup = IsGroup; this.PieceTypePayOut = PieceTypePayOut; this.BonusForVP = BonusForVP; this.BonusForCoreDamage = BonusForCoreDamage; }
}

