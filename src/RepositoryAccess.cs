using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;

namespace gitman {
    public class RepositoryAccess : BaseAction
    {
        public IGitWrapper Wrapper { get; set; }

        private RepositoryDescription desciption;

        public RepositoryAccess(RepositoryDescription description) 
        {
            this.desciption = description;

            l("Checking repository access for team descriptions");
        }

        public override async Task Do()
        {
            Wrapper ??= new GitWrapper(base.Client);

            foreach (var team in desciption.TeamDescriptions)
            {
                // Resolve the repo lists
                IEnumerable<string> not, only;
                desciption.RepoLists.TryGetValue(team.Not, out not);
                desciption.RepoLists.TryGetValue(team.Only, out only);

                // Check the collaborators
                await new Collaborators(Wrapper, team.TeamName, team.Permission, only, not) { Client = this.Client }.Do();
            }
        }
    }
}