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
    public readonly static int inputSimID = 3;

    public readonly static string inputSimName = "Does it work this way though?";

    public readonly static string inputRuleVersion = "v1";

    public readonly static string inputNotes = "No notes";

    //Prefrenece values
    public readonly static bool enabled = true;


    //probably never change values
    private readonly static string SqlConnection = "Server=localhost;Database=GameSim;Trusted_Connection=True;TrustServerCertificate=True";

    //Values that are a manual copy out of somewhere else- when adding/changing content double check if these are right (preferably change these to auto align)
    private readonly static int moveID = 10;
    private readonly static int shootID = 1;
    private readonly static int captureVPID = 2;
    private readonly static int coreDamageID = 3;
    private readonly static int createID = 4;
    private readonly static int endTurnID = 5;

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


    //SK values seeded on boostrap

    private static int skForMoveAction;

    private static int skForShootAction;

    private static int skForCaptureVPAction;

    private static int skForCoreDamageAction;

    private static int skForCreateAction;

    private static int skForEndTurnAction;

    private static int skForpiece;


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

    //ordinals
    private static int gameOrdinal = 0;

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
        "INSERT INTO dbo.DimPiece(simID, pieceID, pieceName, isBuilding, faction, baseCost) " +
        "VALUES (@simID, @pieceID, @pieceName, @isBuilding, @faction, @baseCost);", conn, tx);

    var pSimID       = cmd.Parameters.Add("@simID", SqlDbType.Int);
    var pPieceID     = cmd.Parameters.Add("@pieceID", SqlDbType.Int);
    var pPieceName   = cmd.Parameters.Add("@pieceName", SqlDbType.VarChar, 100);
    var pIsBuilding  = cmd.Parameters.Add("@isBuilding", SqlDbType.Bit);
    var pFactionName = cmd.Parameters.Add("@faction", SqlDbType.VarChar, 50);
    var pBaseCost    = cmd.Parameters.Add("@baseCost", SqlDbType.Decimal);
    pBaseCost.Precision = 19; pBaseCost.Scale = 4;

    pSimID.Value = simID;
    

    var pcs = PiecesCsvImporter.Import(csvPath, onTypeDefined: (pieceID, pieceName, isBuilding, faction, buildCost) =>
    {
        pPieceID.Value     = pieceID;
        pPieceName.Value   = pieceName ?? (object)DBNull.Value;
        pIsBuilding.Value  = isBuilding;
        pFactionName.Value = string.IsNullOrEmpty(faction) ? (object)DBNull.Value : faction;
        pBaseCost.Value    = buildCost == 0 ? (object)DBNull.Value : buildCost; // adjust if 0 is valid

        cmd.ExecuteNonQuery();
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




    // TO DO DimTurn
    public static void turnVersion(
        int roundSK,
        int playerSK,
        int turnOrdinal,
        bool isPassOnly,
        int coreHealthEnd,
        int victoryPointsEnd,
        object resourceTotalEnd,
        object digitsStart,
        object digitsEnd,
        object piecesOnBoardStart,
        object piecesOnBoardEnd)
    {
        Execute(
            "INSERT INTO dbo.DimTurn (roundSK, playerSK, turnOrdinal, isPassOnly, coreHealthEnd, victoryPointsEnd, " +
            "resourceTotalEnd, digitsStart, digitsEnd, piecesOnBoardStart, piecesOnBoardEnd) " +
            "VALUES (@roundSK, @playerSK, @turnOrdinal, @isPassOnly, @coreHealthEnd, @victoryPointsEnd, " +
            "@resourceTotalEnd, @digitsStart, @digitsEnd, @piecesOnBoardStart, @piecesOnBoardEnd);",
            new SqlParameter("@roundSK", roundSK),
            new SqlParameter("@playerSK", playerSK),
            new SqlParameter("@turnOrdinal", turnOrdinal),
            new SqlParameter("@isPassOnly", isPassOnly),
            new SqlParameter("@coreHealthEnd", coreHealthEnd),
            new SqlParameter("@victoryPointsEnd", victoryPointsEnd),
            new SqlParameter("@resourceTotalEnd", resourceTotalEnd ?? DBNull.Value),
            new SqlParameter("@digitsStart", digitsStart ?? DBNull.Value),
            new SqlParameter("@digitsEnd", digitsEnd ?? DBNull.Value),
            new SqlParameter("@piecesOnBoardStart", piecesOnBoardStart ?? DBNull.Value),
            new SqlParameter("@piecesOnBoardEnd", piecesOnBoardEnd ?? DBNull.Value)
        );
    }


    


    // TO DO FactAction
    public static void FactAction(
        long actionID,
        int turnSK,
        int actionTypeSK,
        object pieceSK,
        int actingPlayerSK,
        object targetPlayerSK,
        int actionSeq,
        decimal actionCost,
        decimal buildCost,
        decimal surchargeCost,
        decimal totalCost)
    {
        Execute(
            "INSERT INTO dbo.FactAction (actionID, turnSK, actionTypeSK, pieceSK, actingPlayerSK, targetPlayerSK, actionSeq, " +
            "actionCost, buildCost, surchargeCost, totalCost) " +
            "VALUES (@actionID, @turnSK, @actionTypeSK, @pieceSK, @actingPlayerSK, @targetPlayerSK, @actionSeq, " +
            "@actionCost, @buildCost, @surchargeCost, @totalCost);",
            new SqlParameter("@actionID", actionID),
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
        skForpiece  = GetMostRecentSK("DimPiece", "pieceSK");
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
    }



    public static void logRoundVersion(int RoundsLeft)
    {
        roundVersion(latestGameSK, RoundsLeft);
        latestRoundSK = GetMostRecentSK("DimRound", "roundSK");
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
    int? piecesOnBoardEnd)
    {

        int playerSK = whichPlayer switch
        {
            0 => playerOneSK,
            1 => playerTwoSK,
            2 => playerThreeSK,
            3 => playerFourSK,
            _ => throw new ArgumentOutOfRangeException(nameof(whichPlayer), "Player index must be 0..3.")
        };


        turnVersion(
        latestRoundSK,
        playerSK,
        turnOrdinal,
        isPassOnly,
        coreHealthEnd,
        victoryPointsEnd,
        resourceTotalEnd,      // nullable -> handled inside turnVersion
        digitsStart,
        digitsEnd,
        piecesOnBoardStart,
        piecesOnBoardEnd
    );
    }





}

