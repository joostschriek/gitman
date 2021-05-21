using System;
using System.Linq;

using gitman.Services.Git.DTOs;
using System.Threading.Tasks;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Http;
using GraphQL;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Text;

namespace gitman.Services.Git {
    public class GitGraph {
        private string githubGraphServer = "https://api.github.com/graphql";
        private GraphQLHttpClient client;

        public GitGraph() {
            var token = Encoding.UTF8.GetBytes($"{Config.Github.User}:{Config.Github.Token}");
            client = new GraphQLHttpClient(githubGraphServer, new SystemTextJsonSerializer());
            client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(token));
        }

        public async Task<IEnumerable<User>> GetRepositoryPermissions(string repo) {
            var req = new GraphQLRequest(repoPrmissionsSources, new { org = Config.Github.Org, repo = repo });
            var res = await client.SendQueryAsync<RepoPrmissionsSources>(req);
            
            IList<User> users = new List<User>();
            foreach (var edge in res.Data.Data.Repository.Collaborators.Edges) 
            {
                var user = new User();
                user.Email = edge.Node.Email.ToString();
                user.Login = edge.Node.Login;
                user.Sources = edge.PermissionSources.Select(p => new PermissionSource { 
                    Permission = Enum.Parse<Octokit.Permission>(p.Permission.ToString()),
                    Path = p.Source.ResourcePath 
                });

                users.Add(user);
            }
            
            return users;
        }

        public class User {
            public string Login { get; set; }
            public string Email { get; set; }

            /// <summary>
            /// The heighest permissions given to the user
            /// </summary>
            public PermissionSource ResolvedPermission  => Sources.OrderBy(s => s.Permission).FirstOrDefault();

            public IEnumerable<PermissionSource> Sources { get; set; } = new List<PermissionSource>();
        }

        public class PermissionSource {
            private Regex teamFilter = new Regex(@"^\/orgs\/[^\/]+\/teams"), collaboratorFilter = new Regex(@"^\/[^\/]+\/[^\/]+$");
            
            public string Path { get; set; }
            public Octokit.Permission Permission { get; set; }

            public bool IsTeam() => teamFilter.IsMatch(Path);
            public bool IsCollaborator() => collaboratorFilter.IsMatch(Path);
        }


        private const string repoPrmissionsSources = @"
query perms($org: String!, $repo: String!) {
  repository(owner: $org, name: $repo) {
    collaborators {
      edges {
        permission
        node {
          email
          id
          login
        }
        permissionSources {
          permission
          source {
            ... on Team {
              resourcePath
            }
            ... on Repository {
              resourcePath
            }
          }
        }
      }
      pageInfo {
        startCursor
        hasNextPage
        endCursor
      }
      totalCount
    }
  }
}
        ";

    }
}
