using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient; // System.Data.SqlClient contains SqlConnection/SqlBulkCopy
using System.Threading;
using System.IO;
using UnityEngine;



public static class DbLoggingConfig
{


    //Values you must change for each simulation
    public readonly static int inputSimID = 4;
    public readonly static string inputSimName = "Does it work this way though?";
    public readonly static string inputRuleVersion = "v1";
    public readonly static string inputNotes = "No notes";

    //Prefrenece values
    public readonly static bool enabled = true;


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
    private static int latestActionSK;

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


    //Table methods helper!
    public static void Execute(string sql, params SqlParameter[] p)
    {
        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(p);
        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public static int GetMostRecentSK(string tableName, string skColumn)
    {
        // Basic sanitization to avoid SQL injection: allow only alphanumeric + underscore
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(skColumn))
            throw new ArgumentException("Table name and column name cannot be null or empty.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[A-Za-z0-9_]+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(skColumn, @"^[A-Za-z0-9_]+$"))
            throw new ArgumentException("Invalid table or column name.");

        // Build dynamic SQL safely (identifiers cannot be parameterized)
        string sql = $"SELECT MAX([{skColumn}]) FROM dbo.[{tableName}];";

        using var conn = new SqlConnection(SqlConnection);
        using var cmd = new SqlCommand(sql, conn);
        conn.Open();
        object result = cmd.ExecuteScalar();
        return (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
    }






    //Tables methods!
    public static void SimVersion(int simID, string simName, string ruleVersion, string notes, float maxRounds, float startingBudget, float startOfTurnDeduction)
    {
        Execute(
            "INSERT INTO dbo.DimSim(SimID, SimName, RuleVersion, Notes, MaxRounds, StartingBudget, startOfTurnDeduction) VALUES (@simID, @simName, @ruleVersion, @notes, @maxRounds, @startingBudget, @startOfTurnDeduction);",
            new SqlParameter("@simID", simID),
            new SqlParameter("@simName", simName),
            new SqlParameter("@ruleVersion", ruleVersion),
            new SqlParameter("@notes", notes),
            new SqlParameter("@maxRounds", maxRounds),
            new SqlParameter("@startingBudget", startingBudget),
            new SqlParameter("@startOfTurnDeduction", startOfTurnDeduction)
        );
    }

    public static void actionTypeVersion(int simID, int actionTypeID, string actionName, string category, bool isBuild, bool affectsCore, bool needsPiece)
    {
        Execute(
            "INSERT INTO dbo.DimActionType(simID, actionTypeID, actionName, category, isBuild, affectsCore, needsPiece) VALUES (@simID, @actionTypeID, @actionName, @category, @isBuild, @affectsCore, @needsPiece);",
            new SqlParameter("@simID", simID),
            new SqlParameter("@actionTypeID", actionTypeID),
            new SqlParameter("@actionName", actionName),
            new SqlParameter("@category", category),
            new SqlParameter("@isBuild", isBuild),
            new SqlParameter("@affectsCore", affectsCore),
            new SqlParameter("@needsPiece", needsPiece)
        );
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




    public static void playerVersion(
        int simID,
        int playerID,
        object playerName,
        object policyName,
        object isHuman)
    {
        Execute(
            "INSERT INTO dbo.DimPlayer (simID, playerID, playerName, policyName, isHuman) " +
            "VALUES (@simID, @playerID, @playerName, @policyName, @isHuman);",
            new SqlParameter("@simID", simID),
            new SqlParameter("@playerID", playerID),
            new SqlParameter("@playerName", playerName ?? DBNull.Value),
            new SqlParameter("@policyName", policyName ?? DBNull.Value),
            new SqlParameter("@isHuman", isHuman ?? DBNull.Value)
        );
    }


    public static void winTypeVersion(
        int simID,
        string winName,
        object winCategory)
    {
        Execute(
            "INSERT INTO dbo.DimWinType (simID, winName, winCategory) " +
            "VALUES (@simID, @winName, @winCategory);",
            new SqlParameter("@simID", simID),
            new SqlParameter("@winName", winName),
            new SqlParameter("@winCategory", winCategory ?? DBNull.Value)
        );
    }

    public static void prepDimGameVersion(int simID, int gameOrdinal)
    {
        Execute(
            "INSERT INTO dbo.DimGame (simID, gameOrdinal) " +
            "VALUES (@simID, @gameOrdinal);",
            new SqlParameter("@simID", simID),
            new SqlParameter("@gameOrdinal", gameOrdinal)
        );
    }


    public static void dimGameVersion(int gameSK, object winnerPlayerSK, object winTypeSK)
    {
        Execute(
            "UPDATE dbo.DimGame " +
            "SET winnerPlayerSK = @winnerPlayerSK, " +
            "    winTypeSK      = @winTypeSK " +
            "WHERE gameSK = @gameSK;",
            new SqlParameter("@gameSK", gameSK),
            new SqlParameter("@winnerPlayerSK", winnerPlayerSK ?? DBNull.Value),
            new SqlParameter("@winTypeSK", winTypeSK ?? DBNull.Value)
        );
    }


    // TO DO DimRound
    public static void roundVersion(
        int gameSK,
        int roundOrdinal)
    {
        Execute(
            "INSERT INTO dbo.DimRound (gameSK, roundOrdinal) " + "VALUES (@gameSK, @roundOrdinal);",
            new SqlParameter("@gameSK", gameSK),
            new SqlParameter("@roundOrdinal", roundOrdinal)
        );
    }

    public static void prepTurnVersion(int RoundSK)
    {
        Execute(
            "INSERT INTO dbo.DimTurn (roundSK) " +
            "VALUES (@roundSK);",
            new SqlParameter("@roundSK", RoundSK)
        );
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
        Execute(
            "UPDATE dbo.DimTurn " +
            "SET playerSK = @playerSK, " +
            "    turnOrdinal = @turnOrdinal, " +
            "    isPassOnly = @isPassOnly, " +
            "    coreHealthEnd = @coreHealthEnd, " +
            "    victoryPointsEnd = @victoryPointsEnd, " +
            "    resourceTotalEnd = @resourceTotalEnd, " +
            "    digitsStart = @digitsStart, " +
            "    digitsEnd = @digitsEnd, " +
            "    piecesOnBoardStart = @piecesOnBoardStart, " +
            "    piecesOnBoardEnd = @piecesOnBoardEnd, " +
            "    playerTurnOrdinal = @playerTurnOrdinal " +
            "WHERE turnSK = @turnSK;",
            new SqlParameter("@turnSK", TurnSK),
            new SqlParameter("@playerSK", playerSK),
            new SqlParameter("@turnOrdinal", turnOrdinal),
            new SqlParameter("@isPassOnly", isPassOnly),
            new SqlParameter("@coreHealthEnd", coreHealthEnd),
            new SqlParameter("@victoryPointsEnd", victoryPointsEnd),
            new SqlParameter("@resourceTotalEnd", resourceTotalEnd ?? DBNull.Value),
            new SqlParameter("@digitsStart", digitsStart ?? DBNull.Value),
            new SqlParameter("@digitsEnd", digitsEnd ?? DBNull.Value),
            new SqlParameter("@piecesOnBoardStart", piecesOnBoardStart ?? DBNull.Value),
            new SqlParameter("@piecesOnBoardEnd", piecesOnBoardEnd ?? DBNull.Value),
            new SqlParameter("@playerTurnOrdinal", playerTurnOrdinal)
        );
    }



    // TO DO FactAction
    public static void FactAction(
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
        Execute(
            "INSERT INTO dbo.FactAction (turnSK, actionTypeSK, pieceSK, actingPlayerSK, targetPlayerSK, actionSeq, " +
            "actionCost, buildCost, surchargeCost, totalCost) " +
            "VALUES (@turnSK, @actionTypeSK, @pieceSK, @actingPlayerSK, @targetPlayerSK, @actionSeq, " +
            "@actionCost, @buildCost, @surchargeCost, @totalCost);",
            new SqlParameter("@turnSK", turnSK),
            new SqlParameter("@actionTypeSK", actionTypeSK),
            new SqlParameter("@pieceSK", pieceSK ?? DBNull.Value),
            new SqlParameter("@actingPlayerSK", actingPlayerSK),
            new SqlParameter("@targetPlayerSK", targetPlayerSK ?? DBNull.Value),
            new SqlParameter("@actionSeq", actionSeq),
            new SqlParameter("@actionCost", actionCost),
            new SqlParameter("@buildCost", buildCost),
            new SqlParameter("@surchargeCost", surchargeCost),
            new SqlParameter("@totalCost", totalCost)
        );
    }


    // TO DO FactActionPieceLoss
    public static void FactActionPieceLoss(
        long actionID,
        int lostPlayerSK,
        int lostPieceSK,
        int lostCount,
        object valueLost)
    {
        Execute(
            "INSERT INTO dbo.FactActionPieceLoss (actionID, lostPlayerSK, lostPieceSK, lostCount, valueLost) " +
            "VALUES (@actionID, @lostPlayerSK, @lostPieceSK, @lostCount, @valueLost);",
            new SqlParameter("@actionID", actionID),
            new SqlParameter("@lostPlayerSK", lostPlayerSK),
            new SqlParameter("@lostPieceSK", lostPieceSK),
            new SqlParameter("@lostCount", lostCount),
            new SqlParameter("@valueLost", valueLost ?? DBNull.Value)
        );
    }


    // TO DO FactRoundPlayer
    public static void logFactRoundPlayer(
        int roundSK,
        int playerSK,
        int soldiersRemovedThisRound,
        int piecesOnBoardStart,
        int piecesOnBoardEnd,
        int digitsStart,
        int digitsEnd,
        decimal totalCostThisRound,
        bool passedFlag,
        bool eliminatedFlag)
    {
        Execute(
            "INSERT INTO dbo.FactRoundPlayer (roundSK, playerSK, soldiersRemovedThisRound, piecesOnBoardStart, piecesOnBoardEnd, " +
            "digitsStart, digitsEnd, totalCostThisRound, passedFlag, eliminatedFlag) " +
            "VALUES (@roundSK, @playerSK, @soldiersRemovedThisRound, @piecesOnBoardStart, @piecesOnBoardEnd, " +
            "@digitsStart, @digitsEnd, @totalCostThisRound, @passedFlag, @eliminatedFlag);",
            new SqlParameter("@roundSK", roundSK),
            new SqlParameter("@playerSK", playerSK),
            new SqlParameter("@soldiersRemovedThisRound", soldiersRemovedThisRound),
            new SqlParameter("@piecesOnBoardStart", piecesOnBoardStart),
            new SqlParameter("@piecesOnBoardEnd", piecesOnBoardEnd),
            new SqlParameter("@digitsStart", digitsStart),
            new SqlParameter("@digitsEnd", digitsEnd),
            new SqlParameter("@totalCostThisRound", totalCostThisRound),
            new SqlParameter("@passedFlag", passedFlag),
            new SqlParameter("@eliminatedFlag", eliminatedFlag)
        );
    }










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
        actionTypeVersion(inputSimID, moveID, moveString, placeholder, false, false, true);
        skForMoveAction = GetMostRecentSK("DimActionType", "actionTypeSK");

        actionTypeVersion(inputSimID, shootID, shootString, placeholder, false, false, true);
        skForShootAction = GetMostRecentSK("DimActionType", "actionTypeSK");

        actionTypeVersion(inputSimID, captureVPID, captureVPString, placeholder, false, false, true);
        skForCaptureVPAction = GetMostRecentSK("DimActionType", "actionTypeSK");

        actionTypeVersion(inputSimID, coreDamageID, coreDamageString, placeholder, false, true, true);
        skForCoreDamageAction = GetMostRecentSK("DimActionType", "actionTypeSK");

        actionTypeVersion(inputSimID, createID, createString, placeholder, true, false, false);
        skForCreateAction = GetMostRecentSK("DimActionType", "actionTypeSK");

        actionTypeVersion(inputSimID, endTurnID, endTurnString, placeholder, false, false, false);
        skForEndTurnAction = GetMostRecentSK("DimActionType", "actionTypeSK");
    }


    public static void logDimPiece()
    {
        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");

        ImportAndLogDimPiece(csvPath, inputSimID);
    }

    public static void logPlayerVersion()
    {
        playerVersion(inputSimID, 1, "Mathew", placeholder, false);
        playerOneSK = GetMostRecentSK("DimPlayer", "playerSK");

        playerVersion(inputSimID, 2, "Mark", placeholder, false);
        playerTwoSK = GetMostRecentSK("DimPlayer", "playerSK");

        playerVersion(inputSimID, 3, "Luke", placeholder, false);
        playerThreeSK = GetMostRecentSK("DimPlayer", "playerSK");

        playerVersion(inputSimID, 4, "John", placeholder, false);
        playerFourSK = GetMostRecentSK("DimPlayer", "playerSK");
    }

    public static void logWinTypeVersion()
    {
        winTypeVersion(inputSimID, wonByEndVp, placeholder);
        wonByEndVpSK = GetMostRecentSK("DimWinType", "winTypeSK");

        winTypeVersion(inputSimID, wonByElimination, placeholder);
        wonByEliminationSK = GetMostRecentSK("DimWinType", "winTypeSK");

        winTypeVersion(inputSimID, tie, placeholder);
        tieSK = GetMostRecentSK("DimWinType", "winTypeSK");
    }

    public static void prepDimGame()
    {
        gameOrdinal++;
        roundOrdinalUp = 0; // reset round counter at new game start
        prepDimGameVersion(inputSimID, gameOrdinal);
        latestGameSK = GetMostRecentSK("DimGame", "gameSK");
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
        roundVersion(latestGameSK, roundOrdinalUp);
        latestRoundSK = GetMostRecentSK("DimRound", "roundSK");
    }


    public static void prepTurn()
    {
        prepTurnVersion(latestRoundSK);
        latestTurnSK = GetMostRecentSK("DimTurn", "turnSK");
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

        FactAction(latestTurnSK, typeAction, pieceSK, playerSK, TargetPlayer, actionOrdinal, actionCost, build, surcharge, total);
        latestActionSK = GetMostRecentSK("FactAction", "actionID");
    }


}
