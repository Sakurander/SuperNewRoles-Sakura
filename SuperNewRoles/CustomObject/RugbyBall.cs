using UnityEngine;
using SuperNewRoles.Modules;

namespace SuperNewRoles.CustomObject;

public class RugbyBall
{
    public GameObject ballObject;
    public SpriteRenderer spriteRenderer;
    public RugbyBallBehaviour behaviour;

    private static Sprite sprite;
    public static Sprite GetSprite()
    {
        if (sprite == null)
        {    // ここでラグビーボールの画像ファイルを読み込みます
            sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png"); // TODO
        }
        return sprite;
    }

    public RugbyBall(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        ballObject = new("RugbyBall")
        {
            layer = LayerMask.NameToLayer("Players") // 他のプレイヤーと衝突するように
        };

        spriteRenderer = ballObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSprite();

        ballObject.transform.position = position;

        // 物理演算のためのコンポーネントを追加
        var body = ballObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.velocity = velocity;
        body.angularDrag = 0f; // 回転しないように

        // 壁と衝突させるためのコライダーを追加
        var collider = ballObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.2f; // ボールの大きさに合わせて調整
        collider.isTrigger = false; // 物理的に衝突させる

        // 物理マテリアルで反射を設定
        var physicsMaterial = new PhysicsMaterial2D
        {
            bounciness = 1.0f, // 完全に反射
            friction = 0.0f    // 摩擦なし
        };
        collider.sharedMaterial = physicsMaterial;

        // 振る舞いを管理するコンポーネントを追加
        behaviour = ballObject.AddComponent<RugbyBallBehaviour>();
        behaviour.Initialize(owner, maxBounces);
    }
}

public class RugbyBallBehaviour : MonoBehaviour
{
    private PlayerControl owner;
    private int maxBounces;
    private int currentBounces = 0;
    private float lifeTime = 10f;

    public void Initialize(PlayerControl owner, int maxBounces)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;
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
        if (body.velocity.sqrMagnitude > 0.1f)
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
            // 自分自身やインポスター仲間には当たらない
            /*if (target.PlayerId == owner.PlayerId || (target.Data.Role.IsImpostor && owner.Data.Role.IsImpostor))
            {
                // ここで味方インポスターへのスロー効果を後で実装します
                return;
            }*/

            // クルーメイトに当たった場合、キルを実行
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