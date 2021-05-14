using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;
using System;
using System.Text;
using gitman.models;

namespace gitman
{
    public class GitWrapper : IGitWrapper {
        private Dictionary<string, Team> teamsByIds = new Dictionary<string, Team>();
        private IGitHubClient client;
        
        public IRepo Repo { get; set; }
        public IOrganization Org { get; set;}

        public GitWrapper(IGitHubClient client) {
            this.client = client;
            this.Repo = new Repository(this);
            this.Org = new Organization();
        }

        public async Task<GitTeam> GetTeamAsync(string name) {
            await CacheTeamsAsync();
            var team = teamsByIds.SingleOrDefault(t => t.Key.Equals(name));
            if (team.Equals(default(KeyValuePair<string, Team>))) {
                return new GitTeam();
            }
            return new GitTeam(team.Value);
        }

        public async Task<IEnumerable<string>> GetTeamsAsync()
        {
            await CacheTeamsAsync();
            return teamsByIds.Select(t => t.Value.Name);
        }       

        public class Repository : IRepo {
            private Dictionary<string, IEnumerable<GitTeam>> teamsByRepo = new Dictionary<string, IEnumerable<GitTeam>>();
            private GitWrapper wrapper;

            public Repository(GitWrapper wrapper) {
                this.wrapper = wrapper;
            }

            public async Task<IEnumerable<GitTeam>> GetTeamsAsync(string reponame) {
                if (!teamsByRepo.ContainsKey(reponame)) {
                    var repoTeams = await wrapper.client.Repository.GetAllTeams(Config.Github.Org, reponame);
                    teamsByRepo.Add(reponame, repoTeams.Select(t => new GitTeam(t)));
                }

                return teamsByRepo[reponame];
            }

            public async Task<bool> UpdateTeamPermissionAsync(string reponame, GitTeam target, Permission targetPermission) {
                bool updated = false;
                var removed = await wrapper.client.Organization.Team.RemoveRepository(target.Id, Config.Github.Org, reponame);
                if (removed) {
                    updated = await wrapper.client.Organization.Team.AddRepository(target.Id, Config.Github.Org, reponame, new RepositoryPermissionRequest(targetPermission));
                }

                return updated;
            }
        }
        
        // But why use Gitman.Http instead of the official Octokit.Client? Long story short, the official 
        // API client does not return the organisations plan as described in the API docs. They omit the 
        // useful information, like seat and filled seat counts :) Since we would like to verify that we 
        // have enough seats, we have to build some http requests of our own.
        public class Organization : Gitman.Http, IOrganization
        {
            // The plan is unlikely to change (a lot) during runs. If it does change (members added etc), we
            // only really care if we can do the _current set_ of changes.
            private models.Plan cachedPlan = null;

            public Organization()
            {
                ApiToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Config.Github.User}:{Config.Github.Token}"));
            }

            public async Task<models.Plan> GetPlanAsync()
            {
                if (cachedPlan == null)
                {
                    var org = await Get<models.Organization>($"https://api.github.com/orgs/{Config.Github.Org}");
                    cachedPlan = org.Plan;
                }

                return cachedPlan;
            }
        }

        private async Task CacheTeamsAsync() {
            if (teamsByIds.Count() != 0) {
                return;
            }
            var teams = await client.Organization.Team.GetAll(Config.Github.Org);
            teamsByIds = teams.ToDictionary(t => t.Name, t => t);
        }

    }
}
