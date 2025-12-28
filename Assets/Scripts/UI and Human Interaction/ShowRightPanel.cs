using UnityEngine;
using System.Collections.Generic;
using System;

public static class ShowRightPanel
{
    // only create Actions
    public static void PushCreateActionMenu()
    {
        // Build list shows all Create* actions this turn (grouping/labeling by piece type + quoted cost)
        var items = new List<Game.Core.Action>(UIBridge._count);
        var uiInfo = new List<UIInfo>(UIBridge._count);
        if (UIBridge._count > 0)
        {
            for (int i = 0; i < UIBridge._count; i++)
            {
                var theAction = UIBridge._offers[i];
                if (theAction.kind != Game.Core.ActionKind.Create) continue;

                int fullCost = Mathf.RoundToInt(UIBridge._quoted[i]);
                bool legal = UIBridge._mask[i] != 0;           // 1 = affordable+legal; 0 = masked out by cost, etc. :contentReference[oaicite:8]{index=8}

                items.Add(theAction);
                uiInfo.Add(new UIInfo(legal, fullCost));
            }
        }
        UI.hic.buildMenu.Show(items, uiInfo, UI.hic.config);
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
}
