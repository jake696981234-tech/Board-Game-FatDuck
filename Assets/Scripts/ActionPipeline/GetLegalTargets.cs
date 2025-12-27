
// public static class GetLegalTargets
// {
//     // =====================================================================
//     // ML-FRIENDLY LEGALITY KERNELS (Create is NOT an ability in Plan B)
//     // No allocations; caller supplies buffers; return full counts (may exceed capacity).
//     // =====================================================================

//     /// <summary>
//     /// MOVE structural legality: empty-only reachability; melee-on-move targets among enemies adjacent to reachable cells.
//     /// Uses BoardModel's zero-alloc helpers (EnumerateReachableEmpty, GetNeighbors, etc.).
//     /// </summary>
//     public static int GetLegalTargets_Move(int actorPieceId, int actorType, int[] outTargets, int gameIndex)
//     {
//         var bm = GameRegistry.game[gameIndex].boardModel;

//         int originCell = bm.GetPieceCell(actorPieceId);
//         if (originCell < 0) return 0;

//         int rmin = PieceDefinition.move_rangeMin[actorType];
//         int rmax = PieceDefinition.move_rangeMax[actorType];
//         if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

//         int count = 0;
//         int cap = outTargets != null ? outTargets.Length : 0;

//         // Reachable empty cells up to rmax
//         int[] tmpReachable = Scratch.GetScratchCellBuffer(gameIndex);
//         int reachCount = bm.EnumerateReachableEmpty(originCell, rmax, tmpReachable);

//         // Emit EMPTY destinations (distance-filtered)
//         for (int i = 0; i < reachCount; i++)
//         {
//             int cell = tmpReachable[i];
//             int d = bm.Distance(originCell, cell);
//             if (d >= rmin && d <= rmax)
//             {
//                 if (count < cap) outTargets[count] = cell;
//                 count++;
//             }
//         }

//         // Emit MELEE targets (enemy cells adjacent to any reachable empty approach cell)
//         int meleeStart = count;

//         // Direct adjacent melee (one-step onto enemy) when [rmin,rmax] includes 1
//         if (rmin <= 1 && 1 <= rmax)
//         {
//             int[] neigh0 = Scratch.GetScratchNeighborBuffer(gameIndex);
//             int Neighbors = bm.GetNeighbors(originCell, neigh0);
//             int actorOwner0 = bm.GetPieceOwner(actorPieceId);
//             for (int NeighborNumber = 0; NeighborNumber < Neighbors; NeighborNumber++)
//             {
//                 int tgt = neigh0[NeighborNumber];
//                 int victimId = bm.GetCellOccupant(tgt);
//                 if (victimId < 0) continue;
//                 if (bm.GetPieceOwner(victimId) == actorOwner0) continue;
//                 // de-dup within melee segment
//                 bool seen = false;
//                 for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == tgt) { seen = true; break; } }
//                 if (seen) continue;
//                 if (count < cap) outTargets[count] = tgt;
//                 count++;
//             }
//         }
//         int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
//         int actorOwner = bm.GetPieceOwner(actorPieceId);
//         for (int i = 0; i < reachCount; i++)
//         {
//             int approach = tmpReachable[i];
//             int steps = bm.Distance(originCell, approach);
//             int nCount = bm.GetNeighbors(approach, neigh);
//             for (int n = 0; n < nCount; n++)
//             {
//                 int targetCell = neigh[n];
//                 int pid = bm.GetCellOccupant(targetCell);
//                 if (pid < 0) continue;
//                 if (bm.GetPieceOwner(pid) == actorOwner) continue;
//                 int total = steps + 1;
//                 if (total < rmin || total > rmax) continue;

//                 // de-dup within melee segment
//                 bool seen = false;
//                 for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == targetCell) { seen = true; break; } }
//                 if (seen) continue;

//                 if (count < cap) outTargets[count] = targetCell;
//                 count++;
//             }
//         }
//         return count;
//     }

//     /// <summary>
//     /// SHOOT structural legality: enemy-only, range-filtered, LOS required; single-target only.
//     /// </summary>
//     public static int GetLegalTargets_Shoot(int actorPieceId, int actorType, int[] outTargets, int gameIndex)
//     {
//         var bm = GameRegistry.game[gameIndex].boardModel;

//         int originCell = bm.GetPieceCell(actorPieceId);
//         if (originCell < 0) return 0;
//         int actorOwner = bm.GetPieceOwner(actorPieceId);

//         int rmin = PieceDefinition.shoot_rangeMin[actorType];
//         int rmax = PieceDefinition.shoot_rangeMax[actorType];
//         if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

//         int cap = outTargets != null ? outTargets.Length : 0;
//         int count = 0;

//         int cellCount = bm.GetCellCount();
//         for (int c = 0; c < cellCount; c++)
//         {
//             int pid = bm.GetCellOccupant(c);
//             if (pid < 0) continue;
//             if (bm.GetPieceOwner(pid) == actorOwner) continue;

//             int d = bm.Distance(originCell, c);
//             if (d < rmin || d > rmax) continue;
//             if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

//             if (count < cap) outTargets[count] = pid; // pieceId target
//             count++;
//         }
//         return count;
//     }

//     public static int GetLegalTargets_SacrificeFactory(int actorPieceId, int actorType, int[] outTargets, int gameIndex)
//     {
//         var bm = GameRegistry.game[gameIndex].boardModel;

//         int originCell = bm.GetPieceCell(actorPieceId);
//         if (originCell < 0) return 0;
//         int actorOwner = bm.GetPieceOwner(actorPieceId);

//         int rmin = PieceDefinition.sacrificeFactory_rangeMin[actorType];
//         int rmax = PieceDefinition.sacrificeFactory_rangeMax[actorType];

//         if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

//         int cap = outTargets != null ? outTargets.Length : 0;
//         int count = 0;

//         int cellCount = bm.GetCellCount();
//         for (int c = 0; c < cellCount; c++)
//         {
//             int pid = bm.GetCellOccupant(c);
//             if (pid < 0) continue;
//             if (bm.GetPieceOwner(pid) != actorOwner) continue;

//             int d = bm.Distance(originCell, c);
//             if (d < rmin || d > rmax) continue;
//             if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

//             if (count < cap) outTargets[count] = pid; // pieceId target
//             count++;
//         }
//         return count;
//     }


//     public static int GetLegalTargets_Launcher(int actorPid, int actorType, int[] outPairs, int gameIndex)
//     {
//         var bm = GameRegistry.game[gameIndex].boardModel;


//         int inputRange = PieceDefinition.launcher_inputRange[actorType];
//         int outputRange = PieceDefinition.launcher_outputRange[actorType];
//         bool allowFriendly = PieceDefinition.launcher_isfriendlyFire[actorType];
//         bool allowEnemy = PieceDefinition.launcher_isEnemyFire[actorType];

//         int originCell = bm.GetPieceCell(actorPid);
//         if (originCell < 0) return 0;

//         int cap = outPairs != null ? outPairs.Length : 0;
//         int write = 0;

//         int cellCount = bm.GetCellCount();
//         int actorOwner = bm.GetPieceOwner(actorPid);

//         // Find candidate pieces
//         for (int c = 0; c < cellCount; c++)
//         {
//             int pid = bm.GetCellOccupant(c);
//             if (pid < 0) continue;
//             byte owner = (byte)bm.GetPieceOwner(pid);
//             if (owner == actorOwner && !allowFriendly) continue;
//             if (owner != actorOwner && !allowEnemy) continue;

//             int distIn = bm.Distance(originCell, c);
//             if (distIn < 1 || distIn > inputRange) continue;
//             if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

//             // For each candidate destination within outputRange from launcher
//             for (int dst = 0; dst < cellCount; dst++)
//             {
//                 if (!bm.IsEmpty(dst)) continue;
//                 int distOut = bm.Distance(originCell, dst);
//                 if (distOut < 1 || distOut > outputRange) continue;
//                 if (!BmAbilityCac.LineOfSightClear(originCell, dst, gameIndex)) continue;

//                 if (write + 1 < cap)
//                 {
//                     outPairs[write] = pid;
//                     outPairs[write + 1] = dst;
//                 }
//                 write += 2;
//             }
//         }

//         return write; // count of ints (pairs pid,dst)
//     }

//     public static int GetLegalTargets_Push(int actorPieceId, int actorType, int[] outPieceIds, int gameIndex)
//     {
//         var bm = GameRegistry.game[gameIndex].boardModel;

//         // Inline minimal legality similar to GameActions.GetLegalTargets_Push
//         int originCell = bm.GetPieceCell(actorPieceId);
//         if (originCell < 0) return 0;


//         int actorOwner = bm.GetPieceOwner(actorPieceId);

//         bool allowBuildings = PieceDefinition.push_IsTargetsBuildings[actorType];
//         bool allowSoldiers = PieceDefinition.push_isTargetsSoldiers[actorType];
//         int rangeMax = PieceDefinition.push_rangeMax[actorType];
//         bool allowFriendly = PieceDefinition.push_isFriendlyFire[actorType];

//         int cap = outPieceIds != null ? outPieceIds.Length : 0;
//         int count = 0;
//         int cellCount = bm.GetCellCount();

//         for (int c = 0; c < cellCount; c++)
//         {
//             int victimId = bm.GetCellOccupant(c);
//             if (victimId < 0) continue;

//             if (!allowFriendly && bm.GetPieceOwner(victimId) == actorOwner) continue;

//             byte type = bm.GetPieceType(victimId);
//             bool isBuilding = PieceDefinition.isBuilding[actorType];
//             if (isBuilding && !allowBuildings) continue;
//             if (!isBuilding && !allowSoldiers) continue;

//             int dist = bm.Distance(originCell, c);
//             if (dist < 1 || dist > rangeMax) continue;
//             if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

//             int pushDest = BmAbilityCac.ComputePushDestination(actorPieceId, actorType, victimId, gameIndex);
//             if (pushDest < 0 || !bm.IsValidCellId(pushDest)) continue;

//             if (count < cap) outPieceIds[count] = victimId;
//             count++;
//         }

//         return count;
//     }

// }
