using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Modules.Events.Bases;

namespace SuperNewRoles.CustomObject;

public class RugbyBallObject
{
    private PlayerControl owner;
    private int maxBounces;
    private int currentBounces = 0;
    private float lifeTime = 10f;
    private bool detached = false;

    private GameObject ballObject;
    private Rigidbody2D body;
    private EventListener fixedUpdateEvent;

    public RugbyBallObject(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;

        ballObject = new GameObject("RugbyBall")
        { // レイヤーをGhostにすることで、物理エンジンによる衝突を避けつつRaycastの対象にする
            layer = LayerMask.NameToLayer("Ghost")
        };
        ballObject.transform.position = position;

        var spriteRenderer = ballObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png");

        body = ballObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.velocity = velocity;
        body.angularDrag = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;


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

        // --- ★自前の壁衝突判定ロジック ---
        CheckForWallCollision();

        // 進行方向に回転
        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            ballObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // ホストのみがプレイヤーへの当たり判定を実行
        if (owner.AmOwner)
        {
            CheckForPlayerCollision();
        }
    }

    private void CheckForWallCollision()
    {
        float speed = body.velocity.magnitude;
        Vector2 direction = body.velocity.normalized;
        // 1フレーム先に進んだ位置を予測し、少しだけ長い距離でRayを飛ばす
        float distance = speed * Time.fixedDeltaTime + 0.1f;

        RaycastHit2D hit = Physics2D.Raycast(ballObject.transform.position, direction, distance, Constants.ShipAndObjectsMask);

        if (hit.collider != null)
        {
            // 壁に当たった場合、速度を反射させる
            Vector2 reflectedVelocity = Vector2.Reflect(body.velocity, hit.normal);
            body.velocity = reflectedVelocity;

            currentBounces++;
            // TODO: バウンド音を再生

            if (currentBounces >= maxBounces)
            {
                Detach();
            }
        }
    }

    // 波動砲のロジックを参考にした当たり判定
    private void CheckForPlayerCollision()
    {// IsTouchingAllは重いので、OverlapCircleで代用
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(ballObject.transform.position, 0.2f, Constants.PlayersOnlyMask);

        foreach (var hitCollider in hitColliders)
        {
            PlayerControl target = hitCollider.GetComponent<PlayerControl>();
            if (target != null && target.PlayerId != owner.PlayerId && !target.Data.IsDead)
            {
                bool isImpostorTeammate = target.Data.Role.IsImpostor && owner.Data.Role.IsImpostor;

                if (!isImpostorTeammate)
                {
                    RpcKillTarget(owner.PlayerId, target.PlayerId);
                    // ★ isPlayerHitフラグは不要。DetachはRPCを受け取った各クライアントでボールが消えることで実現する。
                    // サーバー側（ホスト）では次のフレームでDetach()が呼ばれる。
                    Detach();
                    break;
                }
            }
        }
    }

    [CustomRPC]
    public static void RpcKillTarget(byte ownerId, byte targetId)
    {
        ExPlayerControl exOwner = ExPlayerControl.ById(ownerId);
        ExPlayerControl exTarget = ExPlayerControl.ById(targetId);

        if (exTarget != null && exOwner != null && exTarget.IsAlive())
        {
            exOwner.RpcCustomDeath(exTarget, CustomDeathType.RugbyBall);
        }
    }

    public void Detach()
    {
        if (detached) return;
        detached = true;

        fixedUpdateEvent?.RemoveListener();

        if (ballObject != null)
        {
            Object.Destroy(ballObject);
        }
    }
}