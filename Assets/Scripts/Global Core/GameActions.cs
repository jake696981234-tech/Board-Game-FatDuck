using UnityEngine;
using static Game.Core.ActionKind;

namespace Game.Core
{
    public class GameActions
    {
        private BoardModel bm;

        private Pieces pcs;

        private GameState gamestate;

        private PlayerState[] ps;


        public void Initialize(BoardModel board, Pieces pieces, GameState _gamestate)
        {
            bm = board;
            pcs = pieces;
            gamestate = _gamestate;

            gamestate.ps = ps;
        }


        #region Gamestate

        private void ApplyMove(in Action theAction, byte player)
        {
            int actorPid = bm.GetCellOccupant(theAction.srcCell);
            if (actorPid < 0) return;
            int dstOcc = bm.GetCellOccupant(theAction.dstCell);
            if (dstOcc >= 0)
            {
                gamestate.ResolveMelee(actorPid, dstOcc, in theAction);
            }
            else
            {
                bm.MovePieceRow(actorPid, theAction.dstCell);
            }

        }

        private void ApplyShoot(in Action theAction, byte player)
        {
            int targetPid = theAction.aux;
            if (targetPid < 0) return;
            short dmg = gamestate.GetAbilityDamage(in theAction);
            bool killed = bm.DamagePieceRow(targetPid, dmg);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                pieceKilled(targetPid, 0, theAction);
            }
        }

        private void ApplyCreate(in Action theAction, byte player)
        {
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, player, (byte)theAction.pieceType, theAction.dstCell, pcs.maxHPByType[theAction.pieceType]);
            // Grant digit if this type provides one
            int g = pcs.GrantsDigit((byte)theAction.pieceType);
            if (g >= 0) ps[player].GrantDigit(g);
        }

        private void ApplyCaptureVP(in Action theAction, byte player)
        {
            ps[player].OnCaptureVP();
            gamestate.AddCenterVictoryPoints(-1);   // pool now lives in GameState
        }

        private void ApplyCoreDamage(in Action theAction, byte player)
        {
            ps[player].OnCoreDamage();

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

        #endregion
        #region to do ApplyActions / helpers
        private void ApplyPush(in Action theAction, byte player)
        {
            int targetPid = theAction.aux;
            if (targetPid < 0) return;
            short dmg = gamestate.GetAbilityDamage(in theAction);
            bool killed = bm.DamagePieceRow(targetPid, dmg);
            if (killed)
            {
                // Revoke digit from the defender's owner if this type granted one
                pieceKilled(targetPid, 0, theAction);
            }
            else
            {
                // to do- There are 6 cells an enemy can be on. Enumarate the 3 cells furthest away from the ActorPiece, that are empty && if they exist. if there is non, return. Pick the middle cell for pushedCellID. if not legal, pick the cell closet to the target Pieces base. if not legal, move to the last remaining cell.
                int pushedCellID = 0; //replace this with the method described above.
                bm.MovePieceRow(targetPid, pushedCellID);
            }
        }


        private void multiPartAction()
        {
            //If an action has multple parts 
        }


        #endregion
        #region ApplyHelpers


        //private void ifOnKillAction(in Action theAction);
        // {   

        // }

        //this can replace the if(killed) line in both ResolveMelee and ResolveShoot
        public void pieceKilled(int victim, int pieceActor, in Action theAction)
        {
            int deadOwner = bm.GetPieceOwner(victim);
            byte deadType = bm.GetPieceType(victim);
            int g = pcs.GrantsDigit(deadType);
            if (g >= 0) ps[deadOwner].RevokeDigit(g);
            bm.FreeRowSwapBack(victim);
            if (theAction.kind == Move)
                bm.MovePieceRow(pieceActor, theAction.dstCell);
        }

        #endregion
        #region Pieces

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

        #endregion









    }
}
