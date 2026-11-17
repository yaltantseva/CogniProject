namespace Cogni.Abstractions.Services
{
    public interface IMbtiService
    {
        Task<int> GetMbtiTypeIdByName(string nameOfType);
        Task<int> GetCompatibilityAsync(string typeA, string typeB);
        Task<int> GetUserCompatibilityAsync(int currentUserId, int targetUserId);
    }
}
