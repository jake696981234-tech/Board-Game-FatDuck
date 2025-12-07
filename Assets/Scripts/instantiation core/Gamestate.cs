using System;
using static Game.Core.ActionKind;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Core
{
    // Deterministic, allocation-free mutation entrypoint for Phase A.
    // Aligns with Pieces.AbilityKind (incl. CoreDamage), pricing-only CostEngine,
    // and read-only OfferProvider. All state mutations happen through here.
    public class GameState
    {
        #region Class's Refrences
        public event System.Action OnActionExecuted;

        private GameConfigHub hub;

        private BoardModel bm;
        private GameController controller;
        private EventManager events;


        #endregion


        #region Match state values
        public PlayerState[] ps;      // length 4
        public byte currentPlayer;    // 0..3
        private int roundsLeft;
        private bool isGameOver;
        private byte winner; // 0..3; 255 = none/draw
        public int currentRoundNumber = 1;


        private int currentCenterVP;             // was BM.currentCenterCellVictoryPointAmount
        private int[] currentCoreHealthByPlayer;   // was BM.currentCoreHealthByPlayer

        //Apply Methods Values- taken from game Actions

        public bool multiCreateActive;
        public byte multiCreateType;
        public bool multiCreateBorder;
        public int multiCreateRemaining;
        public List<int> multiCreateCells = new List<int>(8);

        public int MultiCreateCellCount => multiCreateCells.Count;

        public readonly HashSet<int> dublicateFilter = new HashSet<int>();

        public void ResetMultiCreate()
        {
            multiCreateActive = false;
            multiCreateType = 0;
            multiCreateBorder = false;
            multiCreateRemaining = 0;
            multiCreateCells.Clear();
        }


        #endregion
        #region Initialize Method

        private int gameIndex;
        public void Initialize(
                       BoardModel board,
                       PlayerState[] players,
                       byte startingPlayer, EventManager eventManager, GameController gameController, int theGameIndex)
        {
            this.hub = GameBootstrapper.hub;
            bm = board;
            events = eventManager;
            controller = gameController;
            gameIndex = theGameIndex;

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
                ps[i].budget = hub.match_startingBudgetPerRound[currentRoundNumber - 1];
            }


            if (controller.twoPlayerHurdle)
            {
                currentCoreHealthByPlayer[3] = 0;
                currentCoreHealthByPlayer[2] = 0;
            }

            // Start first player's turn
            BeginTurn();
        }

        #endregion
        #region The Action method
        public bool Perform(in Action a)
        {
            ref var cur = ref ps[currentPlayer];

            if (!FastCheck(a)) return false;
            if (!IsItLegal.IsStillLegal(in a, currentPlayer, gameIndex)) return false;
            events.actionBegin(new ActionContext { ThePlayer = currentPlayer });

            CostEngine.CostBreakdown quote = default;
            bool isMultiPlacement = multiCreateActive && a.kind == Create && a.pieceType == multiCreateType;

            if (a.kind != EndTurn && !isMultiPlacement)
            {
                if (!CostEngine.IsAffordable(in cur, in a, out quote, gameIndex))
                    return false;
            }
            else
            {
                quote = default;
            }


            // logging stuff
            int loggedplayer = currentPlayer;
            int loggedType = a.kind;
            int? pieceTypeForLog = null;

            if (a.kind == Create)
            {
                pieceTypeForLog = a.pieceType;           // which piece we're creating
            }
            else if (a.kind != EndTurn)
            {
                int actorPid = bm.GetCellOccupant(a.srcCell);
                if (actorPid >= 0)
                    pieceTypeForLog = bm.GetPieceType(actorPid);
            }

            // Special-case EndTurn: log it against the current turn before handoff
            if (a.kind == EndTurn)
            {
                events.RaiseActionLog(new EventManager.ActionLogEvent(
                    loggedType,
                    null,               // no piece for EndTurn
                    null,               // no target for EndTurn
                    loggedplayer,
                    0m,                 // actionCost
                    null,               // buildCost
                    null                // surchargeCost
                ));

                ApplyEndTurn();
                OnActionExecuted?.Invoke();
                return true;
            }

            // Geometry-free "before" snapshot     
            var beforeCounts = SnapshotOwnerTypeCounts(bm);
            var coreBefore = SnapshotCoreHP(this);


            switch (a.kind)
            {
                case Move: GameActions.ApplyMove(in a, currentPlayer, gameIndex); break;
                case Shoot: GameActions.ApplyShoot(in a, currentPlayer, gameIndex); break;
                case Create:
                    if (multiCreateActive && a.pieceType == multiCreateType)
                    {
                        GameActions.ApplyMultiCreatePlacement(in a, currentPlayer, gameIndex);
                    }
                    else
                    {
                        if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0 &&
                            bm.GetPieceCountForPlayer(currentPlayer) >= controller.PieceLimitPerPlayer)
                            return false;
                        GameActions.ApplyCreate(in a, currentPlayer, gameIndex);
                    }
                    break;
                case Push: GameActions.ApplyPush(in a, currentPlayer, gameIndex); break;
                case Upgrade: GameActions.ApplyUpgrade(in a, currentPlayer, gameIndex); break;
                case Launcher: GameActions.ApplyLauncher(in a, currentPlayer, gameIndex); break;
                case Spawner: GameActions.ApplySpawner(in a, currentPlayer, gameIndex); break;
                case GroupBuild: GameActions.ApplyGroupBuild(in a, currentPlayer, gameIndex); break;
                case CaptureVP: GameActions.ApplyCaptureVP(in a, currentPlayer, gameIndex); break; // sets flags + VP counters + vpPool
                case CoreDamage: GameActions.ApplyCoreDamage(in a, currentPlayer, gameIndex); break; // sets flag + damages enemy core + elim check
                case EndTurn: ApplyEndTurn(); break; // unreachable due to early return above
                case SacrificeFactory: GameActions.ApplySacrificeFactory(in a, currentPlayer, gameIndex); break;
                case ConversionFactory: GameActions.ApplyConversionFactory(in a, currentPlayer, gameIndex); break;
                default: return false;
            }

            // For non-EndTurn actions, apply costs and advance index
            bool skipCost = multiCreateActive && a.kind == Create && a.pieceType == multiCreateType;
            if (!skipCost)
            {
                cur.AddBudget(-(float)quote.Total);
                cur.AdvanceActionIndex();
            }


            // Map DB fields so that actionCost == growth-based turn fee (from actionGrowthFactor)
            decimal actionCost = (decimal)quote.TurnFee;
            decimal? buildCost = (decimal)quote.BuildCost;
            decimal? surchargeCost = (decimal)quote.AbilityCost;



            // Geometry-free "after" snapshot and deltas
            var afterCounts = SnapshotOwnerTypeCounts(bm);
            var coreAfter = SnapshotCoreHP(this);
            var losses = new Dictionary<(int owner, int type), int>(afterCounts.Count);
            foreach (var kv in beforeCounts)
            {
                int after = afterCounts.TryGetValue(kv.Key, out var c) ? c : 0;
                int lost = kv.Value - after;
                if (lost > 0) losses[kv.Key] = lost;
            }
            int? targetPlayerForLog = PickTargetPlayer(losses, coreBefore, coreAfter);



            events.RaiseActionLog(new EventManager.ActionLogEvent(
                loggedType,
                pieceTypeForLog,
                targetPlayerForLog,
                loggedplayer,
                actionCost,
                buildCost,
                surchargeCost
            ));


            // Tell listeners (Bootstrapper/View) to refresh visuals
            OnActionExecuted?.Invoke();
            return true;

        }



        private bool FastCheck(in Action a)
        {
            // Allow all known action kinds up to SacrificeFactory; reject only undefined kinds.
            if (a.kind > ConversionFactory) return false;
            if (currentPlayer >= 4) return false;
            return true;
        }

        #endregion
        #region Game Loop

        private void BeginTurn()
        {
            events.turnBegin(new TurnContext { ThePlayer = currentPlayer });
            ResetMultiCreate();
            ps[currentPlayer].BeginTurnReset();


            turnOrdinal++;

            if (turnOrdinal == 1 && currentRoundNumber != 1)
            {
                currentRoundNumber++;
            }

            tStart[currentPlayer].digitsStart = CountDigits(currentPlayer);
            tStart[currentPlayer].piecesStart = CountPiecesOnBoard(currentPlayer);

            if (ps[currentPlayer].applyStartOfTurnBudgetDecrease)
                ps[currentPlayer].AddBudget(-(float)hub.match_startOfTurnBudgetDecrease);

            // Refresh connector capital HP/state at start of turn
            PiecesSides.RecomputeConnectorComponents(gameIndex);

        }

        private void ApplyEndTurn()
        {
            // Player who just ended
            byte ended = currentPlayer;
            ResetMultiCreate();


            // Mark whether they ended without acting this turn
            ps[ended].endedWithoutActionThisCycle = ps[ended].actionIndexThisTurn == 0;
            bool isPassOnly = ps[ended].endedWithoutActionThisCycle;

            int coreEnd = GetCoreHealth(ended);
            int vpEnd = ps[ended].vpTotal;
            decimal budgetEnd = (decimal)ps[ended].budget;

            int digitsStart = tStart[ended].digitsStart;
            int piecesStart = tStart[ended].piecesStart;

            int digitsEnd = CountDigits(ended);
            int piecesEnd = CountPiecesOnBoard(ended);


            incrementPlayerTurnOrdinal();
            events.RaiseTurnLog(new EventManager.TurnLogEvent(
                ended,
                turnOrdinal,
                isPassOnly,
                coreEnd,
                vpEnd,
                budgetEnd,
                digitsStart,
                digitsEnd,
                piecesStart,
                piecesEnd,
                getPlayerTurnOrdinal()));


            // Advance to next alive player and/or player that has not passed there turn
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

        private void EndRound()
        {
            events.roundBegin();
            turnOrdinal = 0;
            Array.Clear(playerTurnOrdinals, 0, playerTurnOrdinals.Length);
            currentRoundNumber = Math.Max(1, currentRoundNumber); // ensure non-zero for next cycle

            // 1) Purge temporary units (soldiers), keep buildings
            GameStateUtilities.RemoveAllSoldiers(gameIndex);

            // 2) Refill center VP pool
            ResetCenterVictoryPointsToStart();

            // 3) Decrement rounds, clear counters and per-cycle flags
            roundsLeft = Math.Max(0, roundsLeft - 1);

            events.RaiseRoundLog();

            for (int i = 0; i < 4; i++)
            {
                // payout uses each player's own round stats, not currentPlayer's
                float payout = ComputePlayerPayOut(i);

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



        #endregion
        #region Game Loop Helpers

        private float[] perPlayerFactoryIncome = new float[4]; // allocated once
        public float ComputePlayerPayOut(int playerId)
        {
            perPlayerFactoryIncome = GameActions.ComputePlayersFactoryIncome(gameIndex);
            float payout =
                    ComputePlayerVPReward(playerId) +
                    ComputePlayeroreDamageReward(playerId) +
                    hub.match_startingBudgetPerRound[currentRoundNumber - 1] +
                    perPlayerFactoryIncome[playerId];

            return payout;
        }

        public float ComputePlayerVPReward(int playerId)
        {
            return ps[playerId].vpGainedThisRound * hub.reward_budgetBonusForVP;
        }

        public float ComputePlayeroreDamageReward(int playerId)
        {
            return ps[playerId].coreHitsThisRound * hub.reward_budgetBonusForCoreDamage;
        }

        private byte NextAlivePlayerAfter(byte p)
        {
            for (int k = 1; k <= 4; k++)
            {
                byte q = (byte)((p + k) & 3);
                if (!ps[q].isEliminated && !ps[q].endedWithoutActionThisCycle) return q;
            }
            return p;
        }


        public void TryEliminatePlayer(byte p)
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
            // CenterVPIsZero         // Rule D

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

            if (currentCenterVP == 0) return true;
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
                events.gameEnd();
                winner = lastAlive;
                // Winner by elimination
                events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.Elimination, winner));
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
            events.gameEnd();
            winner = tie ? (byte)255 : win; // 255 = draw/no single winner
            Debug.Log($"Game over by VP. Winner: {winner} (tie={tie})");
            events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.Elimination, winner));
            if (tie)
            {
                winner = 255; // draw/no single winner
                Debug.Log("Game over by VP. Result: TIE");
                events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.Tie, null));
            }
            else
            {
                winner = win;
                Debug.Log($"Game over by VP. Winner: {winner}");
                events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.EndOfTurnVictoryPoints, winner));
            }
        }

        #endregion
        #region Accessors
        // --- Current player read-only accessors ---
        public byte CurrentPlayerId => currentPlayer;
        public float CurrentBudget => ps[currentPlayer].budget;
        public byte CurrentActionIndex => ps[currentPlayer].actionIndexThisTurn;
        public bool CurrentDidCaptureVP => ps[currentPlayer].didCaptureVP;
        public bool CurrentDidCoreDamage => ps[currentPlayer].didCoreDamage;
        public ref readonly PlayerState CurrentPlayerRef => ref ps[currentPlayer];

        public bool PassedTurn(int playerID)
        => ps[playerID].endedWithoutActionThisCycle;


        // --- simple Accesors ---
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

        // --- Minimal read-only surface for agents/UI ---
        public int RoundsLeft => roundsLeft;
        public bool IsGameOver => isGameOver;   // requires fields existing in your GameState
        public byte Winner => winner;       // 0..3 or 255 for draw/no winner

        public int GetCenterVP() => currentCenterVP;

        public int GetCoreHealth(byte player) => currentCoreHealthByPlayer[player];

        public int GetVP(byte player) => ps[player].vpTotal;

        public float GetBudget(byte player) => ps[player].budget;


        public bool PieceLimitEnabled => controller.PieceLimitEnabled;

        public int pieceLimitPerPlayer => controller.PieceLimitPerPlayer;


        #endregion

        #region SQL Logging and UI

        // ------------------------ Counters for SQL logging ------------------------
        //These counters are for SQL logging- remove this line if you want to use them gamelogic.
        private int turnOrdinal = 0;

        // Store all player ordinals in one array
        private int[] playerTurnOrdinals = new int[4];  // automatically initialized to 0

        private int getPlayerTurnOrdinal()
        {
            if (currentPlayer >= 0 && currentPlayer < playerTurnOrdinals.Length)
                return playerTurnOrdinals[currentPlayer];

            Debug.LogError("PlayerTurnOrdinal out of range");
            return -1;
        }

        private void incrementPlayerTurnOrdinal()
        {
            if (currentPlayer >= 0 && currentPlayer < playerTurnOrdinals.Length)
                playerTurnOrdinals[currentPlayer]++;
            else
                Debug.LogError("PlayerTurnOrdinal increment out of range");
        }


        private struct TurnStartSnap
        {
            public int digitsStart;
            public int piecesStart;
        }
        private TurnStartSnap[] tStart = new TurnStartSnap[4];
        private int CountDigits(byte p)
        {
            var drc = ps[p].digitRefCount;
            if (drc == null) return 0;
            int total = 0;
            for (int i = 0; i < PlayerState.MAX_DIGITS; i++)
                if (drc[i] > 0) total++;
            return total;
        }

        private int CountPiecesOnBoard(byte p)
        {
            int count = 0;
            for (int pid = 0; pid < bm.pieceCount; pid++)
                if (bm.pieceOwner[pid] == p)
                    count++;
            return count;
        }


        // ---- Geometry-free snapshots for analytics/logging ----
        public static Dictionary<(int owner, int type), int> SnapshotOwnerTypeCounts(BoardModel bm)
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


        #endregion
    }
}
