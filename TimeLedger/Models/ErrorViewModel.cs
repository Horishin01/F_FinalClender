// ErrorViewModel
// エラーページでリクエストID表示の可否を判定するシンプルなビューモデル。
namespace TimeLedger.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
