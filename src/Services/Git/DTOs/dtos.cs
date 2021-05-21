using System.Collections.Generic;

namespace gitman.Services.Git.DTOs
{

    public partial class RepoPrmissionsSources
    {
        public Data Data { get; set; }
    }

    public partial class Data
    {
        public Repository Repository { get; set; }
    }

    public partial class Repository
    {
        public Collaborators Collaborators { get; set; }
    }

    public partial class Collaborators
    {
        public List<Edge> Edges { get; set; }
        public PageInfo PageInfo { get; set; }
        public long TotalCount { get; set; }
    }

    public partial class Edge
    {
        public Permission Permission { get; set; }
        public Node Node { get; set; }
        public List<PermissionSource> PermissionSources { get; set; }
    }

    public partial class Node
    {
        public Email Email { get; set; }
        public string Id { get; set; }
        public string Login { get; set; }
    }

    public partial class PermissionSource
    {
        public Permission Permission { get; set; }
        public Source Source { get; set; }
    }

    public partial class Source
    {
        public string? ResourcePath { get; set; }
    }

    public partial class PageInfo
    {
        public string StartCursor { get; set; }
        public bool HasNextPage { get; set; }
        public string EndCursor { get; set; }
    }

    public enum Email { BlastDanGmailCom, DanprimeGmailCom, Empty, JSchriekGmailCom, PatrickMildlygeekyCom, Tb0HdanGmailCom };

    public enum Permission { Admin, Read, Write };

    public enum ResourcePath { OrgsSectigoEngTeamsAdmins, OrgsSectigoEngTeamsAlpha, OrgsSectigoEngTeamsBravo, OrgsSectigoEngTeamsCharlie, OrgsSectigoEngTeamsDevelopers, OrgsSectigoEngTeamsEcho, OrgsSectigoEngTeamsFoxtrot, OrgsSectigoEngTeamsGolf, OrgsSectigoEngTeamsHotel, OrgsSectigoEngTeamsTechWriters, SectigoEngCaService };
}
