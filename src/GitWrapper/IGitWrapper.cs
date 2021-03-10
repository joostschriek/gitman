using System.Collections.Generic;
using System.Threading.Tasks;

namespace gitman
{
    public interface IGitWrapper {
        IRepo Repo { get; set; }
        Task<GitTeam> GetTeamAsync(string name);
        Task<IEnumerable<string>> GetTeamsAsync();
    }
}
