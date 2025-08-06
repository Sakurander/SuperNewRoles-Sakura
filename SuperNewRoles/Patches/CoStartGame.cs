using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using SuperNewRoles.CustomObject;
using SuperNewRoles.Modules;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.SuperTrophies;
using SuperNewRoles.MapCustoms;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils;

namespace SuperNewRoles.Patches;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
class AmongUsClientStartPatch
{
    /// <summary>
    /// クライアント側で実行される同期タイムアウト監視コルーチンの参照を保持します。
    /// </summary>
    private static Coroutine _syncTimeoutCoroutine;

    /// <summary>
    /// ゲーム開始処理の直前に実行されるPrefixメソッド。
    /// 主に乱数シードの同期処理を開始するために使用します。
    /// </summary>
    public static void Prefix(AmongUsClient __instance)
    { // 乱数シードの初期化と同期処理を開始
        InitializeRandomSeed(__instance);
    }
    public static void Postfix(AmongUsClient __instance)
    {
        try
        {
            Logger.Info("CoStartGame");

            // プレイヤー接続状態を確認
            if (PlayerControl.LocalPlayer == null || PlayerControl.AllPlayerControls == null)
            {
                Logger.Warning("Player control not initialized in CoStartGame");
                return;
            }

            // 全プレイヤーの接続状態を確認
            var disconnectedPlayers = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p == null || p.Data == null || p.Data.Disconnected)
                .ToArray();

            if (disconnectedPlayers.Length > 0)
            {
                Logger.Info($"Found {disconnectedPlayers.Length} disconnected players during game start");
            }

            // 初期化処理を一箇所に統合
            ExPlayerControl.SetUpExPlayers();
            EventListenerManager.ResetAllListener();
            SuperTrophyManager.CoStartGame();
            Garbage.ClearAndReload();
            CustomKillAnimationManager.ClearCurrentCustomKillAnimation();

            // The Fungle マップ初期化フラグをリセット
            FungleAdditionalAdmin.Reset();
            FungleAdditionalElectrical.Reset();
            ZiplineUpdown.Reset();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in CoStartGame: {ex.Message}\n{ex.StackTrace}");
            // エラーが発生してもゲームを続行できるようにする
        }
    }

    /// <summary>
    /// SNRRandomCenterの初期化とシード同期処理を行います。
    /// </summary>
    private static void InitializeRandomSeed(AmongUsClient client)
    {
        if (client.AmHost)
        {
            SNRRandomCenter.InitializeAsHost();
        }
        else
        {
            SNRRandomCenter.InitializeAsClient();

            if (_syncTimeoutCoroutine != null)
            {
                client.StopCoroutine(_syncTimeoutCoroutine);
            }
            _syncTimeoutCoroutine = client.StartCoroutine(CheckSyncTimeoutCoroutine());
        }
    }

    /// <summary> クライアント側で、ホストからの乱数シード同期を監視するコルーチンです。</summary>
    private static IEnumerator CheckSyncTimeoutCoroutine()
    {
        yield return new WaitForSeconds(15f);

        if (!SNRRandomCenter.IsSeedSynced)
        {
            Logger.Error("[SNRRandomCenter] CRITICAL: Failed to sync random seed from host within 15 seconds.");
            if (HudManager.Instance != null)
            {// TODO 翻訳
                HudManager.Instance.ShowPopUp("サーバーとの同期に失敗しました。\nゲームが正常に動作しない可能性があります。");
            }
        }
        _syncTimeoutCoroutine = null;
    }
}