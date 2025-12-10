using static Game.Core.ActionKind;
using System;
using UnityEngine;

public static class IsItLegal
{
    public static bool IsLegal_Push(int actorPid, int abilityId, in Game.Core.Action a, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (abilityId < 0 ||
            Pieces.push_TargetsBuildings == null || Pieces.push_TargetsSoldiers == null ||
            Pieces.push_rangeMax == null || Pieces.push_FriendlyFire == null ||
            Pieces.push_PushAmount == null || Pieces.push_pull == null)
            return false;

        int actorOwner = bm.GetPieceOwner(actorPid);
        int targetPid = a.aux;
        if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
        if (bm.GetPieceCell(targetPid) != a.dstCell) return false;

        bool allowFriendly = Pieces.push_FriendlyFire[abilityId];
        if (!allowFriendly && bm.GetPieceOwner(targetPid) == actorOwner) return false;

        bool allowBuildings = Pieces.push_TargetsBuildings[abilityId];
        bool allowSoldiers = Pieces.push_TargetsSoldiers[abilityId];
        byte tgtType = bm.GetPieceType(targetPid);
        bool isBuilding = Pieces.IsBuilding(tgtType);
        if (isBuilding && !allowBuildings) return false;
        if (!isBuilding && !allowSoldiers) return false;

        int rangeMax = Pieces.push_rangeMax[abilityId];
        int originCell = bm.GetPieceCell(actorPid);
        int targetCell = bm.GetPieceCell(targetPid);
        int dist = bm.Distance(originCell, targetCell);
        if (dist < 1 || dist > rangeMax) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, targetCell, gameIndex)) return false;

        int pushDest = BmAbilityCac.ComputePushDestination(actorPid, targetPid, abilityId, gameIndex);
        return pushDest >= 0 && bm.IsValidCellId(pushDest);
    }

    public static bool IsLegal_GroupBuild(int actorPid, int abilityId, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        byte actorType = bm.GetPieceType(actorPid);
        if (!Pieces.groupBuildEnabled[actorType]) return false;
        int targetType = Pieces.groupBuildTargetType[actorType];
        if (targetType < 0 || targetType >= Pieces.typeCount) return false;

        // Create legality for target type at dstCell
        if (bm.GetCellOccupant(a.dstCell) >= 0) return false;
        if (!Pieces.IsBuildable((byte)targetType)) return false;
        int reqDigit = Pieces.GetRequiredDigit((byte)targetType);
        if (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit)) return false;

        // geometric create gate (core/building adjacency)
        if (!BmAbilityCac.IsCreateGeometryLegal(a.dstCell, currentPlayer, gameIndex))
            return false;

        // Cluster size check
        int clusterSize = BmAbilityCac.CountClusterOfType(actorType, bm.GetPieceCell(actorPid), gameIndex);
        return clusterSize >= Pieces.groupBuildRequireNumber[actorType];
    }

    public static bool IsLegal_Launcher(int actorPid, int abilityId, in Game.Core.Action a, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (abilityId < 0 ||
            Pieces.launcher_inputRange == null || Pieces.launcher_outputRange == null ||
            Pieces.launcher_friendlyFire == null || Pieces.launcher_enemyFire == null)
            return false;

        int inputRange = Pieces.launcher_inputRange[abilityId];
        int outputRange = Pieces.launcher_outputRange[abilityId];
        bool allowFriendly = Pieces.launcher_friendlyFire[abilityId];
        bool allowEnemy = Pieces.launcher_enemyFire[abilityId];

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

        int dst = a.dstCell;
        if (bm.GetCellOccupant(dst) >= 0) return false;
        int distOut = bm.Distance(originCell, dst);
        if (distOut < 1 || distOut > outputRange) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, dst, gameIndex)) return false;

        return true;
    }

    public static bool IsLegal_Spawner(int actorPid, int abilityId, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;
        var controller = GameRegistry.game[gameIndex].gameController;

        if (abilityId < 0 ||
            Pieces.spawn_pieceAmount == null || Pieces.spawn_range == null || Pieces.spawn_onlyOncePerTurn == null || Pieces.spawn_targetType == null)
            return false;

        int targetType = Pieces.spawn_targetType[abilityId];
        if (targetType < 0 || targetType >= Pieces.typeCount) return false;
        int amount = Pieces.spawn_pieceAmount[abilityId];
        int range = Pieces.spawn_range[abilityId];
        bool once = Pieces.spawn_onlyOncePerTurn[abilityId];

        if (once && bm.spawnerUsedThisTurn.Contains(actorPid)) return false;

        int origin = bm.GetPieceCell(actorPid);
        if (origin < 0) return false;

        // Digit gate for target type
        int reqDigit = Pieces.GetRequiredDigit((byte)targetType);
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
    public static bool IsLegal_Upgrade(int actorPid, int abilityId, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        if (actorPid < 0) return false;
        byte actorType = bm.GetPieceType(actorPid);
        if (!Pieces.upgradeEnabled[actorType]) return false;
        int targetType = Pieces.upgradeTargetType[actorType];
        if (targetType < 0 || targetType >= Pieces.typeCount) return false;
        if (a.dstCell != a.srcCell) return false;
        if (bm.GetPieceCell(actorPid) != a.srcCell) return false;
        if (!Pieces.IsBuildable((byte)targetType)) return false;
        int reqDigit = Pieces.GetRequiredDigit((byte)targetType);
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
    public static bool IsLegal_CoreDamage(int actorPieceId, int abilityId, int gameIndex)
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
                if (bm.GetCellOccupant(a.dstCell) >= 0) return false;
                if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0 &&
                    bm.GetPieceCountForPlayer(player) >= controller.PieceLimitPerPlayer)
                    return false;
                if (gameState.multiCreateBorder)
                {
                    bool adjacent = false;
                    var scratch = Scratch.GetScratchNeighborBuffer(gameIndex);
                    int n = bm.GetNeighbors(a.dstCell, scratch);
                    for (int i = 0; i < n; i++)
                    {
                        int nb = scratch[i];
                        if (gameState.multiCreateCells.Contains(nb)) { adjacent = true; break; }
                    }
                    if (!adjacent) return false;
                }
                else
                {
                    if (!BmAbilityCac.IsCreateGeometryLegal(a.dstCell, player, gameIndex))
                        return false;
                }
                return true;
            }

            // cell must be empty
            if (bm.GetCellOccupant(a.dstCell) >= 0)
            {
                Debug.Log("GetCellOccupant returned false");
                return false;
            }

            // geometry: on core OR adjacent to core OR adjacent to any of your buildings
            bool geomOk = false;
            int core = bm.GetPlayerCoreCellId(player);
            if (a.dstCell == core) { geomOk = true; }
            if (!geomOk)
            {
                var scratch = Scratch.GetScratchCellBuffer(gameIndex);
                int n = bm.GetNeighbors(core, scratch);
                for (int i = 0; i < n; i++) { if (scratch[i] == a.dstCell) { geomOk = true; break; } }
            }
            if (!geomOk)
            {
                var scratch2 = Scratch.GetScratchCellBuffer(gameIndex);
                int n2 = bm.GetNeighbors(a.dstCell, scratch2);
                for (int i = 0; i < n2; i++)
                {
                    int nb = scratch2[i];
                    int pid = bm.GetCellOccupant(nb);
                    if (pid < 0) continue;
                    if (bm.GetPieceOwner(pid) != player) continue;
                    byte t = bm.GetPieceType(pid);
                    if (Pieces.IsBuilding(t)) { geomOk = true; break; }
                }
            }
            if (!geomOk)
            {
                Debug.Log("Legal Build Location Returned False");
                return false;
            }

            // parity with OfferProvider: buildable flag + required digit gate
            if (!Pieces.IsBuildable(a.pieceType))
            {
                Debug.Log("buildable flag Returned False");
                return false;
            }
            int req = Pieces.GetRequiredDigit(a.pieceType);
            if (req >= 0 && !gameState.ps[player].HasDigit(req))
            {
                Debug.Log("Required digit gate Returned False");
                return false;
            }

            // Connector legality: config index is carried in aux
            if (Pieces.HasConnectors(a.pieceType))
            {
                int cfg = a.aux;
                if (!Pieces.IsConnectorConfigAllowed(a.pieceType, cfg))
                {
                    Debug.Log("IsConnectorConfigAllowed Returned False");
                    return false;
                }
                if (!PiecesSides.IsConnectorPlacementLegal(a.dstCell, a.pieceType, cfg, player, gameIndex))
                {
                    Debug.Log("IsConnectorPlacementLegal Returned False");
                    return false;
                }
            }
            return true;
        }

        // Non-Create actions (Move/Shoot/CaptureVP/CoreDamage) – old path:
        int actorPid = bm.GetCellOccupant(a.srcCell);
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
        if (a.abilitySlot >= Pieces.AbilitySlotCount(type))
        {
            Debug.Log("a.abilitySlot >= Pieces.AbilitySlotCount Returned False");
            return false;
        }


        int abilityId = Pieces.AbilityIdAtSlot(type, a.abilitySlot);
        if (abilityId < 0)
        {
            Debug.Log("abilityId < 0 Returned False");
            return false;
        }

        byte kind = Pieces.GetAbilityKind(type, a.abilitySlot);
        if (kind != a.kind)
        {
            Debug.Log("kind != a.kind Returned False");
            return false; // slot-kind drift guard
        }

        int[] targets = Scratch.GetScratchCellBuffer(gameIndex);
        int[] targetsPiece = Scratch.GetScratchCellBuffer(gameIndex);
        int count;
        switch (a.kind)
        {
            case Move:
                count = GetLegalTargets.GetLegalTargets_Move(abilityId, actorPid, targets, gameIndex);
                if (ContainsFirstN(targets, count, a.dstCell))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Move - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Shoot:
                count = GetLegalTargets.GetLegalTargets_Shoot(abilityId, actorPid, targets, gameIndex);
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
                if (IsLegal_Push(actorPid, abilityId, in a, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Push - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Launcher:
                if (IsLegal_Launcher(actorPid, abilityId, in a, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Launcher - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case Spawner:
                if (IsLegal_Spawner(actorPid, abilityId, in a, player, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("Spawner - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case GroupBuild:
                if (IsLegal_GroupBuild(actorPid, abilityId, in a, player, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("GroupBuild - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case CaptureVP:
                if (IsLegal_CaptureVP(actorPid, abilityId, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("CaptureVP - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case CoreDamage:
                if (IsLegal_CoreDamage(actorPid, abilityId, gameIndex))
                {
                    return true;
                }
                else
                {
                    Debug.Log("CoreDamage - ContainsFirstN Returned False");
                    return false; // slot-kind drift guard
                }
            case SacrificeFactory:
                count = GetLegalTargets.GetLegalTargets_SacrificeFactory(abilityId, actorPid, targetsPiece, gameIndex);
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
