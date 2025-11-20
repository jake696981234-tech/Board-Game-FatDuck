using System;
using static Game.Core.ActionKind;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Core
{

    // Deterministic, allocation-free mutation entrypoint for Phase A.
    // Aligns with Pieces.AbilityKind (incl. CoreDamage), pricing-only CostEngine,
    // and read-only OfferProvider. All state mutations happen here.
    public sealed partial class GameState : IAgentGameState
    {
        // === Phase B: notify views when the world actually changed ===

        private GameConfigHub hub;

        private int currentCenterVP;             // was BM.currentCenterCellVictoryPointAmount
        private int[] currentCoreHealthByPlayer;   // was BM.currentCoreHealthByPlayer

        // simple accessors (optional)


        public void SetCoreHealth(byte player, int hp) // clamps by hub caps
        {
            if (hp < 0) hp = 0;
            if (hp > hub.cap_maxCoreHealth) hp = hub.cap_maxCoreHealth;
            currentCoreHealthByPlayer[player] = hp;
        }

        public void AddCenterVictoryPoints(int delta)
        {
            int v = currentCenterVP + delta;
            if (v < 0) v = 0;
            if (v > hub.cap_maxVPPool) v = hub.cap_maxVPPool;
            currentCenterVP = v;
        }

        public void ResetCenterVictoryPointsToStart() => currentCenterVP = hub.match_startCenterVP;

        public void ResetAllCoreHealthToStart()
        {
            for (byte p = 0; p < currentCoreHealthByPlayer.Length; p++)
                currentCoreHealthByPlayer[p] = hub.match_startCoreHp;
        }





        // --- Match result (Phase A) ---
        private bool isGameOver;
        private byte winner; // 0..3; 255 = none/draw

        // Injected
        private BoardModel bm;
        private Pieces pcs;
        private CostEngine cost;

        private GameController gameController;

        // Match state
        private PlayerState[] ps;      // length 4
        private byte currentPlayer;    // 0..3
        private int roundsLeft;

        /// start

        public void Initialize(in GameConfigHub hub,
                       BoardModel board,
                       Pieces pieces,
                       CostEngine pricing,
                       PlayerState[] players,
                       byte startingPlayer)
        {
            this.hub = hub;
            bm = board;
            pcs = pieces;
            cost = pricing;

            ps = players;
            currentPlayer = startingPlayer;

            roundsLeft = hub.match_numberOfRounds;     // from config (not a raw int param)

            isGameOver = false;
            winner = 255;

            // --- NEW: seed live counters here (BM no longer owns these) ---
            currentCoreHealthByPlayer = new int[4];
            ResetAllCoreHealthToStart();
            ResetCenterVictoryPointsToStart();

            // Freshen per-player state (round + turn)
            for (int i = 0; i < 4; i++)
            {
                ps[i].ClearRoundCounters();
                ps[i].BeginTurnReset();
                ps[i].endedWithoutActionThisCycle = false;
                ps[i].budget = hub.match_startingBudgetPerPlayer;
            }

            if (gameController.twoPlayerHurdle)
            {
                currentCoreHealthByPlayer[3] = 0;
                currentCoreHealthByPlayer[2] = 0;
            }

            // Start first player's turn
            BeginTurn();
        }

        public bool Perform(in Action a)
        {
            ref var cur = ref ps[currentPlayer];

            if (!FastCheck(a)) return false;
            if (!IsStillLegal(in a, currentPlayer)) return false;

            CostEngine.CostBreakdown quote = default;
            if (a.kind != EndTurn)
            {
                if (!cost.IsAffordable(cur, a, bm, pcs, out quote))
                    return false;
            }


            // Special-case EndTurn: log it against the current turn before handoff
            if (a.kind == EndTurn)
            {
                ApplyEndTurn();
                return true;
            }


            switch (a.kind)
            {
                case Move: ApplyMove(in a, currentPlayer); break;
                case Shoot: ApplyShoot(in a, currentPlayer); break;
                case Create:
                    if (gameController.PieceLimitEnabled && gameController.PieceLimitPerPlayer > 0 &&
                        bm.GetPieceCountForPlayer(currentPlayer) >= gameController.PieceLimitPerPlayer)
                        return false;
                    ApplyCreate(in a, currentPlayer);
                    break;
                case CaptureVP: ApplyCaptureVP(in a, currentPlayer); break; // sets flags + VP counters + vpPool
                case CoreDamage: ApplyCoreDamage(in a, currentPlayer); break; // sets flag + damages enemy core + elim check
                case EndTurn: ApplyEndTurn(); break; // unreachable due to early return above
                default: return false;
            }

            // For non-EndTurn actions, apply costs and advance index
            cur.AddBudget(-(float)quote.Total);
            cur.AdvanceActionIndex();


            // Map DB fields so that actionCost == growth-based turn fee (from actionGrowthFactor)
            decimal actionCost = (decimal)quote.TurnFee;
            decimal? buildCost = (decimal)quote.BuildCost;
            decimal? surchargeCost = (decimal)quote.AbilityCost;

            return true;
        }

        // --- Current player read-only accessors (no duplication) ---
        public byte CurrentPlayerId => currentPlayer;
        public float CurrentBudget => ps[currentPlayer].budget;
        public byte CurrentActionIndex => ps[currentPlayer].actionIndexThisTurn;
        public bool CurrentDidCaptureVP => ps[currentPlayer].didCaptureVP;
        public bool CurrentDidCoreDamage => ps[currentPlayer].didCoreDamage;
        public ref readonly PlayerState CurrentPlayerRef => ref ps[currentPlayer];
        public bool CanCurrentAfford(in Action a, out float quoted)
            => cost.IsAffordable(ps[currentPlayer], a, bm, pcs, out quoted);

        // --- Minimal read-only surface for agents/UI ---
        public int RoundsLeft => roundsLeft;
        public bool IsGameOver => isGameOver;   // requires fields existing in your GameState
        public byte Winner => winner;       // 0..3 or 255 for draw/no winner

        public int GetCenterVP() => currentCenterVP;

        public int GetCoreHealth(byte player) => currentCoreHealthByPlayer[player];

        public int GetVP(byte player) => ps[player].vpTotal;

        public float GetBudget(byte player) => ps[player].budget;


        

        // ---- Geometry-free snapshots for analytics/logging ----
        private static Dictionary<(int owner, int type), int> SnapshotOwnerTypeCounts(BoardModel bm)
        {
            var map = new Dictionary<(int, int), int>(32);
            for (int pid = 0; pid < bm.pieceCount; pid++)
            {
                int owner = bm.pieceOwner[pid];
                int type = bm.pieceType[pid];
                var key = (owner, type);
                map.TryGetValue(key, out var c);
                map[key] = c + 1;
            }
            return map;
        }

        private static int[] SnapshotCoreHP(GameState gs)
        {
            var hp = new int[4];
            for (byte p = 0; p < 4; p++) hp[p] = gs.GetCoreHealth(p);
            return hp;
        }

        private static int? PickTargetPlayer(Dictionary<(int owner, int type), int> losses, int[] coreBefore, int[] coreAfter)
        {
            // Prefer any player whose core HP dropped
            if (coreBefore != null && coreAfter != null)
            {
                int n = System.Math.Min(coreBefore.Length, coreAfter.Length);
                for (int p = 0; p < n; p++) if (coreAfter[p] < coreBefore[p]) return p;
            }
            // Else owner with max piece losses
            int bestOwner = -1, bestLoss = 0;
            if (losses != null)
            {
                foreach (var kv in losses)
                    if (kv.Value > bestLoss) { bestLoss = kv.Value; bestOwner = kv.Key.owner; }
            }
            return (bestLoss > 0) ? bestOwner : (int?)null;
        }

        // ------------------------ Legality recheck ------------------------
        private bool IsStillLegal(in Action a, byte p)
        {
            // EndTurn: always structurally legal
            if (a.kind == EndTurn) return true;

            // Plan-B: Create has no actor/slot/ability; structural rule only
            if (a.kind == Create)
            {
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
                if (req >= 0 && !ps[p].HasDigit(req)) return false;
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
            int count;
            switch (a.kind)
            {
                case Move:
                    count = pcs.GetLegalTargets_Move(bm, actorPid, abilityId, targets);
                    return ContainsFirstN(targets, count, a.dstCell);
                case Shoot:
                    count = pcs.GetLegalTargets_Shoot(bm, actorPid, abilityId, targets);
                    return ContainsFirstN(targets, count, a.aux /* targetPieceId */);
                case CaptureVP:
                    return pcs.IsLegal_CaptureVP(bm, actorPid, abilityId);
                case CoreDamage:
                    return pcs.IsLegal_CoreDamage(bm, actorPid, abilityId);
                default:
                    return false;
            }
        }


        private static bool ContainsFirstN(int[] xs, int count, int value)
        {
            int n = (xs != null) ? Math.Min(count, xs.Length) : 0;
            for (int i = 0; i < n; i++) if (xs[i] == value) return true;
            return false;
        }


        private bool FastCheck(in Action a)
        {
            if (a.kind > EndTurn) return false;
            if (currentPlayer >= 4) return false;
            return true;
        }

        // ----------------------------- Reducers -----------------------------
        private void ApplyMove(in Action a, byte p)
        {
            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return;
            int dstOcc = bm.GetCellOccupant(a.dstCell);
            if (dstOcc >= 0)
            {
                ResolveMelee(actorPid, dstOcc, in a);
            }
            else
            {
                bm.MovePieceRow(actorPid, a.dstCell);
            }

        }

        private void ApplyShoot(in Action a, byte p)
        {
            int targetPid = a.aux;
            if (targetPid < 0) return;
            short dmg = GetAbilityDamage(in a);
            bool killed = bm.DamagePieceRow(targetPid, dmg);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                int deadOwner = bm.GetPieceOwner(targetPid);
                byte deadType = bm.GetPieceType(targetPid);
                int g = pcs.GrantsDigit(deadType);
                if (g >= 0) ps[deadOwner].RevokeDigit(g);
                bm.FreeRowSwapBack(targetPid);
            }
        }

        private void ApplyCreate(in Action a, byte p)
        {
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, p, (byte)a.pieceType, a.dstCell, pcs.maxHPByType[a.pieceType]);
            // Grant digit if this type provides one
            int g = pcs.GrantsDigit((byte)a.pieceType);
            if (g >= 0) ps[p].GrantDigit(g);

        }

        private void ApplyCaptureVP(in Action a, byte p)
        {
            ps[p].OnCaptureVP();
            AddCenterVictoryPoints(-1);   // pool now lives in GameState


        }

        private void ApplyCoreDamage(in Action a, byte p)
        {
            ps[p].OnCoreDamage();



            int actorPid = bm.GetCellOccupant(a.srcCell);
            if (actorPid < 0) return;

            int actorCell = bm.GetPieceCell(actorPid);
            byte enemy = bm.OwnerOfCoreCell(actorCell);
            if (enemy >= 4) return;

            byte typ = bm.GetPieceType(actorPid);
            int abi = pcs.AbilityIdAtSlot(typ, a.abilitySlot);
            short dmg = (short)((abi >= 0 && abi < pcs.damage.Length) ? pcs.damage[abi] : 0);

            int hp = GetCoreHealth(enemy);
            SetCoreHealth(enemy, hp - dmg);
            TryEliminatePlayer(enemy);

        }

        private void ApplyEndTurn()
        {
            // Player who just ended
            byte ended = currentPlayer;

            // Mark whether they ended without acting this turn
            ps[ended].endedWithoutActionThisCycle = ps[ended].actionIndexThisTurn == 0;
            bool isPassOnly = ps[ended].endedWithoutActionThisCycle;

            int coreEnd = GetCoreHealth(ended);
            int vpEnd = ps[ended].vpTotal;
            decimal budgetEnd = (decimal)ps[ended].budget;

            // Advance to next alive player
            currentPlayer = NextAlivePlayerAfter(ended);

            // Evaluate round-end rules AFTER the handoff
            bool endRound = ShouldEndRoundAfterEndTurn(ended);
            if (endRound)
            {
                EndRound();
            }
            else
            {
                BeginTurn();
            }

        }


        // --------------------------- Reducer helpers ---------------------------
        private void ResolveMelee(int actorPid, int defenderPid, in Action a)
        {
            short dmg = GetAbilityDamage(in a);
            bool killed = bm.DamagePieceRow(defenderPid, dmg);
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
        }


        private short GetAbilityDamage(in Action a)
        {
            int pid = bm.GetCellOccupant(a.srcCell);
            byte typ = bm.GetPieceType(pid);
            int abi = pcs.AbilityIdAtSlot(typ, a.abilitySlot);
            return (short)((abi >= 0 && abi < pcs.damage.Length) ? pcs.damage[abi] : 0);
        }


        private byte NextAlivePlayerAfter(byte p)
        {
            for (int k = 1; k <= 4; k++)
            {
                byte q = (byte)((p + k) & 3);
                if (!ps[q].isEliminated) return q;
            }
            return p;
        }


        private void TryEliminatePlayer(byte p)
        {
            if (p >= 4) return;
            if (GetCoreHealth(p) <= 0 /* && !bm.PlayerHasAnyBuilding(p) */)
                ps[p].isEliminated = true;
        }


        /// <summary>
        /// Evaluated immediately after a player presses EndTurn (after turn handoff).
        /// Implements three round-end rules:
        /// A) All alive budgets are zero.
        /// B) All alive players ended a turn without acting.
        /// C) Every other alive player is "round-complete" (budget zero OR ended without acting).
        /// </summary>
        private bool ShouldEndRoundAfterEndTurn(byte endedPlayer)
        {
            bool anyAlive = false;

            bool allBudgetsZero = true;         // Rule A
            bool allEndedWithoutAction = true;  // Rule B
            bool othersRoundComplete = true;    // Rule C

            for (byte p = 0; p < 4; p++)
            {
                if (ps[p].isEliminated) continue;
                anyAlive = true;

                bool budgetZero = ps[p].budget <= 0f;
                bool endedNoAct = ps[p].endedWithoutActionThisCycle;

                if (!budgetZero) allBudgetsZero = false;
                if (!endedNoAct) allEndedWithoutAction = false;

                if (p != endedPlayer)
                {
                    bool roundComplete = budgetZero || endedNoAct;
                    if (!roundComplete) othersRoundComplete = false;
                }
            }

            if (!anyAlive) return false; // degenerate; don't loop forever

            return allBudgetsZero || allEndedWithoutAction || othersRoundComplete;
        }


        private void FinalWinCheckByVP()
        {
            // Last-standing shortcut
            int aliveCount = 0;
            byte lastAlive = 255;
            for (byte p = 0; p < 4; p++)
            {
                if (!ps[p].isEliminated) { aliveCount++; lastAlive = p; }
            }
            if (aliveCount == 1)
            {
                isGameOver = true;
                winner = lastAlive;
                // Winner by elimination
                return;
            }

            // If rounds remain, no VP decision yet
            if (roundsLeft > 0) return;

            // End-of-match VP decision
            int best = int.MinValue;
            byte win = 255;
            bool tie = false;

            for (byte p = 0; p < 4; p++)
            {
                if (ps[p].isEliminated) continue;
                int v = ps[p].vpTotal;
                if (v > best) { best = v; win = p; tie = false; }
                else if (v == best) { tie = true; }
            }

            isGameOver = true;
            winner = tie ? (byte)255 : win; // 255 = draw/no single winner
            if (tie)
            {
                winner = 255; // draw/no single winner
            }
            else
            {
                winner = win;
            }
        }



        private void BeginTurn()
        {
            ps[currentPlayer].BeginTurnReset();


            if (ps[currentPlayer].applyStartOfTurnBudgetDecrease)
                ps[currentPlayer].AddBudget(-(float)hub.match_startOfTurnBudgetDecrease);

        }


        private void EndRound()
        {
            // 1) Purge temporary units (soldiers), keep buildings
            GameStateUtilities.RemoveAllSoldiers(bm, pcs);

            // 2) Refill center VP pool
            ResetCenterVictoryPointsToStart();

            // 3) Decrement rounds, clear counters and per-cycle flags
            roundsLeft = Math.Max(0, roundsLeft - 1);

            for (int i = 0; i < 4; i++)
            {
                // payout uses each player's own round stats, not currentPlayer's
                float payout =
                    ps[i].vpGainedThisRound * hub.reward_budgetBonusForVP +
                    ps[i].coreHitsThisRound * hub.reward_budgetBonusForCoreDamage +
                    hub.match_startingBudgetPerPlayer;

                ps[i].budget = payout;   // clamp if you wish via hub.cap_maxBudget
                ps[i].ClearRoundCounters();
                ps[i].endedWithoutActionThisCycle = false;
            }

            // 4) Victory check
            FinalWinCheckByVP();

            // 5) If game continues, begin next round on the already-selected currentPlayer
            if (!isGameOver)
                BeginTurn();
        }

    }
}
