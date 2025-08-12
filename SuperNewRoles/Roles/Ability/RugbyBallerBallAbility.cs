using System;
using UnityEngine;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Modules;
using SuperNewRoles.CustomObject;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Modules.Events;
using SuperNewRoles.Roles.Impostor;

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
    public Action OnEffectEnds => ShootBall;
    public float EffectDuration => RugbyBaller.ShootDuration;
    public float EffectTimer { get; set; }
    public bool effectCancellable => true;

    // 管理するボールオブジェクト
    public RugbyBallObject activeBall;

    // 予測軌道用
    private LineRenderer trajectoryLine;
    private EventListener<PlayerPhysicsFixedUpdateEventData> _physicsUpdateListener;

    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();
        SetupTrajectoryLine();
        _physicsUpdateListener = PlayerPhysicsFixedUpdateEvent.Instance.AddListener(OnPhysicsFixedUpdate);
    }

    public override void DetachToLocalPlayer()
    {
        base.DetachToLocalPlayer();
        activeBall?.Detach(); // 自身がデタッチされる際にボールも破棄
        if (trajectoryLine != null)
        {
            UnityEngine.Object.Destroy(trajectoryLine.gameObject);
        }
        _physicsUpdateListener?.RemoveListener();
    }

    public override bool CheckIsAvailable() => PlayerControl.LocalPlayer.CanMove && !isEffectActive;

    public override void OnClick()
    {
        // OnClickではチャージ開始の準備をするだけ
        if (trajectoryLine != null) trajectoryLine.enabled = RugbyBaller.ShowTrajectory;
    }

    // 毎フレームの更新処理
    public override void OnUpdate()
    {
        base.OnUpdate();
        if (isEffectActive && RugbyBaller.ShowTrajectory)
        {
            UpdateTrajectory();
        }
    }

    private void ShootBall()
    {
        if (trajectoryLine != null) trajectoryLine.enabled = false;

        // マウスカーソル（またはジョイスティック）の方向を取得
        Vector3 mouseDirection = Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2);
        // TODO ホストがコントローラーを使っている場合も考慮（これはより高度な実装）
        // var joystickDirection = DestroyableSingleton<HudManager>.Instance.joystick.Delta;

        Vector3 shotForward = new Vector3(mouseDirection.x, mouseDirection.y, 0).normalized;

        // 速度が0にならないように最低限のベクトルを保証
        if (shotForward.sqrMagnitude < 0.1f)
        {
            shotForward = PlayerControl.LocalPlayer.MyPhysics.FlipX ? Vector3.left : Vector3.right;
        }

        float ballSpeed = RugbyBaller.BallSpeed;
        Vector2 velocity = shotForward * ballSpeed;

        RpcSpawnRugbyBall(PlayerControl.LocalPlayer, PlayerControl.LocalPlayer.GetTruePosition(), velocity, RugbyBaller.MaxBounceCount);

        ResetTimer();
    }

    [CustomRPC]
    public static void RpcSpawnRugbyBall(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        ExPlayerControl exOwner = owner;
        if (exOwner.TryGetAbility<RugbyBallerBallAbility>(out var ability))
        {
            // 古いボールが残っていれば破棄
            ability.activeBall?.Detach();
            // 新しいボールを生成して保持
            ability.activeBall = new RugbyBallObject(owner, position, velocity, maxBounces);
        }
    }

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