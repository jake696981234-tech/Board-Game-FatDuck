using System;
using Game.Core;
using static Game.Core.ActionKind;

public static class LegalityKernals
{
    // =====================================================================
    // ML-FRIENDLY LEGALITY KERNELS (Create is NOT an ability in Plan B)
    // No allocations; caller supplies buffers; return full counts (may exceed capacity).
    // =====================================================================

    /// <summary>
    /// MOVE structural legality: empty-only reachability; melee-on-move targets among enemies adjacent to reachable cells.
    /// Uses BoardModel's zero-alloc helpers (EnumerateReachableEmpty, GetNeighbors, etc.).
    /// </summary>
    public static int GetLegalTargets_Move(int abilityId, int actorPieceId, int[] outTargets, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;

        int rmin = (abilityId >= 0 && abilityId < Pieces.rangeMin.Length) ? Pieces.rangeMin[abilityId] : 0;
        int rmax = (abilityId >= 0 && abilityId < Pieces.rangeMax.Length) ? Pieces.rangeMax[abilityId] : 0;
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int count = 0;
        int cap = outTargets != null ? outTargets.Length : 0;

        // Reachable empty cells up to rmax
        int[] tmpReachable = bm.GetScratchCellBuffer();
        int reachCount = bm.EnumerateReachableEmpty(originCell, rmax, tmpReachable);

        // Emit EMPTY destinations (distance-filtered)
        for (int i = 0; i < reachCount; i++)
        {
            int cell = tmpReachable[i];
            int d = bm.Distance(originCell, cell);
            if (d >= rmin && d <= rmax)
            {
                if (count < cap) outTargets[count] = cell;
                count++;
            }
        }

        // Emit MELEE targets (enemy cells adjacent to any reachable empty approach cell)
        int meleeStart = count;

        // Direct adjacent melee (one-step onto enemy) when [rmin,rmax] includes 1
        if (rmin <= 1 && 1 <= rmax)
        {
            int[] neigh0 = bm.GetScratchNeighborBuffer();
            int n0 = bm.GetNeighbors(originCell, neigh0);
            int actorOwner0 = bm.GetPieceOwner(actorPieceId);
            for (int n = 0; n < n0; n++)
            {
                int tgt = neigh0[n];
                int pid = bm.GetCellOccupant(tgt);
                if (pid < 0) continue;
                if (bm.GetPieceOwner(pid) == actorOwner0) continue;
                // de-dup within melee segment
                bool seen = false;
                for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == tgt) { seen = true; break; } }
                if (seen) continue;
                if (count < cap) outTargets[count] = tgt;
                count++;
            }
        }
        int[] neigh = bm.GetScratchNeighborBuffer();
        int actorOwner = bm.GetPieceOwner(actorPieceId);
        for (int i = 0; i < reachCount; i++)
        {
            int approach = tmpReachable[i];
            int steps = bm.Distance(originCell, approach);
            int nCount = bm.GetNeighbors(approach, neigh);
            for (int n = 0; n < nCount; n++)
            {
                int tgtCell = neigh[n];
                int pid = bm.GetCellOccupant(tgtCell);
                if (pid < 0) continue;
                if (bm.GetPieceOwner(pid) == actorOwner) continue;
                int total = steps + 1;
                if (total < rmin || total > rmax) continue;

                // de-dup within melee segment
                bool seen = false;
                for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == tgtCell) { seen = true; break; } }
                if (seen) continue;

                if (count < cap) outTargets[count] = tgtCell;
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// SHOOT structural legality: enemy-only, range-filtered, LOS required; single-target only.
    /// </summary>
    public static int GetLegalTargets_Shoot(int abilityId, int actorPieceId, int[] outTargets, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;
        int actorOwner = bm.GetPieceOwner(actorPieceId);

        int rmin = (abilityId >= 0 && abilityId < Pieces.rangeMin.Length) ? Pieces.rangeMin[abilityId] : 0;
        int rmax = (abilityId >= 0 && abilityId < Pieces.rangeMax.Length) ? Pieces.rangeMax[abilityId] : 0;
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int cap = outTargets != null ? outTargets.Length : 0;
        int count = 0;

        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) == actorOwner) continue;

            int d = bm.Distance(originCell, c);
            if (d < rmin || d > rmax) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            if (count < cap) outTargets[count] = pid; // pieceId target
            count++;
        }
        return count;
    }

    public static int GetLegalTargets_SacrificeFactory(int abilityId, int actorPieceId, int[] outTargets, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;
        int actorOwner = bm.GetPieceOwner(actorPieceId);

        int rmin = (abilityId >= 0 && abilityId < Pieces.rangeMin.Length) ? Pieces.rangeMin[abilityId] : 0;
        int rmax = (abilityId >= 0 && abilityId < Pieces.rangeMax.Length) ? Pieces.rangeMax[abilityId] : 0;
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int cap = outTargets != null ? outTargets.Length : 0;
        int count = 0;

        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) != actorOwner) continue;

            int d = bm.Distance(originCell, c);
            if (d < rmin || d > rmax) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            if (count < cap) outTargets[count] = pid; // pieceId target
            count++;
        }
        return count;
    }

    public static bool IsLegal_ConversionFactory(int PlayersVPAmount)
    {
        if (PlayersVPAmount <= 0) return false;
        return true;
    }

    /// <summary>
    /// CAPTURE VP (targetless): legal if actor stands on VP cell.
    /// </summary>
    public static bool IsLegal_CaptureVP(int actorPieceId, int abilityId, int gameIndex)
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







    public static int GetLegalTargets_Launcher(int abilityId, int actorPid, int[] outPairs, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (abilityId < 0 ||
            Pieces.launcher_inputRange == null || Pieces.launcher_outputRange == null ||
            Pieces.launcher_friendlyFire == null || Pieces.launcher_enemyFire == null)
            return 0;

        int inputRange = Pieces.launcher_inputRange[abilityId];
        int outputRange = Pieces.launcher_outputRange[abilityId];
        bool allowFriendly = Pieces.launcher_friendlyFire[abilityId];
        bool allowEnemy = Pieces.launcher_enemyFire[abilityId];

        int originCell = bm.GetPieceCell(actorPid);
        if (originCell < 0) return 0;

        int cap = outPairs != null ? outPairs.Length : 0;
        int write = 0;

        int cellCount = bm.GetCellCount();
        int actorOwner = bm.GetPieceOwner(actorPid);

        // Find candidate pieces
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            byte owner = (byte)bm.GetPieceOwner(pid);
            if (owner == actorOwner && !allowFriendly) continue;
            if (owner != actorOwner && !allowEnemy) continue;

            int distIn = bm.Distance(originCell, c);
            if (distIn < 1 || distIn > inputRange) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            // For each candidate destination within outputRange from launcher
            for (int dst = 0; dst < cellCount; dst++)
            {
                if (!bm.IsEmpty(dst)) continue;
                int distOut = bm.Distance(originCell, dst);
                if (distOut < 1 || distOut > outputRange) continue;
                if (!bm.LineOfSightClear(originCell, dst)) continue;

                if (write + 1 < cap)
                {
                    outPairs[write] = pid;
                    outPairs[write + 1] = dst;
                }
                write += 2;
            }
        }

        return write; // count of ints (pairs pid,dst)
    }

    public static int GetLegalTargets_Push(int abilityId, int actorPid, int[] outPieceIds, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        // Inline minimal legality similar to GameActions.GetLegalTargets_Push
        int originCell = bm.GetPieceCell(actorPid);
        if (originCell < 0) return 0;
        if (abilityId < 0 ||
            Pieces.push_TargetsBuildings == null || Pieces.push_TargetsSoldiers == null ||
            Pieces.push_rangeMax == null || Pieces.push_FriendlyFire == null ||
            abilityId >= Pieces.push_TargetsBuildings.Length ||
            abilityId >= Pieces.push_TargetsSoldiers.Length ||
            abilityId >= Pieces.push_rangeMax.Length ||
            abilityId >= Pieces.push_FriendlyFire.Length)
            return 0;

        int actorOwner = bm.GetPieceOwner(actorPid);

        bool allowBuildings = Pieces.push_TargetsBuildings[abilityId];
        bool allowSoldiers = Pieces.push_TargetsSoldiers[abilityId];
        int rangeMax = Pieces.push_rangeMax[abilityId];
        bool allowFriendly = Pieces.push_FriendlyFire[abilityId];

        int cap = outPieceIds != null ? outPieceIds.Length : 0;
        int count = 0;
        int cellCount = bm.GetCellCount();

        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;

            if (!allowFriendly && bm.GetPieceOwner(pid) == actorOwner) continue;

            byte type = bm.GetPieceType(pid);
            bool isBuilding = Pieces.IsBuilding(type);
            if (isBuilding && !allowBuildings) continue;
            if (!isBuilding && !allowSoldiers) continue;

            int dist = bm.Distance(originCell, c);
            if (dist < 1 || dist > rangeMax) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            int pushDest = ComputePushDestination(actorPid, pid, abilityId, gameIndex);
            if (pushDest < 0 || !bm.IsValidCellId(pushDest)) continue;

            if (count < cap) outPieceIds[count] = pid;
            count++;
        }

        return count;
    }

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
        if (!bm.LineOfSightClear(originCell, targetCell)) return false;

        int pushDest = ComputePushDestination(actorPid, targetPid, abilityId, gameIndex);
        return pushDest >= 0 && bm.IsValidCellId(pushDest);
    }



    #region is legal, to do
    // to do- need to review and combine methods that are e.g GetLegalTargets_Push & IsLegal_Push. These were taken from gameActions


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
        if (!IsCreateGeometryLegal(a.dstCell, currentPlayer, gameIndex))
            return false;

        // Cluster size check
        int clusterSize = CountClusterOfType(actorType, bm.GetPieceCell(actorPid), gameIndex);
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
        if (!bm.LineOfSightClear(originCell, targetCell)) return false;

        int dst = a.dstCell;
        if (bm.GetCellOccupant(dst) >= 0) return false;
        int distOut = bm.Distance(originCell, dst);
        if (distOut < 1 || distOut > outputRange) return false;
        if (!bm.LineOfSightClear(originCell, dst)) return false;

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
        int[] scratch = bm.GetScratchCellBuffer();
        int cellCount = bm.GetCellCount();
        int emptyCount = 0;
        for (int c = 0; c < cellCount; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(origin, c);
            if (dist < 1 || dist > range) continue;
            if (!bm.LineOfSightClear(origin, c)) continue;
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
                    var scratch = bm.GetScratchNeighborBuffer();
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
                    if (!IsCreateGeometryLegal(a.dstCell, player, gameIndex))
                        return false;
                }
                return true;
            }

            // cell must be empty
            if (bm.GetCellOccupant(a.dstCell) >= 0) return false;

            // geometry: on core OR adjacent to core OR adjacent to any of your buildings
            bool geomOk = false;
            int core = bm.GetPlayerCoreCellId(player);
            if (a.dstCell == core) { geomOk = true; }
            if (!geomOk)
            {
                var scratch = bm.GetScratchCellBuffer();
                int n = bm.GetNeighbors(core, scratch);
                for (int i = 0; i < n; i++) { if (scratch[i] == a.dstCell) { geomOk = true; break; } }
            }
            if (!geomOk)
            {
                var scratch2 = bm.GetScratchCellBuffer();
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
            if (!geomOk) return false;

            // parity with OfferProvider: buildable flag + required digit gate
            if (!Pieces.IsBuildable(a.pieceType)) return false;
            int req = Pieces.GetRequiredDigit(a.pieceType);
            if (req >= 0 && !gameState.ps[player].HasDigit(req)) return false;

            // Connector legality: config index is carried in aux
            if (Pieces.HasConnectors(a.pieceType))
            {
                int cfg = a.aux;
                if (!Pieces.IsConnectorConfigAllowed(a.pieceType, cfg)) return false;
                if (!PiecesSides.IsConnectorPlacementLegal(a.dstCell, a.pieceType, cfg, player, gameIndex))
                    return false;
            }
            return true;
        }

        // Non-Create actions (Move/Shoot/CaptureVP/CoreDamage) – old path:
        int actorPid = bm.GetCellOccupant(a.srcCell);
        if (actorPid < 0) return false;
        if (bm.GetPieceOwner(actorPid) != player) return false;

        byte type = bm.GetPieceType(actorPid);
        if (a.abilitySlot >= Pieces.AbilitySlotCount(type)) return false;

        int abilityId = Pieces.AbilityIdAtSlot(type, a.abilitySlot);
        if (abilityId < 0) return false;

        byte kind = Pieces.GetAbilityKind(type, a.abilitySlot);
        if (kind != a.kind) return false; // slot-kind drift guard

        int[] targets = bm.GetScratchCellBuffer();
        int[] targetsPiece = bm.GetScratchCellBuffer();
        int count;
        switch (a.kind)
        {
            case Move:
                count = LegalityKernals.GetLegalTargets_Move(actorPid, abilityId, targets, gameIndex);
                return ContainsFirstN(targets, count, a.dstCell);
            case Shoot:
                count = LegalityKernals.GetLegalTargets_Shoot(actorPid, abilityId, targets, gameIndex);
                return ContainsFirstN(targets, count, a.aux /* targetPieceId */);
            case Push:
                return IsLegal_Push(actorPid, abilityId, in a, gameIndex);
            case Launcher:
                return IsLegal_Launcher(actorPid, abilityId, in a, gameIndex);
            case Spawner:
                return IsLegal_Spawner(actorPid, abilityId, in a, player, gameIndex);
            case GroupBuild:
                return IsLegal_GroupBuild(actorPid, abilityId, in a, player, gameIndex);
            case CaptureVP:
                return LegalityKernals.IsLegal_CaptureVP(actorPid, abilityId, gameIndex);
            case CoreDamage:
                return LegalityKernals.IsLegal_CoreDamage(actorPid, abilityId, gameIndex);
            case SacrificeFactory:
                count = LegalityKernals.GetLegalTargets_SacrificeFactory(actorPid, abilityId, targetsPiece, gameIndex);
                return ContainsFirstN(targets, count, a.aux /* targetPieceId */);
            default:
                return false;
        }
    }

    #endregion
    #region Helper that i dont know should be hear

   

    #endregion 

}
