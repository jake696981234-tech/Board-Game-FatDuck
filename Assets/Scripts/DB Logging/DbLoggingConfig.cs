using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient; // System.Data.SqlClient contains SqlConnection/SqlBulkCopy
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEngine;



public static class DbLoggingConfig
{

    //Values you must change for each simulation (can be overridden via Config)
    public static int inputSimID = 4;
    public static string inputSimName = "Does it work this way though?";
    public static string inputRuleVersion = "v1";
    public static string inputNotes = "No notes";

    //Preference values
    public static bool enabled = true;


    //probably never change values
    private readonly static string SqlConnection = "Server=localhost;Database=GameSim;Trusted_Connection=True;TrustServerCertificate=True";

    //Values that are a manual copy out of somewhere else- when adding/changing content double check if these are right (preferably change these to auto align)
    // Hard Coded- subject to break code if the system is changed.
    private readonly static int moveID = 1;
    private readonly static int shootID = 2;
    private readonly static int captureVPID = 3;
    private readonly static int coreDamageID = 4;
    private readonly static int createID = 5;
    private readonly static int endTurnID = 6;

    private readonly static string moveString = "move";
    private readonly static string shootString = "shoot";
    private readonly static string captureVPString = "CaptureVP";
    private readonly static string coreDamageString = "CoreDamage";
    private readonly static string createString = "Create";
    private readonly static string endTurnString = "EndTurn";

    private readonly static string wonByEndVp = "Won from end of turn VP";
    private readonly static string wonByElimination = "Won from elimnation";
    private readonly static string tie = "Tie";
    private readonly static string placeholder = "placeholder";


    //SK values seeded on bootstrap


    private static int skForMoveAction;
    private static int skForShootAction;
    private static int skForCaptureVPAction;
    private static int skForCoreDamageAction;
    private static int skForCreateAction;
    private static int skForEndTurnAction;

    // Cache of pieceSK by pieceID for the current sim (populated during ImportAndLogDimPiece)
    private static readonly Dictionary<int, int> pieceSKByPieceID = new Dictionary<int, int>();

    // Resolve gameplay pieceID to DimPiece.pieceSK for current sim. Returns null if unknown.
    public static int? ResolvePieceSKForCurrentSim(int? pieceId)
    {
        if (!pieceId.HasValue)
            return null;

        int id = pieceId.Value;
        if (pieceSKByPieceID.TryGetValue(id, out var cached))
            return cached;

        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "SELECT TOP 1 pieceSK FROM dbo.DimPiece WHERE simID = @simID AND pieceID = @pieceID ORDER BY pieceSK DESC",
            conn);
        cmd.Parameters.AddWithValue("@simID", inputSimID);
        cmd.Parameters.AddWithValue("@pieceID", id);
        conn.Open();
        object obj = cmd.ExecuteScalar();
        if (obj == null || obj == DBNull.Value)
            return null;

        int pieceSk = Convert.ToInt32(obj);
        pieceSKByPieceID[id] = pieceSk;
        return pieceSk;
    }



    private static int playerOneSK;
    private static int playerTwoSK;
    private static int playerThreeSK;
    private static int playerFourSK;

    public static int wonByEndVpSK;
    public static int wonByEliminationSK;
    public static int tieSK;



    //SK values changed during runtime

    private static int latestGameSK;
    private static int latestRoundSK;
    private static int latestTurnSK;
    private static long latestActionSK;

    //ordinals
    private static int gameOrdinal = 0;
    private static int actionOrdinal = 0;
    private static int roundOrdinalUp = 0; // counts rounds 1..N per game

    //Local Values being seeded

    private static GameConfigHub Hub;

    private static int inputMaxRounds;

    private static float startingBudget;

    private static float TurnBudgetDecrease;

    public static void InitializeLoggingValues(in GameConfigHub hub)
    {
        Hub = hub;
        TurnBudgetDecrease = Hub.match_startOfTurnBudgetDecrease;
        startingBudget = Hub.match_startingBudgetPerPlayer;
        inputMaxRounds = Hub.match_numberOfRounds;
    }

    public static void ApplyConfig(in Config.DbLoggingAuthoring cfg)
    {
        enabled = cfg.enabled;
        inputSimID = cfg.simID;
        inputSimName = string.IsNullOrEmpty(cfg.simName) ? inputSimName : cfg.simName;
        inputRuleVersion = string.IsNullOrEmpty(cfg.ruleVersion) ? inputRuleVersion : cfg.ruleVersion;
        inputNotes = string.IsNullOrEmpty(cfg.notes) ? inputNotes : cfg.notes;
        if (cfg.batchSize > 0) BatchSize = cfg.batchSize;
        if (cfg.flushIntervalMs > 0) FlushIntervalMs = cfg.flushIntervalMs;
        if (cfg.maxQueue > 0) MaxQueue = cfg.maxQueue;
        if (cfg.maxRetries >= 0) MaxRetries = cfg.maxRetries;
        if (cfg.retryBackoffMs >= 0) RetryBackoffMs = cfg.retryBackoffMs;
    }

    // -------------------- Shared connection + prepared commands --------------------
    private static SqlConnection _sharedConn;
    private static SqlTransaction _sharedTx;
    private static bool _sessionActive = false;

    // Prepared commands (created on StartLoggingSession)
    private static SqlCommand _cmdFactAction;
    private static SqlCommand _cmdInsertRound;
    private static SqlCommand _cmdInsertTurn;
    // Async batching for FactAction
    private static readonly bool BatchFactActions = true;
    private static int BatchSize = 200;
    private static int FlushIntervalMs = 250;
    private static int MaxQueue = 10000;
    private static int MaxRetries = 3;
    private static int RetryBackoffMs = 250;

    private struct FactActionRow
    {
        public int turnSK, actionTypeSK, actingPlayerSK, actionSeq;
        public int? pieceSK, targetPlayerSK;
        public decimal actionCost, buildCost, surchargeCost, totalCost;
    }
    private static readonly ConcurrentQueue<FactActionRow> _faQueue = new ConcurrentQueue<FactActionRow>();
    private static CancellationTokenSource _flushCts;
    private static Task _flushTask;

    // Metrics
    private static long _mEnqueued;
    private static long _mDropped;
    private static long _mFlushed;
    private static long _mFlushErrors;
    private static long _mRetryAttempts;
    private static long _mFlushCount;
    private static long _mTotalFlushMs;

    public static void StartLoggingSession(bool transactional = false)
    {
        if (_sessionActive) return;
        _sharedConn = new SqlConnection(SqlConnection);
        _sharedConn.Open();
        _sharedTx = transactional ? _sharedConn.BeginTransaction() : null;

        // Prepare FactAction command
        _cmdFactAction = new SqlCommand(
            "INSERT INTO dbo.FactAction (turnSK, actionTypeSK, pieceSK, actingPlayerSK, targetPlayerSK, actionSeq, actionCost, buildCost, surchargeCost, totalCost) OUTPUT INSERTED.actionID VALUES (@turnSK, @actionTypeSK, @pieceSK, @actingPlayerSK, @targetPlayerSK, @actionSeq, @actionCost, @buildCost, @surchargeCost, @totalCost);",
            _sharedConn, _sharedTx);
        _cmdFactAction.Parameters.Add("@turnSK", SqlDbType.Int);
        _cmdFactAction.Parameters.Add("@actionTypeSK", SqlDbType.Int);
        _cmdFactAction.Parameters.Add("@pieceSK", SqlDbType.Int);
        _cmdFactAction.Parameters.Add("@actingPlayerSK", SqlDbType.Int);
        _cmdFactAction.Parameters.Add("@targetPlayerSK", SqlDbType.Int);
        _cmdFactAction.Parameters.Add("@actionSeq", SqlDbType.Int);
        var pAC = _cmdFactAction.Parameters.Add("@actionCost", SqlDbType.Decimal); pAC.Precision = 19; pAC.Scale = 4;
        var pBC = _cmdFactAction.Parameters.Add("@buildCost", SqlDbType.Decimal); pBC.Precision = 19; pBC.Scale = 4;
        var pSC = _cmdFactAction.Parameters.Add("@surchargeCost", SqlDbType.Decimal); pSC.Precision = 19; pSC.Scale = 4;
        var pTC = _cmdFactAction.Parameters.Add("@totalCost", SqlDbType.Decimal); pTC.Precision = 19; pTC.Scale = 4;

        // Prepare DimRound insert
        _cmdInsertRound = new SqlCommand(
            "INSERT INTO dbo.DimRound (gameSK, roundOrdinal) OUTPUT INSERTED.roundSK VALUES (@gameSK, @roundOrdinal);",
            _sharedConn, _sharedTx);
        _cmdInsertRound.Parameters.Add("@gameSK", SqlDbType.Int);
        _cmdInsertRound.Parameters.Add("@roundOrdinal", SqlDbType.Int);

        // Prepare DimTurn insert
        _cmdInsertTurn = new SqlCommand(
            "INSERT INTO dbo.DimTurn (roundSK) OUTPUT INSERTED.turnSK VALUES (@roundSK);",
            _sharedConn, _sharedTx);
        _cmdInsertTurn.Parameters.Add("@roundSK", SqlDbType.Int);

        _sessionActive = true;

        // Reset metrics
        Interlocked.Exchange(ref _mEnqueued, 0);
        Interlocked.Exchange(ref _mDropped, 0);
        Interlocked.Exchange(ref _mFlushed, 0);
        Interlocked.Exchange(ref _mFlushErrors, 0);
        Interlocked.Exchange(ref _mRetryAttempts, 0);
        Interlocked.Exchange(ref _mFlushCount, 0);
        Interlocked.Exchange(ref _mTotalFlushMs, 0);

        // Start background flusher for FactAction if enabled
        if (BatchFactActions && (_flushTask == null || _flushTask.IsCompleted))
        {
            _flushCts = new CancellationTokenSource();
            _flushTask = Task.Run(() => FlushLoop(_flushCts.Token));
        }
    }

    public static void EndLoggingSession(bool commit = true)
    {
        if (!_sessionActive) return;
        try
        {
            if (_sharedTx != null)
            {
                if (commit) _sharedTx.Commit(); else _sharedTx.Rollback();
            }
        }
        finally
        {
            // Stop background flusher
            if (_flushCts != null)
            {
                try { _flushCts.Cancel(); _flushTask?.Wait(1000); } catch { }
                _flushTask = null; _flushCts.Dispose(); _flushCts = null;
            }
            // Emit metrics summary
            if (enabled)
            {
                long enq = Interlocked.Read(ref _mEnqueued);
                long drp = Interlocked.Read(ref _mDropped);
                long fls = Interlocked.Read(ref _mFlushed);
                long err = Interlocked.Read(ref _mFlushErrors);
                long rty = Interlocked.Read(ref _mRetryAttempts);
                long cnt = Interlocked.Read(ref _mFlushCount);
                long ms = Interlocked.Read(ref _mTotalFlushMs);
                if (enq + fls + err + rty + cnt > 0)
                {
                    UnityEngine.Debug.Log($"[DBLog] enqueued={enq}, flushed={fls}, dropped={drp}, flushes={cnt}, totalMs={ms}, avgMs={(cnt > 0 ? (ms / (double)cnt) : 0):F2}, flushErrors={err}, retryAttempts={rty}");
                }
            }
            _cmdFactAction?.Dispose(); _cmdFactAction = null;
            _cmdInsertRound?.Dispose(); _cmdInsertRound = null;
            _cmdInsertTurn?.Dispose(); _cmdInsertTurn = null;
            _sharedTx?.Dispose(); _sharedTx = null;
            _sharedConn?.Close(); _sharedConn?.Dispose(); _sharedConn = null;
            _sessionActive = false;
        }
    }

    private static async Task FlushLoop(CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(FlushIntervalMs);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { break; }
            FlushFactActions(BatchSize);
        }
        // final drain
        FlushFactActions(int.MaxValue);
    }

    private static void FlushFactActions(int max)
    {
        if (!BatchFactActions) return;
        // Fast check
        if (_faQueue.IsEmpty) return;

        // Build DataTable
        var dt = new DataTable();
        dt.Columns.Add("turnSK", typeof(int));
        dt.Columns.Add("actionTypeSK", typeof(int));
        dt.Columns.Add("pieceSK", typeof(int));
        dt.Columns.Add("actingPlayerSK", typeof(int));
        dt.Columns.Add("targetPlayerSK", typeof(int));
        dt.Columns.Add("actionSeq", typeof(int));
        dt.Columns.Add("actionCost", typeof(decimal));
        dt.Columns.Add("buildCost", typeof(decimal));
        dt.Columns.Add("surchargeCost", typeof(decimal));
        dt.Columns.Add("totalCost", typeof(decimal));

        int taken = 0;
        while (taken < max && _faQueue.TryDequeue(out var row))
        {
            var dr = dt.NewRow();
            dr["turnSK"] = row.turnSK;
            dr["actionTypeSK"] = row.actionTypeSK;
            dr["pieceSK"] = (object)row.pieceSK ?? DBNull.Value;
            dr["actingPlayerSK"] = row.actingPlayerSK;
            dr["targetPlayerSK"] = (object)row.targetPlayerSK ?? DBNull.Value;
            dr["actionSeq"] = row.actionSeq;
            dr["actionCost"] = row.actionCost;
            dr["buildCost"] = row.buildCost;
            dr["surchargeCost"] = row.surchargeCost;
            dr["totalCost"] = row.totalCost;
            dt.Rows.Add(dr);
            taken++;
        }
        if (dt.Rows.Count == 0) return;

        int attempts = 0;
        while (true)
        {
            try
            {
                using var conn = new SqlConnection(SqlConnection);
                conn.Open();
                using var bulk = new SqlBulkCopy(conn)
                {
                    DestinationTableName = "dbo.FactAction"
                };
                var sw = Stopwatch.StartNew();
                bulk.ColumnMappings.Add("turnSK", "turnSK");
                bulk.ColumnMappings.Add("actionTypeSK", "actionTypeSK");
                bulk.ColumnMappings.Add("pieceSK", "pieceSK");
                bulk.ColumnMappings.Add("actingPlayerSK", "actingPlayerSK");
                bulk.ColumnMappings.Add("targetPlayerSK", "targetPlayerSK");
                bulk.ColumnMappings.Add("actionSeq", "actionSeq");
                bulk.ColumnMappings.Add("actionCost", "actionCost");
                bulk.ColumnMappings.Add("buildCost", "buildCost");
                bulk.ColumnMappings.Add("surchargeCost", "surchargeCost");
                bulk.ColumnMappings.Add("totalCost", "totalCost");
                bulk.WriteToServer(dt);
                sw.Stop();
                Interlocked.Add(ref _mFlushed, dt.Rows.Count);
                Interlocked.Increment(ref _mFlushCount);
                Interlocked.Add(ref _mTotalFlushMs, sw.ElapsedMilliseconds);
                break;
            }
            catch (Exception)
            {
                attempts++;
                Interlocked.Increment(ref _mFlushErrors);
                Interlocked.Increment(ref _mRetryAttempts);
                if (attempts >= MaxRetries) break;
                Thread.Sleep(RetryBackoffMs * attempts);
            }
        }
    }


    // Legacy helpers removed; use OUTPUT + prepared commands






    //Tables methods!
    public static void SimVersion(int simID, string simName, string ruleVersion, string notes, float maxRounds, float startingBudget, float startOfTurnDeduction)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.DimSim(SimID, SimName, RuleVersion, Notes, MaxRounds, StartingBudget, startOfTurnDeduction) VALUES (@simID, @simName, @ruleVersion, @notes, @maxRounds, @startingBudget, @startOfTurnDeduction);",
            conn);
        cmd.Parameters.AddWithValue("@simID", simID);
        cmd.Parameters.AddWithValue("@simName", simName);
        cmd.Parameters.AddWithValue("@ruleVersion", ruleVersion);
        cmd.Parameters.AddWithValue("@notes", notes);
        cmd.Parameters.AddWithValue("@maxRounds", maxRounds);
        cmd.Parameters.AddWithValue("@startingBudget", startingBudget);
        cmd.Parameters.AddWithValue("@startOfTurnDeduction", startOfTurnDeduction);
        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public static int actionTypeVersion(int simID, int actionTypeID, string actionName, string category, bool isBuild, bool affectsCore, bool needsPiece)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.DimActionType(simID, actionTypeID, actionName, category, isBuild, affectsCore, needsPiece) " +
            "OUTPUT INSERTED.actionTypeSK VALUES (@simID, @actionTypeID, @actionName, @category, @isBuild, @affectsCore, @needsPiece);",
            conn);
        cmd.Parameters.AddWithValue("@simID", simID);
        cmd.Parameters.AddWithValue("@actionTypeID", actionTypeID);
        cmd.Parameters.AddWithValue("@actionName", actionName);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@isBuild", isBuild);
        cmd.Parameters.AddWithValue("@affectsCore", affectsCore);
        cmd.Parameters.AddWithValue("@needsPiece", needsPiece);
        conn.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }





    public static void ImportAndLogDimPiece(string csvPath, int simID)
    {
        using var conn = new SqlConnection(SqlConnection);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using var cmd = new SqlCommand(
            // Return the generated pieceSK so we can cache it by pieceID
            "INSERT INTO dbo.DimPiece(simID, pieceID, pieceName, isBuilding, faction, baseCost) " +
            "OUTPUT INSERTED.pieceSK " +
            "VALUES (@simID, @pieceID, @pieceName, @isBuilding, @faction, @baseCost);",
            conn, tx);

        var pSimID = cmd.Parameters.Add("@simID", SqlDbType.Int);
        var pPieceID = cmd.Parameters.Add("@pieceID", SqlDbType.Int);
        var pPieceName = cmd.Parameters.Add("@pieceName", SqlDbType.VarChar, 100);
        var pIsBuilding = cmd.Parameters.Add("@isBuilding", SqlDbType.Bit);
        var pFactionName = cmd.Parameters.Add("@faction", SqlDbType.VarChar, 50);
        var pBaseCost = cmd.Parameters.Add("@baseCost", SqlDbType.Decimal);
        pBaseCost.Precision = 19; pBaseCost.Scale = 4;

        pSimID.Value = simID;


        var pcs = PiecesCsvImporter.Import(csvPath, onTypeDefined: (pieceID, pieceName, isBuilding, faction, buildCost) =>
        {
            pPieceID.Value = pieceID;
            pPieceName.Value = pieceName ?? (object)DBNull.Value;
            pIsBuilding.Value = isBuilding;
            pFactionName.Value = string.IsNullOrEmpty(faction) ? (object)DBNull.Value : faction;
            pBaseCost.Value = buildCost == 0 ? (object)DBNull.Value : buildCost; // adjust if 0 is valid

            // Capture the new pieceSK and cache it by pieceID
            object skObj = cmd.ExecuteScalar();
            int newPieceSK = Convert.ToInt32(skObj);
            pieceSKByPieceID[pieceID] = newPieceSK;
        });

        tx.Commit();
    }




    public static int playerVersion(
        int simID,
        int playerID,
        object playerName,
        object policyName,
        object isHuman)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.DimPlayer (simID, playerID, playerName, policyName, isHuman) " +
            "OUTPUT INSERTED.playerSK VALUES (@simID, @playerID, @playerName, @policyName, @isHuman);",
            conn);
        cmd.Parameters.AddWithValue("@simID", simID);
        cmd.Parameters.AddWithValue("@playerID", playerID);
        cmd.Parameters.AddWithValue("@playerName", playerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@policyName", policyName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isHuman", isHuman ?? DBNull.Value);
        conn.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }


    public static int winTypeVersion(
        int simID,
        string winName,
        object winCategory)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.DimWinType (simID, winName, winCategory) " +
            "OUTPUT INSERTED.winTypeSK VALUES (@simID, @winName, @winCategory);",
            conn);
        cmd.Parameters.AddWithValue("@simID", simID);
        cmd.Parameters.AddWithValue("@winName", winName);
        cmd.Parameters.AddWithValue("@winCategory", winCategory ?? DBNull.Value);
        conn.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static int prepDimGameVersion(int simID, int gameOrdinal)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.DimGame (simID, gameOrdinal) OUTPUT INSERTED.gameSK VALUES (@simID, @gameOrdinal);",
            conn);
        cmd.Parameters.AddWithValue("@simID", simID);
        cmd.Parameters.AddWithValue("@gameOrdinal", gameOrdinal);
        conn.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }


    public static void dimGameVersion(int gameSK, object winnerPlayerSK, object winTypeSK)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "UPDATE dbo.DimGame SET winnerPlayerSK=@winnerPlayerSK, winTypeSK=@winTypeSK WHERE gameSK=@gameSK;",
            conn);
        cmd.Parameters.AddWithValue("@gameSK", gameSK);
        cmd.Parameters.AddWithValue("@winnerPlayerSK", winnerPlayerSK ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@winTypeSK", winTypeSK ?? DBNull.Value);
        conn.Open();
        cmd.ExecuteNonQuery();
    }


    // TO DO DimRound
    public static int roundVersion(
        int gameSK,
        int roundOrdinal)
    {
        if (_sessionActive && _cmdInsertRound != null)
        {
            _cmdInsertRound.Parameters["@gameSK"].Value = gameSK;
            _cmdInsertRound.Parameters["@roundOrdinal"].Value = roundOrdinal;
            object sk = _cmdInsertRound.ExecuteScalar();
            return Convert.ToInt32(sk);
        }
        else
        {
            using var conn = new SqlConnection(SqlConnection);
            using var cmd = new SqlCommand(
                "INSERT INTO dbo.DimRound (gameSK, roundOrdinal) OUTPUT INSERTED.roundSK VALUES (@gameSK, @roundOrdinal);",
                conn);
            cmd.Parameters.AddWithValue("@gameSK", gameSK);
            cmd.Parameters.AddWithValue("@roundOrdinal", roundOrdinal);
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public static int prepTurnVersion(int RoundSK)
    {
        if (_sessionActive && _cmdInsertTurn != null)
        {
            _cmdInsertTurn.Parameters["@roundSK"].Value = RoundSK;
            object sk = _cmdInsertTurn.ExecuteScalar();
            return Convert.ToInt32(sk);
        }
        else
        {
            using var conn = new SqlConnection(SqlConnection);
            using var cmd = new SqlCommand(
                "INSERT INTO dbo.DimTurn (roundSK) OUTPUT INSERTED.turnSK VALUES (@roundSK);",
                conn);
            cmd.Parameters.AddWithValue("@roundSK", RoundSK);
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }



    // TO DO DimTurn
    public static void turnVersion(
    int TurnSK,
    int playerSK,
    int turnOrdinal,
    bool isPassOnly,
    int coreHealthEnd,
    int victoryPointsEnd,
    object resourceTotalEnd,
    object digitsStart,
    object digitsEnd,
    object piecesOnBoardStart,
    object piecesOnBoardEnd,
    int playerTurnOrdinal)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(
            "UPDATE dbo.DimTurn SET playerSK=@playerSK, turnOrdinal=@turnOrdinal, isPassOnly=@isPassOnly, coreHealthEnd=@coreHealthEnd, victoryPointsEnd=@victoryPointsEnd, resourceTotalEnd=@resourceTotalEnd, digitsStart=@digitsStart, digitsEnd=@digitsEnd, piecesOnBoardStart=@piecesOnBoardStart, piecesOnBoardEnd=@piecesOnBoardEnd, playerTurnOrdinal=@playerTurnOrdinal WHERE turnSK=@turnSK;",
            conn);
        cmd.Parameters.AddWithValue("@turnSK", TurnSK);
        cmd.Parameters.AddWithValue("@playerSK", playerSK);
        cmd.Parameters.AddWithValue("@turnOrdinal", turnOrdinal);
        cmd.Parameters.AddWithValue("@isPassOnly", isPassOnly);
        cmd.Parameters.AddWithValue("@coreHealthEnd", coreHealthEnd);
        cmd.Parameters.AddWithValue("@victoryPointsEnd", victoryPointsEnd);
        cmd.Parameters.AddWithValue("@resourceTotalEnd", resourceTotalEnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@digitsStart", digitsStart ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@digitsEnd", digitsEnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@piecesOnBoardStart", piecesOnBoardStart ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@piecesOnBoardEnd", piecesOnBoardEnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@playerTurnOrdinal", playerTurnOrdinal);
        conn.Open();
        cmd.ExecuteNonQuery();
    }



    // TO DO FactAction
    public static long FactAction(
        int turnSK,
        int actionTypeSK,
        object pieceSK,
        int actingPlayerSK,
        object targetPlayerSK,
        int actionSeq,
        decimal? actionCost,
        decimal? buildCost,
        decimal? surchargeCost,
        decimal? totalCost)
    {
        if (_sessionActive && _cmdFactAction != null)
        {
            _cmdFactAction.Parameters["@turnSK"].Value = turnSK;
            _cmdFactAction.Parameters["@actionTypeSK"].Value = actionTypeSK;
            _cmdFactAction.Parameters["@pieceSK"].Value = pieceSK ?? DBNull.Value;
            _cmdFactAction.Parameters["@actingPlayerSK"].Value = actingPlayerSK;
            _cmdFactAction.Parameters["@targetPlayerSK"].Value = targetPlayerSK ?? DBNull.Value;
            _cmdFactAction.Parameters["@actionSeq"].Value = actionSeq;
            _cmdFactAction.Parameters["@actionCost"].Value = actionCost;
            _cmdFactAction.Parameters["@buildCost"].Value = buildCost;
            _cmdFactAction.Parameters["@surchargeCost"].Value = surchargeCost;
            _cmdFactAction.Parameters["@totalCost"].Value = totalCost;
            object id = _cmdFactAction.ExecuteScalar();
            return Convert.ToInt64(id);
        }
        else
        {
            using var conn = new SqlConnection(SqlConnection);
            using var cmd = new SqlCommand(
                "INSERT INTO dbo.FactAction (turnSK, actionTypeSK, pieceSK, actingPlayerSK, targetPlayerSK, actionSeq, actionCost, buildCost, surchargeCost, totalCost) OUTPUT INSERTED.actionID VALUES (@turnSK, @actionTypeSK, @pieceSK, @actingPlayerSK, @targetPlayerSK, @actionSeq, @actionCost, @buildCost, @surchargeCost, @totalCost);",
                conn);
            cmd.Parameters.AddWithValue("@turnSK", turnSK);
            cmd.Parameters.AddWithValue("@actionTypeSK", actionTypeSK);
            cmd.Parameters.AddWithValue("@pieceSK", pieceSK ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@actingPlayerSK", actingPlayerSK);
            cmd.Parameters.AddWithValue("@targetPlayerSK", targetPlayerSK ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@actionSeq", actionSeq);
            cmd.Parameters.AddWithValue("@actionCost", actionCost);
            cmd.Parameters.AddWithValue("@buildCost", buildCost);
            cmd.Parameters.AddWithValue("@surchargeCost", surchargeCost);
            cmd.Parameters.AddWithValue("@totalCost", totalCost);
            conn.Open();
            object id = cmd.ExecuteScalar();
            return Convert.ToInt64(id);
        }
    }


    // FactActionPieceLoss removed (unused). Consider TVP batch when needed.


    // FactRoundPlayer logging removed for now (unused)










    //Logging in Order of grain

    public static void DeleteConflictingSimIdRows()
    {
        using (var connection = new SqlConnection(SqlConnection))
        {
            using (var command = new SqlCommand("dbo.sp_reset_sim_by_id", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@simID", inputSimID);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }


    public static void logDimSim()
    {
        SimVersion(inputSimID, inputSimName, inputRuleVersion, inputNotes, inputMaxRounds, startingBudget, TurnBudgetDecrease);
    }


    public static void logDimActionType()
    {
        skForMoveAction = actionTypeVersion(inputSimID, moveID, moveString, placeholder, false, false, true);
        skForShootAction = actionTypeVersion(inputSimID, shootID, shootString, placeholder, false, false, true);
        skForCaptureVPAction = actionTypeVersion(inputSimID, captureVPID, captureVPString, placeholder, false, false, true);
        skForCoreDamageAction = actionTypeVersion(inputSimID, coreDamageID, coreDamageString, placeholder, false, true, true);
        skForCreateAction = actionTypeVersion(inputSimID, createID, createString, placeholder, true, false, false);
        skForEndTurnAction = actionTypeVersion(inputSimID, endTurnID, endTurnString, placeholder, false, false, false);
    }


    public static void logDimPiece()
    {
        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");

        ImportAndLogDimPiece(csvPath, inputSimID);
    }

    public static void logPlayerVersion()
    {
        playerOneSK = playerVersion(inputSimID, 1, "Mathew", placeholder, false);
        playerTwoSK = playerVersion(inputSimID, 2, "Mark", placeholder, false);
        playerThreeSK = playerVersion(inputSimID, 3, "Luke", placeholder, false);
        playerFourSK = playerVersion(inputSimID, 4, "John", placeholder, false);
    }

    public static void logWinTypeVersion()
    {
        wonByEndVpSK = winTypeVersion(inputSimID, wonByEndVp, placeholder);
        wonByEliminationSK = winTypeVersion(inputSimID, wonByElimination, placeholder);
        tieSK = winTypeVersion(inputSimID, tie, placeholder);
    }

    public static void prepDimGame()
    {
        gameOrdinal++;
        roundOrdinalUp = 0; // reset round counter at new game start
        latestGameSK = prepDimGameVersion(inputSimID, gameOrdinal);
    }


    public static void logDimGame(int winTypeSK, int? winnerPlayerIndex)
    {
        // Map winner index (0..3) to the corresponding Player SK; null => tie/no single winner
        int? winnerPlayerSK = winnerPlayerIndex switch
        {
            0 => playerOneSK,
            1 => playerTwoSK,
            2 => playerThreeSK,
            3 => playerFourSK,
            _ => (int?)null
        };

        // dimGameVersion signature is (gameSK, winnerPlayerSK, winTypeSK)
        dimGameVersion(
            latestGameSK,
            winnerPlayerSK.HasValue ? (object)winnerPlayerSK.Value : DBNull.Value,
            winTypeSK
        );

        // Game ended; prepare for next game by resetting round ordinal
        roundOrdinalUp = 0;
    }



    public static void logRoundVersion()
    {
        // Ignore RoundsLeft; log ascending round ordinal starting at 1
        roundOrdinalUp++;
        latestRoundSK = roundVersion(latestGameSK, roundOrdinalUp);
    }


    public static void prepTurn()
    {
        latestTurnSK = prepTurnVersion(latestRoundSK);
    }




    public static void logturnVersion(
    int whichPlayer,          // 0..3
    int turnOrdinal,          // per-player ordinal (1,2,3…)
    bool isPassOnly,
    int coreHealthEnd,
    int victoryPointsEnd,
    decimal? resourceTotalEnd,
    int? digitsStart,
    int? digitsEnd,
    int? piecesOnBoardStart,
    int? piecesOnBoardEnd,
    int playerTurnOrdinal)
    {
        actionOrdinal = 0;

        int playerSK = whichPlayer switch
        {
            0 => playerOneSK,
            1 => playerTwoSK,
            2 => playerThreeSK,
            3 => playerFourSK,
            _ => throw new ArgumentOutOfRangeException(nameof(whichPlayer), "Player index must be 0..3.")
        };


        turnVersion(
        latestTurnSK,
        playerSK,
        turnOrdinal,
        isPassOnly,
        coreHealthEnd,
        victoryPointsEnd,
        resourceTotalEnd,      // nullable -> handled inside turnVersion
        digitsStart,
        digitsEnd,
        piecesOnBoardStart,
        piecesOnBoardEnd,
        playerTurnOrdinal
    );

    }







    public static void logAction(int actionType, int? piece, int? targetPlayer, int thePlayer, decimal actionCost, decimal? buildCost, decimal? surchargeCost)
    {
        actionOrdinal++;
        int typeAction = actionType switch
        {
            0 => skForMoveAction,
            1 => skForShootAction,
            2 => skForCaptureVPAction,
            3 => skForCoreDamageAction,
            4 => skForCreateAction,
            5 => skForEndTurnAction,
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), "Actiontype must be 0..5.")
        };

        int playerSK = thePlayer switch
        {
            0 => playerOneSK,
            1 => playerTwoSK,
            2 => playerThreeSK,
            3 => playerFourSK,
            _ => throw new ArgumentOutOfRangeException(nameof(thePlayer), "Player index must be 0..3.")
        };

        int? TargetPlayer = targetPlayer switch
        {
            0 => playerOneSK,
            1 => playerTwoSK,
            2 => playerThreeSK,
            3 => playerFourSK,
            _ => (int?)null
        };


        var build = buildCost ?? 0m;
        var surcharge = surchargeCost ?? 0m;
        var total = actionCost + build + surcharge;

        // Convert gameplay pieceID to pieceSK (nullable) for logging
        int? pieceSK = ResolvePieceSKForCurrentSim(piece);

        if (BatchFactActions)
        {
            // backpressure: if queue is too large, drop oldest one
            while (_faQueue.Count >= MaxQueue && _faQueue.TryDequeue(out _)) { Interlocked.Increment(ref _mDropped); }
            _faQueue.Enqueue(new FactActionRow
            {
                turnSK = latestTurnSK,
                actionTypeSK = typeAction,
                pieceSK = pieceSK,
                actingPlayerSK = playerSK,
                targetPlayerSK = TargetPlayer,
                actionSeq = actionOrdinal,
                actionCost = actionCost,
                buildCost = build,
                surchargeCost = surcharge,
                totalCost = total
            });
            Interlocked.Increment(ref _mEnqueued);
            latestActionSK = 0; // not available in batch mode
        }
        else
        {
            latestActionSK = FactAction(latestTurnSK, typeAction, pieceSK, playerSK, TargetPlayer, actionOrdinal, actionCost, build, surcharge, total);
        }
    }


}
