using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;

namespace gitman
{
    public class TeamMemberships : BaseAction
    {
        private Audit.AuditDto auditData;
        private IDictionary<string, List<string>> teams;
        private IGitWrapper wrapper;

        public TeamMemberships(Audit.AuditDto auditData, Dictionary<string, List<string>> teams)
        {
            this.auditData = auditData;
            this.teams = teams;
            this.wrapper = new GitWrapper(Client);
        }

        public override async Task Do()
        {
            if (auditData == null)
            {
                throw new Exception("Audit data has to be set!");
            }

            var usersDoNotExist = new List<string>();
            var orgHasEnoughSeats = true;
            models.Plan plan = null;
            if (Config.Validate)
            {
                // We want to know if any of the users do not exsit, but we still want 
                // to process all the actions we can. This is to prevent blocking a user
                // addition or removal.
                l("Validating usernames");
                foreach (var team in this.teams)
                {
                    usersDoNotExist.AddRange(await ValidateUsers(team.Value));
                }

                // Make sure we have enough seats available in our plan. If we cannot add 
                l("Validating available license seats");
                plan = await wrapper.Org.GetPlanAsync();
                var totalProposedMembers = teams.Values.SelectMany(m => m).Distinct().Count();
                orgHasEnoughSeats = plan.Seats > totalProposedMembers;
                l($"We have {plan.Seats} available, and {totalProposedMembers} total proposed members", 1);
            }

            foreach (var team in this.teams)
            {
                // Filter out the users we know that doe not exist
                var proposed_members = team.Value.Except(usersDoNotExist);
                var team_name = team.Key;

                var team_id = auditData.Teams.SingleOrDefault(t => t.Value.Equals(team_name)).Key;
                var actions = new List<Acting>();

                // We didn't find the team in the cache, that means this is a new team!
                if (auditData.Teams.ContainsValue(team_name))
                {
                    // Who should we add?
                    actions.AddRange(proposed_members.Where(m => !auditData.MembersByTeam[team_name].Any(gm => gm.Equals(m))).Distinct().Select(m => new Acting { Action = Acting.Act.Add, Name = m }));
                    // Who should we remove?
                    actions.AddRange(auditData.MembersByTeam[team_name].Where(m => !proposed_members.Contains(m)).Distinct().Select(m => new Acting { Action = Acting.Act.Remove, Name = m }));
                }
                else
                {
                    // This is a new team, so we should add all the proposed members
                    actions.AddRange(proposed_members.Select(m => new Acting { Action = Acting.Act.Add, Name = m }));
                }

                // And then actually do those modifications them.
                foreach (var action in actions.OrderBy(a => a.Name))
                {
                    if (action.Action == Acting.Act.Add)
                    {
                        // We only want to process the removals if we don't have enough seats
                        if (orgHasEnoughSeats)
                        {
                            l($"[UPDATE] Will add {action.Name} to team {team_name} ({team_id}) as a member", 1);
                        }
                        else
                        {
                            l($"[SKIP] Will not add {action.Name} to team {team_name} ({team_id}) as a member becaues we do not have enough license seats", 1);
                            continue;
                        }


                        if (Config.DryRun) continue;

                        try
                        {
                            await Client.Organization.Team.AddOrEditMembership(team_id, action.Name, new UpdateTeamMembership(TeamRole.Member));
                            l($"[MODIFIED] Added {action.Name} to {team_name} ({team_id}", 1);
                            // Ugly update the cache :/
                            auditData.MembersByTeam[team_name].Add(action.Name);
                            auditData.Members.Add(action.Name);
                        }
                        catch (Octokit.ApiException apiEx)
                        {
                            l($"[ERROR] A Github API error occured and we weren't able to add {action.Name} to {team_name}");
                            l($"Message: {apiEx.Message}\nStatusCode:{apiEx.StatusCode}\nException:{apiEx.InnerException}\n\n{apiEx.Source}\n");
                        }
                    }
                    else
                    {
                        l($"[UPDATE] Will remove {action.Name} from team {team_name} ({team_id})", 1);

                        if (Config.DryRun) continue;

                        try
                        {
                            var res = await Client.Organization.Team.RemoveMembership(team_id, action.Name);
                            if (res)
                            {
                                l($"[MODIFIED] Removed {action.Name} from {team_name} ({team_id})", 1);
                                // Ugly update the cache :/
                                auditData.MembersByTeam[team_name].Remove(action.Name);
                                auditData.Members.Remove(action.Name);
                            }
                            else
                            {
                                l($"[ERROR] Could not remove {action.Name} from {team_name} ({team_id})", 1);
                            }
                        }
                        catch (Octokit.ApiException apiEx)
                        {
                            l($"[ERROR] A Github API error occured and we weren't able to remove {action.Name} to {team_name}");
                            l($"Message: {apiEx.Message}\nStatusCode:{apiEx.StatusCode}\nException:{apiEx.InnerException}\n\n{apiEx.Source}\n");
                        }
                    }
                }
            }

            // We make sure that we can process the authoritative teams list while excluding the not existing 
            // members because we want to make sure the membership list is as up to date as possible. Otherwise 
            // there might be a scenario where were a malicious employee (on us trying to remove them from 
            // github) could rename their account to something higher in the alphabet, and could extend their 
            // stay and wreck havoc/steal all our big balls of content.
            // 
            // We eventually do want to crash as to indicate something is bonkers, but only after we processed
            // the valid members.
            var message = "";
            if (!orgHasEnoughSeats)
            {
                var memberCount = teams.Values.SelectMany(m => m).Distinct().Count();
                message += $"We do not have enough license seats available. We required {memberCount} but we have {plan.Seats}.";
                message += "\n";
            }
            if (usersDoNotExist.Any())
            {
                message += "Users do not exist:\n";
                message += string.Join("\n", usersDoNotExist.Select(u => $"\t{u}"));
                message += "\n";
            }

            if (!string.IsNullOrEmpty(message))
            {
                // make the output a bit more readable
                l("");
                throw new Exception("Validation of Team Meberships failed!\n" + message);
            }
        }

        private async Task<IEnumerable<string>> ValidateUsers(IEnumerable<string> usernames)
        {
            // Do the users exist or not?
            var doesNotExist = new List<String>();
            foreach (var username in usernames)
            {
                User user = null;

                try
                {
                    user = await Client.User.Get(username);
                }
                catch (Octokit.NotFoundException) { }

                if (user == null)
                {
                    doesNotExist.Add(username);
                }
            }

            return doesNotExist;
        }
    }
}
