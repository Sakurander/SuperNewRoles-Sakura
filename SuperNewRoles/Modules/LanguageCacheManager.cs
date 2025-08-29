using System;
using System.IO;
using System.Text;

namespace SuperNewRoles.Modules;

/// <summary>
/// MOD起動時の言語設定をキャッシュし、読み書きを管理するクラス
/// </summary>
public static class LanguageCacheManager
{
    // キャッシュファイルのフルパスを定義。
    private static readonly string FilePath = Path.Combine(SuperNewRolesPlugin.BaseDirectory, "language_cache.txt");

    /// <summary>
    /// 現在の言語設定をキャッシュファイルに書き込みます。
    /// </summary>
    /// <param name="lang">保存する言語</param>
    public static void Write(SupportedLangs lang)
    {
        try
        {
            string langString = lang.ToString();
            byte[] langBytes = Encoding.UTF8.GetBytes(langString);
            string base64String = Convert.ToBase64String(langBytes);
            File.WriteAllText(FilePath, base64String);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to write language cache: {e}", " LanguageCache");
        }
    }

    /// <summary>
    /// キャッシュファイルから言語設定を読み込みます。
    /// </summary>
    /// <returns>読み込んだ言語。失敗した場合はnull</returns>
    public static SupportedLangs? Read()
    {
        // ケース1: ファイルが存在しない
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            string base64String = File.ReadAllText(FilePath);

            // ケース2: ファイルは存在するが中身が空
            if (string.IsNullOrWhiteSpace(base64String))
            {
                Logger.Info("Cache file is empty.", " LanguageCache");
                return null;
            }

            byte[] langBytes = Convert.FromBase64String(base64String);
            string langString = Encoding.UTF8.GetString(langBytes);

            // 文字列をEnumに変換
            if (Enum.TryParse<SupportedLangs>(langString, out var lang))
            {
                Logger.Info($"Read language from cache: {lang}", " LanguageCache");
                return lang;
            }
        }
        catch (Exception e)
        {
            // ケース3: ファイル内容が不正（Base64デコード失敗など）
            Logger.Warning($"Failed to read or parse language cache: {e}", " LanguageCache");
        }

        return null;
    }
}
