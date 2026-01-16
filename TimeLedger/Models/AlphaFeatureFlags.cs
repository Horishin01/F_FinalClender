namespace TimeLedger.Models;

/// <summary>
/// 一時的なαテスト機能のオン/オフをまとめるフラグ。
/// 将来削除するときは、このクラスを消すか値を false にするだけでナビ/ページを無効化できる。
/// </summary>
public static class AlphaFeatureFlags
{
    // const にすると呼び出し側の分岐がコンパイル時に折りたたまれ、CS0162（到達不能）が発生するため非 const にする
    public static bool AccountAlphaFeatures { get; set; } = true;
}
