using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;
using System.IO;
using Jil;
using System.Text;

namespace gitman
{
    public class Audit : BaseAction
    {
        public AuditDto Data { get; private set; } = new AuditDto();

        private string outputPath;

        public Audit(string outputPath = null) {
            this.outputPath = outputPath;
        }

        public class AuditDto { 
            public Dictionary<int, string> Teams {get; set;} = new Dictionary<int, string>();
            public List<string>  Members {get; set;} = new List<String>();
            public Dictionary<string, IEnumerable<string>> MembersByTeam { get; set; } = new Dictionary<string, IEnumerable<string>>();
            public List<RepoAuditDto> Repositories { get; set; } = new List<RepoAuditDto>();

            public string ToString(int indent = 0)
            { 
                var tabs = new string(' ', indent);
                var s = new StringBuilder();
                s.AppendLine($"{tabs}Members ({Members.Count})=[{string.Join(", ", Members)}]");
                s.AppendLine($"{tabs}Teams ({Teams.Count})=[{string.Join(", ", Teams.Select(t => t.Value))}]");
                foreach (var team in MembersByTeam)
                {
                    s.AppendLine($"{tabs} {team.Key} ({team.Value.Count()})=[{string.Join(", ", team.Value)}]");
                }

                s.AppendLine($"{tabs}Repositories ({Repositories.Count()})");
                foreach (var repo in Repositories) 
                {
                    s.AppendLine(repo.ToString(indent + 2));
                }

                return s.ToString();
            }
        }

        public class RepoAuditDto {
            public class BranchAuditDto {
                public string Name { get; set; }
                public bool Protected { get; set; }
                public int Reviewers { get; set; }
                public bool Strict { get; set; }

                public override string ToString() => $"[Name={Name} Protected={Protected} Reviewers={Reviewers} Strict={Strict}]";
            }
            
            public string Name { get; set; }
            public List<BranchAuditDto> Branches { get; set; } = new List<BranchAuditDto>();

            public string ToString(int indent = 0) 
            {
                var tabs = new string(' ', indent);
                var s = new StringBuilder();
                var protect = Branches.Where(b => b.Protected);
                
                s.AppendLine($"{tabs} Name={Name}");
                s.AppendLine($"{tabs} Branches ({Branches.Count})");
                s.AppendLine($"{tabs}  Protected Branches ({protect.Count()})");
                s.AppendJoin("\n",protect.Select(branch => $"{tabs}    {branch}"));
                s.AppendLine($"\n{tabs}  Regular Branches ({Branches.Count() - protect.Count()})");
                s.AppendJoin("\n",Branches.Except(protect).Select(branch => $"{tabs}    {branch}"));
                s.AppendLine();

                return s.ToString();
            }
        }

        public override async Task Do() 
        {            
            // Get all the teams and members
            var teams = await Client.Organization.Team.GetAll(Config.Github.Org);
            foreach (var team in teams)
            {
                Data.Teams.Add(team.Id, team.Name);
                var mbs = await Client.Organization.Team.GetAllMembers(team.Id);
                Data.MembersByTeam.Add(team.Name, mbs.Select(m => m.Login));
            }           
            
            // Get all our members
            Data.Members.AddRange((await Client.Organization.Member.GetAll(Config.Github.Org, new ApiOptions { PageSize = 1000 })).Select(m => m.Login));

            // Get all the repos
            var rs = new List<RepoAuditDto>();
            var repos = await Client.Repository.GetAllForOrg(Config.Github.Org);
            
            // Get all their branches and sort them on
            foreach (var repo in repos) 
            {
                var r = new RepoAuditDto { Name = repo.Name };
                var branches = await Client.Repository.Branch.GetAll(Config.Github.Org, repo.Name);
                r.Branches.AddRange(branches
                    .Select(async (b) => await BranchAuditFrom(r.Name, b))
                    .Select(t => t.Result));
                Data.Repositories.Add(r);
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                var path = Path.Combine(outputPath, $"audit_{DateTime.Now.ToString("yyy-MM-dd-hhmm")}.json");
                l($"Saving audit repot to {path}", 1);
                using var writer = new StreamWriter(path);
                JSON.Serialize(Data, writer, Jil.Options.PrettyPrintCamelCase);
            }

            l(Data.ToString(1));
        }

        private async Task<RepoAuditDto.BranchAuditDto> BranchAuditFrom(string reponame, Branch branch) {
            var audit = new RepoAuditDto.BranchAuditDto {
                Name = branch.Name,
                Protected = branch.Protected
            };
            if (branch.Protected) {
                audit.Reviewers = await GetReviewers(reponame, branch.Name);
                audit.Strict = await GetStrict(reponame, branch.Name);
            }

            return audit;
        }

        private async Task<int> GetReviewers(string reponame, string branch) 
        {
            var requiredReviewers = await Client.Repository.Branch.GetBranchProtection(Config.Github.Org, reponame, branch);
            return requiredReviewers?.RequiredPullRequestReviews == null ? 0 : requiredReviewers.RequiredPullRequestReviews.RequiredApprovingReviewCount;
        }

        private async Task<bool> GetStrict(string reponame, string branch) 
        {
            var strict = false;
            try {
                var statusChecks = await Client.Repository.Branch.GetRequiredStatusChecks(Config.Github.Org, reponame, branch);
                strict = statusChecks.Strict;
            } catch (Octokit.NotFoundException) { 
                // no-op -- we didn't find any restrictions so that is good.            
            }

            return strict;
        }
    }
}