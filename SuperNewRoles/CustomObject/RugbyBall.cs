// SuperNewRoles/CustomObject/RugbyBallObject.cs

using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Roles.Impostor;
using System.Linq;
using SuperNewRoles.MapDatabase;

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
    private EventListener fixedUpdateEvent;

    // MapDatabaseのインスタンスを保持
    private MapDatabase.MapDatabase currentMapData;

    public RugbyBallObject(PlayerControl owner, Vector3 position, Vector2 velocity, int maxBounces)
    {
        this.owner = owner;
        this.maxBounces = maxBounces;

        // ★ 現在のマップデータを取得
        currentMapData = MapDatabase.MapDatabase.GetCurrentMapData();
        if (currentMapData == null)
        {
            Logger.Error("Could not get current map data for RugbyBall!", "RugbyBaller");
            Detach();
            return;
        }

        ballObject = new GameObject("RugbyBall_Physics");
        ballObject.layer = LayerMask.NameToLayer("Players"); // RaycastのためにPlayersレイヤーに変更
        ballObject.transform.position = position;

        var spriteRenderer = ballObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetManager.GetAsset<Sprite>("ConjurerStartButton.png"); // TODO: 仮
        spriteRenderer.sortingLayerName = "Players";
        spriteRenderer.sortingOrder = 5; // プレイヤーより手前

        body = ballObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.velocity = velocity;

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

        CheckCollisionAndMove();

        if (body != null && body.velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
            ballObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        if (owner.AmOwner)
        {
            CheckForPlayerCollision();
        }
    }

    // ★★★ MapDatabaseを活用した新しい衝突判定メソッド ★★★
    private void CheckCollisionAndMove()
    {
        if (body == null) return;

        Vector2 currentPos = ballObject.transform.position;
        Vector2 velocity = body.velocity;
        Vector2 nextPos = currentPos + velocity * Time.fixedDeltaTime;

        // 1. 次のフレームの位置が「歩行可能エリア」の外かどうかを判定
        if (!currentMapData.CheckMapArea(nextPos))
        {
            // 2. 衝突したとみなし、反射処理を行う
            // Raycastで正確な衝突法線を取得する
            RaycastHit2D hit = Physics2D.Raycast(currentPos, velocity.normalized, velocity.magnitude * Time.fixedDeltaTime, Constants.ShipAndObjectsMask);

            Vector2 reflectionNormal = hit.collider != null ? hit.normal : -velocity.normalized;

            ReflectAndBounce(reflectionNormal);
        }
        else
        {
            // 3. 衝突がなければそのまま移動
            ballObject.transform.position = nextPos;
        }
    }

    private void ReflectAndBounce(Vector2 normal)
    {
        body.velocity = Vector2.Reflect(body.velocity, normal) * 0.95f; // 少し減速
        currentBounces++;
        Logger.Info($"RugbyBall bounced! ({currentBounces}/{maxBounces})", "RugbyBaller");

        // TODO: バウンド音を再生

        if (currentBounces >= maxBounces)
        {
            Detach();
        }
    }

    // プレイヤーとの衝突判定ロジック (変更なし)
    private void CheckForPlayerCollision()
    {
        if (invincibilityTimer > 0) return;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data.IsDead || p.PlayerId == owner.PlayerId) continue;

            if (Vector2.Distance(ballObject.transform.position, p.GetTruePosition()) < 0.4f)
            {
                HandlePlayerCollision(p);
                return;
            }
        }
    }

    private void HandlePlayerCollision(PlayerControl targetPlayer)
    {
        // 味方インポスターへの衝突
        if (targetPlayer.Data.Role.IsImpostor && owner.Data.Role.IsImpostor)
        {
            RpcHandleAllyCollision(targetPlayer.PlayerId);
            body.velocity *= -0.5f;
            return;
        }

        // それ以外のプレイヤーへの衝突（キル）
        if (owner.AmOwner)
        {
            RpcKillTarget(owner.PlayerId, targetPlayer.PlayerId);
        }
        Detach();
    }

    // --- RPCs (自爆スタン処理は後回し) ---
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
    public static void RpcHandleAllyCollision(byte allyId)
    {
        ExPlayerControl exAlly = ExPlayerControl.ById(allyId);
        if (exAlly != null && exAlly.IsAlive())
        {
            // TODO: スロー効果を実装
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