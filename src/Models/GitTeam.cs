using Octokit;
using System;

namespace gitman
{
            
    public class GitTeam {
        // This enum has to have the same indicies as :octokit.permission
        public enum Perm { 
            admin = 0,
            push = 1,
            pull = 2
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public Perm Permission {get; set; }

        public GitTeam() { }
        public GitTeam(Team team) : this(team.Id, team.Name, Enum.Parse<Perm>(team.Permission.StringValue)) { }
        public GitTeam(int id, string name, Perm perm =  Perm.pull) {
            Id = id;
            Name = name;
            Permission = perm;
        }
    }
}
