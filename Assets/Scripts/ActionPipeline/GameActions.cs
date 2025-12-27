using UnityEngine;
using static Game.Core.ActionKind;
using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class GameActions
    {
//         #region Apply Actions

//         public static void ApplyMove(in Action theAction, byte player, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;

//             int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
//             if (actorPid < 0) return;
//             int dstOcc = bm.GetCellOccupant(theAction.TargetCellId);
//             if (dstOcc >= 0)
//             {
//                 ResolveMelee(actorPid, dstOcc, in theAction, gameIndex);
//                 bm.MovePieceRow(actorPid, theAction.ActorsCellId);
//             }
//             else
//             {
//                 bm.MovePieceRow(actorPid, theAction.TargetCellId);
//             }
//             RefreshConnectorState(gameIndex);
//         }

//         public static void ApplyShoot(in Action theAction, byte player, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;
//             var events = GameRegistry.game[gameIndex].eventManager;

//             int victimID = theAction.aux;
//             if (victimID < 0) return;
//             int dmg = PieceDefinition.shoot_damage[theAction.pieceType];
//             bool killed = ApplyDamageWithCapital(theAction.ActorsCellId, victimID, dmg, gameIndex);
//             if (killed)
//             {
//                 // Revoke digit from the defender's owner if this type granted one
//                 pieceKilled(victimID, gameIndex, theAction);
//             }
//             RefreshConnectorState(gameIndex);
//         }

//         public static void ApplyCaptureVP(in Action theAction, byte player, int gameIndex)
//         {
//             var gameState = GameRegistry.game[gameIndex].gameState;

//             gameState.ps[player].OnCaptureVP();
//             gameState.AddCenterVictoryPoints(-1);
//         }

//         public static void ApplyCoreDamage(in Action theAction, byte player, int gameIndex)
//         {
//             var gameState = GameRegistry.game[gameIndex].gameState;
//             var bm = GameRegistry.game[gameIndex].boardModel;

//             gameState.ps[player].OnCoreDamage();

//             int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
//             if (actorPid < 0) return;

//             int actorCell = bm.GetPieceCell(actorPid);
//             byte enemy = bm.OwnerOfCoreCell(actorCell);
//             if (enemy >= 4) return;

//             byte typ = bm.GetPieceType(actorPid);
//             int dmg = PieceDefinition.coreDamage_damage[typ];

//             int hp = gameState.GetCoreHealth(enemy);
//             gameState.SetCoreHealth(enemy, hp - dmg);
//             gameState.TryEliminatePlayer(enemy);
//         }

//         public static void ApplyCreate(in Action theAction, byte player, int gameIndex)
//         {
//             var gameState = GameRegistry.game[gameIndex].gameState;
//             var bm = GameRegistry.game[gameIndex].boardModel;
            

//             // Upgrade-create: ActorsCellId carries source piece id (for upgrade flow)
//             int sourcePid = bm.GetCellOccupant(theAction.ActorsCellId);
//             bool isUpgradeCreate = PieceDefinition.upgrade_enabled[theAction.pieceType] && sourcePid >= 0 && bm.IsValidPieceId(sourcePid);
//             byte sourceType = isUpgradeCreate ? bm.GetPieceType(sourcePid) : (byte)0xFF;
//             if (isUpgradeCreate)
//             {
//                 int expectedSourceType = PieceDefinition.upgrade_target[theAction.pieceType];
//                 if (sourceType != expectedSourceType) isUpgradeCreate = false;
//             }

//             if (PieceDefinition.sacrificeCost_enabled[theAction.pieceType])
//             {
//                 int need = PieceDefinition.sacrificeCost_howManyItNeeds[theAction.pieceType];
//                 if (need > 0 && theAction.addCost != null)
//                 {
//                     int killed = 0;
//                     for (int i = 0; i < theAction.addCost.Length && killed < need; i++)
//                     {
//                         int pieceid = theAction.addCost[i];
//                         if (!bm.IsValidPieceId(pieceid)) continue;
//                         if (bm.GetPieceOwner(pieceid) != player) continue;
//                         pieceKilled(pieceid, gameIndex);
//                         killed++;
//                     }
//                     if (killed < need) return; // safety: not enough valid sacrifices
//                 }
//             }

//             int pid = bm.AllocateRow();
//             bm.PlacePieceRow(pid, player, (byte)theAction.pieceType, theAction.TargetCellId, PieceDefinition.maxHP[theAction.pieceType]);
//             // Set connector config if applicable (Create uses aux for config index)
//             bm.pieceConnectorConfig[pid] = (byte)theAction.aux;
//             // Preserve connector config from source on upgrade
//             if (isUpgradeCreate && sourcePid >= 0)
//                 bm.pieceConnectorConfig[pid] = bm.pieceConnectorConfig[sourcePid];
//             // Grant digit if this type provides one
//             int g = PieceDefinition.digitItGives[(byte)theAction.pieceType];
//             if (g >= 0) gameState.ps[player].GrantDigit(g);

//             if (PieceDefinition.multiCreate_enabledByType[theAction.pieceType])
//             {
//                 int total = Math.Max(1, PieceDefinition.multiCreate_amountByType[theAction.pieceType]);
//                 if (total > 1)
//                 {
//                     gameState.multiCreateActive = true;
//                     gameState.multiCreateType = (byte)theAction.pieceType;
//                     gameState.multiCreateBorder = PieceDefinition.multiCreate_isBoardering[theAction.pieceType];
//                     gameState.multiCreateRemaining = total - 1;
//                     gameState.multiCreateCells.Clear();
//                     gameState.multiCreateCells.Add(theAction.TargetCellId);
//                 }
//             }

//             RefreshConnectorState(gameIndex);

//             // Remove source piece after placing new one (no connector wipe for intermediate state)
//             if (isUpgradeCreate && bm.IsValidPieceId(sourcePid))
//             {
//                 bm.FreeRowSwapBack(sourcePid);
//             }
//         }

//         //ENDTURN = 5

//         public static void ApplyPush(in Action theAction, byte player, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;
//             var events = GameRegistry.game[gameIndex].eventManager;

//             int victimID = theAction.aux;
//             if (victimID < 0) return;

//             int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
//             if (actorPid < 0) return;

//             byte actorType = bm.GetPieceType(actorPid);

//             int dmg = PieceDefinition.push_damage[theAction.pieceType];
//             bool killed = ApplyDamageWithCapital(theAction.ActorsCellId, victimID, dmg, gameIndex);
//             if (killed)
//             {
//                 // Revoke digit from the defender's owner if this type granted one
//                 pieceKilled(victimID, gameIndex, theAction);
//             }
//             else
//             {
//                 int pushedCellID = BmAbilityCac.ComputePushDestination(actorPid, actorType, victimID, gameIndex);
//                 bm.MovePieceRow(victimID, pushedCellID);
//             }
//             RefreshConnectorState(gameIndex);
//         }




//         public static void ApplyGroupBuild(in Action theAction, byte p, int gameIndex)
//         {
//             var gameState = GameRegistry.game[gameIndex].gameState;
//             var bm = GameRegistry.game[gameIndex].boardModel;
//             var events = GameRegistry.game[gameIndex].eventManager;

//             int targetType = theAction.pieceType;
//             int dst = theAction.TargetCellId;
//             int pid = bm.AllocateRow();
//             bm.PlacePieceRow(pid, p, (byte)targetType, dst, PieceDefinition.maxHP[targetType]);
//             int g = PieceDefinition.digitItGives[(byte)targetType];
//             if (g >= 0) gameState.ps[p].GrantDigit(g);

//             int ActorsCellId = theAction.ActorsCellId;
//             int actorPid = bm.GetCellOccupant(ActorsCellId);
//             if (actorPid >= 0)
//             {
//                 byte actorType = bm.GetPieceType(actorPid);
//                 if (PieceDefinition.groupBuild_deletion[actorType])
//                 {
//                     var list = CollectClusterCells(actorType, ActorsCellId, gameIndex);
//                     int need = PieceDefinition.groupBuild_requireNumber[actorType];
//                     list.Sort();
//                     for (int i = 0; i < need && i < list.Count; i++)
//                     {
//                         int cell = list[i];
//                         int victim = bm.GetCellOccupant(cell);
//                         if (victim >= 0) pieceKilled(victim, gameIndex, theAction);
//                     }
//                 }
//             }

//             RefreshConnectorState(gameIndex);
//         }

//         public static void ApplyUpgrade(in Action a, byte p, int gameIndex)
//         {
//             Debug.Log("Legacy Upgrade Path reached, FIX ME!");
//             // Legacy path unused (upgrade now via create)
//         }

//         public static void ApplyLauncher(in Action a, byte p, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;

//             int targetPid = a.aux;
//             if (targetPid < 0) return;
//             bm.MovePieceRow(targetPid, a.TargetCellId);
//             RefreshConnectorState(gameIndex);
//         }

//         public static void ApplySpawner(in Action theAction, byte currentPlayer, int gameIndex)
//         {
//             var gameState = GameRegistry.game[gameIndex].gameState;

//             var controller = GameRegistry.game[gameIndex].gameController;
//             var bm = GameRegistry.game[gameIndex].boardModel;

//             int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
//             if (actorPid < 0) return;


//             int targetType = PieceDefinition.spawn_targetType[theAction.pieceType];
//             int amount = PieceDefinition.spawn_pieceAmount[theAction.pieceType];
//             int range = PieceDefinition.spawn_range[theAction.pieceType];

//             int origin = bm.GetPieceCell(actorPid);
//             int[] empties = Scratch.GetScratchCellBuffer(gameIndex);
//             int cellCount = bm.GetCellCount();
//             int eCount = 0;
//             for (int c = 0; c < cellCount; c++)
//             {
//                 if (!bm.IsEmpty(c)) continue;
//                 int dist = bm.Distance(origin, c);
//                 if (dist < 1 || dist > range) continue;
//                 if (!BmAbilityCac.LineOfSightClear(origin, c, gameIndex)) continue;
//                 empties[eCount++] = c;
//             }
//             Array.Sort(empties, 0, eCount);

//             int availableLimit = int.MaxValue;
//             if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0)
//                 availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(currentPlayer);

//             int canCreate = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
//             if (canCreate <= 0) return;

//             // Prefer the chosen cell (a.TargetCellId) if still legal; then fill remaining
//             int spawned = 0;

//             bool ChosenCellIsLegal = false;
//             for (int i = 0; i < eCount; i++)
//             {
//                 if (empties[i] == theAction.TargetCellId)
//                 {
//                     ChosenCellIsLegal = true;
//                     break;
//                 }
//             }

//             if (ChosenCellIsLegal && spawned < canCreate)
//             {
//                 int pid = bm.AllocateRow();
//                 bm.PlacePieceRow(pid, currentPlayer, (byte)targetType, theAction.TargetCellId, PieceDefinition.maxHP[targetType]);
//                 int g = PieceDefinition.digitItGives[(byte)targetType];
//                 if (g >= 0) gameState.ps[currentPlayer].GrantDigit(g);
//                 spawned++;
//             }

//             for (int i = 0; i < eCount && spawned < canCreate; i++)
//             {
//                 int cell = empties[i];
//                 if (cell == theAction.TargetCellId) continue; // already used chosen cell
//                 int pid = bm.AllocateRow();
//                 bm.PlacePieceRow(pid, currentPlayer, (byte)targetType, cell, PieceDefinition.maxHP[targetType]);
//                 int g = PieceDefinition.digitItGives[(byte)targetType];
//                 if (g >= 0) gameState.ps[currentPlayer].GrantDigit(g);
//                 spawned++;
//             }

//             // Mark once-per-turn flag
//             if (PieceDefinition.spawn_isOnlyOncePerTurn[theAction.pieceType])
//                 bm.spawnerUsedThisTurn.Add(actorPid);

//             RefreshConnectorState(gameIndex);
//         }

//         public static void ApplySacrificeFactory(in Action theAction, byte player, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;
//             var events = GameRegistry.game[gameIndex].eventManager;

//             int victimID = theAction.aux;
//             if (victimID < 0) return;
//             int Pieceid = bm.GetCellOccupant(theAction.ActorsCellId);
//             bm.pieceFactoryAux[Pieceid] += PieceDefinition.sacrificeFactory_amount[theAction.pieceType];

//             pieceKilled(victimID, gameIndex, theAction);
//             RefreshConnectorState(gameIndex);
//         }

//         public static void ApplyConversionFactory(in Action theAction, byte player, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;
//             var gameState = GameRegistry.game[gameIndex].gameState;

//             int Pieceid = bm.GetCellOccupant(theAction.ActorsCellId);

//             gameState.ps[player].vpTotal--;
//             bm.pieceFactoryAux[Pieceid] += PieceDefinition.conversionFactory_amount[theAction.pieceType];
//         }


//         public static void ApplyMultiCreatePlacement(in Action theAction, byte p, int gameIndex)
//         {
//             var bm = GameRegistry.game[gameIndex].boardModel;
//             var gameState = GameRegistry.game[gameIndex].gameState;

//             if (!gameState.multiCreateActive || theAction.pieceType != gameState.multiCreateType) return;
//             int cell = theAction.TargetCellId;
//             if (!bm.IsEmpty(cell)) return;
//             // Place the piece
//             int pid = bm.AllocateRow();
//             bm.PlacePieceRow(pid, p, (byte)theAction.pieceType, cell, PieceDefinition.maxHP[theAction.pieceType]);
//             int g = PieceDefinition.digitItGives[(byte)theAction.pieceType];
//             if (g >= 0) gameState.ps[p].GrantDigit(g);
//             // Reuse connector config from the initial piece if the type has connectors
//             if (PieceDefinition.connectors_enabled[theAction.pieceType] && gameState.multiCreateCells.Count > 0)
//             {
//                 // initial placed piece is at index 0 in multiCreateCells
//                 int primaryCell = gameState.multiCreateCells[0];
//                 int primaryPid = bm.GetCellOccupant(primaryCell);
//                 if (primaryPid >= 0)
//                     bm.pieceConnectorConfig[pid] = bm.pieceConnectorConfig[primaryPid];
//             }
//             gameState.multiCreateCells.Add(cell);
//             gameState.multiCreateRemaining = Math.Max(0, gameState.multiCreateRemaining - 1);
//             if (gameState.multiCreateRemaining <= 0)
//             {
//                 gameState.multiCreateActive = false;
//                 gameState.multiCreateCells.Clear();
//             }
//             RefreshConnectorState(gameIndex);
//         }


//         #endregion
//         #region ApplyHelpers



        public static void pieceKilled(int victim, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            int deadOwner = bm.GetPieceOwner(victim);
            byte deadType = bm.GetPieceType(victim);
            int g = PieceDefinition.digitItGives[deadType];
            if (g >= 0) gameState.ps[deadOwner].RevokeDigit(g);
            bm.FreeRowSwapBack(victim);
            RefreshConnectorState(gameIndex);
        }

        public static void pieceKilled(int victim, int gameIndex, Action theAction)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int Pieceid = bm.GetCellOccupant(theAction.ActorsCellId);

            bm.pieceFactoryAux[Pieceid] += PieceDefinition.eat_amount[theAction.pieceType];

            PassiveActions.FeedingGround(gameIndex, victim);

            pieceKilled(victim, gameIndex);
        }


        public static bool ApplyDamageWithCapital(int attackerCell, int targetPid, int dmg, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            if (targetPid < 0 || dmg <= 0) return false;

            int incomingDir = BmAbilityCac.GetDirectionIndex(attackerCell, bm.GetPieceCell(targetPid), gameIndex);
            if (incomingDir >= 0 && PieceDefinition.connectors_enabled[bm.GetPieceType(targetPid)])
            {
                int hitSide = PiecesSides.OppositeDir(incomingDir);
                bool sideIsConnector = PiecesSides.IsConnectorSide(bm.pieceConnectorConfig[targetPid], hitSide);
                if (!sideIsConnector)
                {
                    int cap = bm.pieceCapitalHP[targetPid];
                    if (cap > 0)
                    {
                        int remaining = cap - dmg;
                        if (remaining >= 0)
                        {
                            bm.pieceCapitalHP[targetPid] = remaining;
                            dmg = 0;
                        }
                        else
                        {
                            bm.pieceCapitalHP[targetPid] = 0;
                            dmg = (short)(-remaining);
                        }
                    }
                }
            }

            if (dmg <= 0) return false;
            return bm.DamagePieceRow(targetPid, dmg);
        }

        public static void RefreshConnectorState(int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            List<int> toDestroy = PiecesSides.RecomputeConnectorComponents(gameIndex);
            if (toDestroy == null) return;
            for (int i = 0; i < toDestroy.Count; i++)
            {
                int victimID = toDestroy[i];
                if (bm.IsValidPieceId(victimID))
                    pieceKilled(victimID, gameIndex);
            }
        }



        public static void ResolveMelee(int actorPid, int victimID, in Action theAction, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int dmg = PieceDefinition.move_damage[theAction.pieceType];
            bool killed = ApplyDamageWithCapital(actorPid, victimID, dmg, gameIndex);
            if (killed)
            {
                pieceKilled(victimID, gameIndex, theAction);
            }
            else
            {
                int origin = theAction.ActorsCellId;
                int best = BmAbilityCac.FindNearestEmptyAdjacent(origin, bm.GetPieceCell(victimID), gameIndex);
                if (best >= 0) bm.MovePieceRow(actorPid, best);
            }
            // Connector state refresh happens in GameActions after move/shoot/push/kill
        }




        // public static List<int> CollectClusterCells(byte type, int startCell, int gameIndex)
        // {
        //     var bm = GameRegistry.game[gameIndex].boardModel;

        //     var cells = new List<int>();
        //     if (startCell < 0) return cells;
        //     var visited = Scratch.GetScratchCellBuffer(gameIndex);
        //     Array.Clear(visited, 0, visited.Length);
        //     int[] queue = Scratch.GetScratchCellBuffer(gameIndex);
        //     int head = 0, tail = 0;
        //     queue[tail++] = startCell;
        //     visited[startCell] = 1;
        //     while (head < tail)
        //     {
        //         int cell = queue[head++];
        //         int pid = bm.GetCellOccupant(cell);
        //         if (pid >= 0 && bm.GetPieceType(pid) == type)
        //         {
        //             cells.Add(cell);
        //             int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
        //             int n = bm.GetNeighbors(cell, neigh);
        //             for (int i = 0; i < n; i++)
        //             {
        //                 int nb = neigh[i];
        //                 if (nb < 0 || nb >= visited.Length) continue;
        //                 if (visited[nb] != 0) continue;
        //                 int nbPid = bm.GetCellOccupant(nb);
        //                 if (nbPid < 0 || bm.GetPieceType(nbPid) != type) continue;
        //                 visited[nb] = 1;
        //                 queue[tail++] = nb;
        //             }
        //         }
        //     }
        //     return cells;
        // }



        public static void GetMultiCreateState(out bool active, out byte type, out bool border, out int remaining, out int[] cells, out int count, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;

            active = gameState.multiCreateActive;
            type = gameState.multiCreateType;
            border = gameState.multiCreateBorder;
            remaining = gameState.multiCreateRemaining;
            cells = gameState.multiCreateCells.ToArray();
            count = gameState.multiCreateCells.Count;
        }


//         #endregion



//         //Helper to call methods for applyActions easier


    }
}
public readonly struct PerPiecePayout
{
    public readonly int[] pieceType;
    public readonly bool[] isGroup;
    public readonly float[] payout;

    public PerPiecePayout(int[] pieceType, bool[] isGroup, float[] payout)
    {
        this.pieceType = pieceType;
        this.isGroup = isGroup;
        this.payout = payout;
    }
}
