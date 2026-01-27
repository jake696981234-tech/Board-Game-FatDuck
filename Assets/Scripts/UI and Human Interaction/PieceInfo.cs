using System.Text;
using TMPro;
using UnityEngine;
using static Piece.AbilityKind;

public static class PieceInfo
{
    private static int totalCost; //do not refrence this value
    public static void BuildInfo()
    {
        var BuildCost = Piece.BuildCost[pieceType];
        totalCost = BuildCost - ShowLeftPanel.curActionFee;

        UI.hic.createTitleText.text = $"Create: {Piece.name[pieceType]}";
        UI.hic.createCostText.text = $"Build Cost: {BuildCost}"; 
        UI.hic.createActionTurnFee.text = $"Action Fee: {ShowLeftPanel.curActionFee}"; 
        if (UI.hic.createTotalCost) UI.hic.createTotalCost.text = $"Total Cost: {totalCost}"; 

        if (!UI.hic.createSprite) return;
        var s = !string.IsNullOrEmpty(Piece.spritePath[pieceType]) ? Resources.Load<Sprite>(Piece.spritePath[pieceType]) : null;
        UI.hic.createSprite.sprite = s;
        UI.hic.createSprite.enabled = (s != null);  
    }

    public static void UpdateCreateCost()
    {
        var budgetAfter = UIBridge.gameState.ps[UIBridge._humanPlayer].budget - totalCost;
        if (UI.hic.createBudgetAfter) UI.hic.createBudgetAfter.text = $"Budget After: {budgetAfter}"; 
    }


    private const int FieldsPerAbility = 4;
    private static readonly Piece.AbilityKind[] AbilitiesToShow = new Piece.AbilityKind[4];
    private static int pieceType = -1;

    public static void SetPieceInfo(int type)
    {
        for (int i = 0; i < AbilitiesToShow.Length; i++)
        {
            AbilitiesToShow[i] = Invalid;
        }

        if (Piece.typeCount <= 0)
        {
            pieceType = -1;
            ApplyToUi();
            return;
        }

        pieceType = Mathf.Clamp(type, 0, Piece.typeCount - 1);
        int index = 0;

        BuildInfo();

        void AddAbility(bool enabled, Piece.AbilityKind ability)
        {
            if (enabled && index < AbilitiesToShow.Length)
            {
                AbilitiesToShow[index++] = ability;
            }
        }

        AddAbility(Piece.groupBuild_enabled[pieceType], GroupBuild);
        AddAbility(Piece.upgrade_enabled[pieceType], Upgrade);
        AddAbility(Piece.spawn_enabled[pieceType], Spawner);
        AddAbility(Piece.factory_enabled[pieceType], Factory);
        AddAbility(Piece.conversionFactory_enabled[pieceType], ConversionFactory);
        AddAbility(Piece.sacrificeFactory_enabled[pieceType], SacrificeFactory);
        AddAbility(Piece.feedingGround_enabled[pieceType], FeedingGround);
        AddAbility(Piece.sacrificeCost_enabled[pieceType], SacrificeCost);
        AddAbility(Piece.launcher_enabled[pieceType], Launcher);
        AddAbility(Piece.push_enabled[pieceType], Push);
        // AddAbility(Piece.pieceBuild_enabled[pieceType], PieceBuild);
        AddAbility(Piece.move_enabled[pieceType], Move);
        AddAbility(Piece.shoot_enabled[pieceType], Shoot);
        AddAbility(Piece.sniper_enabled[pieceType], Sniper);
        // AddAbility(Piece.captureVP_enabled[pieceType], CaptureVP);
        // AddAbility(Piece.coreDamage_enabled[pieceType], CoreDamage);
        AddAbility(Piece.sanctuary_enabled[pieceType], Sanctuary);
        AddAbility(Piece.eat_enabled[pieceType], Eat);
        AddAbility(Piece.explosive_enabled[pieceType], Explosive);
        AddAbility(Piece.necroSpawn_enabled[pieceType], NecroSpawn);
        AddAbility(Piece.zombie_enabled[pieceType], Zombie);

        ApplyToUi();
    }

    private static void ApplyToUi()
    {
        var headers = UI.hic?.pieceInfoHeaders;
        var fields = UI.hic?.pieceInfoFeilds;
        if (headers == null || fields == null) return;

        for (int i = 0; i < headers.Length; i++)
        {
            if (headers[i] != null) headers[i].text = string.Empty;
        }
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] != null) fields[i].text = string.Empty;
        }

        for (int i = 0; i < AbilitiesToShow.Length; i++)
        {
            string headerText = GetAbilityHeader(AbilitiesToShow[i]);

            if (i < headers.Length && headers[i] != null)
            {
                headers[i].text = headerText;
            }
            int baseIndex = i * FieldsPerAbility;
            WriteAbilityFields(AbilitiesToShow[i], fields, baseIndex);
        }
    }

    private static string GetAbilityHeader(Piece.AbilityKind ability)
    {
        switch (ability)
        {
            case Move: return "Move";
            case Shoot: return "Shoot";
            // case CaptureVP: return "Capture VP";
            // case CoreDamage: return "Core Damage";
            case Push: return IsValidPieceType() && Piece.push_isPull[pieceType] ? "Pull" : "Push";
            case GroupBuild: return "Group Build";
            case Upgrade: return "Upgrade";
            case Launcher: return "Launcher";
            case Spawner: return "Spawner";
            case SacrificeFactory: return "Sacrifice Factory";
            case ConversionFactory: return "Conversion Factory";
            case Explosive: return "Explosive";
            // case PieceBuild: return "Piece Build";
            case Sniper: return "Sniper";
            case NecroSpawn: return "Necro Spawn";
            case Factory: return "Factory";
            case Sanctuary: return "Sanctuary";
            case Eat: return "Eat";
            case SacrificeCost: return "Sacrifice Cost";
            case FeedingGround: return "Feeding Ground";
            case Zombie: return "Zombie";
            default: return string.Empty;
        }
    }

    private static void WriteAbilityFields(Piece.AbilityKind ability, TMP_Text[] fields, int baseIndex)
    {
        if (!IsValidPieceType()) return;
        int write = 0;

        void AddField(string text)
        {
            if (write >= FieldsPerAbility) return;
            int idx = baseIndex + write;
            write++;
            if (idx < 0 || idx >= fields.Length) return;
            if (fields[idx] != null) fields[idx].text = text;
        }

        switch (ability)
        {
            case Move:
                AddField($"Range: {FormatRange(Piece.move_rangeMin[pieceType], Piece.move_rangeMax[pieceType])}");
                AddField($"Damage: {Piece.move_damage[pieceType]}");
                break;
            case Shoot:
                AddField($"Range: {FormatRange(Piece.shoot_rangeMin[pieceType], Piece.shoot_rangeMax[pieceType])}");
                AddField($"Damage: {Piece.shoot_damage[pieceType]}");
                break;
            // case CoreDamage:
            //     AddField($"Core damage: {Piece.coreDamage_damage[pieceType]}");
            //     break;
            case Push:
                AddField($"Range: {Piece.push_rangeMax[pieceType]}");
                AddField($"Amount: {Piece.push_pushAmount[pieceType]}");
                AddField($"Targets: {DescribeTargets()}");
                if (Piece.push_damage[pieceType] != 0)
                {
                    AddField($"Damage: {Piece.push_damage[pieceType]}");
                }
                else
                {
                    AddField($"Friendly fire: {BoolText(Piece.push_isFriendlyFire[pieceType])}");
                }
                break;
            case GroupBuild:
                AddField($"Builds: {GetPieceName(Piece.groupBuild_target[pieceType])}");
                AddField($"Needs: {Piece.groupBuild_requireNumber[pieceType]}");
                AddField($"Deletes builders: {BoolText(Piece.groupBuild_deletion[pieceType])}");
                break;
            case Upgrade:
                AddField($"Upgrades to: {GetPieceName(Piece.upgrade_target[pieceType])}");
                if (Piece.upgrade_killsNeeded[pieceType] > 0)
                {
                    AddField($"Kills needed: {Piece.upgrade_killsNeeded[pieceType]}");
                }
                if (Piece.upgrade_killsNeeded[pieceType] > 0 || Piece.upgrade_isGoalKills[pieceType])
                {
                    AddField($"Goal kills: {BoolText(Piece.upgrade_isGoalKills[pieceType])}");
                }
                break;
            case Launcher:
                AddField($"Input range: {Piece.launcher_inputRange[pieceType]}");
                AddField($"Output range: {Piece.launcher_outputRange[pieceType]}");
                AddField($"Friendly fire: {BoolText(Piece.launcher_isfriendlyFire[pieceType])}");
                AddField($"Enemy fire: {BoolText(Piece.launcher_isEnemyFire[pieceType])}");
                break;
            case Spawner:
                AddField($"Target: {GetPieceName(Piece.spawn_targetType[pieceType])}");
                AddField($"Amount: {Piece.spawn_pieceAmount[pieceType]}");
                AddField($"Range: {Piece.spawn_range[pieceType]}");
                AddField($"Once per turn: {BoolText(Piece.spawn_isOnlyOncePerTurn[pieceType])}");
                break;
            case Factory:
                AddField($"Payout: {Piece.factory_amount[pieceType]}");
                if (Piece.factory_isGroup[pieceType]) AddField($"Group payout: {Piece.factory_groupAmount[pieceType]}");
                if (Piece.factory_isInstantPayOut[pieceType]) AddField($"Instant payout: {Piece.factory_instantPayOutAmount[pieceType]}");
                if (Piece.factory_isKillPenalty[pieceType])
                {
                    AddField($"Kill penalty: -{Piece.factory_killsPunishment[pieceType]} after {Piece.factory_killsNeeded[pieceType]}");
                }
                if (Piece.factory_isRoundMultiplier[pieceType]) AddField("Round multiplier: Yes");
                break;
            case Sanctuary:
                AddField($"Range: {Piece.sanctuary_range[pieceType]}");
                break;
            case ConversionFactory:
                AddField($"Converts: {DescribeConversion()}");
                AddField($"Amount: {Piece.conversionFactory_amount[pieceType]}");
                break;
            case Eat:
                AddField($"Amount: {Piece.eat_amount[pieceType]}");
                break;
            case SacrificeFactory:
                AddField($"Payout: {Piece.sacrificeFactory_amount[pieceType]}");
                AddField($"Range: {FormatRange(Piece.sacrificeFactory_rangeMin[pieceType], Piece.sacrificeFactory_rangeMax[pieceType])}");
                break;
            case SacrificeCost:
                AddField($"Needs: {Piece.sacrificeCost_howManyItNeeds[pieceType]}");
                AddField($"Piece: {(Piece.sacrificeCost_isNeedsSpecificPiece[pieceType] ? GetPieceName(Piece.sacrificeCost_specificPiece[pieceType]) : "Any")}");
                break;
            case FeedingGround:
                AddField($"Range: {Piece.feedingGround_Range[pieceType]}");
                AddField($"Payout: {Piece.feedingGround_payOut[pieceType]}");
                break;
            case Explosive:
                AddField($"Damage: {Piece.explosive_damage[pieceType]}");
                AddField($"Range: {Piece.explosive_range[pieceType]}");
                AddField($"Friendly fire: {BoolText(Piece.explosive_isFriendlyFire[pieceType])}");
                AddField($"Self destruct: {BoolText(Piece.explosive_isKillItself[pieceType])}");
                break;
            // case PieceBuild:
            //     AddField($"Range: {Piece.pieceBuild_range[pieceType]}");
            //     AddField($"Targets: {GetTargetList(Piece.pieceBuild_targetIds[pieceType])}");
            //     break;
            case Sniper:
                AddField($"Range: {FormatRange(Piece.sniper_minRange[pieceType], Piece.sniper_maxRange[pieceType])}");
                AddField($"Damage: {Piece.sniper_damage[pieceType]}");
                if (Piece.sniper_isLineOfSight[pieceType]) AddField($"Line of sight: {BoolText(Piece.sniper_isLineOfSight[pieceType])}");
                if (Piece.sniper_isonlySoldiers[pieceType]) AddField($"Soldiers only: {BoolText(Piece.sniper_isonlySoldiers[pieceType])}");
                if (Piece.sniper_isFriendlyFire[pieceType]) AddField($"Friendly fire: {BoolText(Piece.sniper_isFriendlyFire[pieceType])}");
                if (Piece.sniper_lineLength[pieceType] > 0) AddField($"Line length: {Piece.sniper_lineLength[pieceType]}");
                break;
            case NecroSpawn:
                AddField($"Range: {Piece.necroSpawn_range[pieceType]}");
                break;
            default:
                break;
        }
    }

    private static string FormatRange(int min, int max) => min == max ? $"{min}" : $"{min}-{max}";

    private static string BoolText(bool value) => value ? "Yes" : "No";

    private static string DescribeTargets()
    {
        bool buildings = Piece.push_IsTargetsBuildings[pieceType];
        bool soldiers = Piece.push_isTargetsSoldiers[pieceType];
        if (buildings && soldiers) return "Buildings and soldiers";
        if (buildings) return "Buildings";
        if (soldiers) return "Soldiers";
        return "None";
    }

    private static string DescribeConversion()
    {
        bool toCore = Piece.conversionFactory_isCoreHealth[pieceType];
        bool toVp = Piece.conversionFactory_isVp[pieceType];
        if (toCore && toVp) return "Core HP & VP";
        if (toCore) return "Core HP";
        if (toVp) return "VP";
        return "Resource";
    }

    private static string GetTargetList(int[] ids)
    {
        if (ids == null || ids.Length == 0) return "None";
        var sb = new StringBuilder();
        for (int i = 0; i < ids.Length; i++)
        {
            if (!IsValidType(ids[i])) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(GetPieceName(ids[i]));
        }
        return sb.Length > 0 ? sb.ToString() : "None";
    }

    private static bool IsValidPieceType() => pieceType >= 0 && pieceType < Piece.typeCount;

    private static bool IsValidType(int type) => type >= 0 && type < Piece.typeCount;

    private static string GetPieceName(int type)
    {
        if (!IsValidType(type) || Piece.name == null || Piece.name.Length <= type) return "Unknown";
        return Piece.name[type];
    }
}
