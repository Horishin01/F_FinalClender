namespace Pit2Hi022052.Models
{
    public class Personal
    {
        public string Id { get; set; }
        public string UserId { get; set; } // 外部キー
        public string Weekday { get; set; }
        public string Name { get; set; }

        // ナビゲーションプロパティ
        public ApplicationUser User { get; set; }
    }
}
