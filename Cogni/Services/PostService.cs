using Cogni.Abstractions.Repositories;
using Cogni.Abstractions.Services;
using Cogni.Contracts.Requests;
using Cogni.Database.Entities;
using Cogni.Models;
using StackExchange.Redis;

namespace Cogni.Services;

public class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IImageService _imageService;
    private readonly IDatabase _redisDb;
    private readonly ILogger<PostService> _logger;

    private const string InitializedField = "__initialized";
    private const string TotalField = "total";

    private const string InitializeCacheScript = @"
    if redis.call('HGET', KEYS[1], ARGV[1]) == '1' then
        return 0
    end

    redis.call('DEL', KEYS[1])

    for i = 2, #ARGV, 2 do
        redis.call('HSET', KEYS[1], ARGV[i], ARGV[i + 1])
    end

    return 1";

    private const string UpdateCacheScript = @"
        local previous = ARGV[1]
        local current = ARGV[2]

        if previous ~= '' then
            local count = redis.call('HINCRBY', KEYS[1], previous, -1)

            if count <= 0 then
                redis.call('HDEL', KEYS[1], previous)
            end
        end

        if current ~= '' then
            redis.call('HINCRBY', KEYS[1], current, 1)
        end

        if previous == '' and current ~= '' then
            redis.call('HINCRBY', KEYS[1], 'total', 1)
        elseif previous ~= '' and current == '' then
            redis.call('HINCRBY', KEYS[1], 'total', -1)
        end

        return redis.call('HGET', KEYS[1], 'total')
    ";

    public PostService(
        IPostRepository postRepository,
        IImageService imageService,
        IConnectionMultiplexer redis,
        ILogger<PostService> logger)
    {
        _postRepository = postRepository;
        _imageService = imageService;
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<Post> CreatePost(PostRequest post, int userId)
    {
        var p = new Post
        {
            IdUser = userId,
            PostBody = post.PostBody,
            CreatedAt = DateTime.UtcNow
        };

        if (post.Files != null)
        {
            foreach (var file in post.Files)
            {
                p.PostImages.Add(new PostImage
                {
                    ImageUrl = (await _imageService.UploadImage(file)).Url
                });
            }
        }

        return await _postRepository.CreatePost(p);
    }

    public async Task<bool> DeletePost(
        int id,
        int currentUserId,
        bool isAdmin)
    {
        var post = await _postRepository.GetPostById(id);

        if (post is null)
            return false;

        if (post.IdUser != currentUserId && !isAdmin)
            return false;

        await _postRepository.DeletePost(id);

        try
        {
            await _redisDb.KeyDeleteAsync($"post:reactions:{id}");
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось удалить кеш реакций для удалённого поста {PostId}",
                id);
        }

        return true;
    }

    public async Task<List<Post>> GetAllUserPosts(
        int id,
        int limit,
        int? cursor)
    {
        return await _postRepository.GetAllUserPosts(id, limit, cursor);
    }

    public async Task<bool> ToggleReactionAsync(
        int postId,
        int userId,
        string reactionType = "like")
    {
        if (string.IsNullOrWhiteSpace(reactionType) ||
            reactionType.Length > 20 ||
            reactionType == TotalField ||
            reactionType == InitializedField)
        {
            return false;
        }

        var post = await _postRepository.GetPostById(postId);

        if (post is null)
            return false;

        RedisKey key = $"post:reactions:{postId}";

        bool cacheInitialized = false;

        try
        {
            RedisValue cachedTotal = await _redisDb.HashGetAsync(
                key,
                TotalField);

            if (!cachedTotal.IsNull)
            {
                cacheInitialized = true;
            }
            else
            {
                var counts = await _postRepository
                    .GetReactionCountsByTypeAsync(postId);

                await EnsureReactionCacheInitializedAsync(postId, counts);
                cacheInitialized = true;
            }
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(
                ex,
                "Redis недоступен при инициализации кеша реакций для поста {PostId}",
                postId);
        }

        var (previousReactionType, currentReactionType) =
            await _postRepository.ToggleReactionAsync(
                postId,
                userId,
                reactionType);

        if (cacheInitialized)
        {
            try
            {
                await _redisDb.ScriptEvaluateAsync(
                    UpdateCacheScript,
                    new RedisKey[] { key },
                    new RedisValue[]
                    {
                        previousReactionType ?? string.Empty,
                        currentReactionType ?? string.Empty
                    });
            }
            catch (RedisException ex)
            {
                _logger.LogError(
                    ex,
                    "Не удалось обновить кеш реакций для поста {PostId}. Выполняется инвалидация кеша.",
                    postId);

                try
                {
                    await _redisDb.KeyDeleteAsync(key);
                }
                catch (RedisException deleteEx)
                {
                    _logger.LogError(
                        deleteEx,
                        "Не удалось удалить кеш реакций для поста {PostId}",
                        postId);
                }
            }
        }
        else
        {
            try
            {
                await _redisDb.KeyDeleteAsync(key);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось инвалидировать кеш реакций для поста {PostId}",
                    postId);
            }
        }

        return true;
    }

    public async Task<Dictionary<int, (int ReactionsCount, bool IsReactedByCurrentUser)>>GetReactionsInfoBatchAsync(List<int> postIds, int currentUserId)
    {
        if (postIds.Count == 0)
            return new Dictionary<int, (int, bool)>();

        var uniquePostIds = postIds.Distinct().ToList();

        var userStatuses = await _postRepository
            .GetUserReactionStatusesBatchAsync(uniquePostIds, currentUserId);

        var reactionsCounts = new Dictionary<int, int>();
        var postsWithoutCache = new List<int>();

        var cacheTasks = uniquePostIds.Select(async postId =>
        {
            RedisKey key = $"post:reactions:{postId}";

            try
            {
                RedisValue cachedTotal = await _redisDb.HashGetAsync(
                    key,
                    TotalField);

                return (PostId: postId, Total: cachedTotal, RedisAvailable: true);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось прочитать кеш реакций для поста {PostId}",
                    postId);

                return (PostId: postId, Total: RedisValue.Null, RedisAvailable: false);
            }
        });

        var cacheResults = await Task.WhenAll(cacheTasks);

        bool redisUnavailable = cacheResults.Any(result => !result.RedisAvailable);

        foreach (var result in cacheResults)
        {
            if (result.RedisAvailable && !result.Total.IsNull)
            {
                reactionsCounts[result.PostId] = (int)result.Total;
            }
            else
            {
                postsWithoutCache.Add(result.PostId);
            }
        }

        if (redisUnavailable)
        {
            postsWithoutCache = uniquePostIds
                .Where(postId => !reactionsCounts.ContainsKey(postId))
                .ToList();
        }

        if (postsWithoutCache.Count > 0)
        {
            var countsByType = await _postRepository
                .GetReactionCountsByTypeBatchAsync(postsWithoutCache);

            foreach (int postId in postsWithoutCache)
            {
                countsByType.TryGetValue(postId, out var counts);
                counts ??= new Dictionary<string, int>();

                int databaseTotal = counts.Values.Sum();

                reactionsCounts[postId] = databaseTotal;

                if (redisUnavailable)
                    continue;

                try
                {
                    await EnsureReactionCacheInitializedAsync(postId, counts);

                    RedisValue cachedTotal = await _redisDb.HashGetAsync(
                        $"post:reactions:{postId}",
                        TotalField);

                    if (!cachedTotal.IsNull)
                        reactionsCounts[postId] = (int)cachedTotal;
                }
                catch (RedisException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Не удалось инициализировать кеш реакций для поста {PostId}",
                        postId);
                }
            }
        }

        return uniquePostIds.ToDictionary(
            postId => postId,
            postId =>
            {
                userStatuses.TryGetValue(postId, out bool isReacted);

                reactionsCounts.TryGetValue(postId, out int count);

                return (count, isReacted);
            });
    }

    public async Task<(int ReactionsCount, bool IsReactedByCurrentUser)>
        GetReactionInfoAsync(int postId, int userId)
    {
        return await _postRepository.GetReactionInfoAsync(postId, userId);
    }

    private async Task EnsureReactionCacheInitializedAsync(
        int postId,
        Dictionary<string, int> counts)
    {
        RedisKey key = $"post:reactions:{postId}";

        RedisValue initialized = await _redisDb.HashGetAsync(
            key,
            InitializedField);

        if (initialized == "1")
            return;

        var arguments = new List<RedisValue>
        {
            InitializedField,
            InitializedField,
            "1",
            TotalField,
            counts.Values.Sum().ToString()
        };

        foreach (var item in counts)
        {
            arguments.Add(item.Key);
            arguments.Add(item.Value.ToString());
        }

        await _redisDb.ScriptEvaluateAsync(
            InitializeCacheScript,
            new RedisKey[] { key },
            arguments.ToArray());
    }
}


//TODO доделать + облако
//public async Task<Post> UpdatePost(PostRequest post)
//{
//    return await _postRepository.UpdatePost(new Post
//    {
//        Id = post.Id,
//        IdUser=post.IdUser,
//        PostBody = post.PostBody
//    });
//}

