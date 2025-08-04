using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using System.Security.Cryptography;
using BepInEx.Unity.IL2CPP.Utils;

namespace SuperNewRoles.Modules;

/// <summary>
/// 乱数シードの一元管理によりGC・初期化コスト削減を実現するクラス
/// SNRで使用される現行の乱数生成器(UnityEngineなど)と区別するためSNRRandomCenterという名前にしています
/// </summary>
public static class SNRRandomCenter
{
    #region プライベートフィールド

    /// <summary> System.Random用のメイン乱数生成器 </summary>
    private static System.Random s_mainRandom;

    /// <summary>
    /// UnityEngine.Random用の現在のシード値
    /// Unity側の乱数も一元管理するために保持します
    /// </summary>
    private static int s_unitySeed;

    /// <summary>
    /// 事前生成された乱数のプール
    /// 高頻度で乱数が必要な場面でのGC圧迫を軽減します
    /// </summary>
    private static readonly Queue<float> s_randomPool = new(POOL_SIZE);

    /// <summary>
    /// 事前生成される乱数の数
    /// パフォーマンステストに基づき1000個に設定
    /// </summary>
    private const int POOL_SIZE = 1000;

    /// <summary>
    /// プールの補充を行う閾値
    /// プール残量がこの数以下になったら自動補充します
    /// </summary>
    private const int REFILL_THRESHOLD = 100;

    /// <summary>
    /// 乱数シードがホストと同期済みかどうかを示します。
    /// クライアントは、これがtrueになるまで乱数に依存する処理を実行すべきではありません。
    /// </summary>
    public static bool IsSeedSynced { get; private set; } = false;

    /// <summary>
    /// スレッドセーフティを確保するためのロックオブジェクト。
    /// </summary>
    private static readonly object s_threadLock = new();

    #endregion

    #region 初期化処理

    /// <summary>
    /// 静的コンストラクタ
    /// アプリケーション起動時に一度だけ実行されます
    /// </summary>
    static SNRRandomCenter()
    {
        // 初期化処理をInitStateに集約
        InitState(Environment.TickCount, false);
        Logger.Info($"[SNRRandomCenter] 初回初期化完了 - シード: {s_unitySeed}");
    }

    /// <summary>
    /// カスタムシードで乱数システム全体を初期化・リセットします。
    /// ゲームの再現性を保証するために、このメソッドで状態を管理してください。
    /// </summary>
    /// <param name="seed">使用するシード値</param>
    /// <param name="log">ログを出力するかどうか</param>
    public static void InitState(int seed, bool log = true)
    {
        lock (s_threadLock) // 共有リソースへのアクセス全体をロック
        {        // Unity側のシードを設定
            UnityEngine.Random.InitState(seed);
            s_unitySeed = seed;

            // System.Randomも同じシードで再生成する
            s_mainRandom = new System.Random(seed);

            // プールも新しい乱数生成器で補充
            RefillRandomPool_UNLOCKED(); // ロック内で呼び出すため、内部メソッドを呼ぶ

            if (log) Logger.Info($"[SNRRandomCenter] 乱数システムを再初期化 - シード: {seed}");
        }
    }

    /// <summary>
    /// 乱数プールの補充(アンロック版)
    /// 常にs_mainRandomから補充します
    /// </summary>
    private static void RefillRandomPool_UNLOCKED()
    {
        s_randomPool.Clear();

        for (int i = 0; i < POOL_SIZE; i++)
            s_randomPool.Enqueue((float)s_mainRandom.NextDouble());
    }

    #endregion

    #region シードの同期処理

    /// <summary>
    /// ホストとして乱数システムを初期化し、全クライアントにシードを送信します。
    /// このメソッドは、ゲームのホストがゲーム開始時に一度だけ呼び出す必要があります。
    /// </summary>
    public static void InitializeAsHost()
    {
        int seed = GenerateSecureSeed();
        InitState(seed, false); // ログは出さずに自身の状態を初期化
        IsSeedSynced = true; // ホストは常に同期済み
        Logger.Info($"[SNRRandomCenter] Host initialized with seed: {seed}");

        // 他のクライアントにRPCを送信
        RpcSyncRandomSeed(seed);
    }

    /// <summary>
    /// クライアントとして乱数システムの同期待機を開始します。
    /// このメソッドは、クライアントがゲームに参加する際に一度だけ呼び出す必要があります。
    /// </summary>
    public static void InitializeAsClient()
    {
        IsSeedSynced = false;
        Logger.Info("[SNRRandomCenter] Client waiting for seed from host...");
    }

    /// <summary>
    /// [RPC] 他のクライアントに対し、乱数シードを同期させるためのメソッド。
    /// このメソッドはRPC経由でクライアント側でのみ呼び出されることを意図しています。
    /// </summary>
    /// <param name="seed">ホストが生成したシード値</param>
    [CustomRPC(onlyOtherPlayer: true)]
    public static void RpcSyncRandomSeed(int seed)
    {
        // このメソッドがRPCで呼び出されたクライアントは、受け取ったシードで初期化を行う
        InitState(seed, true);
        IsSeedSynced = true; // 同期完了フラグを立てる
        Logger.Info($"[SNRRandomCenter] Seed successfully synced from host. Seed: {seed}");
    }

    /// <summary> 暗号学的に強力な乱数ジェネレータを使用して、安全なシード値を生成します。</summary>
    /// <returns>ランダムな整数シード</returns>
    private static int GenerateSecureSeed()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        return BitConverter.ToInt32(randomBytes, 0);
    }

    #endregion

    #region System.Random互換メソッド

    /// <summary>
    /// 非負の整数の乱数を生成します
    /// System.Random.Next()と同等の機能です
    /// </summary>
    /// <returns>0以上の整数値</returns>
    public static int Next()
    {
        lock (s_threadLock) // lockで保護
        {
            return s_mainRandom.Next();
        }
    }

    /// <summary>
    /// 指定した範囲内の整数の乱数を生成します
    /// System.Random.Next(int maxValue)と同等の機能です
    /// </summary>
    /// <param name="maxValue">生成される乱数の排他的上限値</param>
    /// <returns>0以上maxValue未満の整数値</returns>
    public static int Next(int maxValue)
    {
        lock (s_threadLock) // lockで保護
        {
            return s_mainRandom.Next(maxValue);
        }
    }

    /// <summary>
    /// 指定した範囲内の整数の乱数を生成します
    /// System.Random.Next(int minValue, int maxValue)と同等の機能です
    /// </summary>
    /// <param name="minValue">生成される乱数の包含的下限値</param>
    /// <param name="maxValue">生成される乱数の排他的上限値</param>
    /// <returns>minValue以上maxValue未満の整数値</returns>
    public static int Next(int minValue, int maxValue)
    {
        lock (s_threadLock) // lockで保護
        {
            return s_mainRandom.Next(minValue, maxValue);
        }
    }

    /// <summary>
    /// 0.0以上1.0未満の浮動小数点数の乱数を生成します
    /// System.Random.NextDouble()と同等の機能です
    /// </summary>
    /// <returns>0.0以上1.0未満の浮動小数点数</returns>
    public static double NextDouble()
    {
        lock (s_threadLock) // lockで保護
        {
            return s_mainRandom.NextDouble();
        }
    }

    /// <summary>
    /// 指定したバイト配列をランダムな値で埋めます
    /// System.Random.NextBytes(byte[] buffer)と同等の機能です
    /// </summary>
    /// <param name="buffer">ランダムな値で埋めるバイト配列</param>
    public static void NextBytes(byte[] buffer)
    {
        lock (s_threadLock) // lockで保護
        {
            s_mainRandom.NextBytes(buffer);
        }
    }

    #endregion

    #region UnityEngine.Random互換メソッド

    /// <summary>
    /// UnityEngine.Random.valueと同等の機能
    /// プールから高速に値を取得し、必要に応じて自動補充します
    /// </summary>
    public static float Value
    {
        get
        {
            lock (s_threadLock) // lockで保護
            {
                if (s_randomPool.Count <= REFILL_THRESHOLD)
                {
                    RefillRandomPool_UNLOCKED();
                }
                return s_randomPool.Dequeue();
            }
        }
    }

    /// <summary>
    /// UnityEngine.Random.Rangeと同等の機能（int版）
    /// 指定された範囲内の整数を生成します
    /// </summary>
    /// <param name="min">最小値（包含）</param>
    /// <param name="max">最大値（排他）</param>
    /// <returns>min以上max未満の整数値</returns>
    public static int Range(int min, int max)
    {
        lock (s_threadLock) // lockで保護
        {
            return s_mainRandom.Next(min, max);
        }
    }

    /// <summary>
    /// UnityEngine.Random.Rangeと同等の機能（float版）
    /// 指定された範囲内の浮動小数点数を生成します
    /// </summary>
    /// <param name="min">最小値（包含）</param>
    /// <param name="max">最大値（包含）</param>
    /// <returns>min以上max以下の浮動小数点数</returns>
    public static float Range(float min, float max)
    {
        lock (s_threadLock) // lockで保護
        {
            return min + (Value * (max - min));
        }
    }

    /// <summary>
    /// UnityEngine.Random.insideUnitCircleと同等の機能(Iが大文字なことに注意)
    /// 単位円内のランダムな点を生成します。
    /// この処理はアトミック（不可分）であり、スレッドセーフです。
    /// </summary>
    public static Vector2 InsideUnitCircle
    {
        get
        {
            // このプロパティが返すVector2は、2つの連続した乱数から生成される必要があります。
            // スレッドの割り込みを防ぎ、処理の原子性（アトミック性）と再現性を保証するため、
            // getアクセサ全体をロックします。
            lock (s_threadLock)
            {
                // 2つの独立した乱数を取得
                float randomAngle = Value * 2f * Mathf.PI;
                float randomRadius = Mathf.Sqrt(Value); // 平方根を取ることで均一分布になる

                return new Vector2(Mathf.Cos(randomAngle) * randomRadius, Mathf.Sin(randomAngle) * randomRadius);
            }
        }
    }

    /// <summary>
    /// UnityEngine.Random.insideUnitSphereと同等の機能(Iが大文字なことに注意)
    /// 単位球内のランダムな点を生成します。
    /// この処理はアトミック（不可分）であり、スレッドセーフです。
    /// </summary>
    public static Vector3 InsideUnitSphere
    {
        get
        {
            // 複数の乱数を用いて計算するため、処理全体をロックして原子性を保証します。
            lock (s_threadLock)
            {
                float phi = Value * 2f * Mathf.PI;
                float cosTheta = 1f - 2f * Value;
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float r = Mathf.Pow(Value, 1f / 3f);

                return new Vector3(
                    r * sinTheta * Mathf.Cos(phi),
                    r * sinTheta * Mathf.Sin(phi),
                    r * cosTheta
                );
            }
        }
    }
    /// <summary>
    /// UnityEngine.Random.onUnitSphereと同等の機能(Oが大文字なことに注意)
    /// 単位球面上のランダムな点を生成します。
    /// この処理はアトミック（不可分）であり、スレッドセーフです。
    /// </summary>
    public static Vector3 OnUnitSphere
    {
        get
        {
            // 複数の乱数を用いて計算するため、処理全体をロックして原子性を保証します。
            lock (s_threadLock)
            {
                float phi = Value * 2f * Mathf.PI;
                float cosTheta = 1f - 2f * Value;
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);

                return new Vector3(
                    sinTheta * Mathf.Cos(phi),
                    sinTheta * Mathf.Sin(phi),
                    cosTheta
                );
            }
        }
    }

    /// <summary>
    /// UnityEngine.Random.rotationと同等の機能(Rが大文字なことに注意)
    /// ランダムな回転を生成します。
    /// この処理はアトミック（不可分）であり、スレッドセーフです。
    /// </summary>
    public static Quaternion Rotation
    {
        get
        {
            // 3つの独立した乱数からQuaternionを生成するため、処理全体をロックします。
            lock (s_threadLock)
            {
                return Quaternion.Euler(
                    Range(0f, 360f),
                    Range(0f, 360f),
                    Range(0f, 360f)
                );
            }
        }
    }

    /// <summary>
    /// UnityEngine.Random.ColorHSVと同等の機能
    /// HSV色空間でランダムな色を生成します。
    /// このメソッドはスレッドセーフです。
    /// </summary>
    /// <param name="hueMin">色相の最小値</param>
    /// <param name="hueMax">色相の最大値</param>
    /// <param name="saturationMin">彩度の最小値</param>
    /// <param name="saturationMax">彩度の最大値</param>
    /// <param name="valueMin">明度の最小値</param>
    /// <param name="valueMax">明度の最大値</param>
    /// <param name="alphaMin">アルファの最小値</param>
    /// <param name="alphaMax">アルファの最大値</param>
    /// <returns>ランダムな色</returns>
    public static Color ColorHSV(float hueMin = 0f, float hueMax = 1f,
                                 float saturationMin = 0f, float saturationMax = 1f,
                                 float valueMin = 0f, float valueMax = 1f,
                                 float alphaMin = 1f, float alphaMax = 1f)
    {
        // 複数の乱数を用いて最終的なColorオブジェクトを生成するため、
        // メソッド全体をロックして原子性を保証します。
        lock (s_threadLock)
        {
            float h = Range(hueMin, hueMax);
            float s = Range(saturationMin, saturationMax);
            float v = Range(valueMin, valueMax);
            float a = Range(alphaMin, alphaMax);

            Color rgb = Color.HSVToRGB(h, s, v, false);
            rgb.a = a;
            return rgb;
        }
    }

    #endregion

    #region 拡張機能
    /// <summary>
    /// コレクションからランダムな要素を1つ選択します。
    /// 読み取り専用のコレクションにも対応しています。
    /// </summary>
    /// <typeparam name="T">コレクションの要素型</typeparam>
    /// <param name="collection">選択元のコレクション</param>
    /// <returns>ランダムに選択された要素</returns>
    /// <exception cref="ArgumentException">コレクションがnullまたは空の場合にスローされます。</exception>
    public static T ChooseRandom<T>(IReadOnlyList<T> collection)
    {
        if (collection == null || collection.Count == 0)
        {
            Logger.Error($"Collection '{nameof(collection)}' is null or empty.", "SNRRandomCenter:ChooseRandom");
            throw new ArgumentException("Collection cannot be null or empty.", nameof(collection));
        }
        return collection[Range(0, collection.Count)];
    }


    /// <summary>
    /// コレクションからランダムな要素を安全に選択します。
    /// 処理に失敗した場合（コレクションがnullまたは空）、例外をスローせずにfalseを返します。
    /// 例外を好まないケースのために使用してください。
    /// </summary>
    /// <typeparam name="T">コレクションの要素型。</typeparam>
    /// <param name="collection">選択元のコレクション。</param>
    /// <param name="result">選択された要素が格納される出力パラメータ。</param>
    /// <returns>要素の選択に成功した場合はtrue、それ以外はfalse。</returns>
    public static bool TryChooseRandom<T>(IReadOnlyList<T> collection, out T result)
    {
        if (collection == null || collection.Count == 0)
        {
            result = default;
            return false;
        }
        result = collection[Range(0, collection.Count)];
        return true;
    }

    /// <summary>
    /// 真偽値をランダムに生成します
    /// 指定した確率でtrueを返します
    /// </summary>
    /// <param name="probability">trueが返される確率（0.0～1.0）</param>
    /// <returns>ランダムな真偽値</returns>
    public static bool RandomBool(float probability = 0.5f) => Value < probability;

    /// <summary>
    /// 重み付きランダム選択
    /// 各要素に重みを持たせてランダム選択を行います
    /// </summary>
    /// <typeparam name="T">要素の型</typeparam>
    /// <param name="items">選択候補のアイテム配列</param>
    /// <param name="weights">各アイテムの重み配列</param>
    /// <returns>重みに基づいてランダム選択された要素</returns>
    public static T WeightedRandom<T>(T[] items, float[] weights)
    {
        if (items == null || weights == null)
        {
            Logger.Error($"{nameof(items)} または {nameof(weights)} が null です", "SNRRandomCenter: WeightedRandom");
            throw new ArgumentNullException("items または weights が null です");
        }
        if (items.Length != weights.Length)
        {
            Logger.Error($"{nameof(items)} と {nameof(weights)} の長さが一致しません: {items.Length} != {weights.Length}", "SNRRandomCenter: WeightedRandom");
            throw new ArgumentException("items と weights の長さが一致しません");
        }

        lock (s_threadLock) // 複合的な処理なのでロックを追加
        {
            float totalWeight = 0f;
            foreach (var weight in weights)
            {
                if (weight > 0) // 正の重みのみを合計する
                {
                    totalWeight += weight;
                }
            }

            // 有効な重みが全くない場合は、例外を投げるか、等確率で選択する
            if (totalWeight <= 0f)
            {
                Logger.Warning("All weights are zero or negative in WeightedRandom. Falling back to uniform random choice.");
                // 安全なフォールバックとして、重みを無視してランダムに選択する
                return items[Range(0, items.Length)];
            }

            float randomValue = Value * totalWeight;
            float currentWeight = 0f;

            for (int i = 0; i < items.Length; i++)
            {
                if (weights[i] > 0) // 正の重みのみを考慮
                {
                    currentWeight += weights[i];
                    if (randomValue <= currentWeight) return items[i];
                }
            }
            // 浮動小数点の誤差によりループを抜けることがあるため、最後の有効な要素を返す
            for (int i = items.Length - 1; i >= 0; i--)
            {
                if (weights[i] > 0) return items[i];
            }
            // ここには到達しないはず
            throw new InvalidOperationException("WeightedRandom failed unexpectedly.");
        }
    }

    /// <summary>
    /// 一様分布のランダムな回転（高精度）
    /// ジンバルロックの問題を回避します。
    /// この処理はアトミック（不可分）であり、スレッドセーフです。
    /// </summary>
    public static Quaternion RotationUniform
    {
        get
        {
            // 複数の乱数を用いて計算するため、処理全体をロックして原子性を保証します。
            lock (s_threadLock)
            {
                // Marsaglia's method for uniform distribution on S3
                float u1 = Value;
                float u2 = Value * 2f * Mathf.PI;
                float u3 = Value * 2f * Mathf.PI;

                float a = Mathf.Sqrt(1f - u1);
                float b = Mathf.Sqrt(u1);

                return new Quaternion(
                    a * Mathf.Sin(u2),
                    a * Mathf.Cos(u2),
                    b * Mathf.Sin(u3),
                    b * Mathf.Cos(u3)
                );
            }
        }
    }

    /// <summary>
    /// Fisher-Yatesアルゴリズムによる配列のインプレースシャッフル。
    /// このメソッドはスレッドセーフです。
    /// </summary>
    public static void ShuffleArray<T>(T[] array)
    {
        // 配列を直接変更する処理のため、メソッド全体をロックして
        // 複数スレッドによる同一配列への同時アクセスを防ぎます。
        lock (s_threadLock)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }
    }

    #endregion

    #region デバッグ・統計情報

    /// <summary>
    /// 現在のプール状況をログ出力します
    /// デバッグ用の情報取得に使用してください
    /// </summary>
    public static void LogPoolStatus() => Logger.Info($"[SNRRandomCenter] プール残量: {s_randomPool.Count}/{POOL_SIZE}");

    /// <summary> 乱数生成器の統計情報を取得します </summary>
    /// <returns>統計情報を含む文字列</returns>
    public static string GetStatistics() => $"プール容量: {POOL_SIZE}, 現在の残量: {s_randomPool.Count}, 補充閾値: {REFILL_THRESHOLD}";

    /// <summary> 現在使用中のシード値を取得します </summary>
    /// <returns>Unity側の現在のシード値</returns>
    public static int GetCurrentSeed() => s_unitySeed;

    #endregion
}

/// <summary>
/// SNRRandomCenterの使用例とベンチマーク用のサンプルクラス
/// 実際の使用方法の参考にしてください
/// </summary>
public static class SNRRandomCenterUsageExample
{
    // 比較対象として、静的インスタンスを持つSystem.Randomを用意
    private static readonly System.Random s_systemRandom = new();

    /// <summary> SNRRandomCenterの基本的な使用方法を示すサンプル </summary>
    public static void BasicUsageExample()
    {
        // 固定シードで初期化
        SNRRandomCenter.InitState(12345);

        // 基本的な整数の乱数生成
        int randomInt = SNRRandomCenter.Range(1, 101); // 1-100の乱数
        Logger.Info($"1-100の乱数: {randomInt}");

        // 浮動小数点数の乱数生成
        float randomFloat = SNRRandomCenter.Range(0.0f, 10.0f);
        Logger.Info($"0.0-10.0の乱数: {randomFloat}");

        // 配列からランダム選択
        string[] options = { "オプション1", "オプション2", "オプション3" };
        string selectedOption = SNRRandomCenter.ChooseRandom(options);
        Logger.Info($"選択されたオプション: {selectedOption}");

        // 配列をシャッフル
        SNRRandomCenter.ShuffleArray(options);
        Logger.Info($"シャッフル後の配列: {string.Join(", ", options)}");

        // 均一な回転
        Quaternion rot = SNRRandomCenter.RotationUniform;
        Logger.Info($"均一な回転: {rot.eulerAngles}");
    }

    /// <summary> System.Random, UnityEngine.Random, SNRRandomCenterの性能を包括的に比較するベンチマーク。 </summary>
    /// <param name="iterations">各テストの反復回数</param>
    /// <param name="runs">平均を取るためのテスト実行回数</param>
    public static void RunComprehensiveBenchmark(int iterations = 100000, int runs = 5)
    {
        Logger.Info($"=== 包括的ベンチマーク開始 (反復:{iterations}回, 実行:{runs}回) ===");

        var systemTimes = new List<double>();
        var unityTimes = new List<double>();
        var snrTimes = new List<double>();
        var stopwatch = new System.Diagnostics.Stopwatch();

        // --- int型乱数の比較 ---
        Logger.Info("\n--- 整数乱数 (Range(0, 100)) の比較 ---");
        for (int i = 0; i < runs; i++)
        {
            stopwatch.Restart();
            for (int j = 0; j < iterations; j++) { int val = s_systemRandom.Next(0, 100); }
            systemTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            for (int j = 0; j < iterations; j++) { int val = UnityEngine.Random.Range(0, 100); }
            unityTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            for (int j = 0; j < iterations; j++) { int val = SNRRandomCenter.Range(0, 100); }
            snrTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        LogAverageResults("System.Random.Next", systemTimes);
        LogAverageResults("UnityEngine.Random.Range", unityTimes);
        LogAverageResults("SNRRandomCenter.Range", snrTimes);
        systemTimes.Clear(); unityTimes.Clear(); snrTimes.Clear();

        // --- float型乱数の比較 ---
        Logger.Info("\n--- float/double型乱数 (0.0-1.0) の比較 ---");
        for (int i = 0; i < runs; i++)
        {
            stopwatch.Restart();
            for (int j = 0; j < iterations; j++) { double val = s_systemRandom.NextDouble(); }
            systemTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            for (int j = 0; j < iterations; j++) { float val = UnityEngine.Random.value; }
            unityTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            for (int j = 0; j < iterations; j++) { float val = SNRRandomCenter.Value; }
            snrTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        LogAverageResults("System.Random.NextDouble", systemTimes);
        LogAverageResults("UnityEngine.Random.value", unityTimes);
        LogAverageResults("SNRRandomCenter.Value", snrTimes);

        Logger.Info("\n=== ベンチマーク終了 ===");
    }

    private static void LogAverageResults(string testName, List<double> times)
    {
        double average = 0;
        foreach (var time in times) average += time;
        average /= times.Count;
        Logger.Info($"[{testName,-28}] 平均時間: {average:F4} ms");
    }
}
