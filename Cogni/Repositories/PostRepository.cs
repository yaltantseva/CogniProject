
using Cogni.Abstractions.Repositories;
using Cogni.Database.Context;
using Cogni.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cogni.Database.Repositories;

public class PostRepository : IPostRepository
{
    private readonly CogniDbContext _context;

    public PostRepository(CogniDbContext context)
    {
        _context = context;
    }

    public async Task<Post> CreatePost(Post post)
    {
        await _context.Posts.AddAsync(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task DeletePost(int id)
    {
        Post? post = await _context.Posts.FindAsync(id);

        if (post is null)
            return;

        _context.Remove(post);
        await _context.SaveChangesAsync();
    }

    public async Task<(string? PreviousReactionType, string? CurrentReactionType)>
        ToggleReactionAsync(
        int postId,
        int userId,
        string reactionType = "like")
    {
        var existingReaction = await _context.Likes
            .FirstOrDefaultAsync(r =>
                r.PostId == postId &&
                r.UserId == userId);

        string? previousReactionType = existingReaction?.ReactionType;
        string? currentReactionType;

        if (existingReaction is null)
        {
            var reaction = new Like
            {
                PostId = postId,
                UserId = userId,
                ReactionType = reactionType
            };

            await _context.Likes.AddAsync(reaction);
            currentReactionType = reactionType;
        }
        else if (existingReaction.ReactionType == reactionType)
        {
            _context.Likes.Remove(existingReaction);
            currentReactionType = null;
        }
        else
        {
            existingReaction.ReactionType = reactionType;
            currentReactionType = reactionType;
        }

        await _context.SaveChangesAsync();

        return (previousReactionType, currentReactionType);
    }

    public async Task<List<Post>> GetAllUserPosts(
    int id,
    int limit,
    int? cursor)
    {
        IQueryable<Post> query = _context.Posts
            .AsNoTracking()
            .Include(p => p.PostImages)
            .Where(p => p.IdUser == id);

        if (cursor.HasValue)
        {
            query = query.Where(p => p.Id < cursor.Value);
        }

        return await query
            .OrderByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<int, bool>> GetUserReactionStatusesBatchAsync(List<int> postIds, int currentUserId)
    {
        if (postIds.Count == 0)
            return new Dictionary<int, bool>();

        var reactedPostIds = await _context.Likes
            .AsNoTracking()
            .Where(r => postIds.Contains(r.PostId) && r.UserId == currentUserId)
            .Select(r => r.PostId)
            .Distinct()
            .ToListAsync();

        var reactedSet = reactedPostIds.ToHashSet();

        return postIds
            .Distinct()
            .ToDictionary(
                postId => postId,
                postId => reactedSet.Contains(postId));
    }

    public async Task<Post> UpdatePost(Post post)
    {
        Post updatedPost = new Post
        {
            Id = post.Id,
            IdUser = post.IdUser,
            PostBody = post.PostBody,
            PostImages = post.PostImages
        };

        await _context.SaveChangesAsync();
        return updatedPost;
    }

    public async Task<Post?> GetPostById(int id)
    {
        return await _context.Posts.FindAsync(id);
    }

    public async Task<(int ReactionsCount, bool IsReactedByCurrentUser)>
        GetReactionInfoAsync(int postId, int userId)
    {
        var reactions = _context.Likes
            .Where(r => r.PostId == postId);

        int reactionsCount = await reactions.CountAsync();

        bool isReactedByCurrentUser = await reactions
            .AnyAsync(r => r.UserId == userId);

        return (reactionsCount, isReactedByCurrentUser);
    }

    public async Task<Dictionary<int, Dictionary<string, int>>>
    GetReactionCountsByTypeBatchAsync(List<int> postIds)
    {
        if (postIds.Count == 0)
        {
            return new Dictionary<int, Dictionary<string, int>>();
        }

        var result = await _context.Likes
            .AsNoTracking()
            .Where(r => postIds.Contains(r.PostId))
            .GroupBy(r => new { r.PostId, r.ReactionType })
            .Select(group => new
            {
                PostId = group.Key.PostId,
                ReactionType = group.Key.ReactionType,
                Count = group.Count()
            })
            .ToListAsync();

        return result
            .GroupBy(r => r.PostId)
            .ToDictionary(
                postGroup => postGroup.Key,
                postGroup => postGroup.ToDictionary(
                    reaction => reaction.ReactionType,
                    reaction => reaction.Count));
    }

    public async Task<Dictionary<string, int>> GetReactionCountsByTypeAsync(
    int postId)
    {
        return await _context.Likes
            .AsNoTracking()
            .Where(r => r.PostId == postId)
            .GroupBy(r => r.ReactionType)
            .Select(group => new
            {
                ReactionType = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                item => item.ReactionType,
                item => item.Count);
    }
}
