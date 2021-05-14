using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;
using System;

namespace gitman {
    public class RepositoryAccess : BaseAction
    {
        public IGitWrapper Wrapper { get; set; }

        private RepositoryDescription description;

        private Audit.AuditDto auditData;

        public RepositoryAccess(RepositoryDescription description, Audit.AuditDto auditData) 
        {
            this.description = description;
            this.auditData = auditData;
        }

        public override async Task Do()
        {
            Wrapper ??= new GitWrapper(base.Client);

            if (Config.Validate)
            {
                // Make sure the configuration can work
                l("Validating RepositiryDescription team names and list references");
                await Validate();
            }

            // Resolve and apply the collaborators
            foreach (var team in description.TeamDescriptions)
            {
                // Resolve the repo lists
                IEnumerable<string> not = null, only = null;
                ResolveList(description.RepoLists, team.Not, ref not);
                ResolveList(description.RepoLists, team.Only, ref only);

                // Check the collaborators
                await new Collaborators(Wrapper, team.TeamName, team.Permission, only, not) { Client = this.Client }.Do();
            }
        }

        internal async Task Validate() 
        {    
            // Validate team names
            var existingTeams = await Wrapper.GetTeamsAsync();
            var teamsFromConfig = auditData.Teams.Values;
            var teamNames = description.TeamDescriptions.Select(t => t.TeamName);
            var teamDoesNotExist = teamNames.Where(t => !teamsFromConfig.Any(tfc => tfc.Equals(t)));

            // validates repo list references to actual repo lists
            var repoListRefs = description.TeamDescriptions.SelectMany(t => new [] { t.Not, t.Only } ).Where(r => !string.IsNullOrEmpty(r)).Distinct();
            var repoListDoesNotExist = repoListRefs.Where(r => !description.RepoLists.ContainsKey(r));
            
            var message = "";
            if (teamDoesNotExist.Any())
            {
                message += "Teams do not exist:\n";
                message += string.Join("\n", teamDoesNotExist.Select(t => $"\t - {t}").ToArray());
                message += "\n";
            }
            if (repoListDoesNotExist.Any())
            {
                message += "Repo list reference do not match pre-defined list\n";
                message += string.Join("\n", repoListDoesNotExist.Select(r => $"\t - {r}").ToArray());
                message += "\n";
            }

            if (!string.IsNullOrEmpty(message))
            {
                throw new Exception("Validation of RepositoryDescription failed!\n" + message);
            }
        }

        private void ResolveList(IDictionary<string, IEnumerable<string>>  repolists, string listname, ref IEnumerable<string> list) {
            if (!string.IsNullOrEmpty(listname)) {
                if (!repolists.TryGetValue(listname, out list)) {
                    throw new ArgumentException($"list {listname} not found!");
                }
            }
        }
    }
}