using System.IO;
using Jil;

namespace gitman {
    public class DescriptionService { 
        public DescriptionService() {
            
        }

        public virtual RepositoryDescription GetRepositoryDescription() {
            if (!Config.HasRepoStructureFile) {
                return null;
            }

            if (!File.Exists(Config.RepoStructureFile)) {
                return null;
            }

            using var reader = new StreamReader(Config.RepoStructureFile);
            return JSON.Deserialize<RepositoryDescription>(reader);
        }
    }
}