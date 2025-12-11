using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

/// <summary>
/// Connector-side utilities shared across OfferProvider/GameActions/GameState.
/// Config index is treated as a 6-bit mask: bit d = 1 means connector on side d, 0 = wall.
/// </summary>
public static class PiecesSides
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsConnectorSide(int configIndex, int dir)
        => ((configIndex >> dir) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OppositeDir(int dir) => (dir + 3) % 6;

    /// <summary>
    /// Returns true if placing a piece of <paramref name="type"/> with the given connector config at <paramref name="cell"/>
    /// does not violate connector-vs-wall adjacency, and (if required) is connected via connectors to a capital.
    /// </summary>
    public static bool IsConnectorPlacementLegal(int cell, byte type, int configIndex, byte playerId, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!PieceDefinition.IsConnectorConfigAllowed(type, configIndex)) return false;

        // Adjacent wall/connector compatibility
        int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
        int n = bm.GetNeighbors(cell, neigh);
        for (int i = 0; i < n; i++)
        {
            int nbCell = neigh[i];
            if (nbCell < 0) continue;
            int nbPid = bm.GetCellOccupant(nbCell);
            if (nbPid < 0) continue;
            byte nbType = bm.GetPieceType(nbPid);
            bool nbHasConn = PieceDefinition.connectors_enabled[nbType];
            int nbConfig = nbHasConn ? bm.pieceConnectorConfig[nbPid] : 0;

            bool ourConn = IsConnectorSide(configIndex, i);
            bool nbConn = nbHasConn ? IsConnectorSide(nbConfig, OppositeDir(i)) : false;

            // Illegal if exactly one side is a connector (connector facing wall)
            if (ourConn != nbConn && (ourConn || nbConn)) return false;
        }

        if (!PieceDefinition.connector_needsCapital[type])
            return true;

        // If this piece is itself a capital, connectivity is satisfied.
        if (PieceDefinition.connector_isCapital[type])
            return true;

        // BFS through connector edges to find any capital.
        return HasPathToCapital(cell, type, configIndex, playerId, gameIndex);
    }

    private static bool HasPathToCapital(int startCell, byte startType, int startConfig, byte playerId, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int cellCount = bm.GetCellCount();
        bool[] visited = new bool[cellCount];
        int[] queue = Scratch.GetScratchCellBuffer(gameIndex);
        int head = 0, tail = 0;

        visited[startCell] = true;
        queue[tail++] = startCell;

        while (head < tail)
        {
            int cell = queue[head++];
            bool isStart = cell == startCell;

            byte type;
            int config;
            int pid = bm.GetCellOccupant(cell);
            if (isStart)
            {
                type = startType;
                config = startConfig;
            }
            else
            {
                if (pid < 0) continue;
                type = bm.GetPieceType(pid);
                if (!PieceDefinition.connectors_enabled[type]) continue;
                config = bm.pieceConnectorConfig[pid];
            }

            if (PieceDefinition.connector_isCapital[type] && bm.GetPieceOwner(pid) == playerId)
                return true;

            int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
            int n = bm.GetNeighbors(cell, neigh);
            for (int d = 0; d < n; d++)
            {
                int nbCell = neigh[d];
                if (nbCell < 0 || visited[nbCell]) continue;
                if (!IsConnectorSide(config, d)) continue; // no connector on this side

                int nbPid = bm.GetCellOccupant(nbCell);
                byte nbType;
                int nbConfig;

                if (nbPid < 0)
                    continue; // empty breaks chain

                nbType = bm.GetPieceType(nbPid);
                if (!PieceDefinition.connectors_enabled[type])
                    continue; // neighbor with no connectors counts as wall

                nbConfig = bm.pieceConnectorConfig[nbPid];
                if (!IsConnectorSide(nbConfig, OppositeDir(d)))
                    continue; // neighbor not connecting back

                visited[nbCell] = true;
                queue[tail++] = nbCell;
            }
        }

        return false;
    }

    /// <summary>
    /// Recompute connector components, assign capital HP, and return a list of pieceIds that must be destroyed
    /// because they require a capital but are disconnected.
    /// </summary>
    public static List<int> RecomputeConnectorComponents(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int pc = bm.pieceCount;
        bool[] visited = new bool[pc];
        List<int> toDestroy = null;

        for (int pid = 0; pid < pc; pid++)
        {
            if (visited[pid]) continue;
            byte t = bm.pieceType[pid];
            if (!PieceDefinition.connectors_enabled[t]) continue;

            // BFS over connector edges
            List<int> comp = new List<int>(8);
            Queue<int> q = new Queue<int>();
            visited[pid] = true;
            q.Enqueue(pid);
            bool hasCapital = false;
            int maxCapHp = 0;

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                comp.Add(cur);
                byte ct = bm.pieceType[cur];
                int cfg = bm.pieceConnectorConfig[cur];
                if (PieceDefinition.connector_isCapital[ct]) hasCapital = true;
                int capHp = PieceDefinition.connector_capitalHealth[ct];
                if (capHp > maxCapHp) maxCapHp = capHp;

                int cell = bm.pieceCellId[cur];
                int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
                int n = bm.GetNeighbors(cell, neigh);
                for (int d = 0; d < n; d++)
                {
                    int nbCell = neigh[d];
                    int nbPid = bm.GetCellOccupant(nbCell);
                    if (nbPid < 0) continue;
                    if (visited[nbPid]) continue;
                    byte nt = bm.pieceType[nbPid];
                    if (!PieceDefinition.connectors_enabled[nt]) continue;
                    int nCfg = bm.pieceConnectorConfig[nbPid];

                    bool ourConn = IsConnectorSide(cfg, d);
                    bool nbConn = IsConnectorSide(nCfg, OppositeDir(d));
                    if (!ourConn || !nbConn) continue;

                    visited[nbPid] = true;
                    q.Enqueue(nbPid);
                }
            }

            int appliedHp = hasCapital ? maxCapHp : 0;
            foreach (int id in comp)
            {
                bm.pieceCapitalHP[id] = appliedHp;
                if (appliedHp == 0 && PieceDefinition.connector_needsCapital[bm.pieceType[id]])
                {
                    toDestroy ??= new List<int>();
                    toDestroy.Add(id);
                }
            }
        }

        return toDestroy;
    }


    // config: 0..63 (aux), returns how many sides are walls.
    public static int CountWalls(int config) //config = Aux
    {
        int connectors = 0;
        for (int d = 0; d < 6; d++)
            connectors += (config >> d) & 1;
        return 6 - connectors;
    }

}
