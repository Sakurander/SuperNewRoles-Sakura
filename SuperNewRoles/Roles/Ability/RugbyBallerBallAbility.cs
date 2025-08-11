using System;
using UnityEngine;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Modules;
using SuperNewRoles.CustomObject;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Modules.Events;
using SuperNewRoles.Roles.Impostor; // RugbyBallerクラスのオプションにアクセスするために必要

namespace SuperNewRoles.Roles.Ability;

public class RugbyBallerBallAbility : CustomButtonBase, IButtonEffect
{
    // --- フィールド定義 ---
    public override float DefaultTimer => RugbyBaller.ShootCooldown;
    public override string buttonText => ModTranslation.GetString("RugbyBallerShootButtonText");
    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("DoorrDoorButton.png"); // TODO : 仮のアイコン
    protected override KeyType keytype => KeyType.Ability1;

    // チャージ機能 (IButtonEffect)
    public bool isEffectActive { get; set; }
    public Action OnEffectEnds => () => { new LateTask(() => ShootBall(), 0f, "RugbyBaller ShootBall Task"); };// チャージ完了時のアクション
    public float EffectDuration => RugbyBaller.ShootDuration; // チャージ時間
    public float EffectTimer { get; set; }
    public bool effectCancellable => true; // チャージキャンセル可能

    // 予測軌道描画用
    private LineRenderer trajectoryLine;
    // 速度低下用
    private EventListener<PlayerPhysicsFixedUpdateEventData> _physicsUpdateListener;

    // --- コンストラクタ ---
    public RugbyBallerBallAbility() { }

    // --- Abilityライフサイクル ---
    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();
        // 予測軌道用のLineRendererを初期化
        SetupTrajectoryLine();
        // 物理演算更新イベントを購読して速度低下を処理
        _physicsUpdateListener = PlayerPhysicsFixedUpdateEvent.Instance.AddListener(OnPhysicsFixedUpdate);
    }

    public override void DetachToLocalPlayer()
    {
        base.DetachToLocalPlayer();
        if (trajectoryLine != null)
        {
            UnityEngine.Object.Destroy(trajectoryLine.gameObject);
            trajectoryLine = null;
        }
        _physicsUpdateListener?.RemoveListener();
    }

    // --- ボタンの振る舞い ---
    public override bool CheckIsAvailable()
    {
        bool canMove = PlayerControl.LocalPlayer.CanMove;
        bool isActive = isEffectActive;
        // ★ログ5：ラグビーボーラーの使用可能条件を確認
        Logger.Info($"[RugbyBallerAbility] CheckIsAvailable: CanMove is {canMove}, isEffectActive is {isActive}");
        return canMove && !isActive;
    }

    public override void OnClick()
    {
        // チャージの開始はIButtonEffect.OnClickに任せる。
        Logger.Info("[RugbyBallerAbility] OnClick called. Charge will be started by IButtonEffect.");
        // ボタンが押されたらチャージ開始
        /*isEffectActive = true;
        EffectTimer = EffectDuration;
        if (trajectoryLine != null) trajectoryLine.enabled = true;*/
    }

    // チャージ中にボタンが離された (キャンセル)
    public void OnCancel(ActionButton actionButton)
    {
        isEffectActive = false;
        EffectTimer = EffectDuration;
        actionButton.cooldownTimerText.color = Palette.EnabledColor;
        if (trajectoryLine != null) trajectoryLine.enabled = false;

        // キャンセル時はクールダウンをリセットする
        Timer = 0f;
        actionButton.SetCoolDown(0f, DefaultTimer);
    }

    // 毎フレームの更新処理
    public override void OnUpdate()
    {
        base.OnUpdate();

        // ★ログは残しておく
        if (isEffectActive)
        {
            Logger.Info($"[RugbyBallerAbility] OnUpdate: Charging... isEffectActive={isEffectActive}, EffectTimer={EffectTimer}, EffectDuration={EffectDuration}");
        }

        if (isEffectActive && RugbyBaller.ShowTrajectory)
        {
            UpdateTrajectory();
        }
    }

    // --- 内部ロジック ---
    private void ShootBall()
    { // ★念のためログを追加
        Logger.Info("[RugbyBallerAbility] Executing ShootBall on main thread.");
        if (trajectoryLine != null) trajectoryLine.enabled = false;

        Vector3 mouseDirection = Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2);
        Vector3 shotForward = new Vector3(mouseDirection.x, mouseDirection.y, 0).normalized;

        float ballSpeed = 15f;
        Vector2 velocity = shotForward * ballSpeed;

        RugbyBall.Create(PlayerControl.LocalPlayer, PlayerControl.LocalPlayer.GetTruePosition(), velocity, RugbyBaller.MaxBounceCount);

        // TODO: AssetManager.PlaySoundFromBundle("RugbyBallerShoot");

        // ★ボール発射後もクールダウンを開始する
        ResetTimer();
    }

    // --- 予測軌道関連 ---
    private void SetupTrajectoryLine()
    {
        var lineObj = new GameObject("TrajectoryLine");
        lineObj.transform.SetParent(PlayerControl.LocalPlayer.transform);
        trajectoryLine = lineObj.AddComponent<LineRenderer>();
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.startColor = new Color(1f, 1f, 1f, 0.5f);
        trajectoryLine.endColor = new Color(1f, 1f, 1f, 0.1f);
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.1f;
        trajectoryLine.positionCount = 3; // 始点、反射点、終点
        trajectoryLine.sortingLayerName = "Players";
        trajectoryLine.enabled = false;
    }

    private void UpdateTrajectory()
    {
        if (trajectoryLine == null) return;

        Vector2 startPos = PlayerControl.LocalPlayer.GetTruePosition();
        Vector3 mouseDirection = Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2);
        Vector2 direction = new Vector2(mouseDirection.x, mouseDirection.y).normalized;

        trajectoryLine.SetPosition(0, startPos);

        // Raycastで壁との衝突を検知
        RaycastHit2D hit = Physics2D.Raycast(startPos, direction, 100f, Constants.ShipAndObjectsMask);

        if (hit.collider != null)
        {
            trajectoryLine.SetPosition(1, hit.point);

            // 反射ベクトルを計算
            Vector2 reflectedDirection = Vector2.Reflect(direction, hit.normal);
            trajectoryLine.SetPosition(2, hit.point + reflectedDirection * 10f);
        }
        else
        {
            // 壁に当たらなかった場合
            trajectoryLine.SetPosition(1, startPos + direction * 100f);
            trajectoryLine.SetPosition(2, startPos + direction * 100f); // 終点も同じ位置に
        }
    }

    // --- 速度低下 ---
    private void OnPhysicsFixedUpdate(PlayerPhysicsFixedUpdateEventData data)
    {
        if (data.Instance.AmOwner && isEffectActive)
        {
            // チャージ中は速度を半分にする
            data.Instance.body.velocity *= 0.5f;
        }
    }
}