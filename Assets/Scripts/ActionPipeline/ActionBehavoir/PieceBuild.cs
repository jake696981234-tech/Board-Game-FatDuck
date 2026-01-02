using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System.Collections.Generic;

public static class PieceBuildAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        if (CreateAction.PieceLimitReached(ref offerBuild)) return;
        int maxRange = Piece.pieceBuild_range[actorType];

        for (int i = 0; i < maxRange; i++)
        {
            var EmptyCells = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
            int NumberOfEmptyCells = BmCac.cellIdsRingAroundCell(cell, i, true, EmptyCells, offerBuild.gameIndex);

            for (int c = 0; c < NumberOfEmptyCells; c++)
            {
                genLegalActions(actorType, cell, EmptyCells[c], ref offerBuild);
            }
        }
    }

    public static void genLegalActions(byte actorType, int cell, int targetCell, ref OfferBuild offerBuild)
    {
        int buildTargetTypes = Piece.pieceBuild_targetIds[actorType].Length;
        for (int i = 0; i < buildTargetTypes; i++)
        {
            int type = Piece.pieceBuild_targetIds[actorType][i];
            
            if (!isPieceTypeLegal(type, ref offerBuild)) continue;

            Action theAction = new Game.Core.Action
            {
                kind = PieceBuild,
                pieceType = (byte)type,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)actorType,
                aux = (ushort)targetCell,
            };

            List<Action> CreateActions = new List<Action> {theAction};
            if (Piece.sacrificeCost_enabled[type] && !CreateAction.GenerateSacrificeCosts(CreateActions, ref offerBuild)) continue;
            if (Piece.connectors_enabled[type] && !CreateAction.CreateConnectorOptions(CreateActions, ref offerBuild)) return;

            for (int c = 0; i < CreateActions.Count; c++) { OfferProvider.Emit(CreateActions[c], ref offerBuild); }
        }
    }

    public static bool isPieceTypeLegal(int type, ref OfferBuild offerBuild)
    {
        if (!CreateAction.HasRequiredDigits(type, ref offerBuild)) return false;                                            
        bool hasConn = Piece.connectors_enabled[type]; 

        ulong allowedMask = hasConn ? Piece.connector_allowedMasks[type] : 0UL;
        if (hasConn && allowedMask == 0UL) return false;

        return true;
    }
}
