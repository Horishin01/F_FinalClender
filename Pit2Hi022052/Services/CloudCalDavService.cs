using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.Services; // ← 追加
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace Pit2Hi022052.Services // ← 名前空間を合わせる
{
    public class CloudCalDavService : ICloudCalDavService
    {
        private readonly ILogger<CloudCalDavService> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContext;

        public CloudCalDavService(
            ILogger<CloudCalDavService> logger,
            ApplicationDbContext db,
            IHttpContextAccessor httpContext)
        {
            _logger = logger;
            _db = db;
            _httpContext = httpContext;
        }

        public async Task<List<Event>> GetAllEventsAsync()
        {
            // 省略（現在の実装のままでOK）
            return new List<Event>();
        }
    }
}
