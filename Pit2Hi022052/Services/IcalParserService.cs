using Ical.Net;
using Pit2Hi022052.Models;

public class IcalParserService
{
    public List<Event> ParseIcsToEventList(string icsData)
    {
        var calendars = CalendarCollection.Load(icsData);
        var result = new List<Event>();

        foreach (var calendar in calendars)
        {
            foreach (var e in calendar.Events)
            {
                // DtStartがnullでない場合に.Valueを使う
                DateTime start = e.DtStart != null ? e.DtStart.Value : DateTime.MinValue;
                DateTime end = e.DtEnd != null ? e.DtEnd.Value : DateTime.MinValue;

                result.Add(new Event
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = e.Summary ?? string.Empty,
                    StartDate = start,
                    EndDate = end,
                    Description = e.Description ?? string.Empty
                });
            }
        }

        return result;
    }
}
