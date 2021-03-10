using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;
using System;

namespace gitman {
    public class RepositoryAccess : BaseAction
    {
        public IGitWrapper Wrapper { get; set; }

        private RepositoryDescription desciption;

        public RepositoryAccess(RepositoryDescription description) 
        {
            this.desciption = description;
        }

        public override async Task Do()
        {
            Wrapper ??= new GitWrapper(base.Client);

            foreach (var team in desciption.TeamDescriptions)
            {
                // Resolve the repo lists
                IEnumerable<string> not = null, only = null;
                ResolveList(desciption.RepoLists, team.Not, ref not);
                ResolveList(desciption.RepoLists, team.Only, ref only);

                // desciption.RepoLists.TryGetValue(team.Not, out not);
                // desciption.RepoLists.TryGetValue(team.Only, out only);

                // Check the collaborators
                await new Collaborators(Wrapper, team.TeamName, team.Permission, only, not) { Client = this.Client }.Do();
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