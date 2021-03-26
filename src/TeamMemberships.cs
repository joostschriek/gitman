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

        public TeamMemberships(Audit.AuditDto auditData, Dictionary<string, List<string>> teams)
        {
            this.auditData = auditData;

            this.teams = teams;
        }

        public override async Task Do()
        {
            if (auditData == null)
            {
                throw new Exception("Audit data has to be set!");
            }

            foreach (var team in this.teams)
            {
                var proposed_members = team.Value;
                var team_name = team.Key;

                var team_id = auditData.Teams.SingleOrDefault(t => t.Value.Equals(team_name)).Key;
                var actions = new List<Acting>();

                // We didn't find the team in the cache, that means this is a new team!
                if (auditData.Teams.ContainsValue(team_name))
                {
                    actions.AddRange(proposed_members.Where(m => !auditData.MembersByTeam[team_name].Any(gm => gm.Equals(m))).Distinct().Select(m => new Acting { Action = Acting.Act.Add, Name = m }));
                    actions.AddRange(auditData.MembersByTeam[team_name].Where(m => !proposed_members.Contains(m)).Distinct().Select(m => new Acting { Action = Acting.Act.Remove, Name = m }));
                }
                else
                {
                    actions.AddRange(proposed_members.Select(m => new Acting { Action = Acting.Act.Add, Name = m }));
                }

                // And then actually do those modifications them.
                foreach (var action in actions.OrderBy(a => a.Name))
                {
                    if (action.Action == Acting.Act.Add)
                    {
                        l($"[UPDATE] Will add {action.Name} to team {team_name} ({team_id}) as a member", 1);

                        if (Config.DryRun) continue;

                        var res = await Client.Organization.Team.AddOrEditMembership(team_id, action.Name, new UpdateTeamMembership(TeamRole.Member));
                        l($"[MODIFIED] Added {action.Name} to {team_name} ({team_id}", 1);
                        // Ugly update the cache :/
                        auditData.MembersByTeam[team_name].Add(action.Name);
                        auditData.Members.Add(action.Name);
                    }
                    else
                    {
                        l($"[UPDATE] Will remove {action.Name} from team {team_name} ({team_id})", 1);

                        if (Config.DryRun) continue;

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
                }
            }
        }
    }
}
