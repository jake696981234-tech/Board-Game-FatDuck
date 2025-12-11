using static Game.Core.ActionKind;
using System;
using UnityEngine;

public static class IsItLegal
{
    public static bool IsLegal_Push(int actorPid, int actorType, in Game.Core.Action a, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;


        int actorOwner = bm.GetPieceOwner(actorPid);
        int targetPid = a.aux;
        if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
        if (bm.GetPieceCell(targetPid) != a.TargetCellId) return false;

        bool allowFriendly = PieceDefinition.push_FriendlyFire[actorType];
        if (!allowFriendly && bm.GetPieceOwner(targetPid) == actorOwner) return false;

        bool allowBuildings = PieceDefinition.push_TargetsBuildings[actorType];
        bool allowSoldiers = PieceDefinition.push_TargetsSoldiers[actorType];
        byte tgtType = bm.GetPieceType(targetPid);
        bool isBuilding = PieceDefinition.isBuildingByType[actorType];
        if (isBuilding && !allowBuildings) return false;
        if (!isBuilding && !allowSoldiers) return false;

        int rangeMax = PieceDefinition.push_rangeMax[actorType];
        int originCell = bm.GetPieceCell(actorPid);
        int targetCell = bm.GetPieceCell(targetPid);
        int dist = bm.Distance(originCell, targetCell);
        if (dist < 1 || dist > rangeMax) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, targetCell, gameIndex)) return false;

        int pushDest = BmAbilityCac.ComputePushDestination(actorPid, actorType, targetPid, gameIndex);
        return pushDest >= 0 && bm.IsValidCellId(pushDest);
    }

    public static bool IsLegal_GroupBuild(int actorPid, int abilityId, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        byte actorType = bm.GetPieceType(actorPid);
        if (!PieceDefinition.groupBuild_enabled[actorType]) return false;
        int targetType = PieceDefinition.groupBuildTargetType[actorType];
        if (targetType < 0 || targetType >= PieceDefinition.typeCount) return false;

        // Create legality for target type at dstCell
        if (bm.GetCellOccupant(a.TargetCellId) >= 0) return false;
        int reqDigit = PieceDefinition.codeDigitsByType[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit)) return false;

        // geometric create gate (core/building adjacency)
        if (!BmAbilityCac.IsCreateGeometryLegal(a.TargetCellId, currentPlayer, gameIndex))
            return false;

        // Cluster size check
        int clusterSize = BmAbilityCac.CountClusterOfType(actorType, bm.GetPieceCell(actorPid), gameIndex);
        return clusterSize >= PieceDefinition.groupBuildTargetType[actorType];
    }

    public static bool IsLegal_Launcher(int actorPid, int actorType, in Game.Core.Action a, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;


        int inputRange = PieceDefinition.launcher_inputRange[actorType];
        int outputRange = PieceDefinition.launcher_outputRange[actorType];
        bool allowFriendly = PieceDefinition.launcher_friendlyFire[actorType];
        bool allowEnemy = PieceDefinition.launcher_enemyFire[actorType];

        int targetPid = a.aux;
        if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
        int originCell = bm.GetPieceCell(actorPid);
        int targetCell = bm.GetPieceCell(targetPid);
        if (originCell < 0 || targetCell < 0) return false;

        int actorOwner = bm.GetPieceOwner(actorPid);
        int tgtOwner = bm.GetPieceOwner(targetPid);
        if (tgtOwner == actorOwner && !allowFriendly) return false;
        if (tgtOwner != actorOwner && !allowEnemy) return false;

        int distIn = bm.Distance(originCell, targetCell);
        if (distIn < 1 || distIn > inputRange) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, targetCell, gameIndex)) return false;

        int dst = a.TargetCellId;
        if (bm.GetCellOccupant(dst) >= 0) return false;
        int distOut = bm.Distance(originCell, dst);
        if (distOut < 1 || distOut > outputRange) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, dst, gameIndex)) return false;

        return true;
    }

    public static bool IsLegal_Spawner(int actorPid, int actorType, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;
        var controller = GameRegistry.game[gameIndex].gameController;


        int targetType = PieceDefinition.spawn_targetType[actorType];
        if (targetType < 0 || targetType >= PieceDefinition.typeCount) return false;
        int amount = PieceDefinition.spawn_pieceAmount[actorType];
        int range = PieceDefinition.spawn_range[actorType];
        bool once = PieceDefinition.spawn_onlyOncePerTurn[actorType];

        if (once && bm.spawnerUsedThisTurn.Contains(actorPid)) return false;

        int origin = bm.GetPieceCell(actorPid);
        if (origin < 0) return false;

        // Digit gate for target type
        int reqDigit = PieceDefinition.codeDigitsByType[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit)) return false;

        // Gather empty cells in range with LOS
        int[] scratch = Scratch.GetScratchCellBuffer(gameIndex);
        int cellCount = bm.GetCellCount();
        int emptyCount = 0;
        for (int c = 0; c < cellCount; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(origin, c);
            if (dist < 1 || dist > range) continue;
            if (!BmAbilityCac.LineOfSightClear(origin, c, gameIndex)) continue;
            scratch[emptyCount++] = c;
        }

        if (emptyCount <= 0) return false;

        // Piece limit check: allow as many as possible
        int availableLimit = int.MaxValue;
        if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0)
            availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(currentPlayer);

        int possible = Math.Min(amount, Math.Min(emptyCount, Math.Max(0, availableLimit)));
        return possible > 0;
    }

    // this probs needs to be fixed
    public static bool IsLegal_Upgrade(int actorPid, int abilityId, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        if (actorPid < 0) return false;
        byte actorType = bm.GetPieceType(actorPid);
        if (!PieceDefinition.upgradeEnabled[actorType]) return false;
        int targetType = PieceDefinition.upgradeTargetType[actorType];
        if (targetType < 0 || targetType >= PieceDefinition.typeCount) return false;
        if (a.TargetCellId != a.ActorsCellId) return false;
        if (bm.GetPieceCell(actorPid) != a.ActorsCellId) return false;
        int reqDigit = PieceDefinition.codeDigitsByType[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit)) return false;
        return true;
    }

    public static bool IsLegal_ConversionFactory(int PlayersVPAmount)
    {
        if (PlayersVPAmount <= 0) return false;
        return true;
    }

    /// <summary>
    /// CAPTURE VP (targetless): legal if actor stands on VP cell.
    /// </summary>
    public static bool IsLegal_CaptureVP(int actorPieceId, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return false;
        return originCell == bm.GetVictoryPointCellId();
    }

    /// <summary>
    /// CORE DAMAGE (targetless): legal if actor stands on an ENEMY core cell.
    /// </summary>
    public static bool IsLegal_CoreDamage(int actorPieceId, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return false;
        int owner = bm.GetPieceOwner(actorPieceId);
        return bm.IsEnemyCoreCell(originCell, owner);
    }

    #region IsStillLegal Method
    public static bool IsStillLegal(in Game.Core.Action a, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var controller = GameRegistry.game[gameIndex].gameController;
        var bm = GameRegistry.game[gameIndex].boardModel;


        // EndTurn: always structurally legal
        if (a.kind == EndTurn) return true;

        // Plan-B: Create has no actor/slot/ability; structural rule only
        if (a.kind == Create)
        {
            // If pending multi-create placements are active, treat this as a placement
            if (gameState.multiCreateActive && a.pieceType == gameState.multiCreateType)
            {
                if (bm.GetCellOccupant(a.TargetCellId) >= 0) return false;
                if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0 &&
                    bm.GetPieceCountForPlayer(player) >= controller.PieceLimitPerPlayer)
                    return false;
                if (gameState.multiCreateBorder)
                {
                    bool adjacent = false;
                    var scratch = Scratch.GetScratchNeighborBuffer(gameIndex);
                    int n = bm.GetNeighbors(a.TargetCellId, scratch);
                    for (int i = 0; i < n; i++)
                    {
                        int nb = scratch[i];
                        if (gameState.multiCreateCells.Contains(nb)) { adjacent = true; break; }
                    }
                    if (!adjacent) return false;
                }
                else
                {
                    if (!BmAbilityCac.IsCreateGeometryLegal(a.TargetCellId, player, gameIndex))
                        return false;
                }
                return true;
            }

            // cell must be empty
            if (bm.GetCellOccupant(a.TargetCellId) >= 0)
            {
                Debug.Log("GetCellOccupant returned false");
                return false;
            }

            // geometry: on core OR adjacent to core OR adjacent to any of your buildings
            bool geomOk = false;
            int core = bm.GetPlayerCoreCellId(player);
            if (a.TargetCellId == core) { geomOk = true; }
            if (!geomOk)
            {
                var scratch = Scratch.GetScratchCellBuffer(gameIndex);
                int n = bm.GetNeighbors(core, scratch);
                for (int i = 0; i < n; i++) { if (scratch[i] == a.TargetCellId) { geomOk = true; break; } }
            }
            if (!geomOk)
            {
                var scratch2 = Scratch.GetScratchCellBuffer(gameIndex);
                int n2 = bm.GetNeighbors(a.TargetCellId, scratch2);
                for (int i = 0; i < n2; i++)
                {
                    int nb = scratch2[i];
                    int pid = bm.GetCellOccupant(nb);
                    if (pid < 0) continue;
                    if (bm.GetPieceOwner(pid) != player) continue;
                    byte t = bm.GetPieceType(pid);
                    if (PieceDefinition.isBuildingByType[t]) { geomOk = true; break; }
                }
            }
            if (!geomOk)
            {
                Debug.Log("Legal Build Location Returned False");
                return false;
            }

            // parity with OfferProvider: buildable flag + required digit gate
            if (!PieceDefinition.isBuildingByType[a.pieceType])
            {
                Debug.Log("buildable flag Returned False");
                return false;
            }
            int req = PieceDefinition.codeDigitsByType[a.pieceType];
            if (req >= 0 && !gameState.ps[player].HasDigit(req))
            {
                Debug.Log("Required digit gate Returned False");
                return false;
            }

            // Connector legality: config index is carried in aux
            if (PieceDefinition.connectors_enabled[a.pieceType])
            {
                int cfg = a.aux;
                if (!PieceDefinition.IsConnectorConfigAllowed(a.pieceType, cfg))
                {
                    Debug.Log("IsConnectorConfigAllowed Returned False");
                    return false;
                }
                if (!PiecesSides.IsConnectorPlacementLegal(a.TargetCellId, a.pieceType, cfg, player, gameIndex))
                {
                    Debug.Log("IsConnectorPlacementLegal Returned False");
                    return false;
                }
            }
            return true;
        }

        // Non-Create actions (Move/Shoot/CaptureVP/CoreDamage) – old path:
        int actorPid = bm.GetCellOccupant(a.ActorsCellId);
        if (actorPid < 0)
        {
            Debug.Log("Theres no target Returned False");
            return false;
        }
        if (bm.GetPieceOwner(actorPid) != player)
        {
            Debug.Log("Piece owner == Piece Actor Returned False");
            return false;
        }

        byte type = bm.GetPieceType(actorPid);


        int[] targets = Scratch.GetScratchCellBuffer(gameIndex);
        int[] targetsPiece = Scratch.GetScratchCellBuffer(gameIndex);
        int count;
        switch (a.kind)
        {
            case Move:
                count = GetLegalTargets.GetLegalTargets_Move(actorPid, a.pieceType, targets, gameIndex);
                if (ContainsFirstN(targets, count, a.TargetCellId))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Move - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Shoot:
                count = GetLegalTargets.GetLegalTargets_Shoot(actorPid, a.pieceType, targets, gameIndex);
                if (ContainsFirstN(targets, count, a.aux /* targetPieceId */))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Shoot - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Push:
                if (IsLegal_Push(actorPid, a.pieceType, in a, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Push - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Launcher:
                if (IsLegal_Launcher(actorPid, a.pieceType, in a, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Launcher - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Spawner:
                if (IsLegal_Spawner(actorPid, a.pieceType, in a, player, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Spawner - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case GroupBuild:
                if (IsLegal_GroupBuild(actorPid, a.pieceType, in a, player, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("GroupBuild - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case CaptureVP:
                if (IsLegal_CaptureVP(actorPid, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("CaptureVP - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case CoreDamage:
                if (IsLegal_CoreDamage(actorPid, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("CoreDamage - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case SacrificeFactory:
                count = GetLegalTargets.GetLegalTargets_SacrificeFactory(actorPid, a.pieceType, targetsPiece, gameIndex);
                if (ContainsFirstN(targets, count, a.aux /* targetPieceId */))
                {
                    return true;
                }
                else
                {
                    Debug.Log("SacrificeFactory - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case ConversionFactory:
                return IsLegal_ConversionFactory(gameState.ps[player].vpTotal);
            default:
                Debug.Log("Is Legal X Function returned false");
                return false;
        }
    }
    #endregion


    //helpers
    private static bool ContainsFirstN(int[] xs, int count, int value)
    {
        int n = (xs != null) ? Math.Min(count, xs.Length) : 0;
        for (int i = 0; i < n; i++) if (xs[i] == value) return true;
        return false;
    }

}
