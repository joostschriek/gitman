using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;
using System;

namespace gitman
{
    /// <remarks>
    /// For some reason, I can't find the `update repo permisions for teams` call in 
    /// octokit. This means, that in order for the permission check to properly work
    /// we have to remove the team, and re-add it with the correct permissions. Super 
    /// annoying.
    /// </remarks>
    public class Collaborators : BaseRepositoryAction
    {
        public enum Update { Nothing, Skip, Add, UpdatePermission }

        public IGitWrapper Wrapper { get; set; } 
     
        private GitTeam team;
        private string teamname;
        private Permission permission;
        private List<Repository> update_perms = new List<Repository>();
        private IEnumerable<string> only, not;

        public Collaborators(IGitWrapper wrapper, string teamname, Permission permission = Permission.Push, IEnumerable<string> only = null, IEnumerable<string> not = null)
        {
            this.teamname = teamname;
            this.permission = permission;
            this.only = only ?? new List<string>();
            this.not = not ?? new List<string>();

            this.Wrapper = wrapper;
            
            var info = $"Checking if {teamname} has {permission.ToString()} on {Config.Github.Org}";
            if (this.only.Any()) 
            {
                info += $" but only " + string.Join(", ", this.only);
            }
            if (this.not.Any()) 
            {
                info += $" but not " + string.Join(", ", this.not);
            }

            l(info, 1);
        }

        public override async Task Check(List<Repository> all_repos, Repository repo)
        {
            // Figure out the team id, this only has to happen once (and this function get repeated)
            if (team == null)
            {
                team = await Wrapper.GetTeamAsync(teamname);
            }
            var repo_teams = await Wrapper.Repo.GetTeamsAsync(repo.Name);
            var repo_team = repo_teams.SingleOrDefault(t => t.Name.Equals(team.Name));

            var action = Should(repo.Name, repo_teams, team, permission);

            switch (action)
            {
                case Update.Skip:
                    l($"[SKIP] {repo.Name} doesn't need this action applied.", 2);
                    break;
                case Update.Nothing:
                    l($"[OK] {team.Name} is already a collaborator of {repo.Name}", 2);
                    break;
                case Update.Add:
                    l($"[UPDATE] will add {team.Name} to {repo.Name} as {this.permission}", 2);
                    break;
                case Update.UpdatePermission:
                    l($"[UPDATE] {team.Name} is not at {this.permission} (but is {repo_team.Permission}) for {repo.Name}", 2);
                    break;
            }

            if (new [] { Update.Add, Update.UpdatePermission}.Contains(action))
            {
                all_repos.Add(repo);
            }
        }

        public Update Should(string repo, IEnumerable<GitTeam> repoTeams, GitTeam targetTeam, Permission targetPermission) {
            // If there is no :only filter specified, then we have to assume it's included
            var isIncluded = this.only.Any() ? this.only.Any(t => t.Equals(repo, StringComparison.CurrentCultureIgnoreCase)) : true;
            var isExcluded = this.not.Any(t => t.Equals(repo, StringComparison.CurrentCultureIgnoreCase));

            if (!isIncluded || isExcluded) {
                return Update.Skip;
            }

            var repoTeam = repoTeams.SingleOrDefault(t => t.Name.Equals(targetTeam.Name));
            if (repoTeam == null) {
                return Update.Add;
            }

            if ((int) repoTeam.Permission > (int) targetPermission) {
                return Update.UpdatePermission;
            }

            return Update.Nothing;
        }

        public override async Task Action(Repository repo)
        {
            var res = await Wrapper.Repo.UpdateTeamPermissionAsync(repo.Name, team, permission);
            if (res)
            {
                l($"[MODIFIED] Added {team.Name} ({team.Id}) as a collaborator to {repo.Name}", 2);
            }
            else
            {
                l($"[ERROR] Failed to add {team.Name} ({team.Id}) as a collaborator to {repo.Name}", 2);
            }
        }
    }
}
