using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;

namespace gitman
{
    public class Teams : BaseAction
    {
        private Audit.AuditDto auditData;
        private IDictionary<string, List<string>> teams;

        public Teams(Audit.AuditDto auditData, Dictionary<string, List<string>> teams)
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

            var proposed_teams = teams.Keys.Distinct();
            var current_teams = auditData.Teams.Select(t => t.Value).Distinct();

            // figure out the modifictions to the orgs teams
            var actions = new List<Acting>();
            actions.AddRange(current_teams.Except(proposed_teams).Distinct().Select(t => new Acting { Action = Acting.Act.Remove, Name = t }));
            actions.AddRange(proposed_teams.Except(current_teams).Distinct().Select(t => new Acting { Action = Acting.Act.Add, Name = t }));

            // And then actually do those modifications them. We do some ugly things here to keep our "cache" up 
            // to date by manually inserting and deleting things. This should have a solve in the future. We mostly
            // do this to make the teams members check look a bit more sane.
            foreach (var action in actions.OrderBy(a => a.Name))
            {
                if (action.Action == Acting.Act.Add)
                {
                    l($"[UPDATE] Will create {action.Name} team to {Config.Github.Org}", 1);

                    if (Config.DryRun) continue;

                    var team = await this.Client.Organization.Team.Create(Config.Github.Org, new NewTeam(action.Name));
                    l($"[MODIFIED] Created team {action.Name} ({team.Id})", 1);
                    // Ugly update the cache :/
                    auditData.Teams.Add(team.Id, team.Name);
                }
                else
                {
                    var team_id = auditData.Teams.Single(t => t.Value.Equals(action.Name)).Key;
                    l($"[UPDATE] Will remove {action.Name} ({team_id}) team from {Config.Github.Org}", 1);

                    if (Config.DryRun) continue;

                    await this.Client.Organization.Team.Delete(team_id);
                    l($"[MODIFIED] Removed team {action.Name} ({team_id})", 1);
                    // Ugly update the cache :/
                    auditData.Teams.Remove(team_id);
                }
            }
        }
    }
}
