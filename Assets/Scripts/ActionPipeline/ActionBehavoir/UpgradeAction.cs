using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class UpgradeAction
{
    public static void CreateActions()
    {
        // Upgrade-as-piece-action: find destination types that upgrade from this actorType
        for (int upgradedToPieceType = 0; upgradedToPieceType < PieceDefinition.typeCount; upgradedToPieceType++)
        {
            if (!PieceDefinition.upgrade_enabled[upgradedToPieceType]) continue;
            if (PieceDefinition.upgrade_target[upgradedToPieceType] != actorType) continue;
            int reqDigit = PieceDefinition.requiredDigit[(byte)upgradedToPieceType];
            if (reqDigit >= 0 && !gameState.ps[player].HasDigit(reqDigit)) continue;

            var a = new Action
            {
                kind = Upgrade,
                pieceType = actorType, // destination type
                ActorsCellId = (ushort)cell, // to do need to change the rest of the method - I switched this around. PieceType = used to be upgradedToPieceType- and TargetCellId used to be cell.
                TargetCellId = (byte)upgradedToPieceType,
                aux = 0
            };

            if (PieceDefinition.sacrificeCost_enabled[upgradedToPieceType]) //pretty sure this sets Aux as piece IDs, i made this be reflected in UI. If theres issues, check this.
            {
                SacrificeCostOptions.Clear();
                if (PassiveActions.GenerateSacrificeCosts(in a, player, gameIndex, SacrificeCostOptions))
                {
                    for (int opt = 0; opt < SacrificeCostOptions.Count; opt++)
                    {
                        var withCost = a;
                        withCost.addCost = SacrificeCostOptions[opt];
                        Emit(ref withCost, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                    }
                }
            }
            else
            {
                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
            }
        }
    }
}
