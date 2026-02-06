using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Game.Core; // Action, OfferQuery, GameState, PlayerState
using static MLActions.MLState;
using System.Collections.Generic;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class MLObservation
{
    // need to
    // 2. Action Cost Amount

    public static void GiveMeObservations(VectorSensor sensor, MLSam MLSam)
    {
        WriteObservations(MLSam);

        for (int i = 0; i < Info.vectorObservationSize; i++)
        {
            sensor.AddObservation(MLSam.Observations[i]);
        }
    }

    public static void WriteObservations(MLSam MLSam)
    {
        var gameState = GameRegistry.game[MLSam.bot.gameIndex].gameState;
        var bm = GameRegistry.game[MLSam.bot.gameIndex].boardModel;

        MLSam.Count = 0;
        //Global
        MLSam.Observations[MLSam.Count++] = VPRemaining(gameState);
        MLSam.Observations[MLSam.Count++] = RoundsLeft(gameState);
        MLSam.Observations[MLSam.Count++] = CostEngine.turnFee(MLSam.bot.playerId, gameState);
        WritePlayersStats(gameState, MLSam);

        for (int cell = 0; cell < Info.totalCells; cell++)
        {
            float Occupancy = CellOccupancy(bm, cell);
            MLSam.Observations[MLSam.Count++] = Occupancy;
            if (Occupancy == Info.invalidId) { WriteNotOccupiedCellObservations(MLSam); } else { WriteOccupiedCellObservations(cell, MLSam); }
            // distance to cores
            // distance to Vp
            MLSam.Observations[MLSam.Count++] = isPieceVPCell(bm, cell);
            MLSam.Observations[MLSam.Count++] = isYourCore(bm, MLSam, cell);
            MLSam.Observations[MLSam.Count++] = isEnemyCore(bm, MLSam, cell);
        }
    }

    #region Write Occ Cells
    private static void WriteNotOccupiedCellObservations(MLSam MLSam)
    {
        for (int i = 0; i < 34; i++)
        {
            MLSam.Observations[MLSam.Count++] = 0f;
        }
    }

    private static void WriteOccupiedCellObservations(int cell, MLSam MLSam)
    { //34
        var bm = GameRegistry.game[MLSam.bot.gameIndex].boardModel;

        MLSam.Observations[MLSam.Count++] = PieceOwner(bm, cell, MLSam.bot.playerId);
        MLSam.Observations[MLSam.Count++] = PieceEnemy(bm, cell, MLSam.bot.playerId);
        MLSam.Observations[MLSam.Count++] = PieceHP(bm, cell);
        WritePieceSides(MLSam.bot.gameIndex, cell, MLSam); //6
        int PieceType = bm.GetPieceTypeFromCell(cell);
        WriteActiveAbilties(cell, PieceType, MLSam); //Piece.ActiveAbilityCount - 15
        MLSam.Observations[MLSam.Count++] = Piece.isBuilding[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.connectors_enabled[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.connector_isCapital[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.factory_enabled[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.sanctuary_enabled[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.eat_enabled[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.feedingGround_enabled[PieceType] ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = Piece.zombie_enabled[PieceType] ? 1f : 0f;
        //to do check order of building offers
        MLSam.Observations[MLSam.Count++] = isLegalActor(MLSam, cell) ? 1f : 0f;
        MLSam.Observations[MLSam.Count++] = isLegalTarget(MLSam, cell) ? 1f : 0f;

    }



    private static bool isLegalTarget(MLSam MLSam, int cell)
    {
        for (int theAction = 0; theAction < MLSam.bot.Offers.Length; theAction++)
        {
            if (MLSam.bot.ActionMask[theAction] != 0) continue;
            switch (MLSam.bot.Offers[theAction].kind)
            {
                case EndTurn:
                case CaptureVP:
                case CoreDamage:
                case GroupBuild:
                case Spawner:
                case SacrificeFactory:
                case ConversionFactory:
                    continue;
                case Explosive:
                    if (isExplosiveVictimAction(MLSam.bot.Offers[theAction], MLSam, cell)) { return true; } else { continue; }
                case Move:
                case Shoot:
                case Push:
                    //need to add aniper here to- to do
                    if (MLSam.bot.Offers[theAction].TargetCell == cell) { return true; } else { continue; }
            }
        }
        return false;
    }

    private static bool isExplosiveVictimAction(Action theAction, MLSam MLSam, int cell)
    {
        var bm = GameRegistry.game[MLSam.bot.gameIndex].boardModel;

        int maxRange = Piece.explosive_range[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        bool friendlyFire = Piece.explosive_isFriendlyFire[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        var occCells = BmCac.CellIdsRingAndLessthanRing(originCell: theAction.ActorsCell, ringSize: maxRange, requireEmpty: false, requireOcc: true, requireOwned: -1, gameIndex: MLSam.bot.gameIndex);

        for (int i = 0; i < occCells.Count; i++)
        {
            if (occCells[i] == cell) continue;
            if (occCells[i] == theAction.ActorsCell) continue;
            int victim = bm.GetCellOccupant(occCells[i]);
            if (!friendlyFire && bm.pieceOwner[victim] == MLSam.bot.playerId) continue;
            return true;
        }
        return false;
    }



    private static bool isLegalActor(MLSam MLSam, int cell)
    {
        for (int theAction = 0; theAction < MLSam.bot.Offers.Length; theAction++)
        {
            if (MLSam.bot.Offers[theAction].kind == EndTurn) continue;
            if (MLSam.bot.Offers[theAction].kind == Create) continue;
            if (MLSam.bot.Offers[theAction].ActorsCell == cell && MLSam.bot.ActionMask[theAction] != 0) return true;
        }
        return false;
    }

    public static void WriteActiveAbilties(int cell, int PieceType, MLSam MLSam)
    {
        for (int i = 0; i < Piece.ActiveAbilityCount; i++)
        {
            MLSam.Observations[MLSam.Count++] = Piece.ActiveAbilitesEnabledFromType[PieceType, i] ? 1f : 0f;
        }
    }

    public static void WritePieceSides(int gameIndex, int cell, MLSam MLSam)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        bool ConnectorEnabled = Piece.connectors_enabled[bm.GetPieceTypeFromCell(cell)];
        for (int i = 0; i < 6; i++)
        {
            if (!ConnectorEnabled)
            {
                MLSam.Observations[MLSam.Count++] = 0f;
                continue;
            }
            MLSam.Observations[MLSam.Count++] = isPieceSideConnector(cell, i, gameIndex);
        }
    }
    #endregion
    private static void WritePlayersStats(GameState gameState, MLSam MLSam)
    {
        MLSam.Observations[MLSam.Count++] = Budget(gameState, MLSam.bot.playerId);
        MLSam.Observations[MLSam.Count++] = VPGained(gameState, MLSam.bot.playerId);
        MLSam.Observations[MLSam.Count++] = CoreHealth(gameState, MLSam.bot.playerId);
        MLSam.Observations[MLSam.Count++] = RoundPayOut(MLSam.bot.gameIndex, MLSam.bot.playerId);
        for (int i = 0; i < Info.playerCount; i++)
        {
            if (i == MLSam.bot.playerId) continue;
            MLSam.Observations[MLSam.Count++] = Budget(gameState, i);
            MLSam.Observations[MLSam.Count++] = VPGained(gameState, i);
            MLSam.Observations[MLSam.Count++] = CoreHealth(gameState, i);
            MLSam.Observations[MLSam.Count++] = RoundPayOut(MLSam.bot.gameIndex, i);
        }

    }

    #region Getters
    //Getters With Normlisation- an attempt to make the above Code more readable.
    private static float RoundsLeft(GameState gameState) => normalize(gameState.RoundsLeft, Info.numberOfRounds);
    private static float VPRemaining(GameState gameState) => normalize(gameState.GetCenterVP(), Info.capMaxVPPool);
    private static float Budget(GameState gameState, int playerIndex) => normalize(gameState.GetBudget((byte)playerIndex), Info.capMaxBudget);
    private static float VPGained(GameState gameState, int playerIndex) => normalize(gameState.GetVP((byte)playerIndex), Info.capMaxVP);
    private static float CoreHealth(GameState gameState, int playerIndex) => normalize(gameState.GetCoreHealth((byte)playerIndex), Info.capMaxCoreHealth);
    private static float RoundPayOut(int gameIndex, int playerIndex) => normalize(Payout.GiveMePayPlayersOut(playerIndex, gameIndex), Info.capMaxBudget);
    private static float CellOccupancy(BoardModel bm, int cell)
    {
        if (bm.GetCellOccupant(cell) != Info.invalidId)
        { return 1f; }
        else
        { return 0f; }
    }

    private static float PieceOwner(BoardModel bm, int cell, int PlayerIndex) => (bm.GetPieceOwnerFromCell(cell) == PlayerIndex) ? 1f : 0f;
    private static float PieceEnemy(BoardModel bm, int cell, int PlayerIndex) => (bm.GetPieceOwnerFromCell(cell) == PlayerIndex) ? 0f : 1f;
    private static float PieceHP(BoardModel bm, int cell) => normalize(bm.GetPieceHPFromCell(cell), Info.capMaxPieceHP);
    private static float isPieceVPCell(BoardModel bm, int cell) => (bm._vpCellId == cell) ? 1f : 0f;
    private static float isYourCore(BoardModel bm, MLSam MLSam, int cell) => (bm.GetPlayerCoreCellId(MLSam.bot.playerId) == cell) ? 1f : 0f;
    private static float isEnemyCore(BoardModel bm, MLSam MLSam, int cell)
    {
        for (byte p = 0; p < Info.playerCount; p++)
        {
            if (p == MLSam.bot.playerId) continue;
            if (cell == bm.GetPlayerCoreCellId(p)) return 1f;
        }
        return 0f;
    }

    //piece State
    private static float isPieceSideConnector(int cell, int direction, int gameIndex) => BmCac.isConnectorSideFromCell(cell, direction, gameIndex) ? 1f : 0f;
    //abilitys
    #endregion
    private static float normalize(int num, int Max)
    {
        if (Max <= 0) return 0f;
        if (num <= 0) return 0f;
        if (num >= Max) return 1f;
        return (float)num / Max;
    }

    private static float normalize(float num, float den)
    {
        if (den <= 0f) return 0f;
        if (num <= 0f) return 0f;
        if (num >= den) return 1f;
        return num / den;
    }



    // GLOBAL BLOCK (always first)

    // vpLeftNorm

    // PLAYER BLOCK (repeated for playerIndex = 0..3, self always index 0)

    // For each playerIndex:

    // player_budgetNorm

    // player_vpGainedNorm

    // player_coreHealthNorm

    // player_nextRoundPayoutNorm

    // BOARD CELL BLOCK (repeated for cellIndex = 0..216)

    // For each cellIndex:

    // Occupancy & Ownership

    // cell_isOccupied

    // cell_isMine

    // cell_isEnemy

    // Piece State

    // cell_pieceHpNorm

    // Wall Configuration (6 sides, fixed order)

    // cell_wallSide0

    // cell_wallSide1

    // cell_wallSide2

    // cell_wallSide3

    // cell_wallSide4

    // cell_wallSide5

    // Piece Ability Flags (matches ActionKind branch, length = 15)

    // cell_abilityFlag_0

    // cell_abilityFlag_1

    // cell_abilityFlag_2

    // cell_abilityFlag_3

    // cell_abilityFlag_4

    // cell_abilityFlag_5

    // cell_abilityFlag_6

    // cell_abilityFlag_7

    // cell_abilityFlag_8

    // cell_abilityFlag_9

    // cell_abilityFlag_10

    // cell_abilityFlag_11

    // cell_abilityFlag_12

    // cell_abilityFlag_13

    // cell_abilityFlag_14

    // Other Piece Attributes (fixed semantic meaning)

    // cell_attr_isBuilding

    // cell_attr_isCapital

    // cell_attr_isSpawner

    // cell_attr_isFactory

    // cell_attr_hasWallSystem

    // Legal Action Hints (derived from OfferProvider)

    // cell_isLegalActor

    // cell_isLegalTarget

    // Objective Distance

    // cell_distToVPCellNorm

    // Core Distance (player ordered, self first)

    // cell_distToCore_player0Norm

    // cell_distToCore_player1Norm

    // cell_distToCore_player2Norm

    // cell_distToCore_player3Norm
}
