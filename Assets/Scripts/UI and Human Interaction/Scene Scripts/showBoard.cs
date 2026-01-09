using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public static class showBoard
{
    readonly static HashSet<int> _highlighted = new HashSet<int>();

    private static readonly Dictionary<int, CellView> _cellById = new();
    private static readonly List<PieceView> _piecePool = new();
    private static readonly Dictionary<int, Sprite> _spriteCache = new();


    private static void OnEnable() { ApplyCellIdVisibility(UI.hic.showCellIds); }

    public static void IndexCellViews()
    {
        _cellById.Clear();
        if (!UI.hic.cellRoot) UI.hic.cellRoot = UI.hic.transform;
        var cells = UI.hic.cellRoot.GetComponentsInChildren<CellView>(includeInactive: true);
        foreach (var cv in cells)
        {
            if (!_cellById.ContainsKey(cv.cellId))
            {
                cv.Init();
                _cellById.Add(cv.cellId, cv);
            }
            else Debug.LogWarning($"Duplicate CellView id={cv.cellId} on {cv.name}");
        }
    }



    public static void SetShowCellIds(bool on)
    {
        UI.hic.showCellIds = on;
        ApplyCellIdVisibility(on);
    }

    public static void SetShowPieceHP(bool on)
    {
        UI.hic.showPieceHP = on;
        if (UIBridge._snapshot == null) return;
        for (int i = 0; i < _piecePool.Count; i++)
        {
            bool active = i < UIBridge._snapshot.pieceCount && on;
            short hp = (i < UIBridge._snapshot.pieceCount) ? UIBridge._snapshot.pieceHP[i] : (short)0;
            _piecePool[i].SetHP(hp, active);
        }
    }

    private static void ApplyCellIdVisibility(bool on)
    {
        foreach (var kv in _cellById)
        {
            var cv = kv.Value;
            if (!cv) continue;
            cv.SetIdVisible(on);
            if (on) cv.SetIdText(kv.Key.ToString());
        }
    }

    private static void EnsurePiecePool(int target)
    {
        while (_piecePool.Count < target)
        {
            var v = UnityEngine.Object.Instantiate(UI.hic.piecePrefab, UI.hic.pieceRoot ? UI.hic.pieceRoot : UI.hic.transform);
            v.Init();
            v.SetVisible(false);
            _piecePool.Add(v);
        }
    }

    private static Sprite GetOrLoadSprite(int type)
    {
        if (_spriteCache.TryGetValue(type, out var s) && s != null) return s;
        string path = SafeLookup(UIBridge._snapshot.spritePathByType, type);
        Sprite loaded = null;
        if (!string.IsNullOrEmpty(path)) loaded = Resources.Load<Sprite>(path);
        _spriteCache[type] = loaded;
        return loaded;
    }

    private static string SafeLookup(string[] xs, int i)
        => (xs != null && i >= 0 && i < xs.Length) ? xs[i] : null;

    private static Color SafeOwnerTint(int owner, Color[] palette)
        => (palette != null && owner >= 0 && owner < palette.Length) ? palette[owner] : Color.white;

    // --- New helper: print a number on a cell’s status label (auto-hides on <= 0) ---
    private static void SetCellStatusNumber(int cellId, int value)
    {
        if (!_cellById.TryGetValue(cellId, out var cv) || !cv) return;
        if (value > 0)
        {
            cv.SetStatusText(value.ToString());
            cv.SetStatusVisible(true);
        }
        else
        {
            cv.SetStatusVisible(false);
        }
    }

    //Helper For location to in realttion to the real world cell objects location
    private static Vector3 GetCellWorldPos(int cellId)
    {
        if (_cellById != null && _cellById.TryGetValue(cellId, out var cv) && cv)
            return cv.transform.position; // use scene object position

        Debug.LogWarning($"[BoardView] Missing CellView for cellId={cellId}. Check that all cells are indexed and have valid IDs.");
        return Vector3.zero; // safe neutral position
    }


    public static void ClearHighlights()
    {
        if (_cellById == null) return;
        foreach (var id in _highlighted)
        {
            if (_cellById.TryGetValue(id, out var cv)) cv.SetForcedTint(null);
            // Also prompt the button/proxy to re-apply the default/hover tint immediately
            if (_cellById.TryGetValue(id, out var cv2) && cv2)
            {
                var proxy = cv2.GetComponentInChildren<CellButtonTintProxy>(true);
                if (proxy) proxy.ApplyNow();
            }
        }
        _highlighted.Clear();
    }

    public static void HighlightCells(IEnumerable<int> ids, Color color)
    {
        if (_cellById == null)
        {
            Debug.Log("_cellById == null");
            return;
        }
        foreach (var id in ids)
        {
            if (_cellById.TryGetValue(id, out var cv))
            {
                cv.SetForcedTint(color);
                _highlighted.Add(id);
            }
        }
    }

    public static void HighlightSelection(int cellId, Color color)
    {
        if (_cellById != null && _cellById.TryGetValue(cellId, out var cv))
        {
            cv.SetForcedTint(color);
            _highlighted.Add(cellId);
        }
    }

    // --- NEW: apply default cell colour everywhere (respecting highlights) ---
    public static void ApplyDefaultCellColor(Color c)
    {
        if (_cellById == null) return;
        foreach (var kv in _cellById)
        {
            var cv = kv.Value;
            if (!cv) continue;
            if (!cv.tintForced) // don't override active highlights
            {
                var proxy = cv.GetComponentInChildren<CellButtonTintProxy>(true);
                if (proxy && proxy.sprite) proxy.sprite.color = c;
            }
        }
    }


    public static void ApplySnapshotData(GameSnapshot snapshot)
    {
        UIBridge._snapshot = snapshot;


        // IDs
        showBoard.ApplyCellIdVisibility(UI.hic.showCellIds);

        // --- Clear all cell status labels up front ---
        foreach (var kv in _cellById)
        {
            var cv = kv.Value;
            if (cv) { cv.SetStatusVisible(false); }
        }

        // --- Center VP pool ---
        if (UIBridge._snapshot.victoryPointCellId >= 0 && UIBridge._snapshot.victoryPointCellId < UIBridge._snapshot.cellCount)
        {
            SetCellStatusNumber(UIBridge._snapshot.victoryPointCellId, UIBridge._snapshot.centerVP);
        }

        // --- Core HP per player ---
        if (UIBridge._snapshot.coreCellIdByPlayer != null && UIBridge._snapshot.coreHPByPlayer != null)
        {
            int n = Mathf.Min(UIBridge._snapshot.coreCellIdByPlayer.Length, UIBridge._snapshot.coreHPByPlayer.Length);
            for (int p = 0; p < n; p++)
            {
                int cellId = UIBridge._snapshot.coreCellIdByPlayer[p];
                if (cellId >= 0 && cellId < UIBridge._snapshot.cellCount)
                {
                    SetCellStatusNumber(cellId, UIBridge._snapshot.coreHPByPlayer[p]);
                }
            }
        }



        // ShowLeftPanel.updatePlayerEndRoundTotals((int)ShowLeftPanel.endRoundTotalsPlayer);
        // if (PanelToggles.leftPanelMode == PanelToggles.LeftPanelsModes.EndRoundTotalPanel2) ShowLeftPanel.updatePerTypeEndRoundTotals();


        // Pieces
        EnsurePiecePool(UIBridge._snapshot.pieceCount);
        for (int i = 0; i < UIBridge._snapshot.pieceCount; i++)
        {
            var v = _piecePool[i];
            v.pieceIndex = i;
            v.cellId = UIBridge._snapshot.pieceCellId[i];
            v.owner = UIBridge._snapshot.pieceOwner[i];
            v.type = UIBridge._snapshot.pieceType[i];
            v.wallConfig = UIBridge._snapshot.connector[i];

            var pos = GetCellWorldPos(v.cellId);
            v.SetWorldPosition(pos);

            var sprite = GetOrLoadSprite(v.type);
            v.SetSprite(sprite);
            v.SetTint(SafeOwnerTint(v.owner, UIBridge._snapshot.ownerTintByPlayer));

            v.setWalls();

            v.SetHP(UIBridge._snapshot.pieceHP[i], UI.hic.showPieceHP);
            v.SetVisible(true);

            if (UI.hic.logConnectorMasks)
            {
                string mask = v.wallConfig.HasValue
                    ? Convert.ToString(v.wallConfig.Value, 2).PadLeft(6, '0')
                    : "null";
                Debug.Log($"[BoardView] Piece #{i} type={v.type} owner={v.owner} cell={v.cellId} connectorMask={mask}");
            }
        }
        for (int i = UIBridge._snapshot.pieceCount; i < _piecePool.Count; i++)
            _piecePool[i].SetVisible(false);
    }


}
