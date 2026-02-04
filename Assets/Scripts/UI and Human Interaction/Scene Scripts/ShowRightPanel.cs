using UnityEngine;
using System.Collections.Generic;
using System;
using static Game.Core.ActionKind;


public static class ShowRightPanel
{
    // only create Actions
    
    
    

    public static void PushEndTurn()
    {
        var items = new List<ActionItem>();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var theAction = UIBridge._offers[i];
            if (theAction.kind != Game.Core.ActionKind.EndTurn) continue; // future: add more non-piece kinds here
            int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
            bool legal = UIBridge._mask[i] != 0;
            int kind = theAction.kind;
            items.Add(new ActionItem(i.ToString(), UIHelpers.PrettyAction(theAction), cost, legal, Array.Empty<int>(), kind));
        }
        UI.hic.nonPieceActionList.Show(items);
    }


}
