using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Modules.Events.Bases;

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
        if (lifeTime <= 0 || owner == null || owner.Data.IsDead)
        {
            Detach();
            return;
        }

        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            ballObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    // ヘルパーコンポーネントから呼び出される
    public void HandleCollision(Collision2D collision)
    {
        if (detached) return;

        PlayerControl target = collision.gameObject.GetComponent<PlayerControl>();
        if (target != null)
        {
            //if (target.PlayerId == owner.PlayerId || (target.Data.Role.IsImpostor && owner.Data.Role.IsImpostor)) return;

            if (!target.Data.IsDead)
            {
                Logger.Info($"[RugbyBallObject] Hit Player: {target.PlayerId} by {owner.PlayerId}");
                // ModHelpers.CheckMurderAttemptAndKill(owner, target, showAnimation: false, CustomDeathType.Rugby);
            }
            Detach(); // プレイヤーに当たったら消滅
            return;
        }

        // 壁に当たった場合
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