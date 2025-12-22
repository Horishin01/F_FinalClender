// UserAccessLoggingMiddleware
// 認証ユーザーのアクセス履歴を DB に保存するミドルウェア。エラー時も例外を再スローしつつ 500 を付与し、処理時間や例外種別を記録する。

﻿using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Middleware
{
    public class UserAccessLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserAccessLoggingMiddleware> _logger;

        public UserAccessLoggingMiddleware(RequestDelegate next, ILogger<UserAccessLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            var started = Stopwatch.GetTimestamp();
            Exception? exception = null;
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                exception = ex;
                if (context.Response?.StatusCode < 400)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                }
                throw;
            }
            finally
            {
                await WriteAsync(context, db, started, exception);
            }
        }

        private async Task WriteAsync(HttpContext context, ApplicationDbContext db, long startedTicks, Exception? exception)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var path = context.Request.Path.Value ?? "/";
            var pathOnly = StripQuery(path);
            if (ShouldSkip(path))
            {
                return;
            }

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var elapsedMs = (long)((Stopwatch.GetTimestamp() - startedTicks) * 1000.0 / Stopwatch.Frequency);
            var statusCode = context.Response?.StatusCode ?? 0;
            var isError = exception is not null || statusCode >= 400;
            var errorType = exception?.GetType().Name ?? (statusCode >= 500 ? "ServerError" : statusCode >= 400 ? "ClientError" : null);
            var errorHash = exception is null ? null : Hash(exception.Message);

            var log = new UserAccessLog
            {
                UserId = userId,
                AccessedAtUtc = DateTime.UtcNow,
                Path = Truncate(pathOnly, 512) ?? "/",
                HttpMethod = Truncate(context.Request.Method, 16) ?? "GET",
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512),
                RemoteIp = Truncate(AnonymizeIp(context.Connection.RemoteIpAddress?.ToString()), 64),
                StatusCode = statusCode,
                DurationMs = elapsedMs,
                IsError = isError,
                ErrorType = Truncate(errorType, 128),
                ErrorHash = errorHash
            };

            try
            {
                db.UserAccessLogs.Add(log);
                await db.SaveChangesAsync(context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to persist user access log.");
            }
        }

        private static bool ShouldSkip(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            // Skip obvious static or diagnostics endpoints to reduce noise.
            return path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/robots", StringComparison.OrdinalIgnoreCase);
        }

        private static string StripQuery(string path)
        {
            var idx = path.IndexOf('?', StringComparison.Ordinal);
            return idx >= 0 ? path.Substring(0, idx) : path;
        }

        private static string? AnonymizeIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return null;
            }

            if (ip.Contains('.'))
            {
                var parts = ip.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4)
                {
                    return $"{parts[0]}.{parts[1]}.{parts[2]}.0";
                }
            }

            if (ip.Contains(':'))
            {
                // Collapse IPv6 tail to reduce identifiability
                var segments = ip.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 4)
                {
                    return string.Join(':', segments.Take(4)) + "::";
                }
            }

            return ip;
        }

        private static string? Hash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }

    public static class UserAccessLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserAccessLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UserAccessLoggingMiddleware>();
        }
    }
}
