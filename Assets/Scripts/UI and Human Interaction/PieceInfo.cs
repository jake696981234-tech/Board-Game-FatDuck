using System.Text;
using UnityEngine;
using static Piece.AbilityKind;

public static class PieceInfo
{
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
        AddAbility(Piece.pieceBuild_enabled[pieceType], PieceBuild);
        AddAbility(Piece.move_enabled[pieceType], Move);
        AddAbility(Piece.shoot_enabled[pieceType], Shoot);
        AddAbility(Piece.sniper_enabled[pieceType], Sniper);
        AddAbility(Piece.captureVP_enabled[pieceType], CaptureVP);
        AddAbility(Piece.coreDamage_enabled[pieceType], CoreDamage);
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

        for (int i = 0; i < AbilitiesToShow.Length; i++)
        {
            string headerText = GetAbilityHeader(AbilitiesToShow[i]);
            string bodyText = GetAbilityDescription(AbilitiesToShow[i]);

            if (i < headers.Length && headers[i] != null)
            {
                headers[i].text = headerText;
            }
            if (i < fields.Length && fields[i] != null)
            {
                fields[i].text = bodyText;
            }
        }
    }

    private static string GetAbilityHeader(Piece.AbilityKind ability)
    {
        switch (ability)
        {
            case Move: return "Move";
            case Shoot: return "Shoot";
            case CaptureVP: return "Capture VP";
            case CoreDamage: return "Core Damage";
            case Push: return "Push/Pull";
            case GroupBuild: return "Group Build";
            case Upgrade: return "Upgrade";
            case Launcher: return "Launcher";
            case Spawner: return "Spawner";
            case SacrificeFactory: return "Sacrifice Factory";
            case ConversionFactory: return "Conversion Factory";
            case Explosive: return "Explosive";
            case PieceBuild: return "Piece Build";
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

    private static string GetAbilityDescription(Piece.AbilityKind ability)
    {
        if (pieceType < 0 || pieceType >= Piece.typeCount) return string.Empty;
        var sb = new StringBuilder();

        switch (ability)
        {
            case Move:
                AppendLine(sb, $"Range: {FormatRange(Piece.move_rangeMin[pieceType], Piece.move_rangeMax[pieceType])}");
                if (Piece.move_damage[pieceType] != 0) AppendLine(sb, $"Damage: {Piece.move_damage[pieceType]}");
                AppendBotSurcharge(sb, Piece.move_botSurcharge[pieceType]);
                break;
            case Shoot:
                AppendLine(sb, $"Range: {FormatRange(Piece.shoot_rangeMin[pieceType], Piece.shoot_rangeMax[pieceType])}");
                AppendLine(sb, $"Damage: {Piece.shoot_damage[pieceType]}");
                AppendBotSurcharge(sb, Piece.shoot_botSurcharge[pieceType]);
                break;
            case CaptureVP:
                AppendLine(sb, "Capture VP locations");
                AppendBotSurcharge(sb, Piece.captureVP_botSurcharge[pieceType]);
                break;
            case CoreDamage:
                AppendLine(sb, $"Core damage: {Piece.coreDamage_damage[pieceType]}");
                AppendBotSurcharge(sb, Piece.coreDamage_botSurcharge[pieceType]);
                break;
            case Push:
                AppendLine(sb, $"Range: {Piece.push_rangeMax[pieceType]}");
                AppendLine(sb, $"{(Piece.push_isPull[pieceType] ? "Pull" : "Push")} amount: {Piece.push_pushAmount[pieceType]}");
                AppendLine(sb, $"Targets: {DescribeTargets()}");
                if (Piece.push_damage[pieceType] != 0) AppendLine(sb, $"Damage: {Piece.push_damage[pieceType]}");
                AppendLine(sb, $"Friendly fire: {BoolText(Piece.push_isFriendlyFire[pieceType])}");
                break;
            case GroupBuild:
                AppendLine(sb, $"Builds: {GetPieceName(Piece.groupBuild_target[pieceType])}");
                AppendLine(sb, $"Needs: {Piece.groupBuild_requireNumber[pieceType]}");
                AppendLine(sb, $"Deletes builders: {BoolText(Piece.groupBuild_deletion[pieceType])}");
                AppendBotSurcharge(sb, Piece.groupBuild_botSurcharge[pieceType]);
                break;
            case Upgrade:
                AppendLine(sb, $"Upgrades to: {GetPieceName(Piece.upgrade_target[pieceType])}");
                if (Piece.upgrade_killsNeeded[pieceType] > 0) AppendLine(sb, $"Kills needed: {Piece.upgrade_killsNeeded[pieceType]}");
                AppendLine(sb, $"Goal kills: {BoolText(Piece.upgrade_isGoalKills[pieceType])}");
                AppendBotSurcharge(sb, Piece.upgrade_botSurcharge[pieceType]);
                break;
            case Launcher:
                AppendLine(sb, $"Input range: {Piece.launcher_inputRange[pieceType]}");
                AppendLine(sb, $"Output range: {Piece.launcher_outputRange[pieceType]}");
                AppendLine(sb, $"Friendly fire: {BoolText(Piece.launcher_isfriendlyFire[pieceType])}");
                AppendLine(sb, $"Hits enemies: {BoolText(Piece.launcher_isEnemyFire[pieceType])}");
                AppendBotSurcharge(sb, Piece.launcher_botSurcharge[pieceType]);
                break;
            case Spawner:
                AppendLine(sb, $"Spawns: {Piece.spawn_pieceAmount[pieceType]} x {GetPieceName(Piece.spawn_targetType[pieceType])}");
                AppendLine(sb, $"Range: {Piece.spawn_range[pieceType]}");
                AppendLine(sb, $"Once per turn: {BoolText(Piece.spawn_isOnlyOncePerTurn[pieceType])}");
                AppendBotSurcharge(sb, Piece.spawn_botSurcharge[pieceType]);
                break;
            case Factory:
                AppendLine(sb, $"Payout: {Piece.factory_amount[pieceType]}");
                if (Piece.factory_isRoundMultiplier[pieceType]) AppendLine(sb, "Scales with round");
                if (Piece.factory_isGroup[pieceType]) AppendLine(sb, $"Group payout: {Piece.factory_groupAmount[pieceType]}");
                if (Piece.factory_isInstantPayOut[pieceType]) AppendLine(sb, $"Instant payout: {Piece.factory_instantPayOutAmount[pieceType]}");
                if (Piece.factory_isKillPenalty[pieceType]) AppendLine(sb, $"Kill penalty after {Piece.factory_killsNeeded[pieceType]}: -{Piece.factory_killsPunishment[pieceType]}");
                break;
            case Sanctuary:
                AppendLine(sb, $"Range: {Piece.sanctuary_range[pieceType]}");
                break;
            case ConversionFactory:
                AppendLine(sb, $"Converts to: {DescribeConversion()}");
                AppendLine(sb, $"Amount: {Piece.conversionFactory_amount[pieceType]}");
                AppendBotSurcharge(sb, Piece.conversionFactory_botSurcharge[pieceType]);
                break;
            case Eat:
                AppendLine(sb, $"Eat amount: {Piece.eat_amount[pieceType]}");
                break;
            case SacrificeFactory:
                AppendLine(sb, $"Payout: {Piece.sacrificeFactory_amount[pieceType]}");
                AppendLine(sb, $"Range: {FormatRange(Piece.sacrificeFactory_rangeMin[pieceType], Piece.sacrificeFactory_rangeMax[pieceType])}");
                AppendBotSurcharge(sb, Piece.sacrificeFactory_botSurcharge[pieceType]);
                break;
            case SacrificeCost:
                AppendLine(sb, $"Needs: {Piece.sacrificeCost_howManyItNeeds[pieceType]}");
                AppendLine(sb, $"Piece: {(Piece.sacrificeCost_isNeedsSpecificPiece[pieceType] ? GetPieceName(Piece.sacrificeCost_specificPiece[pieceType]) : "Any")}");
                break;
            case FeedingGround:
                AppendLine(sb, $"Range: {Piece.feedingGround_Range[pieceType]}");
                AppendLine(sb, $"Payout: {Piece.feedingGround_payOut[pieceType]}");
                break;
            case Explosive:
                AppendLine(sb, $"Damage: {Piece.explosive_damage[pieceType]}");
                AppendLine(sb, $"Range: {Piece.explosive_range[pieceType]}");
                AppendLine(sb, $"Friendly fire: {BoolText(Piece.explosive_isFriendlyFire[pieceType])}");
                AppendLine(sb, $"Self destructs: {BoolText(Piece.explosive_isKillItself[pieceType])}");
                break;
            case PieceBuild:
                AppendLine(sb, $"Range: {Piece.pieceBuild_range[pieceType]}");
                AppendLine(sb, $"Builds: {GetTargetList(Piece.pieceBuild_targetIds[pieceType])}");
                break;
            case Sniper:
                AppendLine(sb, $"Range: {FormatRange(Piece.sniper_minRange[pieceType], Piece.sniper_maxRange[pieceType])}");
                AppendLine(sb, $"Damage: {Piece.sniper_damage[pieceType]}");
                if (Piece.sniper_lineLength[pieceType] > 0) AppendLine(sb, $"Line length: {Piece.sniper_lineLength[pieceType]}");
                AppendLine(sb, $"Line of sight: {BoolText(Piece.sniper_isLineOfSight[pieceType])}");
                AppendLine(sb, $"Friendly fire: {BoolText(Piece.sniper_isFriendlyFire[pieceType])}");
                AppendLine(sb, $"Soldiers only: {BoolText(Piece.sniper_isonlySoldiers[pieceType])}");
                break;
            case NecroSpawn:
                AppendLine(sb, $"Range: {Piece.necroSpawn_range[pieceType]}");
                AppendBotSurcharge(sb, Piece.necroSpawn_botSurcharge[pieceType]);
                break;
            case Zombie:
                AppendLine(sb, "Zombie effect enabled");
                break;
            default:
                break;
        }

        return sb.ToString();
    }

    private static void AppendLine(StringBuilder sb, string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(line);
    }

    private static void AppendBotSurcharge(StringBuilder sb, int surcharge)
    {
        if (surcharge <= 0) return;
        AppendLine(sb, $"Bot surcharge: +{surcharge}");
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

    private static bool IsValidType(int type) => type >= 0 && type < Piece.typeCount;

    private static string GetPieceName(int type)
    {
        if (!IsValidType(type) || Piece.name == null || Piece.name.Length <= type) return "Unknown";
        return Piece.name[type];
    }
}
