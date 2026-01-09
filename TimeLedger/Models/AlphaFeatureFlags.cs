namespace TimeLedger.Models;

/// <summary>
/// 一時的なαテスト機能のオン/オフをまとめるフラグ。
/// 将来削除するときは、このクラスを消すか値を false にするだけでナビ/ページを無効化できる。
/// </summary>
public static class AlphaFeatureFlags
{
    public const bool AccountAlphaFeatures = true;
}
