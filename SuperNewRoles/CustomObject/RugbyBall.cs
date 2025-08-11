using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Roles.Impostor;
using System.Linq;

namespace SuperNewRoles.CustomObject;

public class RugbyBallObject
{
    private PlayerControl owner;
    private int maxBounces;
    private int currentBounces = 0;
    private float lifeTime = 10f;
    private bool detached = false;
    private float invincibilityTimer = 0.2f;

    private GameObject ballObject;
    private Rigidbody2D body;
    private CircleCollider2D ballCollider;
    private EventListener fixedUpdateEvent;

    // ★★★ 衝突判定に必要なマスクをここで定義 ★★★
    private static int collisionMask = -1;

    public RugbyBallObject(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;

        // ★★★ レイヤーマスクの初期化（一度だけ行う） ★★★
        if (collisionMask == -1)
        {
            // 壁、オブジェクト、そしてプレイヤー自身を検出対象とする
            collisionMask = Constants.ShipAndObjectsMask | Constants.PlayersOnlyMask;
        }

        ballObject = new GameObject("RugbyBall_Physics");
        // レイヤーはGhostのまま。物理的な押し出しは行わないため。
        ballObject.layer = LayerMask.NameToLayer("Ghost");
        ballObject.transform.position = position;

        var spriteRenderer = ballObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png"); // TODO: 仮
        spriteRenderer.sortingLayerName = "Players";

        body = ballObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.velocity = velocity;

        ballCollider = ballObject.AddComponent<CircleCollider2D>();
        ballCollider.radius = 0.2f;
        // isTriggerはfalseのまま。Castで衝突を検知する。
        ballCollider.isTrigger = false;

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

        // ★★★ 新しい衝突解決メソッドを呼び出す ★★★
        ResolveCollisionsAndMove();

        // 回転処理
        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            ballObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    // ★★★ 衝突解決と移動をまとめて行う新メソッド ★★★
    private void ResolveCollisionsAndMove()
    {
        if (body == null || ballCollider == null) return;

        float deltaTime = Time.fixedDeltaTime;
        Vector2 currentVelocity = body.velocity;
        float distanceToMove = currentVelocity.magnitude * deltaTime;

        // 移動距離が非常に小さい場合は処理をスキップ
        if (distanceToMove < 0.001f)
        {
            return;
        }

        // 1. 移動経路上にある全ての衝突を検出
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            body.position,
            ballCollider.radius,
            currentVelocity.normalized,
            distanceToMove,
            collisionMask
        );

        // 2. 衝突があった場合
        if (hits.Length > 0)
        {
            // ★★★ 自分自身とオーナーを除外してから、最も近い衝突オブジェクトを取得 ★★★
            var hit = hits
                .Where(h => !h.collider.isTrigger && h.collider.gameObject != this.ballObject && h.collider.gameObject != owner.gameObject)
                .OrderBy(h => h.distance)
                .FirstOrDefault();

            // 有効な衝突があった場合
            if (hit.collider != null)
            {
                // 衝突点まで移動
                body.position = body.position + currentVelocity.normalized * hit.distance;

                // プレイヤーとの衝突か？
                PlayerControl hitPlayer = hit.collider.GetComponent<PlayerControl>();
                if (hitPlayer != null && invincibilityTimer <= 0)
                {
                    HandlePlayerCollision(hitPlayer);
                    return;
                }
                else if (hitPlayer == null) // プレイヤー以外のオブジェクト（壁など）との衝突
                {
                    // 速度を反射させる
                    Vector2 reflectedVelocity = Vector2.Reflect(currentVelocity, hit.normal);
                    body.velocity = reflectedVelocity * 0.95f; // 少し減速

                    currentBounces++;
                    // TODO: バウンド音
                    Logger.Info($"RugbyBall bounced on {hit.collider.name}! ({currentBounces}/{maxBounces})", "RugbyBaller");

                    if (currentBounces >= maxBounces)
                    {
                        Detach();
                        return;
                    }
                }
            }
            else // 有効な衝突がない場合（自分やオーナーのみだった場合）
            {
                body.position += currentVelocity * deltaTime;
            }
        }
        // 3. 衝突がなかった場合
        else
        {
            body.position += currentVelocity * deltaTime;
        }
    }


    private void HandlePlayerCollision(PlayerControl targetPlayer)
    {
        if (targetPlayer.Data.IsDead) return;

        // ★★★ 味方インポスターへの衝突判定を修正 ★★★
        if (targetPlayer.Data.Role != null && targetPlayer.Data.Role.IsImpostor && owner.Data.Role != null && owner.Data.Role.IsImpostor)
        {
            // 自分自身はここでは処理しない（既に上で除外されているため）
            if (targetPlayer.PlayerId != owner.PlayerId)
            {
                RpcHandleAllyCollision(targetPlayer.PlayerId);
                // 味方に当たった場合は跳ね返る
                body.velocity *= -0.5f;
            }
            return;
        }

        // それ以外のプレイヤー（クルー陣営）への衝突（キル）
        if (owner.AmOwner)
        {
            RpcKillTarget(owner.PlayerId, targetPlayer.PlayerId);
        }
        Detach();
    }


    // --- RPCs (変更なし) ---
    [CustomRPC]
    public static void RpcKillTarget(byte ownerId, byte targetId)
    {
        ExPlayerControl exOwner = ExPlayerControl.ById(ownerId);
        ExPlayerControl exTarget = ExPlayerControl.ById(targetId);

        if (exTarget != null && exOwner != null && exTarget.IsAlive())
        {
            exOwner.RpcCustomDeath(exTarget, CustomDeathType.Kill); // いずれ RugbyBall に変更
        }
    }

    [CustomRPC]
    public static void RpcHandleSelfCollision(byte ownerId)
    {
        ExPlayerControl exOwner = ExPlayerControl.ById(ownerId);
        if (exOwner != null && exOwner.IsAlive())
        {
            // ★★★ スタン処理を実装 ★★★
            if (exOwner.AmOwner)
            {
                // 一定時間移動不可にする
                exOwner.Player.moveable = false;
                new LateTask(() =>
                {
                    if (exOwner != null && exOwner.Player != null)
                    {
                        exOwner.Player.moveable = true;
                    }
                }, RugbyBaller.SelfStunTime);
            }
            Logger.Info($"RugbyBaller {exOwner.Player.name} stunned themself!", "RugbyBaller");
        }
    }

    [CustomRPC]
    public static void RpcHandleAllyCollision(byte allyId)
    {
        ExPlayerControl exAlly = ExPlayerControl.ById(allyId);
        if (exAlly != null && exAlly.IsAlive())
        {
            // ★★★ スロー効果を実装 ★★★
            if (exAlly.AmOwner)
            {
                // SpeedBoosterのロジックを参考に速度を変更する
                float originalSpeed = exAlly.Player.MyPhysics.Speed;
                exAlly.Player.MyPhysics.Speed *= 0.5f; // 例として速度を半分に
                new LateTask(() =>
                {
                    if (exAlly != null && exAlly.Player != null)
                    {
                        exAlly.Player.MyPhysics.Speed = originalSpeed;
                    }
                }, RugbyBaller.AllySlowTime);
            }
            Logger.Info($"RugbyBaller hit ally {exAlly.Player.name}!", "RugbyBaller");
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