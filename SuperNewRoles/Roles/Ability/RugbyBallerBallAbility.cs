using System;
using UnityEngine;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Modules;
using SuperNewRoles.CustomObject;
using SuperNewRoles.Roles.Impostor;

namespace SuperNewRoles.Roles.Ability;

public class RugbyBallerBallAbility : CustomButtonBase, IButtonEffect
{
    public override float DefaultTimer => RugbyBaller.ShootCooldown;
    public override string buttonText => ModTranslation.GetString("RugbyBallerShootButtonText");
    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("DoorrDoorButton.png"); // TODO : 仮のアイコン
    protected override KeyType keytype => KeyType.Ability1;

    // IButtonEffect の実装 (チャージ機能)
    public bool isEffectActive { get; set; }
    public Action OnEffectEnds => ShootBall; // チャージ完了時のアクション
    public float EffectDuration => RugbyBaller.ShootDuration; // チャージ時間
    public float EffectTimer { get; set; }
    public bool effectCancellable => true; // チャージキャンセル可能

    public RugbyBallerBallAbility()
    {
        // 初期化
    }

    public override bool CheckIsAvailable()
    {
        // 通常キルボタンと競合しないように、ここではチャージ中でないときだけtrueを返す
        return PlayerControl.LocalPlayer.CanMove && !isEffectActive;
    }

    public override void OnClick()
    {
        // ボタンが押されたらチャージ開始
        isEffectActive = true;
        EffectTimer = EffectDuration;

        // チャージ中は移動速度を低下させる
        // (PlayerPhysicsの速度を直接変更するパッチが必要になります)
    }

    private void ShootBall()
    {
        // Kunoichi.cs を参考に、マウスの方向を取得
        Vector3 mouseDirection = Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2);
        Vector3 shotForward = new Vector3(mouseDirection.x, mouseDirection.y, 0).normalized;

        // ボールの初速を設定
        float ballSpeed = 10f; // 速度は調整が必要
        Vector2 velocity = shotForward * ballSpeed;

        // ボールを生成
        new RugbyBall(PlayerControl.LocalPlayer.GetTruePosition(), velocity, RugbyBaller.MaxBounceCount);

        // 発射音を再生する処理をここに追加します

        // クールダウンを開始
        ResetTimer();
    }

    // チャージ中にボタンが離された場合 (IButtonEffectのデフォルト実装をオーバーライド)
    public void OnCancel(ActionButton actionButton)
    {
        isEffectActive = false;
        // チャージが中断されたのでタイマーをリセット
        EffectTimer = EffectDuration;
        actionButton.cooldownTimerText.color = Palette.EnabledColor;
    }
}