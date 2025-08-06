using System.Linq;
using HarmonyLib;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Modules; // Assuming MapCustomHandler and Handlers (PolusHandler, FungleHandler) are here
using UnityEngine;

namespace SuperNewRoles.MapCustoms;

public static class RandomSpawn
{
    public class MapRandomSpawnData
    {
        public Vector2[] SpawnPositions { get; }
        public System.Func<SpawnTypeOptions, bool> IsRandomSpawnTypeEnabled { get; }
        public MapCustomHandler.MapCustomId MapId { get; }

        public MapRandomSpawnData(Vector2[] spawnPositions, System.Func<SpawnTypeOptions, bool> isRandomSpawnTypeEnabled, MapCustomHandler.MapCustomId mapId)
        {
            SpawnPositions = spawnPositions;
            IsRandomSpawnTypeEnabled = isRandomSpawnTypeEnabled;
            MapId = mapId;
        }
    }

    private static readonly MapRandomSpawnData PolusRandomData = new(
        spawnPositions: new Vector2[]
        {
            new(25.7343f, -12.8777f), //BackRock
            new(3.3584f, -21.68f),    //Oxygen
            new(5.3372f, -9.7048f),   //Electrical
            new(23.9309f, -22.5169f), //Admin
            new(19.5145f, -17.4998f), //Office
            new(12.0384f, -23.34f),   //Weapons
            new(10.6821f, -16.0105f), //Comms
            new(20.5637f, -11.9088f), //Storage
            new(16.6458f, -3.2058f),  //Dropship
            new(34.3056f, -7.8901f),  //Laboratory
            new(34.3056f, -7.8901f)   //Specimens (Same as Laboratory)
        },
        isRandomSpawnTypeEnabled: PolusHandler.IsPolusSpawnType,
        mapId: MapCustomHandler.MapCustomId.Polus
    );

    private static readonly MapRandomSpawnData FungleRandomData = new(
        spawnPositions: new Vector2[]
        {
            new(-9.81f, 0.6f),    // Campfire area
            new(-8f, 10.5f),      // Dropship
            new(-16.16f, 7.25f),  // Cafeteria
            new(-15.5f, -7.5f),   // Kitchen
            new(9.25f, -12f),     // Greenhouse (Hotroom)
            new(14.75f, 0f),      // Upper Engine (Mid-slope)
            new(21.65f, 13.75f)   // Comms
        },
        isRandomSpawnTypeEnabled: FungleHandler.IsFungleSpawnType,
        mapId: MapCustomHandler.MapCustomId.TheFungle
    );

    // 【ステップ2】ホストだけが実行する、スポーン割り当てと同期のメソッド
    private static void AssignAndSyncSpawns(Vector2[] spawnPositions)
    {
        // スポーン位置が一つもない場合は、予期せぬエラーを避けるために何もしない
        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            Logger.Error("Cannot perform random spawn: No spawn positions are defined.", "RandomSpawn");
            return;
        }

        var activePlayers = PlayerControl.AllPlayerControls.ToArray();

        var playerIds = new byte[activePlayers.Length];
        var assignedPositions = new Vector2[activePlayers.Length];

        for (int i = 0; i < activePlayers.Length; i++)
        {
            var player = activePlayers[i];
            playerIds[i] = player.PlayerId;

            Vector2 chosenSpawn = SNRRandomCenter.ChooseRandom(spawnPositions);
            assignedPositions[i] = chosenSpawn;
        }

        // 決定した割り当て結果を全クライアントに送信
        RpcSetPlayerSpawns(playerIds, assignedPositions);
    }

    // このRPCはホストから呼ばれ、全クライアント（ホスト自身含む）で実行される
    [CustomRPC]
    public static void RpcSetPlayerSpawns(byte[] playerIds, Vector2[] positions)
    {
        var allPlayersArray = PlayerControl.AllPlayerControls.ToArray();

        for (int i = 0; i < playerIds.Length; i++)
        {
            PlayerControl player = allPlayersArray.FirstOrDefault(p => p.PlayerId == playerIds[i]);
            if (player != null)
            {
                player.NetTransform.RpcSnapTo(positions[i]);
            }
        }
    }

    // 【ステップ3】既存ロジックの置き換え
    private static void TriggerRandomSpawnForMap(MapRandomSpawnData mapData)
    {
        // 実行条件のチェックはそのまま
        if (!MapCustomHandler.IsMapCustom(mapData.MapId, false) || !mapData.IsRandomSpawnTypeEnabled(SpawnTypeOptions.Random))
        {
            return;
        }

        // ホストだけが実行する
        if (AmongUsClient.Instance.AmHost)
        {
            AssignAndSyncSpawns(mapData.SpawnPositions);
        }
    }

    // ゲーム開始時の役職表示メソッドに直接パッチを当てることで、初回スポーンを確実に実行する。
    [HarmonyPatch(typeof(IntroCutscene))]
    public static class GameStartIntro_Patch
    {
        // チームがクルーメイトだった場合の処理にフック
        [HarmonyPatch(nameof(IntroCutscene.BeginCrewmate))]
        [HarmonyPostfix]
        public static void Postfix_BeginCrewmate()
        {
            TriggerRandomSpawnForMap(PolusRandomData);
            TriggerRandomSpawnForMap(FungleRandomData);
        }

        // チームがインポスターだった場合の処理にフック
        [HarmonyPatch(nameof(IntroCutscene.BeginImpostor))]
        [HarmonyPostfix]
        public static void Postfix_BeginImpostor()
        {
            TriggerRandomSpawnForMap(PolusRandomData);
            TriggerRandomSpawnForMap(FungleRandomData);
        }
    }

    [HarmonyPatch(typeof(MeetingHud))]
    private static class MeetingHud_Patch
    {
        [HarmonyPatch(nameof(MeetingHud.Close))]
        [HarmonyPostfix]
        public static void Close_Postfix()
        {
            TriggerRandomSpawnForMap(PolusRandomData);
            TriggerRandomSpawnForMap(FungleRandomData);
        }
    }
}