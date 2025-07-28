using System;
using System.Collections.Generic;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
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
                    var start = (e.DtStart as CalDateTime)?.Value ?? DateTime.MinValue;
                    var end = (e.DtEnd as CalDateTime)?.Value ?? DateTime.MinValue;

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
                Console.WriteLine($"ICS解析エラー: {ex.Message}");
            }

            return result;
        }
    }
}
