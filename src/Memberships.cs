using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.Linq;

namespace gitman
{
    public class Memberships : BaseAction
    {
        private Audit.AuditDto auditData;
        private string team_name;
        private List<string> proposed_members;

        public Memberships(Audit.AuditDto auditData, string team_name, List<string> proposed_members) {
            this.auditData = auditData;
            this.team_name = team_name;
            this.proposed_members = proposed_members;
        }

        public override async Task Do()
        {
            if (auditData == null) {
                throw new Exception("Audit data has to be set!");
            }

            var team_id = auditData.Teams.Single(t => t.Value.Equals(team_name)).Key;
            l($"Team members in {team_name} ({team_id})", 1);

            // figure out the modifications to the team
            var to_remove = auditData.MembersByTeam[team_name].Where(m => !proposed_members.Contains(m));
            var to_add = proposed_members.Where(m => !auditData.MembersByTeam[team_name].Any(gm => gm.Equals(m)));

            foreach (var m in to_add)
            {
                l($"[UPDATE] Will add {m} to {team_name} ({team_id})", 2);
            }

            foreach (var m in to_remove)
            {
                l($"[UPDATE] Will remove {m} from {team_name} ({team_id})", 2);
            }

            if (Config.DryRun) return;

            // Make our output pretty!
            l("");

            foreach (var member in to_add)
            {
                var res = await Client.Organization.Team.AddOrEditMembership(team_id, member, new UpdateTeamMembership(TeamRole.Member));
                l($"[MODIFIED] Added {member} to {team_name} ({team_id}", 2);
            }

            foreach (var member in to_remove)
            {
                var res = await Client.Organization.Team.RemoveMembership(team_id, member);
                if (res)
                    l($"[MODIFIED] Removed {member} from {team_name} ({team_id}", 2);
                else
                    l($"[ERROR] Could not remove {member} from {team_name} ({team_id}", 2);
            }
        }
    }
}
