using UnityEngine;
using static Game.Core.ActionKind;
using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class GameActions
    {
        #region Apply Actions

        public static void ApplyMove(in Action theAction, byte player, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;
            int dstOcc = bm.GetCellOccupant(theAction.dstCell);
            if (dstOcc >= 0)
            {
                ResolveMelee(actorPid, dstOcc, in theAction, gameIndex);
                bm.MovePieceRow(actorPid, theAction.dstCell);
            }
            else
            {
                bm.MovePieceRow(actorPid, theAction.dstCell);
            }
            RefreshConnectorState(gameIndex);
        }

        public static void ApplyShoot(in Action theAction, byte player, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            int victimID = theAction.aux;
            if (victimID < 0) return;
            short dmg = GetAbilityDamage(in theAction, gameIndex);
            bool killed = ApplyDamageWithCapital(theAction.srcCell, victimID, dmg, gameIndex);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                pieceKilled(victimID, gameIndex);
            }
            RefreshConnectorState(gameIndex);
        }

        public static void ApplySacrificeFactory(in Action theAction, byte player, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            int victimID = theAction.aux;
            if (victimID < 0) return;
            int Pieceid = bm.GetCellOccupant(theAction.srcCell);
            bm.pieceFactoryAux[Pieceid] += GetSacrificeFactoryAmount(in theAction, gameIndex);

            pieceKilled(victimID, gameIndex); ;
            RefreshConnectorState(gameIndex);
        }

        public static void ApplyConversionFactory(in Action theAction, byte player, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            int Pieceid = bm.GetCellOccupant(theAction.srcCell);

            gameState.ps[player].vpTotal--;
            bm.pieceFactoryAux[Pieceid] += GetConversionFactoryAmount(theAction, gameIndex);
        }

        public static void ApplyCreate(in Action theAction, byte player, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;


            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, player, (byte)theAction.pieceType, theAction.dstCell, Pieces.maxHPByType[theAction.pieceType]);
            // Set connector config if applicable (Create uses aux for config index)
            bm.pieceConnectorConfig[pid] = (byte)theAction.aux;
            // Grant digit if this type provides one
            int g = Pieces.GrantsDigit((byte)theAction.pieceType);
            if (g >= 0) gameState.ps[player].GrantDigit(g);

            if (Pieces.multiCreate_enabledByType[theAction.pieceType])
            {
                int total = Math.Max(1, Pieces.multiCreate_amountByType[theAction.pieceType]);
                if (total > 1)
                {
                    gameState.multiCreateActive = true;
                    gameState.multiCreateType = (byte)theAction.pieceType;
                    gameState.multiCreateBorder = Pieces.multiCreate_boarderingByType[theAction.pieceType];
                    gameState.multiCreateRemaining = total - 1;
                    gameState.multiCreateCells.Clear();
                    gameState.multiCreateCells.Add(theAction.dstCell);
                }
            }

            RefreshConnectorState(gameIndex);
        }


        public static void ApplyMultiCreatePlacement(in Action a, byte p, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            if (!gameState.multiCreateActive || a.pieceType != gameState.multiCreateType) return;
            int cell = a.dstCell;
            if (!bm.IsEmpty(cell)) return;
            // Place the piece
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, p, (byte)a.pieceType, cell, Pieces.maxHPByType[a.pieceType]);
            int g = Pieces.GrantsDigit((byte)a.pieceType);
            if (g >= 0) gameState.ps[p].GrantDigit(g);
            // Reuse connector config from the initial piece if the type has connectors
            if (Pieces.HasConnectors(a.pieceType) && gameState.multiCreateCells.Count > 0)
            {
                // initial placed piece is at index 0 in multiCreateCells
                int primaryCell = gameState.multiCreateCells[0];
                int primaryPid = bm.GetCellOccupant(primaryCell);
                if (primaryPid >= 0)
                    bm.pieceConnectorConfig[pid] = bm.pieceConnectorConfig[primaryPid];
            }
            gameState.multiCreateCells.Add(cell);
            gameState.multiCreateRemaining = Math.Max(0, gameState.multiCreateRemaining - 1);
            if (gameState.multiCreateRemaining <= 0)
            {
                gameState.multiCreateActive = false;
                gameState.multiCreateCells.Clear();
            }
            RefreshConnectorState(gameIndex);
        }


        public static void ApplyGroupBuild(in Action a, byte p, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            int targetType = a.pieceType;
            int dst = a.dstCell;
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, p, (byte)targetType, dst, Pieces.maxHPByType[targetType]);
            int g = Pieces.GrantsDigit((byte)targetType);
            if (g >= 0) gameState.ps[p].GrantDigit(g);

            int srcCell = a.srcCell;
            int actorPid = bm.GetCellOccupant(srcCell);
            if (actorPid >= 0)
            {
                byte actorType = bm.GetPieceType(actorPid);
                if (Pieces.groupBuildDeletion[actorType])
                {
                    var list = CollectClusterCells(actorType, srcCell, gameIndex);
                    int need = Pieces.groupBuildRequireNumber[actorType];
                    list.Sort();
                    for (int i = 0; i < need && i < list.Count; i++)
                    {
                        int cell = list[i];
                        int victim = bm.GetCellOccupant(cell);
                        if (victim >= 0) pieceKilled(victim, gameIndex);
                    }
                }
            }

            RefreshConnectorState(gameIndex);
        }


        public static void ApplySpawner(in Action a, byte p, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;

            var controller = GameRegistry.game[gameIndex].gameController;
            var bm = GameRegistry.game[gameIndex].boardModel;

            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return;
            int abilityId = Pieces.AbilityIdAtSlot(bm.GetPieceType(actorPid), a.abilitySlot);
            if (abilityId < 0) return;

            int targetType = Pieces.spawn_targetType[abilityId];
            int amount = Pieces.spawn_pieceAmount[abilityId];
            int range = Pieces.spawn_range[abilityId];

            int origin = bm.GetPieceCell(actorPid);
            int[] empties = bm.GetScratchCellBuffer();
            int cellCount = bm.GetCellCount();
            int eCount = 0;
            for (int c = 0; c < cellCount; c++)
            {
                if (!bm.IsEmpty(c)) continue;
                int dist = bm.Distance(origin, c);
                if (dist < 1 || dist > range) continue;
                if (!bm.LineOfSightClear(origin, c)) continue;
                empties[eCount++] = c;
            }
            Array.Sort(empties, 0, eCount);

            int availableLimit = int.MaxValue;
            if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0)
                availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(p);

            int canCreate = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
            if (canCreate <= 0) return;

            // Prefer the chosen cell (a.dstCell) if still legal; then fill remaining
            int spawned = 0;

            bool ChosenCellIsLegal = false;
            for (int i = 0; i < eCount; i++)
            {
                if (empties[i] == a.dstCell)
                {
                    ChosenCellIsLegal = true;
                    break;
                }
            }

            if (ChosenCellIsLegal && spawned < canCreate)
            {
                int pid = bm.AllocateRow();
                bm.PlacePieceRow(pid, p, (byte)targetType, a.dstCell, Pieces.maxHPByType[targetType]);
                int g = Pieces.GrantsDigit((byte)targetType);
                if (g >= 0) gameState.ps[p].GrantDigit(g);
                spawned++;
            }

            for (int i = 0; i < eCount && spawned < canCreate; i++)
            {
                int cell = empties[i];
                if (cell == a.dstCell) continue; // already used chosen cell
                int pid = bm.AllocateRow();
                bm.PlacePieceRow(pid, p, (byte)targetType, cell, Pieces.maxHPByType[targetType]);
                int g = Pieces.GrantsDigit((byte)targetType);
                if (g >= 0) gameState.ps[p].GrantDigit(g);
                spawned++;
            }

            // Mark once-per-turn flag
            if (Pieces.spawn_onlyOncePerTurn[abilityId])
                bm.spawnerUsedThisTurn.Add(actorPid);

            RefreshConnectorState(gameIndex);
        }


        public static void ApplyUpgrade(in Action a, byte p, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return;
            byte actorType = bm.GetPieceType(actorPid);
            int targetType = Pieces.upgradeTargetType[actorType];
            if (targetType < 0 || targetType >= Pieces.typeCount) return;

            // Preserve connector config
            byte oldConfig = bm.pieceConnectorConfig[actorPid];

            // Revoke digit from old type if it granted one
            int gOld = Pieces.GrantsDigit(actorType);
            if (gOld >= 0) gameState.ps[p].RevokeDigit(gOld);

            // Replace type and reset HP
            bm.pieceType[actorPid] = (byte)targetType;
            bm.pieceHP[actorPid] = Pieces.maxHPByType[targetType];
            bm.pieceConnectorConfig[actorPid] = oldConfig;

            // Grant digit for new type
            int gNew = Pieces.GrantsDigit((byte)targetType);
            if (gNew >= 0) gameState.ps[p].GrantDigit(gNew);

            // No connector refresh per requirement
        }


        public static void ApplyLauncher(in Action a, byte p, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int targetPid = a.aux;
            if (targetPid < 0) return;
            bm.MovePieceRow(targetPid, a.dstCell);
            RefreshConnectorState(gameIndex);
        }



        public static void ApplyCaptureVP(in Action theAction, byte player, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;

            gameState.ps[player].OnCaptureVP();
            gameState.AddCenterVictoryPoints(-1);
        }

        public static void ApplyCoreDamage(in Action theAction, byte player, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            gameState.ps[player].OnCoreDamage();

            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;

            int actorCell = bm.GetPieceCell(actorPid);
            byte enemy = bm.OwnerOfCoreCell(actorCell);
            if (enemy >= 4) return;

            byte typ = bm.GetPieceType(actorPid);
            int abi = Pieces.AbilityIdAtSlot(typ, theAction.abilitySlot);
            short dmg = (short)((abi >= 0 && abi < Pieces.damage.Length) ? Pieces.damage[abi] : 0);

            int hp = gameState.GetCoreHealth(enemy);
            gameState.SetCoreHealth(enemy, hp - dmg);
            gameState.TryEliminatePlayer(enemy);
        }

        public static void ApplyPush(in Action theAction, byte player, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            int victimID = theAction.aux;
            if (victimID < 0) return;

            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;

            byte actorType = bm.GetPieceType(actorPid);
            int abilityId = Pieces.AbilityIdAtSlot(actorType, theAction.abilitySlot);
            if (abilityId < 0) return;

            short dmg = GetAbilityDamage(in theAction, gameIndex);
            bool killed = ApplyDamageWithCapital(theAction.srcCell, victimID, dmg, gameIndex);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                pieceKilled(victimID, gameIndex);
            }
            else
            {
                int pushedCellID = LegalityKernals.ComputePushDestination(actorPid, victimID, abilityId, gameIndex);
                bm.MovePieceRow(victimID, pushedCellID);
            }
            RefreshConnectorState(gameIndex);
        }


        //Dont Use this method
        public static void ApplyFactoryIncome(int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;

            float[] income = ComputePlayersFactoryIncome(gameIndex);

            for (int i = 0; i < gameState.ps.Length; i++)
            {
                gameState.ps[i].AddBudget(income[i]);
            }
        }


        public static float[] ComputePlayersFactoryIncome(int gameIndex)
        {
            float[] perPlayerFactoryIncome = new float[4];
            Array.Clear(perPlayerFactoryIncome, 0, 4);

            for (int i = 0; i < 4; i++)
            {
                PerPiecePayout income = ComputeDetailedPlayerFactoryIncome(i, gameIndex);
                float total = 0f;

                for (int c = 0; c < income.payout.Length; i++)
                {
                    total += income.payout[c];
                }
                perPlayerFactoryIncome[i] = total;
            }
            return perPlayerFactoryIncome;
        }

        public static PerPiecePayout ComputeDetailedPlayerFactoryIncome(int playerId, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            // Build per-piece payouts for a single player (type, whether grouped, payout per type)
            var pieceTypes = new List<int>();
            var isGroups = new List<bool>();
            var payouts = new List<float>();

            // Ensure round number is at least 1
            int roundNum = Math.Max(1, gameState.currentRoundNumber);
            var counts = GameState.SnapshotOwnerTypeCounts(bm); // map of (owner,type) -> count

            foreach (var kv in counts)
            {
                if (kv.Key.owner != playerId)
                    continue;

                int owner = kv.Key.owner;
                if (owner < 0 || owner >= gameState.ps.Length) continue;

                int type = kv.Key.type;
                int count = kv.Value;

                int factoryAid = GetFactoryAbilityId((byte)type); // ability slot that grants factory income
                if (factoryAid < 0) continue;

                float AuxPayout = AuxFactoryPayout(owner, type, gameIndex);

                int baseAmt = (factoryAid < Pieces.factory_amount.Length)
                    ? Pieces.factory_amount[factoryAid]
                    : 0;
                if (baseAmt == 0 && AuxPayout == 0) continue;

                // Flags for scaling
                bool roundMul = factoryAid < Pieces.factory_roundMultiplier.Length
                             && Pieces.factory_roundMultiplier[factoryAid];

                bool group = factoryAid < Pieces.factory_group.Length
                          && Pieces.factory_group[factoryAid];

                int groupAmt = (factoryAid < Pieces.factory_groupAmount.Length)
                    ? Pieces.factory_groupAmount[factoryAid]
                    : 1;

                int pay = baseAmt;
                if (roundMul) pay *= roundNum;

                float payout;
                if (group)
                {
                    int groups = count / Math.Max(1, groupAmt); // pay per full group
                    payout = pay * groups + AuxPayout;
                }
                else
                {
                    payout = pay * count + AuxPayout; // pay per individual piece
                }

                if (payout == 0)
                    continue;

                pieceTypes.Add(type);
                isGroups.Add(group);
                payouts.Add(payout);
            }

            // Convert lists to arrays for the struct
            return new PerPiecePayout(
                pieceTypes.ToArray(),
                isGroups.ToArray(),
                payouts.ToArray()
            );
        }

        private static float AuxFactoryPayout(int playerId, int thisType, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            float payOut = 0;

            for (int pid = 0; pid < bm.pieceCount; pid++)
            {
                byte type = bm.GetPieceType(pid);
                if (thisType != type) continue;
                if (!Pieces.HasAbilityKind(type, Pieces.AbilityKind.Factory)) continue;
                if (bm.pieceOwner[pid] != playerId) continue;

                payOut += bm.pieceFactoryAux[pid];
            }
            return payOut;
        }


        #endregion
        #region ApplyHelpers


        //private void ifOnKillAction(in Action theAction);
        // {   

        // }

        //this can replace the if(killed) line in both ResolveMelee and ResolveShoot
        public static void pieceKilled(int victim, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            int deadOwner = bm.GetPieceOwner(victim);
            byte deadType = bm.GetPieceType(victim);
            int g = Pieces.GrantsDigit(deadType);
            if (g >= 0) gameState.ps[deadOwner].RevokeDigit(g);
            bm.FreeRowSwapBack(victim);
            RefreshConnectorState(gameIndex);
        }

        private static bool ApplyDamageWithCapital(int attackerCell, int targetPid, short dmg, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            if (targetPid < 0 || dmg <= 0) return false;

            int incomingDir = bm.GetDirectionIndex(attackerCell, bm.GetPieceCell(targetPid));
            if (incomingDir >= 0 && Pieces.HasConnectors(bm.GetPieceType(targetPid)))
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

        private static void RefreshConnectorState(int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            List<int> toDestroy = PiecesSides.RecomputeConnectorComponents(gameIndex);
            if (toDestroy == null) return;
            for (int i = 0; i < toDestroy.Count; i++)
            {
                int pid = toDestroy[i];
                if (bm.IsValidPieceId(pid))
                    pieceKilled(pid, gameIndex);
            }
        }



        public static void ResolveMelee(int actorPid, int defenderPid, in Action a, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            short dmg = GetAbilityDamage(in a, gameIndex);
            bool killed = ApplyDamageWithCapital(actorPid, defenderPid, dmg, gameIndex);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                int deadOwner = bm.GetPieceOwner(defenderPid);
                byte deadType = bm.GetPieceType(defenderPid);
                int g = Pieces.GrantsDigit(deadType);
                if (g >= 0) gameState.ps[deadOwner].RevokeDigit(g);
                bm.FreeRowSwapBack(defenderPid);
                bm.MovePieceRow(actorPid, a.dstCell);
            }
            else
            {
                int origin = a.srcCell;
                int best = bm.FindNearestEmptyAdjacent(origin, bm.GetPieceCell(defenderPid));
                if (best >= 0) bm.MovePieceRow(actorPid, best);
            }
            // Connector state refresh happens in GameActions after move/shoot/push/kill
        }

        public static short GetAbilityDamage(in Action a, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int pid = bm.GetCellOccupant(a.srcCell);
            byte typ = bm.GetPieceType(pid);
            int abi = Pieces.AbilityIdAtSlot(typ, a.abilitySlot);
            return (short)((abi >= 0 && abi < Pieces.damage.Length) ? Pieces.damage[abi] : 0);
        }

        public static int GetSacrificeFactoryAmount(in Action a, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int pid = bm.GetCellOccupant(a.srcCell);
            byte typ = bm.GetPieceType(pid);
            int abi = Pieces.AbilityIdAtSlot(typ, a.abilitySlot);
            return (abi >= 0 && abi < Pieces.sacrificeFactory_amount.Length) ? Pieces.sacrificeFactory_amount[abi] : 0;
        }

        public static int GetConversionFactoryAmount(in Action a, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int pid = bm.GetCellOccupant(a.srcCell);
            byte typ = bm.GetPieceType(pid);
            int abi = Pieces.AbilityIdAtSlot(typ, a.abilitySlot);
            return (abi >= 0 && abi < Pieces.conversionFactory_amount.Length) ? Pieces.conversionFactory_amount[abi] : 0;
        }

        private static int GetFactoryAbilityId(byte type)
        {
            int limit = Pieces.AbilitySlotCount(type);
            for (int s = 0; s < limit; s++)
            {
                int aid = Pieces.AbilityIdAtSlot(type, s);
                if (aid >= 0 && Pieces.abilityKind[aid] == Pieces.AbilityKind.Factory)
                    return aid;
            }
            return -1;
        }

        private static List<int> CollectClusterCells(byte type, int startCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            var cells = new List<int>();
            if (startCell < 0) return cells;
            var visited = bm.GetScratchCellBuffer();
            Array.Clear(visited, 0, visited.Length);
            int[] queue = bm.GetScratchCellBuffer();
            int head = 0, tail = 0;
            queue[tail++] = startCell;
            visited[startCell] = 1;
            while (head < tail)
            {
                int cell = queue[head++];
                int pid = bm.GetCellOccupant(cell);
                if (pid >= 0 && bm.GetPieceType(pid) == type)
                {
                    cells.Add(cell);
                    int[] neigh = bm.GetScratchNeighborBuffer();
                    int n = bm.GetNeighbors(cell, neigh);
                    for (int i = 0; i < n; i++)
                    {
                        int nb = neigh[i];
                        if (nb < 0 || nb >= visited.Length) continue;
                        if (visited[nb] != 0) continue;
                        int nbPid = bm.GetCellOccupant(nb);
                        if (nbPid < 0 || bm.GetPieceType(nbPid) != type) continue;
                        visited[nb] = 1;
                        queue[tail++] = nb;
                    }
                }
            }
            return cells;
        }



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


        #endregion



        //Helper to call methods for applyActions easier


        public static int ProtectedBySanctuary(Span<int> outPieceIds, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            gameState.dublicateFilter.Clear();
            Span<int> protectedpieces = stackalloc int[240];
            int foundPieces = 0;

            for (int pid = 0; pid < bm.pieceCount; pid++)
            {
                byte type = bm.GetPieceType(pid);
                int sanctuaryRange = -1;

                // Find a Sanctuary ability on this type and grab its range
                int slotLimit = Pieces.AbilitySlotCount(type);
                for (int s = 0; s < slotLimit; s++)
                {
                    int aid = Pieces.AbilityIdAtSlot(type, s);
                    if (aid < 0 || aid >= Pieces.sanctuary_enabled.Length) continue;
                    if (Pieces.abilityKind[aid] != Pieces.AbilityKind.Sanctuary) continue;
                    if (!Pieces.sanctuary_enabled[aid]) continue;
                    sanctuaryRange = (aid < Pieces.Sanctuary_range.Length) ? Pieces.Sanctuary_range[aid] : -1;
                    break;
                }

                if (sanctuaryRange < 0) continue;
                int centerCell = bm.pieceCellId[pid];
                if (centerCell < 0) continue;

                for (int range = 0; range <= sanctuaryRange; range++)
                {
                    int found = bm.pieceIdsRingAroundCell(centerCell, range, protectedpieces);

                    if (found <= 0) continue;

                    for (int pp = 0; pp < found; pp++)
                    {
                        if (gameState.dublicateFilter.Add(protectedpieces[pp]))
                        {
                            outPieceIds[foundPieces] = protectedpieces[pp];
                            foundPieces++;
                        }
                    }
                }
            }
            return foundPieces;
        }

        public static bool IsPieceProtectedBySanctuary(int pieceId, int gameIndex)
        {
            Span<int> protectedpieces = stackalloc int[240];
            int numberOfProtectedPieces = ProtectedBySanctuary(protectedpieces, gameIndex);

            for (int i = 0; i < numberOfProtectedPieces; i++)
            {
                if (pieceId != protectedpieces[i]) continue;
                return true;
            }
            return false;
        }

        public static bool IsPieceApartOfSpan(int PieceId, Span<int> inPieceIds, int spanLength)
        {
            for (int i = 0; i < spanLength; i++)
            {
                if (PieceId != inPieceIds[i]) continue;
                return true;
            }
            return false;
        }


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
