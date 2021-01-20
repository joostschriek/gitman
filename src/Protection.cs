using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Octokit;

namespace gitman
{
    public class Protection : BaseBranchAction
    {
        private readonly IReadOnlyList<string> EmptyContexts;
        private int reviewers;
        private const string UPDATE = "[UPDATE] ";
        private Dictionary<string, BranchProtectionRequiredStatusChecks> cachedStatusContexts = new Dictionary<string, BranchProtectionRequiredStatusChecks>();

        public Protection(int reviewers = 2)
        {
            EmptyContexts = new List<string>().AsReadOnly();

            this.reviewers = reviewers;
        }

        public override async Task Check(List<Repository> all_repos, Repository repo)
        {
            var message = UPDATE;            
            if (await ShouldUnsetStrict(repo))
            {
                if (!message.Equals(UPDATE)) message += " and ";
                message += "will remove strict reviewers";
            }

            if (await ShouldSetReviewers(repo))
            {
                if (!message.Equals(UPDATE)) message += " and ";
                message += $"will add {reviewers} review enforcement and unset stale reviewers";
            }

            if (message.Equals(UPDATE))
            {
                l($"[OK] {repo.Name} already has {repo.DefaultBranch} branch protection with the number of reviewers and non-strict", 1);
            }
            else
            {
                l($"{message} on {repo.Name}", 1);
                all_repos.Add(repo);
            }
        }

        private async Task<bool> ShouldUnsetStrict(Repository repo) 
        {
            var should = false;
            try {
                var statusChecks = await Client.Repository.Branch.GetRequiredStatusChecks(repo.Owner.Login, repo.Name, repo.DefaultBranch);
                this.cachedStatusContexts.Add(repo.Name, statusChecks);
                if (statusChecks.Strict) 
                {
                    should = true;
                }
            } catch (Octokit.NotFoundException) { 
                // no-op -- we didn't find any restrictions so that is good. 
            }
            return should;
        }

        private async Task<bool> ShouldSetReviewers(Repository repo) 
        {
            var should = false;
            try {
                var requiredReviewers = await Client.Repository.Branch.GetBranchProtection(repo.Owner.Login, repo.Name, repo.DefaultBranch);
                // Does not have the required amount of reviewers?
                var hasReviewers = requiredReviewers?.RequiredPullRequestReviews == null || requiredReviewers.RequiredPullRequestReviews.RequiredApprovingReviewCount >= reviewers;
                // Is is set to stale?
                var dismissStaleReviews = requiredReviewers?.RequiredPullRequestReviews == null || requiredReviewers.RequiredPullRequestReviews.DismissStaleReviews;
                // Check if code owners are required to review a PR
                var requireOwners = requiredReviewers?.RequiredPullRequestReviews?.RequireCodeOwnerReviews ?? false;

                should = !hasReviewers || dismissStaleReviews || !requireOwners;
            } catch (Octokit.NotFoundException) {
                // this usually means that it's a new repo, and we have to set it up
                should = true;
            }

            return should;
        }

        public override async Task Action(Repository repo)
        {
            try {
                BranchProtectionRequiredStatusChecks statusChecks;
                BranchProtectionRequiredStatusChecksUpdate statusChecksUpdate;

                if (cachedStatusContexts.TryGetValue(repo.Name, out statusChecks)) {
                    statusChecksUpdate = new BranchProtectionRequiredStatusChecksUpdate(false, statusChecks.Contexts);
                } else {
                    statusChecksUpdate = new BranchProtectionRequiredStatusChecksUpdate(false, EmptyContexts);
                }

                l($"[MODIFING] Setting branch protections on {repo.Name} to unstrict and with contexts {string.Join(",", statusChecksUpdate.Contexts)} ", 1);
                await Client.Repository.Branch.UpdateBranchProtection(
                    repo.Owner.Login,
                    repo.Name, 
                    repo.DefaultBranch, 
                    new BranchProtectionSettingsUpdate(
                        statusChecksUpdate,
                        new BranchProtectionRequiredReviewsUpdate(false, true, reviewers),
                        false
                    )
                );
            } catch (Octokit.NotFoundException) {
                l($"[WARN] could not set anything on {repo.Name} because {repo.DefaultBranch} does not exist.", 1);
            }
        }
    }
}