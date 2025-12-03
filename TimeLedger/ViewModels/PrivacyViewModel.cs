using System.Collections.Generic;
using TimeLedger.Models;

namespace TimeLedger.ViewModels;

public class PrivacyViewModel
{
    public List<AppNotice> Updates { get; set; } = new();
    public List<AppNotice> Incidents { get; set; } = new();
}
