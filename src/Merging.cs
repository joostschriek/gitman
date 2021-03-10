using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;

namespace gitman
{
    public class Merging : BaseRepositoryAction
    {
        private bool allowSquash, allowMerge, allowRebase;
        private string allowed;

        public Merging(bool squash = false, bool merge = false, bool rebase = false)
        {
            if (!squash && !merge && !rebase)
            {
                throw new Exception("You have to let people merge their work!");
            }

            allowSquash = squash;
            allowMerge = merge;
            allowRebase = rebase;

            allowed = "";
            if (merge) allowed += "AllowedMergeCommit, ";
            if (squash) allowed += "AllowSquashMerge, ";
            if (rebase) allowed += "AllowRebaseCommit, ";

            allowed = allowed.Remove(allowed.Length - 2);
        }

        public override async Task Check(List<Repository> all_repos, Repository repo)
        {
            var r = await Client.Repository.Get(repo.Owner.Login, repo.Name);
            if (r.AllowMergeCommit != allowMerge ||
                r.AllowRebaseMerge != allowRebase ||
                r.AllowSquashMerge != allowSquash)
            {
                l($"[UPDATE] Changing {repo.Name} to {allowed}", 1);
                all_repos.Add(repo);
            }
            else
            {
                l($"[OK] {repo.Name} is already set to {allowed}", 1);
            }
        }

        public override async Task Action(Repository repo)
        {
            var repoRes = await Client.Repository.Edit(repo.Owner.Login, repo.Name, new RepositoryUpdate(repo.Name)
            {
                AllowMergeCommit = allowMerge,
                AllowSquashMerge = allowSquash,
                AllowRebaseMerge = allowRebase
            });
            l($"[MODIFIED] {repo.Name} is set to {allowed}", 1);
        }
    }
}