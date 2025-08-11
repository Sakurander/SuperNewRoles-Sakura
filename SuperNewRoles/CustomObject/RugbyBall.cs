using System.Linq;
using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Roles.Impostor; // CustomDeathType.Rugby のため

namespace SuperNewRoles.CustomObject;

public class RugbyBallObject
{
    private PlayerControl owner;
    private int maxBounces;
    private float lifeTime = 10f;
    private bool detached = false;

    private GameObject ballObject;
    private Rigidbody2D body;
    private EventListener fixedUpdateEvent;
    private RugbyBallCollisionHelper collisionHelper;
    private bool isPlayerHit = false; // プレイヤーに命中したかどうかのフラグ

    public RugbyBallObject(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;

        // ★コンストラクタ内でGameObjectとコンポーネントを生成
        ballObject = new GameObject("RugbyBall")
        {
            layer = LayerMask.NameToLayer("Players")
        };
        ballObject.transform.position = position;

        var spriteRenderer = ballObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png"); // 画像名を適切に

        body = ballObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.velocity = velocity;
        body.angularDrag = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = ballObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.2f;

        var physicsMaterial = new PhysicsMaterial2D { bounciness = 1.0f, friction = 0.0f };
        collider.sharedMaterial = physicsMaterial;

        // 衝突検知用のヘルパーコンポーネントを追加
        collisionHelper = ballObject.AddComponent<RugbyBallCollisionHelper>();
        collisionHelper.Initialize(this);

        fixedUpdateEvent = FixedUpdateEvent.Instance.AddListener(OnFixedUpdate);
    }

    public void OnFixedUpdate()
    {
        if (detached) return;

        lifeTime -= Time.fixedDeltaTime;
        if (lifeTime <= 0 || owner == null || owner.Data.IsDead || isPlayerHit)
        {
            Detach();
            return;
        }

        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            ballObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // --- ★ホストのみが当たり判定を実行 ---
        if (owner.AmOwner)
        {
            CheckForPlayerCollision();
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
                    // ★キル処理をRPC経由で実行するように変更
                    RpcKillTarget(owner.PlayerId, target.PlayerId);
                    isPlayerHit = true; // 命中フラグを立てて、次のフレームでオブジェクトを消す
                    break; // 一人倒したらループを抜ける
                }
                else
                {
                    // TODO: 味方インポスターへのスロー効果を後で実装
                }
            }
        }
    }

    // ★キルを実行するRPCメソッド
    [CustomRPC]
    public static void RpcKillTarget(byte ownerId, byte targetId)
    {
        ExPlayerControl exOwner = ExPlayerControl.ById(ownerId);
        ExPlayerControl exTarget = ExPlayerControl.ById(targetId);

        if (exTarget != null && exOwner != null && exTarget.IsAlive())
        {
            // 各クライアントが、自分のローカルプレイヤーを"source"としてキルを実行する
            // これにより、キルアニメーションや死体生成が正しく同期される
            exOwner.RpcCustomDeath(exTarget, CustomDeathType.WaveCannon);// TODO: CDTのじっそう
        }
    }


    public void HandleCollision(Collision2D collision)
    {
        if (detached) return;

        // プレイヤーとの衝突はFixedUpdateで処理するため、ここでは何もしない
        if (collision.gameObject.GetComponent<PlayerControl>() != null)
        {
            return;
        }

        collisionHelper.currentBounces++;
        // TODO: バウンド音

        if (collisionHelper.currentBounces >= maxBounces)
        {
            Detach();
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

// 衝突イベントを受け取るためだけのシンプルなMonoBehaviour
public class RugbyBallCollisionHelper : MonoBehaviour
{
    private RugbyBallObject parent;
    public int currentBounces = 0;

    public void Initialize(RugbyBallObject parent)
    {
        this.parent = parent;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        parent?.HandleCollision(collision);
    }
}