using UnityEngine;
using System.Collections.Generic;
using System;

public static class ShowRightPanel
{
    public static void PushBuildMenu()
    {
        // Build list shows all Create* actions this turn (grouping/labeling by piece type + quoted cost)
        var items = new List<BuildItem>(UIBridge._count);
        if (UIBridge._count > 0)
        {
            var names = PieceDefinition.displayNameByType;     // assumed from your Pieces registry
            var paths = PieceDefinition.spritePathByType;      // assumed from your Pieces registry

            for (int i = 0; i < UIBridge._count; i++)
            {
                var a = UIBridge._offers[i];
                if (a.kind != Game.Core.ActionKind.Create) continue;
                byte t = a.pieceType;
                string name = (t < names.Length) ? names[t] : $"Type {t}";
                string path = (t < paths.Length) ? paths[t] : null;
                int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
                bool legal = UIBridge._mask[i] != 0;           // 1 = affordable+legal; 0 = masked out by cost, etc. :contentReference[oaicite:8]{index=8}

                //Adding the Connector Field
                ushort auxiliary = a.aux;

                items.Add(new BuildItem(t, name, path, cost, legal, auxiliary));
            }
        }
        UI.hic.buildMenu.Show(items, UI.hic.config);
    }

    public static void PushNonPieceActionList()
    {
        var items = new List<ActionItem>();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (a.kind != Game.Core.ActionKind.EndTurn) continue; // future: add more non-piece kinds here
            int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
            bool legal = UIBridge._mask[i] != 0;
            int kind = a.kind;
            items.Add(new ActionItem(i.ToString(), UIHelpers.PrettyAction(a), cost, legal, Array.Empty<int>(), kind));
        }
        UI.hic.nonPieceActionList.Show(items);
    }

    public static void PushPieceActionListForSelection()
    {
        var items = new List<ActionItem>();
        bool moveAddedForCell = false;
        if (UIHelpers._selectedCellId.HasValue)
        {
            int cell = UIHelpers._selectedCellId.Value;
            for (int i = 0; i < UIBridge._count; i++)
            {
                var a = UIBridge._offers[i];
                if (a.kind == Game.Core.ActionKind.EndTurn) continue; // exclude non-piece actions
                if (a.ActorsCellId != (ushort)cell) continue;               // only actions from this piece

                // Show only one Move per selected piece unless raw offers requested
                if (a.kind == Game.Core.ActionKind.Move && !UI.hic.config.GiveRawActionOffers)
                {
                    if (moveAddedForCell) continue;
                    moveAddedForCell = true;
                }

                int kind = a.kind;
                string label = UIHelpers.PrettyAction(a);
                int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
                bool legal = UIBridge._mask[i] != 0;
                items.Add(new ActionItem(i.ToString(), label, cost, legal, Array.Empty<int>(), kind));
            }
        }
        UI.hic.pieceActionListFull.Show(items);
    }


}
