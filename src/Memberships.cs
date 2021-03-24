using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;

namespace gitman
{
    public class Memberships : BaseAction
    {
        public class Acting {
            public enum Act { Add, Remove }
            public Act Action { get; set; }
            public string Thing { get; set; }
        }

        private Audit.AuditDto auditData;
        private IDictionary<string, List<string>> teams;

        public Memberships(Audit.AuditDto auditData, Dictionary<string, List<string>> teams) {
            this.auditData = auditData;

            this.teams = teams;
        }

        public override async Task Do()
        {
            if (auditData == null) {
                throw new Exception("Audit data has to be set!");
            }
            
            var proposed_teams = teams.Keys.Distinct();
            var current_teams = auditData.Teams.Select(t => t.Value).Distinct();

            // figure out the modifictions to the orgs teams
            var actions = new List<Acting>();
            actions.AddRange(current_teams.Except(proposed_teams).Distinct().Select(t => new Acting { Action = Acting.Act.Remove, Thing = t}));
            actions.AddRange(proposed_teams.Except(current_teams).Distinct().Select(t => new Acting { Action = Acting.Act.Add, Thing = t}));
            
            // And then actually do those modifications them. We do some ugly things here to keep our "cache" up 
            // to date by manually inserting and deleting things. This should have a solve in the future. We mostly
            // do this to make the teams members check look a bit more sane.
            foreach (var action in actions.OrderBy(a => a.Thing)) {
                if (action.Action == Acting.Act.Add)
                {
                    l($"[UPDATE] Will add {action.Thing} team to {Config.Github.Org}", 2);
                 
                    if (Config.DryRun) continue;
                 
                    var team = await this.Client.Organization.Team.Create(Config.Github.Org, new NewTeam(action.Thing));
                    l($"[MODIFIED] Create team {action.Thing} ({team.Id})");
                    auditData.Teams.Add(team.Id, team.Name);
                }
                else
                {
                    var team_id = auditData.Teams.Single(t => t.Value.Equals(action.Thing)).Key;
                    l($"[UPDATE] Will remove {action.Thing} ({team_id}) team from {Config.Github.Org}", 2);
                 
                    if (Config.DryRun) continue;
                 
                    await this.Client.Organization.Team.Delete(team_id);
                    l($"[MODIFIED] Removed team {action.Thing} ({team_id})");
                    auditData.Teams.Remove(team_id);
                }
            }

            // figure out the modifications to the teams memberships
            foreach (var team in this.teams)
            {
                await DoTeamMembershipChecks(team.Key, team.Value);
            }
        }

        private async Task DoTeamMembershipChecks(string team_name, List<string> proposed_members) {
            var team_id = auditData.Teams.SingleOrDefault(t => t.Value.Equals(team_name)).Key;

            var actions = new List<Acting>();

            // We didn't find the team in the cache, that means this is a new team!
            if (auditData.Teams.ContainsValue(team_name)) 
            {
                actions.AddRange(proposed_members.Where(m => !auditData.MembersByTeam[team_name].Any(gm => gm.Equals(m))).Distinct().Select(m => new Acting { Action = Acting.Act.Add, Thing = m}));
                actions.AddRange(auditData.MembersByTeam[team_name].Where(m => !proposed_members.Contains(m)).Distinct().Select(m => new Acting { Action = Acting.Act.Remove, Thing = m }));
            }
            else 
            {
                actions.AddRange(proposed_members.Select(m => new Acting { Action = Acting.Act.Add, Thing = m}));
            }

            // And then actually do those modifications them. 
            foreach (var action in actions.OrderBy(a => a.Thing)) {
                if (action.Action == Acting.Act.Add)
                {
                    l($"[UPDATE] Will add {action.Thing} to  team {team_name} ({team_id}) as a member", 2);
                    
                    if (Config.DryRun) continue;
                    
                    var res = await Client.Organization.Team.AddOrEditMembership(team_id, action.Thing, new UpdateTeamMembership(TeamRole.Member));
                    l($"[MODIFIED] Added {action.Thing} to {team_name} ({team_id}", 2);
                }
                else 
                {
                    l($"[UPDATE] Will remove {action.Thing} from team {team_name} ({team_id})", 2);

                    if (Config.DryRun) continue;
                    
                    var res = await Client.Organization.Team.RemoveMembership(team_id, action.Thing);
                    if (res)
                        l($"[MODIFIED] Removed {action.Thing} from {team_name} ({team_id})", 2);
                    else
                        l($"[ERROR] Could not remove {action.Thing} from {team_name} ({team_id})", 2);
                }
            }
        }
    }
}
