using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BoardViewController : MonoBehaviour
{
    readonly HashSet<int> _highlighted = new HashSet<int>();
    [Header("Scene/Hierarchy")]
    public Transform cellRoot;
    public Transform pieceRoot;

    [Header("Prefabs")]
    public PieceView piecePrefab;

    [Header("Toggles")]
    public bool showCellIds = false;
    public bool showPieceHP = true;

    private readonly Dictionary<int, CellView> _cellById = new();
    private readonly List<PieceView> _piecePool = new();
    private readonly Dictionary<int, Sprite> _spriteCache = new();

    private GameSnapshot _snapshot;

    public event Action<int> CellClicked;
    public event Action<bool> ViewRefreshed;

    private void Awake() { IndexCellViews(); }
    private void OnEnable() { ApplyCellIdVisibility(showCellIds); }

    private void IndexCellViews()
    {
        _cellById.Clear();
        if (!cellRoot) cellRoot = transform;
        var cells = cellRoot.GetComponentsInChildren<CellView>(includeInactive: true);
        foreach (var cv in cells)
        {
            if (!_cellById.ContainsKey(cv.cellId))
            {
                cv.Init(this);
                _cellById.Add(cv.cellId, cv);
            }
            else Debug.LogWarning($"Duplicate CellView id={cv.cellId} on {cv.name}");
        }
    }

    public void ApplySnapshot(GameSnapshot s)
    {
        _snapshot = s;
        if (!isActiveAndEnabled || _snapshot == null) { ViewRefreshed?.Invoke(false); return; }

        // IDs
        ApplyCellIdVisibility(showCellIds);

        // --- Clear all cell status labels up front ---
        foreach (var kv in _cellById)
        {
            var cv = kv.Value;
            if (cv) { cv.SetStatusVisible(false); }
        }

        // --- Center VP pool ---
        if (_snapshot.victoryPointCellId >= 0 && _snapshot.victoryPointCellId < _snapshot.cellCount)
        {
            SetCellStatusNumber(_snapshot.victoryPointCellId, _snapshot.centerVP);
        }

        // --- Core HP per player ---
        if (_snapshot.coreCellIdByPlayer != null && _snapshot.coreHPByPlayer != null)
        {
            int n = Mathf.Min(_snapshot.coreCellIdByPlayer.Length, _snapshot.coreHPByPlayer.Length);
            for (int p = 0; p < n; p++)
            {
                int cellId = _snapshot.coreCellIdByPlayer[p];
                if (cellId >= 0 && cellId < _snapshot.cellCount)
                {
                    SetCellStatusNumber(cellId, _snapshot.coreHPByPlayer[p]);
                }
            }
        }


        // Pieces
        EnsurePiecePool(_snapshot.pieceCount);
        for (int i = 0; i < _snapshot.pieceCount; i++)
        {
            var v = _piecePool[i];
            v.pieceIndex = i;
            v.cellId = _snapshot.pieceCellId[i];
            v.owner = _snapshot.pieceOwner[i];
            v.type = _snapshot.pieceType[i];

            var pos = GetCellWorldPos(v.cellId);
            v.SetWorldPosition(pos);

            var sprite = GetOrLoadSprite(v.type);
            v.SetSprite(sprite);
            v.SetTint(SafeOwnerTint(v.owner, _snapshot.ownerTintByPlayer));

            v.SetHP(_snapshot.pieceHP[i], showPieceHP);
            v.SetVisible(true);
        }
        for (int i = _snapshot.pieceCount; i < _piecePool.Count; i++)
            _piecePool[i].SetVisible(false);

        ViewRefreshed?.Invoke(true);
    }

    public void OnCellClicked(int cellId) => CellClicked?.Invoke(cellId);

    public void NotifyCellClicked(int cellId)
    {
        CellClicked?.Invoke(cellId);
    }

    public void SetShowCellIds(bool on)
    {
        showCellIds = on;
        ApplyCellIdVisibility(on);
    }

    public void SetShowPieceHP(bool on)
    {
        showPieceHP = on;
        if (_snapshot == null) return;
        for (int i = 0; i < _piecePool.Count; i++)
        {
            bool active = i < _snapshot.pieceCount && on;
            short hp = (i < _snapshot.pieceCount) ? _snapshot.pieceHP[i] : (short)0;
            _piecePool[i].SetHP(hp, active);
        }
    }

    private void ApplyCellIdVisibility(bool on)
    {
        foreach (var kv in _cellById)
        {
            var cv = kv.Value;
            if (!cv) continue;
            cv.SetIdVisible(on);
            if (on) cv.SetIdText(kv.Key.ToString());
        }
    }

    private void EnsurePiecePool(int target)
    {
        while (_piecePool.Count < target)
        {
            var v = Instantiate(piecePrefab, pieceRoot ? pieceRoot : transform);
            v.Init();
            v.SetVisible(false);
            _piecePool.Add(v);
        }
    }

    private Sprite GetOrLoadSprite(int type)
    {
        if (_spriteCache.TryGetValue(type, out var s) && s != null) return s;
        string path = SafeLookup(_snapshot.spritePathByType, type);
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
    private void SetCellStatusNumber(int cellId, int value)
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
    private Vector3 GetCellWorldPos(int cellId)
    {
        if (_cellById != null && _cellById.TryGetValue(cellId, out var cv) && cv)
            return cv.transform.position; // use scene object position

        Debug.LogWarning($"[BoardView] Missing CellView for cellId={cellId}. Check that all cells are indexed and have valid IDs.");
        return Vector3.zero; // safe neutral position
    }


    public void ClearHighlights()
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

    public void HighlightCells(IEnumerable<int> ids, Color color)
    {
        if (_cellById == null) return;
        foreach (var id in ids)
        {
            if (_cellById.TryGetValue(id, out var cv))
            {
                cv.SetForcedTint(color);
                _highlighted.Add(id);
            }
        }
    }

    public void HighlightSelection(int cellId, Color color)
    {
        if (_cellById != null && _cellById.TryGetValue(cellId, out var cv))
        {
            cv.SetForcedTint(color);
            _highlighted.Add(cellId);
        }
    }

// --- NEW: apply default cell colour everywhere (respecting highlights) ---
    public void ApplyDefaultCellColor(Color c)
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
}
