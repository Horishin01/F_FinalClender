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
                var calendar = Calendar.Load(icsData);

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ICS解析エラー: {ex.Message}");
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
