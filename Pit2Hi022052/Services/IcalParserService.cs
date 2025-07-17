using System;
using System.Collections.Generic;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.Services
{
    public class IcalParserService
    {
        public List<Event> ParseIcsToEventList(string icsData)
        {
            var result = new List<Event>();

            try
            {
                if (string.IsNullOrWhiteSpace(icsData))
                {
                    Console.WriteLine(" ICSデータが空または null です。");
                    return result;
                }

                Console.WriteLine("ICSデータ取得（先頭200文字表示）:");
                Console.WriteLine(icsData.Substring(0, Math.Min(200, icsData.Length)));

                var calendar = Calendar.Load(icsData);
                if (calendar == null)
                {
                    Console.WriteLine(" Calendar.Load() に失敗しました（null が返されました）");
                    return result;
                }

                if (calendar.Events == null || !calendar.Events.Any())
                {
                    Console.WriteLine("calendar.Events が空です。イベントが含まれていない可能性があります。");
                    return result;
                }

                foreach (var e in calendar.Events)
                {
                    var start = ToDateTimeSafe(e.DtStart);
                    var end = ToDateTimeSafe(e.DtEnd);

                    result.Add(new Event
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = e.Summary ?? "(無題)",
                        StartDate = start,
                        EndDate = end,
                        Description = e.Description ?? ""
                    });
                }

                Console.WriteLine($" イベント数: {result.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ICS解析エラー: {ex.GetType().Name} - {ex.Message}");
            }

            return result;
        }

        private DateTime? ToDateTimeSafe(IDateTime rawDateTime)
        {
            if (rawDateTime == null)
                return null;

            if (rawDateTime is CalDateTime calDateTime)
            {
                if (calDateTime.Value == DateTime.MinValue)
                    return null;

                // Localに変換（UnspecifiedもLocalにする）
                return DateTime.SpecifyKind(calDateTime.Value, DateTimeKind.Local);
            }

            return null;
        }
    }
}
