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
            sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png");
        }
        return sprite;
    }

    public RugbyBall(Vector3 position, Vector2 velocity, int maxBounces)
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
        behaviour.Initialize(maxBounces);
    }
}

// ボールの振る舞いを管理するコンポーネント
public class RugbyBallBehaviour : MonoBehaviour
{
    private int maxBounces;
    private int currentBounces = 0;
    private float lifeTime = 10f; // 10秒で消滅

    public void Initialize(int maxBounces)
    {
        this.maxBounces = maxBounces;
    }

    void Update()
    {
        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0)
        {
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // プレイヤーに当たったかチェック
        PlayerControl target = collision.gameObject.GetComponent<PlayerControl>();
        if (target != null)
        {
            // ここにプレイヤーに当たった時の処理を書きます（キル、スタンなど）
            // 例：ModHelpers.CheckMurderAttemptAndKill(...)
            Destroy(gameObject); // 当たったら消滅
            return;
        }

        // 壁に当たった場合
        currentBounces++;
        // ここにバウンド音を再生する処理を追加します

        if (currentBounces >= maxBounces)
        {
            Destroy(gameObject); // 最大反射回数に達したら消滅
        }
    }
}