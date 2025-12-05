using UnityEngine;
using static Game.Core.ActionKind;
using System;
using System.Collections.Generic;

namespace Game.Core
{
    public class GameActions
    {
        private BoardModel bm;

        private Pieces pcs;

        private GameState gamestate;

        private PlayerState[] ps;

        EventManager events;

        GameController controller;


        public void Initialize(BoardModel board, Pieces pieces, GameState _gamestate, PlayerState[] players, EventManager eventManager, GameController gamecontroller)
        {
            bm = board;
            pcs = pieces;
            gamestate = _gamestate;
            ps = players;
            events = eventManager;
            controller = gamecontroller;

            subscribe();
        }


        public void subscribe()
        {
            events.PieceKilled += pieceKilled;
        }


        //Apply Methods Values

        public bool multiCreateActive;
        public byte multiCreateType;
        public bool multiCreateBorder;
        public int multiCreateRemaining;
        public List<int> multiCreateCells = new List<int>(8);

        public int MultiCreateCellCount => multiCreateCells.Count;


        public HashSet<int> spawnerUsedThisTurn = new HashSet<int>();

        private bool _isGameActionSubscribe = false;
        public void gameActionSubscribe()
        {
            if (_isGameActionSubscribe)
                return;

            events.TurnBegin += whenTurnBegins;
            _isGameActionSubscribe = true;
        }

        #region GameLoop

        private void whenTurnBegins(TurnContext _)
        {
            spawnerUsedThisTurn.Clear();
            ResetMultiCreate();
        }

        #endregion
        #region Apply Actions

        public void ApplyMove(in Action theAction, byte player)
        {
            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;
            int dstOcc = bm.GetCellOccupant(theAction.dstCell);
            if (dstOcc >= 0)
            {
                ResolveMelee(actorPid, dstOcc, in theAction);
                bm.MovePieceRow(actorPid, theAction.dstCell);
            }
            else
            {
                bm.MovePieceRow(actorPid, theAction.dstCell);
            }
            RefreshConnectorState();
        }

        public void ApplyShoot(in Action theAction, byte player)
        {
            int victimID = theAction.aux;
            if (victimID < 0) return;
            short dmg = GetAbilityDamage(in theAction);
            bool killed = ApplyDamageWithCapital(theAction.srcCell, victimID, dmg);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                events.RaisePieceKilled(victimID);
            }
            RefreshConnectorState();
        }

        public void ApplySacrificeFactory(in Action theAction, byte player)
        {
            int victimID = theAction.aux;
            if (victimID < 0) return;
            int Pieceid = bm.GetCellOccupant(theAction.srcCell);
            bm.pieceFactoryAux[Pieceid] += GetSacrificeFactoryAmount(in theAction);

            events.RaisePieceKilled(victimID);
            RefreshConnectorState();
        }

        public void ApplyConversionFactory(in Action theAction, byte player)
        {
            int HowMuchToConvert = theAction.aux;
            if (HowMuchToConvert < 0) return;
            int Pieceid = bm.GetCellOccupant(theAction.srcCell);
            bm.pieceFactoryAux[Pieceid] += HowMuchToConvert;
        }

        public void ApplyCreate(in Action theAction, byte player)
        {
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, player, (byte)theAction.pieceType, theAction.dstCell, pcs.maxHPByType[theAction.pieceType]);
            // Set connector config if applicable (Create uses aux for config index)
            bm.pieceConnectorConfig[pid] = (byte)theAction.aux;
            // Grant digit if this type provides one
            int g = pcs.GrantsDigit((byte)theAction.pieceType);
            if (g >= 0) gamestate.ps[player].GrantDigit(g);

            if (pcs.multiCreate_enabledByType[theAction.pieceType])
            {
                int total = Math.Max(1, pcs.multiCreate_amountByType[theAction.pieceType]);
                if (total > 1)
                {
                    multiCreateActive = true;
                    multiCreateType = (byte)theAction.pieceType;
                    multiCreateBorder = pcs.multiCreate_boarderingByType[theAction.pieceType];
                    multiCreateRemaining = total - 1;
                    multiCreateCells.Clear();
                    multiCreateCells.Add(theAction.dstCell);
                }
            }

            RefreshConnectorState();
        }


        public void ApplyMultiCreatePlacement(in Action a, byte p)
        {
            if (!multiCreateActive || a.pieceType != multiCreateType) return;
            int cell = a.dstCell;
            if (!bm.IsEmpty(cell)) return;
            // Place the piece
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, p, (byte)a.pieceType, cell, pcs.maxHPByType[a.pieceType]);
            int g = pcs.GrantsDigit((byte)a.pieceType);
            if (g >= 0) gamestate.ps[p].GrantDigit(g);
            // Reuse connector config from the initial piece if the type has connectors
            if (pcs.HasConnectors(a.pieceType) && multiCreateCells.Count > 0)
            {
                // initial placed piece is at index 0 in multiCreateCells
                int primaryCell = multiCreateCells[0];
                int primaryPid = bm.GetCellOccupant(primaryCell);
                if (primaryPid >= 0)
                    bm.pieceConnectorConfig[pid] = bm.pieceConnectorConfig[primaryPid];
            }
            multiCreateCells.Add(cell);
            multiCreateRemaining = Math.Max(0, multiCreateRemaining - 1);
            if (multiCreateRemaining <= 0)
            {
                multiCreateActive = false;
                multiCreateCells.Clear();
            }
            RefreshConnectorState();
        }


        public void ApplyGroupBuild(in Action a, byte p)
        {
            int targetType = a.pieceType;
            int dst = a.dstCell;
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, p, (byte)targetType, dst, pcs.maxHPByType[targetType]);
            int g = pcs.GrantsDigit((byte)targetType);
            if (g >= 0) gamestate.ps[p].GrantDigit(g);

            int srcCell = a.srcCell;
            int actorPid = bm.GetCellOccupant(srcCell);
            if (actorPid >= 0)
            {
                byte actorType = bm.GetPieceType(actorPid);
                if (pcs.groupBuildDeletion[actorType])
                {
                    var list = CollectClusterCells(actorType, srcCell);
                    int need = pcs.groupBuildRequireNumber[actorType];
                    list.Sort();
                    for (int i = 0; i < need && i < list.Count; i++)
                    {
                        int cell = list[i];
                        int victim = bm.GetCellOccupant(cell);
                        if (victim >= 0) events.RaisePieceKilled(victim);
                    }
                }
            }

            RefreshConnectorState();
        }


        public void ApplySpawner(in Action a, byte p)
        {
            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return;
            int abilityId = pcs.AbilityIdAtSlot(bm.GetPieceType(actorPid), a.abilitySlot);
            if (abilityId < 0) return;

            int targetType = pcs.spawn_targetType[abilityId];
            int amount = pcs.spawn_pieceAmount[abilityId];
            int range = pcs.spawn_range[abilityId];

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
                bm.PlacePieceRow(pid, p, (byte)targetType, a.dstCell, pcs.maxHPByType[targetType]);
                int g = pcs.GrantsDigit((byte)targetType);
                if (g >= 0) gamestate.ps[p].GrantDigit(g);
                spawned++;
            }

            for (int i = 0; i < eCount && spawned < canCreate; i++)
            {
                int cell = empties[i];
                if (cell == a.dstCell) continue; // already used chosen cell
                int pid = bm.AllocateRow();
                bm.PlacePieceRow(pid, p, (byte)targetType, cell, pcs.maxHPByType[targetType]);
                int g = pcs.GrantsDigit((byte)targetType);
                if (g >= 0) gamestate.ps[p].GrantDigit(g);
                spawned++;
            }

            // Mark once-per-turn flag
            if (pcs.spawn_onlyOncePerTurn[abilityId])
                spawnerUsedThisTurn.Add(actorPid);

            RefreshConnectorState();
        }


        public void ApplyUpgrade(in Action a, byte p)
        {
            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return;
            byte actorType = bm.GetPieceType(actorPid);
            int targetType = pcs.upgradeTargetType[actorType];
            if (targetType < 0 || targetType >= pcs.typeCount) return;

            // Preserve connector config
            byte oldConfig = bm.pieceConnectorConfig[actorPid];

            // Revoke digit from old type if it granted one
            int gOld = pcs.GrantsDigit(actorType);
            if (gOld >= 0) gamestate.ps[p].RevokeDigit(gOld);

            // Replace type and reset HP
            bm.pieceType[actorPid] = (byte)targetType;
            bm.pieceHP[actorPid] = pcs.maxHPByType[targetType];
            bm.pieceConnectorConfig[actorPid] = oldConfig;

            // Grant digit for new type
            int gNew = pcs.GrantsDigit((byte)targetType);
            if (gNew >= 0) gamestate.ps[p].GrantDigit(gNew);

            // No connector refresh per requirement
        }


        public void ApplyLauncher(in Action a, byte p)
        {
            int targetPid = a.aux;
            if (targetPid < 0) return;
            bm.MovePieceRow(targetPid, a.dstCell);
            RefreshConnectorState();
        }



        public void ApplyCaptureVP(in Action theAction, byte player)
        {
            gamestate.ps[player].OnCaptureVP();
            gamestate.AddCenterVictoryPoints(-1);
        }

        public void ApplyCoreDamage(in Action theAction, byte player)
        {
            gamestate.ps[player].OnCoreDamage();

            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;

            int actorCell = bm.GetPieceCell(actorPid);
            byte enemy = bm.OwnerOfCoreCell(actorCell);
            if (enemy >= 4) return;

            byte typ = bm.GetPieceType(actorPid);
            int abi = pcs.AbilityIdAtSlot(typ, theAction.abilitySlot);
            short dmg = (short)((abi >= 0 && abi < pcs.damage.Length) ? pcs.damage[abi] : 0);

            int hp = gamestate.GetCoreHealth(enemy);
            gamestate.SetCoreHealth(enemy, hp - dmg);
            gamestate.TryEliminatePlayer(enemy);
        }

        public void ApplyPush(in Action theAction, byte player)
        {
            int victimID = theAction.aux;
            if (victimID < 0) return;

            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;

            byte actorType = bm.GetPieceType(actorPid);
            int abilityId = pcs.AbilityIdAtSlot(actorType, theAction.abilitySlot);
            if (abilityId < 0) return;

            short dmg = GetAbilityDamage(in theAction);
            bool killed = ApplyDamageWithCapital(theAction.srcCell, victimID, dmg);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                events.RaisePieceKilled(victimID);
            }
            else
            {
                int pushedCellID = PushDestination(actorPid, victimID, abilityId);
                bm.MovePieceRow(victimID, pushedCellID);
            }
            RefreshConnectorState();
        }


        //Dont Use this method
        public void ApplyFactoryIncome()
        {
            float[] income = ComputePlayersFactoryIncome();

            for (int i = 0; i < ps.Length; i++)
            {
                ps[i].AddBudget(income[i]);
            }
        }

        private float[] perPlayerFactoryIncome = new float[4]; // allocated once
        public float[] ComputePlayersFactoryIncome()
        {
            Array.Clear(perPlayerFactoryIncome, 0, 4);

            for (int i = 0; i < 4; i++)
            {
                PerPiecePayout income = ComputeDetailedPlayerFactoryIncome(i);
                float total = 0f;

                for (int c = 0; c < income.payout.Length; i++)
                {
                    total += income.payout[c];
                }
                perPlayerFactoryIncome[i] = total;
            }
            return perPlayerFactoryIncome;
        }

        public PerPiecePayout ComputeDetailedPlayerFactoryIncome(int playerId)
        {
            // Build per-piece payouts for a single player (type, whether grouped, payout per type)
            var pieceTypes = new List<int>();
            var isGroups = new List<bool>();
            var payouts = new List<float>();

            // Ensure round number is at least 1
            int roundNum = Math.Max(1, gamestate.currentRoundNumber);
            var counts = GameState.SnapshotOwnerTypeCounts(bm); // map of (owner,type) -> count

            foreach (var kv in counts)
            {
                if (kv.Key.owner != playerId)
                    continue;

                int owner = kv.Key.owner;
                if (owner < 0 || owner >= ps.Length) continue;

                int type = kv.Key.type;
                int count = kv.Value;

                int factoryAid = GetFactoryAbilityId((byte)type); // ability slot that grants factory income
                if (factoryAid < 0) continue;

                float AuxPayout = AuxFactoryPayout(owner, type);

                int baseAmt = (factoryAid < pcs.factory_amount.Length)
                    ? pcs.factory_amount[factoryAid]
                    : 0;
                if (baseAmt == 0 && AuxPayout == 0) continue;

                // Flags for scaling
                bool roundMul = factoryAid < pcs.factory_roundMultiplier.Length
                             && pcs.factory_roundMultiplier[factoryAid];

                bool group = factoryAid < pcs.factory_group.Length
                          && pcs.factory_group[factoryAid];

                int groupAmt = (factoryAid < pcs.factory_groupAmount.Length)
                    ? pcs.factory_groupAmount[factoryAid]
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

        private float AuxFactoryPayout(int playerId, int thisType)
        {
            float payOut = 0;

            for (int pid = 0; pid < bm.pieceCount; pid++)
            {
                byte type = bm.GetPieceType(pid);
                if (thisType != type) continue;
                if (!pcs.HasAbilityKind(type, Pieces.AbilityKind.Factory)) continue;
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
        public void pieceKilled(int victim)
        {
            int deadOwner = bm.GetPieceOwner(victim);
            byte deadType = bm.GetPieceType(victim);
            int g = pcs.GrantsDigit(deadType);
            if (g >= 0) ps[deadOwner].RevokeDigit(g);
            bm.FreeRowSwapBack(victim);
            RefreshConnectorState();
        }

        private bool ApplyDamageWithCapital(int attackerCell, int targetPid, short dmg)
        {
            if (targetPid < 0 || dmg <= 0) return false;

            int incomingDir = bm.GetDirectionIndex(attackerCell, bm.GetPieceCell(targetPid));
            if (incomingDir >= 0 && pcs.HasConnectors(bm.GetPieceType(targetPid)))
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

        private void RefreshConnectorState()
        {
            if (pcs == null || bm == null) return;
            List<int> toDestroy = PiecesSides.RecomputeConnectorComponents(bm, pcs);
            if (toDestroy == null) return;
            for (int i = 0; i < toDestroy.Count; i++)
            {
                int pid = toDestroy[i];
                if (bm.IsValidPieceId(pid))
                    events.RaisePieceKilled(pid);
            }
        }



        public void ResolveMelee(int actorPid, int defenderPid, in Action a)
        {
            short dmg = GetAbilityDamage(in a);
            bool killed = ApplyDamageWithCapital(actorPid, defenderPid, dmg);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                int deadOwner = bm.GetPieceOwner(defenderPid);
                byte deadType = bm.GetPieceType(defenderPid);
                int g = pcs.GrantsDigit(deadType);
                if (g >= 0) ps[deadOwner].RevokeDigit(g);
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

        public short GetAbilityDamage(in Action a)
        {
            int pid = bm.GetCellOccupant(a.srcCell);
            byte typ = bm.GetPieceType(pid);
            int abi = pcs.AbilityIdAtSlot(typ, a.abilitySlot);
            return (short)((abi >= 0 && abi < pcs.damage.Length) ? pcs.damage[abi] : 0);
        }

        public int GetSacrificeFactoryAmount(in Action a)
        {
            int pid = bm.GetCellOccupant(a.srcCell);
            byte typ = bm.GetPieceType(pid);
            int abi = pcs.AbilityIdAtSlot(typ, a.abilitySlot);
            return (abi >= 0 && abi < pcs.sacrificeFactory_amount.Length) ? pcs.sacrificeFactory_amount[abi] : 0;
        }

        private int GetFactoryAbilityId(byte type)
        {
            int limit = pcs.AbilitySlotCount(type);
            for (int s = 0; s < limit; s++)
            {
                int aid = pcs.AbilityIdAtSlot(type, s);
                if (aid >= 0 && pcs.abilityKind[aid] == Pieces.AbilityKind.Factory)
                    return aid;
            }
            return -1;
        }

        private List<int> CollectClusterCells(byte type, int startCell)
        {
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



        public void GetMultiCreateState(out bool active, out byte type, out bool border, out int remaining, out int[] cells, out int count)
        {
            active = multiCreateActive;
            type = multiCreateType;
            border = multiCreateBorder;
            remaining = multiCreateRemaining;
            cells = multiCreateCells.ToArray();
            count = multiCreateCells.Count;
        }

        public void ResetMultiCreate()
        {
            multiCreateActive = false;
            multiCreateType = 0;
            multiCreateBorder = false;
            multiCreateRemaining = 0;
            multiCreateCells.Clear();
        }

        #endregion
        #region Get Legal Targets Methods

        // =====================================================================
        // ML-FRIENDLY LEGALITY KERNELS (Create is NOT an ability in Plan B)
        // No allocations; caller supplies buffers; return full counts (may exceed capacity).
        // =====================================================================

        /// <summary>
        /// MOVE structural legality: empty-only reachability; melee-on-move targets among enemies adjacent to reachable cells.
        /// Uses BoardModel's zero-alloc helpers (EnumerateReachableEmpty, GetNeighbors, etc.).
        /// </summary>

        public int GetLegalTargets_Move(BoardModel bm, int actorPieceId, int abilityId, int[] outTargets)
        {
            int originCell = bm.GetPieceCell(actorPieceId);
            if (originCell < 0) return 0;

            int rmin = (abilityId >= 0 && abilityId < pcs.rangeMin.Length) ? pcs.rangeMin[abilityId] : 0;
            int rmax = (abilityId >= 0 && abilityId < pcs.rangeMax.Length) ? pcs.rangeMax[abilityId] : 0;
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
        public int GetLegalTargets_Shoot(BoardModel bm, int actorPieceId, int abilityId, int[] outTargets)
        {
            int originCell = bm.GetPieceCell(actorPieceId);
            if (originCell < 0) return 0;
            int actorOwner = bm.GetPieceOwner(actorPieceId);

            int rmin = (abilityId >= 0 && abilityId < pcs.rangeMin.Length) ? pcs.rangeMin[abilityId] : 0;
            int rmax = (abilityId >= 0 && abilityId < pcs.rangeMax.Length) ? pcs.rangeMax[abilityId] : 0;
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



        /// <summary>
        /// CAPTURE VP (targetless): legal if actor stands on VP cell.
        /// </summary>
        public bool IsLegal_CaptureVP(BoardModel bm, int actorPieceId, int abilityId)
        {
            int originCell = bm.GetPieceCell(actorPieceId);
            if (originCell < 0) return false;
            return originCell == bm.GetVictoryPointCellId();
        }

        /// <summary>
        /// CORE DAMAGE (targetless): legal if actor stands on an ENEMY core cell.
        /// </summary>
        public bool IsLegal_CoreDamage(BoardModel bm, int actorPieceId, int abilityId)
        {
            int originCell = bm.GetPieceCell(actorPieceId);
            if (originCell < 0) return false;
            int owner = bm.GetPieceOwner(actorPieceId);
            return bm.IsEnemyCoreCell(originCell, owner);
        }



        public int GetLegalTargets_Push(
                BoardModel bm,
                Pieces pcs,
                int actorPieceId,
                int abilityId,
                int[] outTargets)    // pieceId targets

        {
            int originCell = bm.GetPieceCell(actorPieceId);
            if (originCell < 0)
                return 0;

            if (abilityId < 0 ||
                pcs.push_TargetsBuildings == null || pcs.push_TargetsSoldiers == null ||
                pcs.push_rangeMax == null || pcs.push_FriendlyFire == null ||
                abilityId >= pcs.push_TargetsBuildings.Length ||
                abilityId >= pcs.push_TargetsSoldiers.Length ||
                abilityId >= pcs.push_rangeMax.Length ||
                abilityId >= pcs.push_FriendlyFire.Length)
                return 0;

            int actorOwner = bm.GetPieceOwner(actorPieceId);

            bool allowBuildings = pcs.push_TargetsBuildings[abilityId];
            bool allowSoldiers = pcs.push_TargetsSoldiers[abilityId];
            int rangeMax = pcs.push_rangeMax[abilityId];
            bool allowFriendly = pcs.push_FriendlyFire[abilityId];

            int cap = outTargets != null ? outTargets.Length : 0;
            int count = 0;

            int cellCount = bm.CellCount;

            for (int c = 0; c < cellCount; c++)
            {
                int pid = bm.GetCellOccupant(c);
                if (pid < 0) continue;                          // empty

                if (!allowFriendly && bm.GetPieceOwner(pid) == actorOwner) // friendly disallowed
                    continue;

                // Type filtering
                byte type = bm.GetPieceType(pid);
                bool isBuilding = bm.IsBuildingType != null && bm.IsBuildingType(type);
                bool isSoldier = !isBuilding;                  // assuming "not building" == soldier

                if (isBuilding && !allowBuildings) continue;
                if (isSoldier && !allowSoldiers) continue;

                // Range + LOS
                int dist = bm.Distance(originCell, c);
                if (dist < 1 || dist > rangeMax) continue;
                if (!bm.LineOfSightClear(originCell, c)) continue;

                // IMPORTANT:
                // Only treat this as a legal target if we can actually find a push destination.
                int pushDest = ComputePushDestination(bm, pcs, actorPieceId, pid, abilityId);
                if (pushDest < 0 || !bm.IsValidCellId(pushDest))
                    continue;

                if (count < cap)
                    outTargets[count] = pid;   // we return pieceIds as targets
                count++;
            }

            return count;
        }


        #endregion



        private (int minRange, int maxRange) getMINAndMaxRange(int abilityId)
        {
            int minRange = (abilityId >= 0 && abilityId < pcs.rangeMin.Length) ? pcs.rangeMin[abilityId] : 0;
            int maxRange = (abilityId >= 0 && abilityId < pcs.rangeMax.Length) ? pcs.rangeMax[abilityId] : 0;
            if (maxRange < minRange) { int t = maxRange; maxRange = minRange; minRange = t; }

            return (minRange, maxRange);
        }

        public int ComputePushDestination(
    BoardModel bm,
    Pieces pcs,
    int actorPieceId,
    int targetPieceId,
    int abilityId)
        {
            int actorCell = bm.GetPieceCell(actorPieceId);
            int targetCell = bm.GetPieceCell(targetPieceId);

            if (actorCell < 0 || targetCell < 0)
                return bm.InvalidId;

            if (abilityId < 0 ||
                pcs.push_PushAmount == null || pcs.push_pull == null ||
                abilityId >= pcs.push_PushAmount.Length || abilityId >= pcs.push_pull.Length)
                return bm.InvalidId;

            int pushAmount = pcs.push_PushAmount[abilityId];
            if (pushAmount <= 0)
                return bm.InvalidId;

            bool isPull = pcs.push_pull[abilityId];

            // Direction from actor -> target (where "behind" is continuing past target).
            int dir = isPull
                ? bm.GetDirectionIndex(targetCell, actorCell) // pull: reverse direction
                : bm.GetDirectionIndex(actorCell, targetCell);
            if (dir < 0)
                return bm.InvalidId;

            // For the "move toward owner core" fallback.
            int targetOwner = bm.GetPieceOwner(targetPieceId);
            int ownerCoreCell = bm.CoreCellIdForPlayer((byte)targetOwner);

            // Scratch buffers for ring and farthest cells.
            // Tune this if your board can have huge rings.
            const int MaxRingCells = 256;
            Span<int> ringCells = stackalloc int[MaxRingCells];
            Span<int> farCells = stackalloc int[MaxRingCells];

            // Try from max push distance down to 1
            for (int dist = pushAmount; dist > 0; dist--)
            {
                // 1) Get all EMPTY cells in the ring at exact distance = dist from target.
                int totalOnRing = bm.cellIdsRingAroundCell(
                    originCell: targetCell,
                    ringSize: dist,
                    requireEmpty: true,
                    outCells: ringCells);

                if (totalOnRing <= 0)
                    continue;

                int ringCount = Math.Min(totalOnRing, MaxRingCells);

                // 2) Find extreme distance from ACTOR among these ring cells.
                int extremeDistFromActor = isPull ? int.MaxValue : -1;
                for (int i = 0; i < ringCount; i++)
                {
                    int cell = ringCells[i];
                    int dAct = bm.Distance(actorCell, cell);
                    if (isPull)
                    {
                        if (dAct < extremeDistFromActor)
                            extremeDistFromActor = dAct;
                    }
                    else
                    {
                        if (dAct > extremeDistFromActor)
                            extremeDistFromActor = dAct;
                    }
                }
                if ((!isPull && extremeDistFromActor < 0) || (isPull && extremeDistFromActor == int.MaxValue))
                    continue;

                // 3) Collect only the ring cells that match the extreme distance from the actor.
                int farCount = 0;
                for (int i = 0; i < ringCount; i++)
                {
                    int cell = ringCells[i];
                    int dAct = bm.Distance(actorCell, cell);
                    if (dAct == extremeDistFromActor)
                        farCells[farCount++] = cell;
                }
                if (farCount == 0)
                    continue;

                // 4) Preferred "directly behind" cell: stepping from target along dir.
                int idealBehindCell = bm.StepInDirection(targetCell, dir, dist);

                // Check if idealBehindCell is one of the farCells and is a legal destination.
                if (idealBehindCell >= 0 && bm.IsValidCellId(idealBehindCell) && bm.IsEmpty(idealBehindCell))
                {
                    for (int i = 0; i < farCount; i++)
                    {
                        if (farCells[i] == idealBehindCell)
                            return idealBehindCell;
                    }
                }

                // 5) Fallback: among farCells, pick the one closest to the target owner's core.
                int bestCell = bm.InvalidId;
                int bestScore = int.MaxValue;

                for (int i = 0; i < farCount; i++)
                {
                    int cell = farCells[i];

                    // This check is technically redundant because we already asked for requireEmpty:true
                    // in EnumerateCellsAtExactDistance, but it's cheap and safe.
                    if (!bm.IsValidCellId(cell) || !bm.IsEmpty(cell))
                        continue;

                    int dCore = ownerCoreCell >= 0 ? bm.Distance(cell, ownerCoreCell) : 0;
                    if (dCore < bestScore)
                    {
                        bestScore = dCore;
                        bestCell = cell;
                    }
                }

                if (bestCell >= 0)
                    return bestCell;

                // Else: this ring has no usable destination; try a smaller ring.
            }

            // No legal destination found at any distance.
            return bm.InvalidId;
        }

        //Helper to call methods for applyActions easier

        private readonly HashSet<int> dublicateFilter = new HashSet<int>();
        public int ProtectedBySanctuary(Span<int> outPieceIds)
        {
            dublicateFilter.Clear();
            Span<int> protectedpieces = stackalloc int[240];
            int foundPieces = 0;

            for (int pid = 0; pid < bm.pieceCount; pid++)
            {
                byte type = bm.GetPieceType(pid);
                int sanctuaryRange = -1;

                // Find a Sanctuary ability on this type and grab its range
                int slotLimit = pcs.AbilitySlotCount(type);
                for (int s = 0; s < slotLimit; s++)
                {
                    int aid = pcs.AbilityIdAtSlot(type, s);
                    if (aid < 0 || aid >= pcs.sanctuary_enabled.Length) continue;
                    if (pcs.abilityKind[aid] != Pieces.AbilityKind.Sanctuary) continue;
                    if (!pcs.sanctuary_enabled[aid]) continue;
                    sanctuaryRange = (aid < pcs.Sanctuary_range.Length) ? pcs.Sanctuary_range[aid] : -1;
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
                        if (dublicateFilter.Add(protectedpieces[pp]))
                        {
                            outPieceIds[foundPieces] = protectedpieces[pp];
                            foundPieces++;
                        }
                    }
                }
            }
            return foundPieces;
        }

        public bool IsPieceProtectedBySanctuary(int pieceId)
        {
            Span<int> protectedpieces = stackalloc int[240];
            int numberOfProtectedPieces = ProtectedBySanctuary(protectedpieces);

            for (int i = 0; i < numberOfProtectedPieces; i++)
            {
                if (pieceId != protectedpieces[i]) continue;
                return true;
            }
            return false;
        }

        public bool IsPieceApartOfSpan(int PieceId, Span<int> inPieceIds, int spanLength)
        {
            for (int i = 0; i < spanLength; i++)
            {
                if (PieceId != inPieceIds[i]) continue;
                return true;
            }
            return false;
        }



        public int PushDestination(int actorPieceId, int targetPieceId, int abilityId)
        {
            return ComputePushDestination(bm, pcs, actorPieceId, targetPieceId, abilityId);
        }

        #region Action Stil Legal?

        public bool IsStillLegal(in Action a, byte p)
        {
            // EndTurn: always structurally legal
            if (a.kind == EndTurn) return true;

            // Plan-B: Create has no actor/slot/ability; structural rule only
            if (a.kind == Create)
            {
                // If pending multi-create placements are active, treat this as a placement
                if (multiCreateActive && a.pieceType == multiCreateType)
                {
                    if (bm.GetCellOccupant(a.dstCell) >= 0) return false;
                    if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0 &&
                        bm.GetPieceCountForPlayer(gamestate.currentPlayer) >= controller.PieceLimitPerPlayer)
                        return false;
                    if (multiCreateBorder)
                    {
                        bool adjacent = false;
                        var scratch = bm.GetScratchNeighborBuffer();
                        int n = bm.GetNeighbors(a.dstCell, scratch);
                        for (int i = 0; i < n; i++)
                        {
                            int nb = scratch[i];
                            if (multiCreateCells.Contains(nb)) { adjacent = true; break; }
                        }
                        if (!adjacent) return false;
                    }
                    else
                    {
                        if (!IsCreateGeometryLegal(a.dstCell, p))
                            return false;
                    }
                    return true;
                }

                // cell must be empty
                if (bm.GetCellOccupant(a.dstCell) >= 0) return false;

                // geometry: on core OR adjacent to core OR adjacent to any of your buildings
                bool geomOk = false;
                int core = bm.GetPlayerCoreCellId(p);
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
                        if (bm.GetPieceOwner(pid) != p) continue;
                        byte t = bm.GetPieceType(pid);
                        if (pcs.IsBuilding(t)) { geomOk = true; break; }
                    }
                }
                if (!geomOk) return false;

                // parity with OfferProvider: buildable flag + required digit gate
                if (!pcs.IsBuildable(a.pieceType)) return false;
                int req = pcs.GetRequiredDigit(a.pieceType);
                if (req >= 0 && !gamestate.ps[p].HasDigit(req)) return false;

                // Connector legality: config index is carried in aux
                if (pcs.HasConnectors(a.pieceType))
                {
                    int cfg = a.aux;
                    if (!pcs.IsConnectorConfigAllowed(a.pieceType, cfg)) return false;
                    if (!PiecesSides.IsConnectorPlacementLegal(bm, pcs, a.dstCell, a.pieceType, cfg, p))
                        return false;
                }
                return true;
            }

            // Non-Create actions (Move/Shoot/CaptureVP/CoreDamage) – old path:
            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return false;
            if (bm.GetPieceOwner(actorPid) != p) return false;

            byte type = bm.GetPieceType(actorPid);
            if (a.abilitySlot >= pcs.AbilitySlotCount(type)) return false;

            int abilityId = pcs.AbilityIdAtSlot(type, a.abilitySlot);
            if (abilityId < 0) return false;

            byte kind = pcs.GetAbilityKind(type, a.abilitySlot);
            if (kind != a.kind) return false; // slot-kind drift guard

            int[] targets = bm.GetScratchCellBuffer();
            int[] targetsPiece = bm.GetScratchCellBuffer();
            int count;
            switch (a.kind)
            {
                case Move:
                    count = pcs.GetLegalTargets_Move(bm, actorPid, abilityId, targets);
                    return ContainsFirstN(targets, count, a.dstCell);
                case Shoot:
                    count = pcs.GetLegalTargets_Shoot(bm, actorPid, abilityId, targets);
                    return ContainsFirstN(targets, count, a.aux /* targetPieceId */);
                case Push:
                    return IsLegal_Push(actorPid, abilityId, in a);
                case Launcher:
                    return IsLegal_Launcher(actorPid, abilityId, in a);
                case Spawner:
                    return IsLegal_Spawner(actorPid, abilityId, in a);
                case GroupBuild:
                    return IsLegal_GroupBuild(actorPid, abilityId, in a);
                case CaptureVP:
                    return pcs.IsLegal_CaptureVP(bm, actorPid, abilityId);
                case CoreDamage:
                    return pcs.IsLegal_CoreDamage(bm, actorPid, abilityId);
                case SacrificeFactory:
                    count = pcs.GetLegalTargets_SacrificeFactory(bm, actorPid, abilityId, targetsPiece);
                    return ContainsFirstN(targets, count, a.aux /* targetPieceId */);
                default:
                    return false;
            }
        }



        private bool IsLegal_Push(int actorPid, int abilityId, in Action a)
        {
            if (abilityId < 0 ||
                pcs.push_TargetsBuildings == null || pcs.push_TargetsSoldiers == null ||
                pcs.push_rangeMax == null || pcs.push_FriendlyFire == null ||
                pcs.push_PushAmount == null || pcs.push_pull == null)
                return false;

            int actorOwner = bm.GetPieceOwner(actorPid);
            int targetPid = a.aux;
            if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
            if (bm.GetPieceCell(targetPid) != a.dstCell) return false;

            bool allowFriendly = pcs.push_FriendlyFire[abilityId];
            if (!allowFriendly && bm.GetPieceOwner(targetPid) == actorOwner) return false;

            bool allowBuildings = pcs.push_TargetsBuildings[abilityId];
            bool allowSoldiers = pcs.push_TargetsSoldiers[abilityId];
            byte tgtType = bm.GetPieceType(targetPid);
            bool isBuilding = pcs.IsBuilding(tgtType);
            if (isBuilding && !allowBuildings) return false;
            if (!isBuilding && !allowSoldiers) return false;

            int rangeMax = pcs.push_rangeMax[abilityId];
            int originCell = bm.GetPieceCell(actorPid);
            int targetCell = bm.GetPieceCell(targetPid);
            int dist = bm.Distance(originCell, targetCell);
            if (dist < 1 || dist > rangeMax) return false;
            if (!bm.LineOfSightClear(originCell, targetCell)) return false;

            int pushDest = ComputePushDestination(bm, pcs, actorPid, targetPid, abilityId);
            return pushDest >= 0 && bm.IsValidCellId(pushDest);
        }

        private bool IsLegal_GroupBuild(int actorPid, int abilityId, in Action a)
        {
            byte actorType = bm.GetPieceType(actorPid);
            if (!pcs.groupBuildEnabled[actorType]) return false;
            int targetType = pcs.groupBuildTargetType[actorType];
            if (targetType < 0 || targetType >= pcs.typeCount) return false;

            // Create legality for target type at dstCell
            if (bm.GetCellOccupant(a.dstCell) >= 0) return false;
            if (!pcs.IsBuildable((byte)targetType)) return false;
            int reqDigit = pcs.GetRequiredDigit((byte)targetType);
            if (reqDigit >= 0 && !ps[gamestate.currentPlayer].HasDigit(reqDigit)) return false;

            // geometric create gate (core/building adjacency)
            if (!IsCreateGeometryLegal(a.dstCell, gamestate.currentPlayer))
                return false;

            // Cluster size check
            int clusterSize = CountClusterOfType(actorType, bm.GetPieceCell(actorPid));
            return clusterSize >= pcs.groupBuildRequireNumber[actorType];
        }

        private bool IsLegal_Launcher(int actorPid, int abilityId, in Action a)
        {
            if (abilityId < 0 ||
                pcs.launcher_inputRange == null || pcs.launcher_outputRange == null ||
                pcs.launcher_friendlyFire == null || pcs.launcher_enemyFire == null)
                return false;

            int inputRange = pcs.launcher_inputRange[abilityId];
            int outputRange = pcs.launcher_outputRange[abilityId];
            bool allowFriendly = pcs.launcher_friendlyFire[abilityId];
            bool allowEnemy = pcs.launcher_enemyFire[abilityId];

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

        private bool IsLegal_Spawner(int actorPid, int abilityId, in Action a)
        {
            if (abilityId < 0 ||
                pcs.spawn_pieceAmount == null || pcs.spawn_range == null || pcs.spawn_onlyOncePerTurn == null || pcs.spawn_targetType == null)
                return false;

            int targetType = pcs.spawn_targetType[abilityId];
            if (targetType < 0 || targetType >= pcs.typeCount) return false;
            int amount = pcs.spawn_pieceAmount[abilityId];
            int range = pcs.spawn_range[abilityId];
            bool once = pcs.spawn_onlyOncePerTurn[abilityId];

            if (once && spawnerUsedThisTurn.Contains(actorPid)) return false;

            int origin = bm.GetPieceCell(actorPid);
            if (origin < 0) return false;

            // Digit gate for target type
            int reqDigit = pcs.GetRequiredDigit((byte)targetType);
            if (reqDigit >= 0 && !ps[gamestate.currentPlayer].HasDigit(reqDigit)) return false;

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
                availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(gamestate.currentPlayer);

            int possible = Math.Min(amount, Math.Min(emptyCount, Math.Max(0, availableLimit)));
            return possible > 0;
        }
        private bool IsLegal_Upgrade(int actorPid, int abilityId, in Action a)
        {
            if (actorPid < 0) return false;
            byte actorType = bm.GetPieceType(actorPid);
            if (!pcs.upgradeEnabled[actorType]) return false;
            int targetType = pcs.upgradeTargetType[actorType];
            if (targetType < 0 || targetType >= pcs.typeCount) return false;
            if (a.dstCell != a.srcCell) return false;
            if (bm.GetPieceCell(actorPid) != a.srcCell) return false;
            if (!pcs.IsBuildable((byte)targetType)) return false;
            int reqDigit = pcs.GetRequiredDigit((byte)targetType);
            if (reqDigit >= 0 && !ps[gamestate.currentPlayer].HasDigit(reqDigit)) return false;
            return true;
        }

        #endregion
        #region Helpers for isLegal checks?

        private static bool ContainsFirstN(int[] xs, int count, int value)
        {
            int n = (xs != null) ? Math.Min(count, xs.Length) : 0;
            for (int i = 0; i < n; i++) if (xs[i] == value) return true;
            return false;
        }

        private int CountClusterOfType(byte type, int startCell)
        {
            if (startCell < 0) return 0;
            var visited = bm.GetScratchCellBuffer();
            Array.Clear(visited, 0, visited.Length);
            int[] queue = bm.GetScratchCellBuffer();
            int head = 0, tail = 0;
            queue[tail++] = startCell;
            visited[startCell] = 1;
            int count = 0;
            while (head < tail)
            {
                int cell = queue[head++];
                int pid = bm.GetCellOccupant(cell);
                if (pid >= 0 && bm.GetPieceType(pid) == type) count++;
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
            return count;
        }

        private bool IsCreateGeometryLegal(int cell, byte player)
        {
            if (!bm.IsEmpty(cell)) return false;
            int core = bm.GetPlayerCoreCellId(player);
            if (cell == core) return true;
            var scratch = bm.GetScratchCellBuffer();
            int n = bm.GetNeighbors(core, scratch);
            for (int i = 0; i < n; i++) if (scratch[i] == cell) return true;
            int n2 = bm.GetNeighbors(cell, scratch);
            for (int i = 0; i < n2; i++)
            {
                int nb = scratch[i];
                int pid = bm.GetCellOccupant(nb);
                if (pid < 0) continue;
                if (bm.GetPieceOwner(pid) != player) continue;
                byte t = bm.GetPieceType(pid);
                if (pcs.IsBuilding(t)) return true;
            }
            return false;
        }

        #endregion



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
