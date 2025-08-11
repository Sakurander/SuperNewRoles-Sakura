using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using Hazel;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.CustomObject;
using SuperNewRoles.Ability;

namespace SuperNewRoles.Roles.Impostor;

class RugbyBaller : RoleBase<RugbyBaller>
{
    public override RoleId Role { get; } = RoleId.RugbyBaller;
    public override Color32 RoleColor { get; } = Palette.ImpostorRed;
    public override List<Func<AbilityBase>> Abilities { get; } = new()
    {
        () => new RugbyBallerBallAbility(),
        // タックル能力は後でここに追加
        () => new KillableAbility(() => false)
    };

    public override QuoteMod QuoteMod { get; } = QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType { get; } = RoleTypes.Impostor;
    public override short IntroNum { get; } = 1;

    public override AssignedTeamType AssignedTeam { get; } = AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam { get; } = WinnerTeamType.Impostor;
    public override TeamTag TeamTag { get; } = TeamTag.Impostor;
    public override RoleTag[] RoleTags { get; } = [RoleTag.SpecialKiller, RoleTag.Killer, RoleTag.CustomObject];
    public override RoleOptionMenuType OptionTeam { get; } = RoleOptionMenuType.Impostor;

    // ボールシュートのクールタイム	ボールシュートが再度使用可能になるまでの時間（秒）
    [CustomOptionFloat("RugbyBallerShootCooldown", 2.5f, 60f, 2.5f, 25f)]
    public static float ShootCooldown;

    // ボールシュートのチャージ時間	ボールを発射するために必要な長押しの時間（秒）
    [CustomOptionFloat("RugbyBallerShootDuration", 0f, 10f, 0.25f, 2.0f)]
    public static float ShootDuration;

    // ボールの最大反射回数	ボールが壁に反射できる上限回数（回）
    [CustomOptionInt("RugbyBallerMaxBounceCount", 1, 10, 1, 4)]
    public static int MaxBounceCount;

    // タックルのクールタイム	タックルが再度使用可能になるまでの時間（秒）
    [CustomOptionFloat("RugbyBallerTackleCooldown", 2.5f, 60f, 2.5f, 20f)]
    public static float TackleCooldown;

    // タックルのスタン時間 タックルでクルーをスタンさせる時間（秒）
    [CustomOptionFloat("RugbyBallerTackleStunTime", 0.5f, 10f, 0.1f, 1.5f)]
    public static float TackleStunTime;

    // 自身へのスタン時間 跳ね返ったボールが自身に当たった際のスタン時間（秒）
    [CustomOptionFloat("RugbyBallerSelfStunTime", 0.5f, 10f, 0.1f, 3.0f)]
    public static float SelfStunTime;

    // 味方への移動速度低下時間	ボールが味方に当たった際の移動速度低下時間（秒）
    [CustomOptionFloat("RugbyBallerAllySlowTime", 0.5f, 10f, 0.1f, 1.5f)]
    public static float AllySlowTime;

    // 予測軌道を表示する	チャージ中に予測軌道を表示するか (ON/OFF)
    [CustomOptionBool("RugbyBallerShowTrajectory", true)]
    public static bool ShowTrajectory;
}
