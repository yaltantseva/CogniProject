
using Cogni.Contracts.Requests;
using Cogni.Database.Entities;

namespace Cogni.Abstractions.Services;

public interface IPostService
{
    Task<List<Post>> GetAllUserPosts(int id, int limit, int? cursor);

    Task<Dictionary<int, (int ReactionsCount, bool IsReactedByCurrentUser)>>
        GetReactionsInfoBatchAsync(List<int> postIds, int currentUserId);

    Task<Post> CreatePost(PostRequest post, int userId);

    Task<bool> ToggleReactionAsync(
        int postId,
        int userId,
        string reactionType = "like");

    Task<(int ReactionsCount, bool IsReactedByCurrentUser)>
        GetReactionInfoAsync(int postId, int userId);

    Task<bool> DeletePost(int id, int currentUserId, bool isAdmin);
}

