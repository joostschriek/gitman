using System.Collections.Generic;
using System.IO;
using gitman;
using Xunit;
using Xunit.Abstractions;
// using Jil;

using System.Text.Json;
using System.Text.Json.Serialization;

using rdesc = gitman.RepositoryDescription;
using tdesc = gitman.TeamDesciption;

namespace Tests {
    public class RepositoryAccessTests {
        
        private readonly ITestOutputHelper output;

        public RepositoryAccessTests(ITestOutputHelper output) => this.output = output;


        [Fact]
        public void write() {
            var r = new rdesc {
                RepoLists = new Dictionary<string, IEnumerable<string>> {
                    { "admin", new List<string> { "repo-one", "repo-two" } },
                    { "devops", new List<string> { "repo-three", "repo-four" } }
                },
                TeamDescriptions = new List<tdesc> {
                    new tdesc { TeamName = "admin" },
                    new tdesc { TeamName = "alpha",  Permission = Octokit.Permission.Admin, Only = "devops" }
                }
            };

            using var writer = new StringWriter();
            Jil.JSON.Serialize(r, writer, Jil.Options.PrettyPrintCamelCase);
            output.WriteLine(writer.ToString());
        }

        [Fact]
        public void read() {
            Assert.True(File.Exists("/home/joost/src/joostschriek/gitman/rdesc.json"), "cannot find file");

            using var reader = new StreamReader("/home/joost/src/joostschriek/gitman/rdesc.json");
            var rdesc = Jil.JSON.Deserialize<RepositoryDescription>(reader, Jil.Options.CamelCase );
            // var json = reader.ReadToEnd();
            // var rdesc = JsonSerializer.Deserialize<RepositoryDescription>(json, new JsonSerializerOptions {
            //     AllowTrailingCommas = true,
            //     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // });

            Assert.NotNull(rdesc);
            Assert.NotNull(rdesc.RepoLists);
            Assert.Contains<string>("admin", rdesc.RepoLists?.Keys);
            Assert.Contains<string>("gutcheck", rdesc.RepoLists["admin"]);
        }
    }
}