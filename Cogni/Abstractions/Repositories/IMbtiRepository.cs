namespace Cogni.Abstractions.Repositories
{
    public interface IMbtiRepository
    {
        Task<int> GetMbtiTypeByName(string nameOfType);
        Task<int> GetCompatibilityAsync(int sourceTypeId, int targetTypeId);
        Task<int?> GetUserMbtiTypeIdAsync(int userId);
    }
}
