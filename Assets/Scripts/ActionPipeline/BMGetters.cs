using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class BoardModel
{
    #region General Fields
    public int[] occupantPieceId; // occupantPieceId[cellId] = pieceId | -1
    public int _vpCellId;
    private int[] _coreCellIdByPlayer = new int[4];
    public int pieceCount;    // rows in use [0..pieceCount-1]
    public int pieceCapacity; // allocated length of columns

    // ---------- Arrays Indexed by Piece ID - [pieceId] -> ----------
    #endregion
    #region Piece Fields
    public int[] pieceCellId;
    public int[] pieceOwner;
    public byte[] pieceType;
    public short[] pieceHP;
    public int[] pieceFactoryAux;
    public int[] Instantfactory_killGoal;
    public int[] pieceKillCount;
    public int[] necroSpawnStore;
    public int[] WorkYardBudget;
    public ushort[] pieceConnectorConfig;
    public int[] pieceCapitalHP;
    #endregion
    #region Pre Computed
    //These are precomputed to optmise
    private int[] _distFromVP;

    #endregion
    #region is X
    public bool isPieceIDOwnedFromCell(int playerId, int cell) => pieceOwner[GetCellOccupant(cell)] == playerId;
    public bool IsValidCellId(int cellId) => (uint)cellId < (uint)Info.totalCells;
    public bool IsValidPieceId(int pieceId) => (uint)pieceId < (uint)pieceCount;
    public bool IsCellOccupied(int cell) => GetCellOccupant(cell) != Info.invalidId;
    // =====================================================================
    // Minimal wrappers many systems expect (ID-only)
    // =====================================================================
    #endregion
    #region Get X about Piece
    public int GetCellOccupant(int cellId) => (IsValidCellId(cellId) && occupantPieceId != null) ? occupantPieceId[cellId] : Info.invalidId;
    public int GetPieceTypeFromCell(int cellId) => pieceType[GetCellOccupant(cellId)];
    public int GetPieceOwnerFromCell(int cellId) => pieceOwner[GetCellOccupant(cellId)];
    public int GetPieceHPFromCell(int cellId) => PieceHP(GetCellOccupant(cellId));
    public int GetPieceCell(int pieceId) => (IsValidPieceId(pieceId) && pieceCellId != null) ? pieceCellId[pieceId] : Info.invalidId;
    public int GetPieceOwner(int pieceId) => (IsValidPieceId(pieceId) && pieceOwner != null) ? pieceOwner[pieceId] : -1;
    public byte GetPieceType(int pieceId) => (IsValidPieceId(pieceId) && pieceType != null) ? pieceType[pieceId] : (byte)0;
    #endregion
    #region Get X General
    public int GetPlayerCoreCellId(int owner) => (owner >= 0 && owner < _coreCellIdByPlayer.Length) ? _coreCellIdByPlayer[owner] : Info.invalidId;
    public int CoreCellIdForPlayer(byte p) => _coreCellIdByPlayer[p];

    /// <summary>True if cell belongs to any opponent core (owner != actorOwner).</summary>
    public bool IsEnemyCoreCell(int cellId, int actorOwner)
    {
        for (int i = 0; i < _coreCellIdByPlayer.Length; i++)
        {
            if (i == actorOwner) continue;
            if (_coreCellIdByPlayer[i] == cellId) return true;
        }
        return false;
    }
    #endregion
    #region GraveYard
    // public int GetNeighborCell(int cellId, int dir)
    // {
    //     if (!IsValidCellId(cellId) || (uint)dir >= 6) return Info.invalidId;
    //     return geo.neighborsById[cellId][dir];
    // }
    #endregion
}
