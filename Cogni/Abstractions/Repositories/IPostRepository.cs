using Cogni.Database.Entities;

namespace Cogni.Abstractions.Repositories
{
    public interface IPostRepository
    {
        Task<List<Post>> GetAllUserPosts(int id, int limit, int? cursor);

        Task<Dictionary<int, bool>> GetUserReactionStatusesBatchAsync(List<int> postIds, int currentUserId);

        Task<Post> CreatePost(Post post);
        Task<Post> UpdatePost(Post post);
        Task<Post?> GetPostById(int id);
        Task DeletePost(int id);

        Task<(string? PreviousReactionType, string? CurrentReactionType)>
            ToggleReactionAsync(
            int postId,
            int userId,
            string reactionType = "like");

        Task<(int ReactionsCount, bool IsReactedByCurrentUser)>
            GetReactionInfoAsync(int postId, int userId);

        Task<Dictionary<string, int>> GetReactionCountsByTypeAsync(int postId);

        Task<Dictionary<int, Dictionary<string, int>>> GetReactionCountsByTypeBatchAsync(List<int> postIds);
    }
}
