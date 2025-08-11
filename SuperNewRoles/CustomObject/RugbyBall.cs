using UnityEngine;
using SuperNewRoles.Modules;

namespace SuperNewRoles.CustomObject;

// MonoBehaviourを継承した1つのクラスに統合
public class RugbyBall : MonoBehaviour
{
    private PlayerControl owner;
    private int maxBounces;
    private int currentBounces = 0;
    private float lifeTime = 10f;

    private static Sprite _sprite;

    // RugbyBallオブジェクトを安全に生成して初期化する静的メソッド
    public static void Create(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        // まずGameObjectを生成
        var ballObject = new GameObject("RugbyBall")
        {
            layer = LayerMask.NameToLayer("Players")
        };
        ballObject.transform.position = position;

        // 次にこのRugbyBallコンポーネントを追加
        var ballComponent = ballObject.AddComponent<RugbyBall>();

        // コンポーネントの初期化メソッドを呼び出す
        ballComponent.Initialize(owner, maxBounces, velocity);
    }

    // 初期化メソッド
    private void Initialize(PlayerControl owner, int maxBounces, Vector2 velocity)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;

        // スプライトの設定
        var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        if (_sprite == null)
        {// ここでラグビーボールの画像ファイルを読み込みます
            _sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png"); // TODO : 画像ファイル名を適切に変更
        }
        spriteRenderer.sprite = _sprite;

        // 物理演算の設定
        var body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.velocity = velocity;
        body.angularDrag = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 衝突検出精度を上げる

        var collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.2f;

        var physicsMaterial = new PhysicsMaterial2D
        {
            bounciness = 1.0f,
            friction = 0.0f
        };
        collider.sharedMaterial = physicsMaterial;
    }

    void Update()
    {
        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0)
        {
            Destroy(gameObject);
        }

        // 常に進行方向に回転させる
        var body = GetComponent<Rigidbody2D>();
        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // プレイヤーに当たったかチェック
        var target = collision.gameObject.GetComponent<ExPlayerControl>();
        if (target != null)
        {
            if (target.PlayerId == owner.PlayerId || (target.Data.Role.IsImpostor && owner.Data.Role.IsImpostor))
            {
                return;
            }

            if (!target.Data.IsDead)
            {
                target.CustomDeath(CustomDeathType.Kill, source: ExPlayerControl.LocalPlayer);
            }
            Destroy(gameObject);
            return;
        }

        // 壁に当たった場合
        currentBounces++;
        // バウンド音を再生
        // TODO AssetManager.PlaySoundFromBundle("RugbyBallerBounce");

        if (currentBounces >= maxBounces)
        {
            Destroy(gameObject);
        }
    }
}