using System.Threading.Tasks;

namespace gitman
{
    public interface IOrganization
    {
        Task<models.Plan> GetPlanAsync();
    }
}