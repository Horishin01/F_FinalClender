using Pit2Hi022052.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pit2Hi022052.Services
{
    public interface ICloudCalDavService
    {
        Task<List<Event>> GetAllEventsAsync();
    }
}
