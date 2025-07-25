using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules.Events.Bases;

namespace SuperNewRoles.Roles.Impostor;

class EvilSeer : RoleBase<EvilSeer>
{
    public override RoleId Role { get; } = RoleId.EvilSeer;
    public override Color32 RoleColor { get; } = Palette.ImpostorRed;
    public override List<Func<AbilityBase>> Abilities { get; } = [
        () => new SeerAbility(new SeerData() {
            Mode = EvilSeerSeerMode,
            LimitSoulDuration = EvilSeerLimitSoulDuration,
            SoulDuration = EvilSeerSoulDuration,
            FlashColorMode =  EvilSeerAbilityColorModeSelect is EvilSeerAbility.All or EvilSeerAbility.FlashOnly ? EvilSeerColorMode : DeadBodyColorMode.None,
            SoulColorMode = EvilSeerAbilityColorModeSelect is EvilSeerAbility.All or EvilSeerAbility.SoulOnly ? EvilSeerColorMode : DeadBodyColorMode.None
        }),
        () => new DeadBodyArrowsAbility(() => EvilSeerShowArrows, colorMode : EvilSeerAbilityColorModeSelect is EvilSeerAbility.All or EvilSeerAbility.ArrowOnly ? EvilSeerColorMode : DeadBodyColorMode.None),
        () => new EvilSeerHauntAbility(EvilSeerHauntMode is EvilSeerHauntMode.Both or EvilSeerHauntMode.OnlyEvilSeerHaunt),
        () => new CustomModifierGrantedButtonAbility(new(
        canGrantModifier: (granted) => !granted && EvilSeerHauntMode is EvilSeerHauntMode.Both or EvilSeerHauntMode.OnlyOthersHaunt,
        cooldown: () => EvilSeerCreateAbilityCooldown,
        modifierRole: () => ModifierRoleId.ModifierHauntedWolf,
        buttonSprite: AssetManager.GetAsset<Sprite>("EvilSeerHauntButton.png"),
        buttonText: ModTranslation.GetString("EvilSeerHauntButtonText")
        ))
    ];

    public override QuoteMod QuoteMod { get; } = QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType { get; } = RoleTypes.Phantom;
    public override short IntroNum { get; } = 1;

    public override AssignedTeamType AssignedTeam { get; } = AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam { get; } = WinnerTeamType.Impostor;
    public override TeamTag TeamTag { get; } = TeamTag.Impostor;
    public override RoleTag[] RoleTags { get; } = [RoleTag.Information];
    public override RoleOptionMenuType OptionTeam { get; } = RoleOptionMenuType.Impostor;


    [CustomOptionSelect("EvilSeer.SeerColorMode", typeof(DeadBodyColorMode), "Seer.SeerColorMode.", translationName: "Seer.SeerColorMode")]
    public static DeadBodyColorMode EvilSeerColorMode = DeadBodyColorMode.None;
    [CustomOptionSelect("EvilSeer.AbilityColorModeSelect", typeof(EvilSeerAbility), "EvilSeer.AbilityColorModeSelect.", translationName: "EvilSeer.AbilityColorModeSelect", parentFieldName: nameof(EvilSeerColorMode))]
    public static EvilSeerAbility EvilSeerAbilityColorModeSelect = EvilSeerAbility.SoulOnly;


    [CustomOptionSelect("EvilSeer.Mode", typeof(CrewMate.SeerMode), "Seer.Mode.", translationName: "Seer.Mode")]
    public static CrewMate.SeerMode EvilSeerSeerMode = CrewMate.SeerMode.Both;


    [CustomOptionBool("EvilSeer.LimitSoulDuration", false, translationName: "Seer.LimitSoulDuration")]
    public static bool EvilSeerLimitSoulDuration = false;

    [CustomOptionFloat("EvilSeer.SoulDuration", 2.5f, 60f, 2.5f, 30f, translationName: "Seer.SoulDuration", parentFieldName: nameof(EvilSeerLimitSoulDuration))]
    public static float EvilSeerSoulDuration = 30f;


    [CustomOptionBool("EvilSeer.ShowArrows", true, translationName: "VultureShowArrows")]
    public static bool EvilSeerShowArrows = true;

    [CustomOptionSelect("EvilSeer.EvilSeerHauntMode", typeof(EvilSeerHauntMode), "EvilSeer.EvilSeerHauntMode.", translationName: "EvilSeer.EvilSeerHauntMode")]
    public static EvilSeerHauntMode EvilSeerHauntMode;

    [CustomOptionFloat("EvilSeer.CreateAbilityCooldown", 2.5f, 60f, 2.5f, 30f, parentFieldName: nameof(EvilSeerHauntMode))]
    public static float EvilSeerCreateAbilityCooldown;
}

public enum EvilSeerAbility
{
    All,
    FlashOnly,
    SoulOnly,
    ArrowOnly
}

public enum EvilSeerHauntMode
{
    None, // 無し
    Both, // 両方
    OnlyOthersHaunt, // 取り憑かせる
    OnlyEvilSeerHaunt, // 取り憑く
}

public class EvilSeerHauntAbility(bool canHaunt) : AbilityBase
{
    private EventListener<DieEventData> dieEventListener;

    private readonly bool CanHaunt = canHaunt;

    public override void AttachToLocalPlayer() =>
        dieEventListener = DieEvent.Instance.AddListener(OnPlayerDead);
    public override void DetachToLocalPlayer()
    {
        dieEventListener?.RemoveListener();
        base.DetachToLocalPlayer();
    }

    private void OnPlayerDead(DieEventData data)
    {
        if (!CanHaunt) return;
        if (data.player != PlayerControl.LocalPlayer) return;

        // MurderData 記録待ちの為に遅延を入れている
        new LateTask(() =>
        {
            if (MurderDataManager.TryGetMurderData(data.player, out var murderData))
            {
                if (murderData.Target != murderData.Killer)
                    murderData.Killer.RpcCustomSetModifierRoleInGame(ModifierRoleId.ModifierHauntedWolf);
            }
        }, 1f);
    }
}