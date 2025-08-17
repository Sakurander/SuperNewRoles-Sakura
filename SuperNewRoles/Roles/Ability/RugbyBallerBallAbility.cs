using System;
using UnityEngine;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Modules;
using SuperNewRoles.CustomObject;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Modules.Events;
using SuperNewRoles.Roles.Impostor;
using SuperNewRoles.MapDatabase;
using System.Collections.Generic; // Listを使うために追加
using System.Linq;
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
        trajectoryLine = lineObj.AddComponent<LineRenderer>();
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));

        // ★★★ Gradientを使用するとIL2CPPがうまく解釈できない。
        // これが最も安全で確実な方法
        trajectoryLine.startColor = new Color(1f, 1f, 1f, 0.5f); // 始点の色 (半透明の白)
        trajectoryLine.endColor = new Color(1f, 1f, 1f, 0.0f);   // 終点の色 (完全に透明な白)

        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.1f;
        trajectoryLine.positionCount = 0; // 頂点数はUpdateTrajectoryで動的に設定
        trajectoryLine.sortingLayerName = "Players";
        trajectoryLine.enabled = false;
    }

    // ★★★ ボール本体と同じロジックで予測軌道を計算する最終版メソッド ★★★
    private void UpdateTrajectory()
    {
        if (trajectoryLine == null || !RugbyBaller.ShowTrajectory) return;

        var mapData = MapDatabase.MapDatabase.GetCurrentMapData();
        if (mapData == null)
        {
            trajectoryLine.enabled = false;
            return;
        }

        // --- 仮想ボールのパラメータ ---
        Vector2 currentPos = PlayerControl.LocalPlayer.GetTruePosition();
        Vector3 mouseDirection = Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2);
        Vector2 currentVelocity = new Vector2(mouseDirection.x, mouseDirection.y).normalized * RugbyBaller.BallSpeed;
        if (currentVelocity.sqrMagnitude < 0.1f) // 無入力時の方向を補正
        {
            currentVelocity = (PlayerControl.LocalPlayer.MyPhysics.FlipX ? Vector2.left : Vector2.right) * RugbyBaller.BallSpeed;
        }

        var points = new List<Vector2> { currentPos };

        // --- 軌道シミュレーション ---
        // 1回目の反射までを計算
        // 1. まず、物理的な壁(ShipOnlyMask)に当たるまでの軌跡を計算
        RaycastHit2D wallHit = Physics2D.Raycast(currentPos, currentVelocity.normalized, 100f, Constants.ShipOnlyMask);
        float maxDistance = (wallHit.collider != null) ? wallHit.distance : 100f;

        // 2. MapDatabaseを使って、壁の手前にあるオブジェクトとの衝突をシミュレート
        bool hasHitObject = false;
        Vector2 firstHitPoint = currentPos + currentVelocity.normalized * maxDistance;

        // 軌道上を細かくチェック
        for (float dist = 0.1f; dist < maxDistance; dist += 0.1f)
        {
            Vector2 checkPos = currentPos + currentVelocity.normalized * dist;
            if (!mapData.CheckMapArea(checkPos))
            {
                // マップ外に出た＝机などのオブジェクトに衝突した
                firstHitPoint = checkPos;
                hasHitObject = true;
                break; // 最初の衝突点が見つかったのでループを抜ける
            }
        }

        points.Add(firstHitPoint);

        // 3. 物理的な壁に当たった場合のみ、反射後の軌道を計算
        if (!hasHitObject && wallHit.collider != null)
        {
            Vector2 reflectedVelocity = Vector2.Reflect(currentVelocity, wallHit.normal);
            points.Add(wallHit.point + reflectedVelocity.normalized * 10f); // 反射後、少しだけ線を描画
        }

        // --- LineRendererに頂点を設定 ---
        trajectoryLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            trajectoryLine.SetPosition(i, points[i]);
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