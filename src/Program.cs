using System;
using System.Collections.Generic;
using Octokit;
using System.Threading.Tasks;
using Mono.Options;
using Jil;
using System.IO;

namespace gitman
{
    class Program
    {
        private static GitHubClient client;

        static async Task Main(string[] args)
        {
            var opts = new OptionSet() {
                {"u|user=", "(REQUIRED) A github user with admin access.", u => Config.Github.User = u}
                , {"t|token=", "(REQUIRED) A github token that has admin access to the org.", t => Config.Github.Token = t  }
                , {"o|org=", "The organization we need to run the actions against (defaults to `sectigo-eng`)", o => Config.Github.Org = o}
                , {"teams=", "A file with the desired team structure in JSON format. If this is set, this will enforce that team structure (including removing members from teams). We expect a Dictionary where the key is the team name, and the value a list of string with the user login names.", ts => Config.TeamStructureFile = ts }
                , {"repos=", "A file with the desired repository teams access in JSON format.", rs => Config.RepoStructureFile = rs }
                , {"report=", "The path were we output the audit report. This defaults to ./", o => Config.ReportingPath = o }
                , {"d|dryrun=", "Should this run authoritative updates (`no`-dryrun), or only display changes (`yes` do a dryrun please). Must be either 'yes' or 'no'. Defaults to 'yes'.", (string dry) => Config.DryRunMode = dry.ToLower() }
                , {"no-dryrun", "Run authoritative updates. This can be destructive. This is the greedy option over --dryrun=yes|no.", d => Config.DryRunMode = "no" }
                , {"validate=", "Should this run include validation checks (does X exist or not). Excluding these saves significant api hits for rate limiting. Must be either `yes` or `no`. Defaults to `yes`)", (string val) => Config.ValidationMode = val.ToLower() }
                , {"h|help", p => Config.Help = true}
            };

            try
            {
                opts.Parse(args);
                Console.WriteLine($"Current configuration: {Config.ToString()}");
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            if (!Config.ValidateInput() || Config.Help)
            {
                opts.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (!Config.DryRun)
            {
                Console.WriteLine("\n\n!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("!!!                Non-DryRun mode - The actions will be DESTRUCTIVE                !!!");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\n\n");
            }

            client = new GitHubClient(new ProductHeaderValue("SuperMassiveCLI"));
            client.Credentials = new Credentials(Config.Github.User, Config.Github.Token);

            Console.WriteLine("\n\nChecking merge setting");
            await new Merging(squash: true) { Client = client }.Do();
            
            Console.WriteLine("\n\nChecking repo collaborators");
            var wrapper = new GitWrapper(client);

            Console.WriteLine("\n\nChecking branch protections");
            await new Protection() { Client = client }.Do();

            Console.WriteLine("\n\nPerforming team audit");
            var audit = new Audit(outputPath: Config.ReportingPath) { Client = client };
            await audit.Do();

            if (Config.HasTeamsStructureFile)
            {
                var teams = GetTeams();

                Console.WriteLine("\n\nChecking teams");
                await new Teams(audit.Data, teams) { Client = client }.Do();

                Console.WriteLine("\n\nChecking teams memberships");
                await new TeamMemberships(audit.Data, teams) { Client = client }.Do();
            }

            if (Config.HasRepoStructureFile) {
                Console.WriteLine("Checking repository access");
                await new RepositoryAccess(GetRepositoryDescription(), audit.Data) { Client = client, Wrapper = wrapper }.Do();
            }

        }

        private static Dictionary<string, List<string>> GetTeams() 
        {
            using var reader = new StreamReader(Config.TeamStructureFile);
            return JSON.Deserialize<Dictionary<string, List<string>>>(reader);
        }

        private static RepositoryDescription GetRepositoryDescription() 
        {
            using var reader = new StreamReader(Config.RepoStructureFile);
            return JSON.Deserialize<RepositoryDescription>(reader);
        }
    }
}
