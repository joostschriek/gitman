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
    public class Collaborators : BaseBranchAction
    {
        private Team team;
        private string teamname;
        private Permission permission;
        private List<Repository> update_perms = new List<Repository>();
        private List<string> included;

        public Collaborators(string teamname, Permission permission = Permission.Push, List<string> only = null)
        {
            this.teamname = teamname;
            this.permission = permission;
            this.included = only ?? new List<string>();
        }

        public override async Task Check(List<Repository> all_repos, Repository repo)
        {
            // Figure out the team id, this only has to happen once (and this function get repeated)
            if (team == null)
            {
                var all_teams = await Client.Organization.Team.GetAll(Config.Github.Org);
                team = all_teams.Single(t => t.Name.Equals(teamname, StringComparison.OrdinalIgnoreCase));
            }

            Func<string, bool> excluded = (string tm) => included.Any() && included.All(r => !repo.Name.Equals(r, StringComparison.CurrentCultureIgnoreCase));

            var repo_teams = await Client.Repository.GetAllTeams(repo.Owner.Login, repo.Name);
            var repo_team = repo_teams.SingleOrDefault(t => t.Name.Equals(team.Name));

            // If we didn't find the team on the repo, but it's in the :only list, then we need to remove it.  If it's not on 
            //  the :only list then we're OK as long as we have the correct permissions.
            // If we didn't find the team on the repo, and they aren't on the :only list, then we should add them. If we 
            //  didn't find the team on the repo and they _are_ on the :only list, we should skip.
            if (repo_team != null)
            {
                if (excluded(repo_team.Name))
                {
                    l($"[UPDATE] Will remove {team.Name} from {repo.Name}", 1);
                }
                else
                {
                    if (repo_team.Permission.Equals(this.permission.ToString()))
                    {
                        l($"[OK] {team.Name} is already a collaborator of {repo.Name}", 1);
                    } 
                    else 
                    {
                        l($"[UPDATE] {team.Name} is not at {this.permission} (but is {repo_team.Permission}) of {repo.Name}", 1);
                        all_repos.Add(repo);
                    }
                }
            }
            else if (excluded(repo.Name))
            {
                l($"[SKIP] {repo.Name} does not need {team.Name} as a collaborator", 1);
            } 
            else 
            {
                l($"[UPDATE] will add {team.Name} to {repo.Name} as {this.permission}", 1);
                all_repos.Add(repo);
            }
        }

        public override async Task Action(Repository repo)
        {
            var removed = await Client.Organization.Team.RemoveRepository(team.Id, repo.Owner.Login, repo.Name);
            if (!removed)
            {
                l($"[ERROR] Couldn't remove {repo.Name} from {team.Name}");
                return;
            }

            var res = await Client.Organization.Team.AddRepository(team.Id, repo.Owner.Login, repo.Name, new RepositoryPermissionRequest(this.permission));
            if (res)
            {
                l($"[MODIFIED] Added {team.Name} ({team.Id}) as a collaborator to {repo.Name}", 1);
            }
            else
            {
                l($"[ERROR] Failed to add {team.Name} ({team.Id}) as a collaborator to {repo.Name}", 1);
            }
        }
    }
}