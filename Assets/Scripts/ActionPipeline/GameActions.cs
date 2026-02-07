using UnityEngine;
using static Game.Core.ActionKind;
using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class GameActions
    {
        public static void placePiece(ushort wallConfig, int createdPieceType, int targetCell, int player, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;
            // PaySacCost(theAction, player, gameIndex);
            int createdPieceId = bm.AllocateRow();
            bm.PlacePieceRow(createdPieceId, player, (byte)createdPieceType, targetCell, Piece.maxHP[createdPieceType]);
            bm.pieceConnectorConfig[createdPieceId] = wallConfig;
            if (Piece.digitItGives[(byte)createdPieceType] >= 0) gameState.ps[player].GrantDigit(Piece.digitItGives[(byte)createdPieceType]);
            InstantFactoryAction.PlaceInstantFactory(createdPieceType: createdPieceType, createdPieceId: createdPieceId, player: player, gameIndex: gameIndex);
            RefreshConnectorState(gameIndex);
        }
        public static void pieceKilledWithNoTriggers(int victimsCell, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            int g = Piece.digitItGives[(byte)bm.GetPieceTypeFromCell(victimsCell)];
            if (g >= 0) gameState.ps[bm.GetPieceOwnerFromCell(victimsCell)].RevokeDigit(g);
            bm.FreeRowSwapBack(pieceId: bm.occupantPieceId[victimsCell]);
            RefreshConnectorState(gameIndex);
        }

        public static void pieceKilled(int victimsCell, int actorsCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            int ActorsPieceId = bm.GetCellOccupant(actorsCell);

            bm.pieceKillCount[ActorsPieceId]++;
            InstantFactoryAction.IncrementInstantFactoryKills(gameIndex);
            bm.addToEndRoundPayout[ActorsPieceId] += Piece.eat_amount[bm.pieceType[ActorsPieceId]];
            gameState.ps[bm.pieceOwner[ActorsPieceId]].perRoundPieceKillCount++;
            FeedingGroundAction.FeedingGround(gameIndex, bm.GetCellOccupant(victimsCell));
            if (Piece.zombie_enabled[bm.GetPieceTypeFromCell(actorsCell)]) //move this above the above the other benefifts if you dont want the others to trigger
            {
                Zombie(victimsCell, actorsCell, gameIndex);
                return;
            }
            NecroSpawnAction.Record(gameIndex, bm.GetCellOccupant(victimsCell));

            pieceKilledWithNoTriggers(victimsCell: victimsCell, gameIndex: gameIndex);
        }

        private static void Zombie(int victimsCell, int actorsCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;
            bm.pieceOwner[bm.GetCellOccupant(victimsCell)] = bm.GetPieceOwnerFromCell(actorsCell);
            bm.pieceHP[bm.GetCellOccupant(victimsCell)] = Piece.maxHP[bm.GetPieceTypeFromCell(victimsCell)];
            int g = Piece.digitItGives[(byte)bm.GetPieceTypeFromCell(victimsCell)];
            if (g >= 0) gameState.ps[bm.GetPieceOwnerFromCell(victimsCell)].GrantDigit(g);
            RefreshConnectorState(gameIndex);
        }

        public static bool ApplyTypicalDamageAndPieceKill(int actorsCell, int victimsCell, int dmg, int gameIndex)
        {
            bool killed = ApplyDamageToPiece(ActorsCell: actorsCell, victimCell: victimsCell, dmg: dmg, gameIndex: gameIndex);

            if (killed) pieceKilled(victimsCell: victimsCell, actorsCell: actorsCell, gameIndex: gameIndex);
            return killed;
        }
        public static bool ApplyDamageToPiece(int ActorsCell, int victimCell, int dmg, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            if (dmg <= 0) return false;
            int targetPid = bm.GetCellOccupant(victimCell);
            if (!Piece.connectors_enabled[bm.GetPieceType(targetPid)]) return bm.DamagePieceRow(targetPid, dmg);
            if (!PiecesSides.IsConnectorSide(bm.pieceConnectorConfig[targetPid], PiecesSides.OppositeDir(BmCac.GetDirectionIndex(ActorsCell, victimCell, gameIndex)))) return bm.DamagePieceRow(targetPid, dmg);
            int CapitalHealth = bm.pieceCapitalHP[targetPid];
            if (CapitalHealth <= 0) return bm.DamagePieceRow(targetPid, dmg);
            int CapitalHealthremaining = CapitalHealth - dmg;
            if (CapitalHealthremaining >= 0)
            {
                bm.pieceCapitalHP[targetPid] = CapitalHealthremaining;
                dmg = 0;
            }
            else
            {
                bm.pieceCapitalHP[targetPid] = 0;
                dmg = (short)-CapitalHealthremaining;
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
                if (bm.IsValidPieceId(victimID)) pieceKilledWithNoTriggers(victimsCell: bm.pieceCellId[victimID], gameIndex: gameIndex);
            }
        }



        public static void ResolveMelee(int actorsCell, int victimsCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            if (ApplyTypicalDamageAndPieceKill(actorsCell: actorsCell, victimsCell: victimsCell, dmg: Piece.move_damage[bm.GetPieceTypeFromCell(actorsCell)], gameIndex: gameIndex))
            {
                if (bm.IsEmpty(victimsCell)) bm.MovePieceRow(bm.occupantPieceId[actorsCell], victimsCell);
                return;
            }
            bm.MovePieceRow(bm.occupantPieceId[actorsCell], BmCac.FindNearestEmptyAdjacent(actorsCell, victimsCell, gameIndex));
        }

        #region Offers for Create

        // public static void CreateActions(int cell, ref OfferBuild offerBuild)
        // {
        //     for (int type = 0; type <  Piece.typeCount; type++)
        //     {
        //         Action theAction = new Action
        //         {
        //             kind = Create,
        //             ActorsCell = -1,
        //             TargetCell = cell,
        //             TargetType = type,
        //         };
        //         GenerateCompleteCreateActions(in theAction, ref offerBuild);
        //     }
        // }

        // public static void GiveMeCreateActions(byte actionKind, int targetcell, bool careAboutPieceLimit, ref OfferBuild offerBuild)
        // {
        //     if (PieceLimitReached(ref offerBuild) && careAboutPieceLimit) return;
        //     if (!isCellLegalPlacement(targetcell, ref offerBuild)) return;

        //     int typeCount = Piece.typeCount;
        //     for (int type = 0; type < typeCount; type++)
        //     {
        //         if (!Piece.isBuildable[type]) continue;
        //         if (!isPieceTypeLegal(type, ref offerBuild)) continue;
        //         GenerateCompleteCreateActions(in theAction, ref offerBuild);
        //     }
        // }

        public static void GenerateCompleteCreateActions(in Action theAction, in int targetType, ref OfferBuild offerBuild)
        {
            List<Action> CreateActions = new List<Action> { theAction };
            if (Piece.connectors_enabled[targetType] && !CreateConnectorOptions(CreateActions, in targetType, ref offerBuild)) return;
            // if (Piece.sacrificeCost_enabled[theAction.TargetType] && !GenerateSacrificeCosts(CreateActions, ref offerBuild)) return;
            for (int i = 0; i < CreateActions.Count; i++) { OfferProvider.Emit(CreateActions[i], ref offerBuild); }
        }

        public static bool CreateConnectorOptions(List<Action> actions, in int targetType, ref OfferBuild offerBuild)
        {
            List<Action> ConnectorActions = new List<Action>();
            bool legal = false;

            for (int i = 0; i < actions.Count; i++)
            {
                for (int cfg = 0; cfg < 64; cfg++)
                {
                    // if ((allowedMask & (1UL << cfg)) == 0) continue;
                    if (!PiecesSides.IsConnectorPlacementLegal(actions[i].TargetCell, (byte)targetType, cfg, offerBuild.query.playerId, offerBuild.gameIndex)) continue;

                    legal = true;
                    Action theAction = new Action
                    {
                        kind = actions[i].kind,
                        ActorsCell = actions[i].ActorsCell,
                        TargetCell = actions[i].TargetCell,
                        TargetType = actions[i].TargetType,
                        WallConfig = (ushort)cfg,
                    };
                    ConnectorActions.Add(theAction);
                }
            }

            if (legal)
            {
                actions.Clear();
                actions.AddRange(ConnectorActions);
                return true;
            }
            return false;
        }
        public static bool PieceLimitReached(ref OfferBuild offerBuild)
        {
            var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
            bool limitActive = offerBuild.query.pieceLimitEnabled && offerBuild.query.pieceLimitPerPlayer > 0;
            return limitActive && bm.GetPieceCountForPlayer(offerBuild.query.playerId) >= offerBuild.query.pieceLimitPerPlayer;
        }

        public static bool isCellLegalPlacement(int cell, ref OfferBuild offerBuild)
        {
            var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
            if (!bm.IsEmpty(cell)) return false; // only empties

            var coreCell = bm.GetPlayerCoreCellId(offerBuild.query.playerId);

            if (cell == coreCell) return true;
            if (isBaseHex(ref offerBuild, coreCell, cell)) return true;
            if (isAdjecentABuilding(cell, ref offerBuild)) return true;
            return false;
        }

        public static bool isPieceTypeLegal(int type, ref OfferBuild offerBuild)
        {

            if (!HasRequiredDigits(type, ref offerBuild)) return false;

            bool hasConn = Piece.connectors_enabled[type];
            ulong allowedMask = hasConn ? Piece.connector_allowedMasks[type] : 0UL;
            if (hasConn && allowedMask == 0UL) return false;

            return true;
        }
        public static bool HasRequiredDigits(int type, ref OfferBuild offerBuild)
        {
            var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;
            int req = Piece.requiredDigit[(byte)type];
            if (req >= 0 && !gameState.ps[offerBuild.query.playerId].HasDigit(req)) return false;
            return true;
        }

        public static bool isBaseHex(ref OfferBuild offerBuild, int coreCell, int cell)
        {
            var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
            int[] neighScratch = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);

            int numberOfCoreNeighbors = bm.GetNeighbors(coreCell, neighScratch);
            for (int i = 0; i < numberOfCoreNeighbors; i++)
            {
                if (neighScratch[i] == cell) return true;
            }
            return false;
        }

        public static bool isAdjecentABuilding(int cell, ref OfferBuild offerBuild)
        {
            var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

            int[] neighScratch = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);
            int nNbrs = bm.GetNeighbors(cell, neighScratch);
            for (int i = 0; i < nNbrs; i++)
            {
                int nbCell = neighScratch[i];
                int nbPid = bm.GetCellOccupant(nbCell);
                if (OfferProvider.IsInvalid(bm, nbPid)) continue;
                if (bm.GetPieceOwner(nbPid) != offerBuild.query.playerId) continue;
                byte nbType = bm.GetPieceType(nbPid);
                if (Piece.isBuilding[nbType]) return true;
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
