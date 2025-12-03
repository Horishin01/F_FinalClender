using System.Collections.Generic;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.ViewModels;

public class PrivacyViewModel
{
    public List<AppNotice> Updates { get; set; } = new();
    public List<AppNotice> Incidents { get; set; } = new();
}
