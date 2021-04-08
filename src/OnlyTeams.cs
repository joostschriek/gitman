using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Octokit;
using Octokit.GraphQL;
namespace gitman
{
    public class OnlyTeams : BaseRepositoryAction
    {
        private readonly RepositoryDescription rdesc;
        private readonly Audit.AuditDto audit;
        private readonly Dictionary<string, List<string>> teams;

        public OnlyTeams(RepositoryDescription desc, Audit.AuditDto audit, Dictionary<string, List<string>> teams)
        {
            this.rdesc = desc;
            this.audit = audit;
            this.teams = teams;
        }

        public override async Task Check(List<Repository> all_repos, Repository repo)
        {
            l($"Checking collaborators permissions for {repo.Name}");
            // For all the collaborators get their current permissions
            var collabs = await Client.Repository.Collaborator.GetAll(Config.Github.Org, repo.Name);

            var conn = new Octokit.GraphQL.Connection(new Octokit.GraphQL.ProductHeaderValue("SuperMassiveCLI"), Config.Github.Token);
            
            var query = new Octokit.GraphQL.Query()
                .Repository(Variable.Var(Config.Github.Org), Variable.Var(repo.Name))
                .Collaborators()
                .Edges
                .Select(re => new {
                    perm = re.Permission
                });
            var res = await conn.Run(query);
            foreach (var member in collabs)
            {
                var currentPermission = await Client.Repository.Collaborator.ReviewPermission(Config.Github.Org, repo.Name, member.Login);
                // resolve their highest possible permissions
                var proposedPermissions = ResolvePermissionsFor(repo.Name, member.Login);
                l($"[CHECK] {member.Login} has {currentPermission.Permission.StringValue} but has proposed {proposedPermissions}", 1);

                // and test to see if we need to update them
                if (currentPermission.Permission.Value == proposedPermissions)
                {
                    l($"[SKIP] {member.Login} is set properly at {currentPermission.Permission.StringValue}", 1);
                }
                else
                {
                    l($"[UPDATE] Should set {member.Login} from {currentPermission.Permission.StringValue} to {proposedPermissions}", 1);
                }
            }
        }

        public override Task Action(Repository repo)
        {
            throw new NotImplementedException();
        }

        private PermissionLevel ResolvePermissionsFor(string reponame, string username)
        {
            var permission = PermissionLevel.None;
            var permissionsByTeam = new Dictionary<string, Permission>();

            var membersAssignedTeams = teams.Where(t => t.Value.Contains(username)).Select(at => at.Key);
            var tdescs = rdesc.TeamDescriptions.Where(t => membersAssignedTeams.Any(at => at.ToLower().Equals(t.TeamName.ToLower())));
            foreach (var tdesc in tdescs)
            {
                l($"found {tdesc.TeamName} at {tdesc.Permission} in for user {username} and repo {reponame}", 2);
                IEnumerable<string> not = null, only = null;
                rdesc.ResolveList(tdesc.Not, out not);
                rdesc.ResolveList(tdesc.Only, out only);

                if (not.Any() && not.Contains(reponame))
                {
                    l($"in not list {tdesc.Not}", 3);
                    continue;
                }
                if (only.Any() && !only.Contains(reponame))
                {
                    l($"not in only list {tdesc.Only}", 3);
                    continue;
                }

                l($"adding {tdesc.TeamName} at {tdesc.Permission}", 3);

                permissionsByTeam.Add(tdesc.TeamName, tdesc.Permission);
            }

            if (permissionsByTeam.Any())
            {
                permission = (PermissionLevel)((int)permissionsByTeam.OrderBy(p => p.Value).First().Value);
            }

            return permission;
        }
    }
}