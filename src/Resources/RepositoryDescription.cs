using System.Collections.Generic;
using Octokit;

namespace gitman {
    public class RepositoryDescription {
        public IDictionary<string, IEnumerable<string>> RepoLists { get; set; }
        public IEnumerable<TeamDesciption> TeamDescriptions { get; set; }

        public void ResolveList(string listname, out IEnumerable<string> list) {
            if (string.IsNullOrEmpty(listname) || !RepoLists.TryGetValue(listname, out list)) {
                list = new List<string>();
            }
        }
    }

    public class TeamDesciption {
        public string TeamName { get; set; }
        public Permission Permission { get; set; } = Permission.Push;
        public string Only { get; set; }
        public string Not { get; set; }
    }
}