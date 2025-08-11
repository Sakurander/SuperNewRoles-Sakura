using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Roles.Impostor;

namespace SuperNewRoles.CustomObject;

public class RugbyBallObject
{
    private PlayerControl owner;
    private int maxBounces;
    private int currentBounces = 0;
    private float lifeTime = 10f; // ボールの生存期間
    private bool detached = false;
    private float invincibilityTimer = 0.2f; // 生成直後の無敵時間（自分や味方に即ヒットするのを防ぐ）

    private GameObject ballObject;
    private Rigidbody2D body;
    private CircleCollider2D ballCollider; // 物理的なコライダーを追加
    private EventListener fixedUpdateEvent;

    public RugbyBallObject(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;

        // --- オブジェクトのセットアップ ---
        ballObject = new GameObject("RugbyBall_Physics");
        ballObject.layer = LayerMask.NameToLayer("Ghost"); // 他プレイヤーを押さないようにGhostレイヤーに設定
        ballObject.transform.position = position;

        var spriteRenderer = ballObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png"); // TODO: 仮のスプライト
        spriteRenderer.sortingLayerName = "Players";
        spriteRenderer.sortingOrder = 1; // プレイヤーより少し手前に表示

        // --- Rigidbody2Dのセットアップ ---
        body = ballObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // すり抜け防止
        body.sharedMaterial = new PhysicsMaterial2D("Bouncy") { bounciness = 1.0f, friction = 0f };
        body.velocity = velocity;
        body.angularDrag = 0f; // 回転による減速なし

        // --- Collider2Dのセットアップ ---
        ballCollider = ballObject.AddComponent<CircleCollider2D>();
        ballCollider.radius = 0.2f;
        ballCollider.isTrigger = false; // 物理的な衝突を検知するためTriggerはOFF

        // --- イベントリスナーの登録 ---
        fixedUpdateEvent = FixedUpdateEvent.Instance.AddListener(OnFixedUpdate);
    }

    private void OnFixedUpdate()
    {
        if (detached) return;

        lifeTime -= Time.fixedDeltaTime;
        if (lifeTime <= 0 || owner == null || owner.Data.IsDead)
        {
            Detach();
            return;
        }

        if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.fixedDeltaTime;
        }

        // 常に壁との衝突をチェック
        CheckForWallAndPlayerCollision();

        // 回転処理
        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            ballObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    // ★★★ 物理エンジンベースの新しい衝突判定メソッド ★★★
    private void CheckForWallAndPlayerCollision()
    {
        if (body == null) return;

        // 1. 次のフレームの位置を計算
        Vector2 nextPosition = (Vector2)ballObject.transform.position + body.velocity * Time.fixedDeltaTime;

        // 2. その位置に移動しようとした場合に衝突するオブジェクトを検出
        RaycastHit2D[] hits = new RaycastHit2D[1];
        int hitCount = ballCollider.Cast(body.velocity.normalized, hits, body.velocity.magnitude * Time.fixedDeltaTime);

        if (hitCount > 0)
        {
            RaycastHit2D hit = hits[0];

            // --- プレイヤーとの衝突判定 (ホストのみ) ---
            if (owner.AmOwner && invincibilityTimer <= 0)
            {
                var hitPlayer = hit.collider.GetComponent<PlayerControl>();
                if (hitPlayer != null && !hitPlayer.Data.IsDead)
                {
                    HandlePlayerCollision(hitPlayer);
                    return; // プレイヤーに当たったらそのフレームは終了
                }
            }

            // --- 壁との衝突判定 ---
            // isTriggerでないコライダーはすべて壁とみなす
            if (!hit.collider.isTrigger)
            {
                // バウンド音を再生
                // AssetManager.PlaySoundFromBundle("RugbyBallBounce.wav", false, 0.5f, ballObject.transform.position);

                currentBounces++;
                Logger.Info($"RugbyBall bounced on Collider: {hit.collider.name}! ({currentBounces}/{maxBounces})", "RugbyBaller");

                // 速度を少し減速させる（お好みで調整）
                body.velocity *= 0.95f;

                if (currentBounces >= maxBounces)
                {
                    Detach();
                }
            }
        }

        // 3. 衝突がなければ、計算した次の位置へ移動
        body.MovePosition(nextPosition);
    }

    // プレイヤー衝突処理を分離
    private void HandlePlayerCollision(PlayerControl targetPlayer)
    {
        // 自分自身への衝突（自爆スタン）
        if (targetPlayer.PlayerId == owner.PlayerId)
        {
            RpcHandleSelfCollision(owner.PlayerId);
            Detach();
            return;
        }

        // 味方インポスターへの衝突（スロー効果）
        if (targetPlayer.Data.Role.IsImpostor && owner.Data.Role.IsImpostor)
        {
            RpcHandleAllyCollision(targetPlayer.PlayerId);
            // 味方に当たった場合はボールを消さずに跳ね返す（お好みで変更）
            // Detach();
            return;
        }

        // それ以外のプレイヤーへの衝突（キル）
        RpcKillTarget(owner.PlayerId, targetPlayer.PlayerId);
        Detach();
    }


    [CustomRPC]
    public static void RpcKillTarget(byte ownerId, byte targetId)
    {
        ExPlayerControl exOwner = ExPlayerControl.ById(ownerId);
        ExPlayerControl exTarget = ExPlayerControl.ById(targetId);

        if (exTarget != null && exOwner != null && exTarget.IsAlive())
        {
            // 特殊な死体を実装するまでは通常のキル
            exOwner.RpcCustomDeath(exTarget, CustomDeathType.RugbyBall);
        }
    }

    [CustomRPC]
    public static void RpcHandleSelfCollision(byte ownerId)
    {
        ExPlayerControl exOwner = ExPlayerControl.ById(ownerId);
        if (exOwner != null && exOwner.IsAlive())
        {
            // TODO: スタン処理を実装
            SuperNewRoles.Logger.Info($"RugbyBaller {exOwner.Player.name} stunned themself!");
        }
    }

    [CustomRPC]
    public static void RpcHandleAllyCollision(byte allyId)
    {
        ExPlayerControl exAlly = ExPlayerControl.ById(allyId);
        if (exAlly != null && exAlly.IsAlive())
        {
            // TODO: スロー効果を実装
            SuperNewRoles.Logger.Info($"RugbyBaller hit ally {exAlly.Player.name}!");
        }
    }

    public void Detach()
    {
        if (detached) return;
        detached = true;
        fixedUpdateEvent?.RemoveListener();
        if (ballObject != null)
        {
            UnityEngine.Object.Destroy(ballObject);
        }
    }
}