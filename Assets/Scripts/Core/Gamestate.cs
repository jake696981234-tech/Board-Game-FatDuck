using System;
using static Game.Core.ActionKind;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Deterministic, allocation-free mutation entrypoint for Phase A.
    // Aligns with Pieces.AbilityKind (incl. CoreDamage), pricing-only CostEngine,
    // and read-only OfferProvider. All state mutations happen through here.


    //general to do list:
    //
    //1. Organise Board Model
    //2. If a player has 0 budget, and their payout is zero, they should be eliminated.
    public class GameState
    {
        public void TickPlayer() // to do
        {
            playerManager.tickPlayerIndex(currentPlayer, this);
        }


        #region The Action method

        public bool Perform(in Action theAction, Action[] offers)
        {
            bool success = ActuallyPerform(theAction, offers);
            TickPlayer();
            return success;
        }


        private bool ActuallyPerform(in Action theAction, Action[] offers)
        {
            ref var cur = ref ps[currentPlayer];

            // if (!FastCheck(theAction))
            // {
            //     Debug.Log("FastCheck returned false");
            //     return false;
            // }
            // if (!IsItLegal.IsStillLegal(in theAction, currentPlayer, gameIndex))
            // {
            //     Debug.Log("Is Still Legal returned false");
            //     return false;
            // }
            if (!IsStillLegal(offers, theAction))
            {
                Debug.Log("Is Still Legal returned false");
                return false;
            }
            events.actionBegin(new ActionContext { ThePlayer = currentPlayer });
            CostEngine.CostBreakdown quote = default;
            // bool isMultiPlacement = multiCreateActive && theAction.kind == Create && theAction.pieceType == multiCreateType;

            if (!CostEngine.IsAffordable(in cur, in theAction, out quote, gameIndex) && theAction.kind != EndTurn)
            {
                Debug.Log("CostEngine Is Affordable Returned False");
                return false;
            }
            else
            {
                quote = default;
            }

            if (LogEnabled) DbLog.PreLogPerform(currentPlayer, theAction, gameIndex);

            switch (theAction.kind)
            {
                case Move: MoveAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Shoot: ShootAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Create:
                    if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0 && bm.GetPieceCountForPlayer(currentPlayer) >= controller.PieceLimitPerPlayer)
                    {
                        Debug.Log("Piece Limit returned false");
                        return false;
                    }
                    CreateAction.Apply(in theAction, currentPlayer, gameIndex);
                    break;
                case Push: PushAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Upgrade: UpgradeAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Launcher: LauncherAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Spawner: SpawnAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case GroupBuild: GroupBuildAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case CaptureVP: CaptureVPAction.Apply(in theAction, currentPlayer, gameIndex); break; // sets flags + VP counters + vpPool
                case CoreDamage: CoreDamageAction.Apply(in theAction, currentPlayer, gameIndex); break; // sets flag + damages enemy core + elim check
                case EndTurn: ApplyEndTurn(); break; // unreachable due to early return above
                case SacrificeFactory: SacrificeFactoryAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case ConversionFactory: ConversionFactoryAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Explosive: ExplosiveAction.Apply(in theAction, currentPlayer, gameIndex); break;
                // case PieceBuild: PieceBuildAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Sniper: SniperAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case NecroSpawn: NecroSpawnAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case WorkYard: WorkYardAction.Apply(in theAction, currentPlayer, gameIndex); break;
                case Hop: HopAction.Apply(in theAction, currentPlayer, gameIndex); break;
                default:
                    Debug.Log("Find Action Match returned false");
                    return false;
            }
            cur.AddBudget(-(float)quote.Total);
            cur.AdvanceActionIndex();
            if (LogEnabled) DbLog.PostLogPerform(quote, gameIndex);

            // Tell listeners (Bootstrapper/View) to refresh visuals
            OnActionExecuted?.Invoke();
            return true;

        }



        // private bool FastCheck(in Action a)
        // {
        //     if (a.kind > PieceBuild) return false;
        //     if (currentPlayer >= 4) return false;
        //     return true;
        // }

        public static bool IsStillLegal(Action[] offers, Action theAction)
        {
            for (int i = 0; i < offers.Length; i++)
            {
                if (theAction.kind != offers[i].kind) continue;
                if (theAction.ActorsCell != offers[i].ActorsCell) continue;
                if (theAction.TargetCell != offers[i].TargetCell) continue;
                if (theAction.TargetType != offers[i].TargetType) continue;
                if (theAction.WallConfig != offers[i].WallConfig) continue;
                // if ((theAction.kind == Create || theAction.kind == Upgrade) && Piece.sacrificeCost_enabled[theAction.TargetType])
                // {
                //     if (!SacCostEquals(theAction.SacCost, offers[i].SacCost)) continue;
                // }
                return true;
            }
            return false;
        }

        private static bool SacCostEquals(int[] a, int[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        #endregion
        #region Game Loop

        public void BeginTurn()
        {
            events.turnBegin(new TurnContext { ThePlayer = currentPlayer });
            ps[currentPlayer].BeginTurnReset();
            if (LogEnabled) DbLog.onTurnBegin(currentPlayer, gameIndex);
            if (ps[currentPlayer].applyStartOfTurnBudgetDecrease) ps[currentPlayer].AddBudget(-(float)Info.startOfTurnBudgetDecrease);
            PiecesSides.RecomputeConnectorComponents(gameIndex);
        }

        private void ApplyEndTurn()
        {
            // Player who just ended
            byte playerWhoEnded = currentPlayer;
            // Mark whether they ended without acting this turn
            ps[playerWhoEnded].endedWithoutActionThisCycle = ps[playerWhoEnded].actionIndexThisTurn == 0;
            if (LogEnabled) DbLog.logEndGame(playerWhoEnded, gameIndex);
            // Advance to next alive player and/or player that has not passed there turn
            currentPlayer = NextAlivePlayerAfter(playerWhoEnded);
            // Evaluate round-end rules AFTER the handoff
            if (ShouldEndRoundAfterEndTurn(playerWhoEnded)) { EndRound(); } else { BeginTurn(); }
        }

        // public int[] cachedPieceDrivenPenalties = new int[4];

        // public int playerPieceDrivenPenalties(int playerId)
        // {
        //     int cachedPieceDrivenPenalties = 0;
        //     for (int c = 0; c < bm.pieceCount; c++)
        //     {
        //         if (bm.pieceOwner[c] != playerId) continue;
        //         if (Piece.Instantfactory_isKillPenalty[bm.pieceType[c]]) cachedPieceDrivenPenalties += Piece.Instantfactory_killsPunishment[bm.pieceType[c]];
        //     }
        //     return cachedPieceDrivenPenalties;
        // }

        private void EndRound()
        {
            // for (int i = 0; i < 4; i++)
            // {
            //     cachedPieceDrivenPenalties[i] = 0;
            //     cachedPieceDrivenPenalties[i] = playerPieceDrivenPenalties(i);
            // }
            events.roundBegin();
            if (LogEnabled) DbLog.logEndRound();
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
                ps[i].budget = Payout.GiveMePayPlayersOut(player: i, gameIndex: gameIndex);   // clamp if you wish via hub.cap_maxBudget
                ps[i].ClearRoundCounters();
                ps[i].endedWithoutActionThisCycle = false;
            }

            // 4) Victory check
            GameEndCheck();
            if (isGameOver) controller.GameEnd();

            // 5) If game continues, begin next round on the already-selected currentPlayer
            currentRoundNumber++;
            if (!isGameOver) BeginTurn();
        }



        #endregion
        #region Game Loop Helpers

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

            if (DidLeaguePlayerDie(p)) controller.GameEnd();

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

        private bool DidLeaguePlayerDie(int EliminatedPlayer)
        {
            if (!Info.EnableMLLeague) return false;
            if (EliminatedPlayer != 0) return false;

            int best = int.MinValue;
            byte win = 255;
            bool tie = false;
            for (byte p = 1; p < 3; p++)
            {
                if (ps[p].isEliminated) continue;
                int v = ps[p].vpTotal;
                if (v > best) { best = v; win = p; tie = false; }
                else if (v == best) { tie = true; }
            }
            if (tie) { winner = 255; } else { winner = win; }
            return true;
        }

        private void GameEndCheck()
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
                // events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.Elimination, winner));
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
            // events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.Elimination, winner));
            if (tie)
            {
                winner = 255; // draw/no single winner
                Debug.Log("Game over by VP. Result: TIE");
                // events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.Tie, null));
            }
            else
            {
                winner = win;
                Debug.Log($"Game over by VP. Winner: {winner}");
                // events.RaiseGameResult(new EventManager.GameResultEvent(EventManager.GameResultType.EndOfTurnVictoryPoints, winner));
            }
        }



        #endregion
        #region Accessors
        // --- Current player read-only accessors ---
        public byte CurrentPlayerId => currentPlayer;

        public bool PassedTurn(int playerID)
        => ps[playerID].endedWithoutActionThisCycle;


        // --- simple Accesors ---
        public void SetCoreHealth(byte player, int hp) // clamps by hub caps
        {
            if (hp < 0) hp = 0;
            if (hp > Info.capMaxCoreHealth) hp = Info.capMaxCoreHealth;
            currentCoreHealthByPlayer[player] = hp;
        }

        public void AddCenterVictoryPoints(int delta)
        {
            int v = currentCenterVP + delta;
            if (v < 0) v = 0;
            if (v > Info.capMaxVPPool) v = Info.capMaxVPPool;
            currentCenterVP = v;
        }

        public void ResetCenterVictoryPointsToStart() => currentCenterVP = Info.startCenterVP;

        public void ResetAllCoreHealthToStart()
        {
            for (byte p = 0; p < currentCoreHealthByPlayer.Length; p++)
                currentCoreHealthByPlayer[p] = Info.startCoreHp;
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

        #region Class's Refrences
        public event System.Action OnActionExecuted;

        private BoardModel bm;
        private GameController controller;
        private EventManager events;
        private PlayerManager playerManager; // to do- hook this up


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

        public readonly HashSet<int> dublicateFilter = new HashSet<int>();

        #endregion
        #region init Method

        private int gameIndex;
        public void Initialize(
                       BoardModel board,
                       PlayerState[] players,
                       byte startingPlayer, EventManager eventManager, GameController gameController, PlayerManager thePlayerManager, int theGameIndex)
        {
            bm = board;
            events = eventManager;
            controller = gameController;
            gameIndex = theGameIndex;
            playerManager = thePlayerManager;

            ps = players;
            currentPlayer = startingPlayer;

            roundsLeft = Info.numberOfRounds;     // from config (not a raw int param)

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
                ps[i].budget = Info.startingBudgetPerRound[currentRoundNumber - 1];
            }


            if (controller.twoPlayerHurdle)
            {
                currentCoreHealthByPlayer[3] = 0;
                currentCoreHealthByPlayer[2] = 0;
            }

            LogEnabled = Info.dbLogging.enabled && gameIndex == 0;

            // Start first player's turn
            // BeginTurn();
        }

        private bool LogEnabled;
        #endregion
    }
}
