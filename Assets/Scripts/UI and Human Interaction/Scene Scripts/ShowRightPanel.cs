using UnityEngine;
using System.Collections.Generic;
using System;

public static class ShowRightPanel
{
    // only create Actions
    public static void BuildMeanufilterSubscribe()
    {
        UI.hic.legalButtonFilter.onClick.AddListener(() => BuildMeanuFilter[0] = !BuildMeanuFilter[0]);
        UI.hic.legalButtonFilter.onClick.AddListener(() => PushCreateActionMenu());

        UI.hic.BuildingButtonFilter.onClick.AddListener(() => BuildMeanuFilter[1] = !BuildMeanuFilter[1]);
        UI.hic.BuildingButtonFilter.onClick.AddListener(() => PushCreateActionMenu());

        UI.hic.SolidierButtonFilter.onClick.AddListener(() => BuildMeanuFilter[2] = !BuildMeanuFilter[2]);
        UI.hic.SolidierButtonFilter.onClick.AddListener(() => PushCreateActionMenu());

        UI.hic.BearButtonFilter.onClick.AddListener(() => BuildMeanuFilter[3] = !BuildMeanuFilter[3]);
        UI.hic.BearButtonFilter.onClick.AddListener(() => PushCreateActionMenu());

        UI.hic.PenguinButtonFilter.onClick.AddListener(() => BuildMeanuFilter[4] = !BuildMeanuFilter[4]);
        UI.hic.PenguinButtonFilter.onClick.AddListener(() => PushCreateActionMenu());

        UI.hic.FrogButtonFilter.onClick.AddListener(() => BuildMeanuFilter[5] = !BuildMeanuFilter[5]);
        UI.hic.FrogButtonFilter.onClick.AddListener(() => PushCreateActionMenu());
    }
    public static bool[] BuildMeanuFilter = new bool[6];
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

                if (BuildMeanuFilter[0] && !legal) continue;
                if (BuildMeanuFilter[1] && !Piece.isBuilding[theAction.pieceType]) continue;
                if (BuildMeanuFilter[2] && Piece.isBuilding[theAction.pieceType]) continue;
                if (BuildMeanuFilter[3] && Piece.factionName[theAction.pieceType] != "Bear") continue;
                if (BuildMeanuFilter[4] && Piece.factionName[theAction.pieceType] != "Penguin") continue;
                if (BuildMeanuFilter[5] && Piece.factionName[theAction.pieceType] != "Frog") continue;

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
