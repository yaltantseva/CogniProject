using Cogni.Abstractions.Services;
using Cogni.Authentication.Abstractions;
using Cogni.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Cogni.Authentication;

[ApiController]
[Route("[controller]/[action]")]
public class TokenController : ControllerBase
{
    private readonly IDatabase _redisDb;
    private readonly ITokenService _tokenService;
    private readonly IUserService _userService;
    public TokenController(IUserService userService, ITokenService tokenService, IConnectionMultiplexer redis)
    {
        _redisDb = redis.GetDatabase();
        _userService = userService;
        _tokenService = tokenService;
    }

    [HttpGet]
    public async Task<ActionResult<TokenResponse>> Refresh(
    [FromHeader(Name = "Refresh-token")] string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized("Refresh token is missing");
        }
        var tokenKey = $"refresh_token:{refreshToken}";
        var userIdStr = await _redisDb.StringGetAsync(tokenKey);
        if (userIdStr.IsNullOrEmpty)
        {
            return Unauthorized("Refresh token is invalid or expired");
        }
        if (!int.TryParse(userIdStr.ToString(), out var userId))
        {
            return Unauthorized("Invalid user ID");
        }
        var userTokensKey = $"user_refresh_tokens:{userId}";
        if (!await _redisDb.SetContainsAsync(userTokensKey, refreshToken))
        {
            return Unauthorized("Refresh token is invalid");
        }
        var deletedUserId = await _redisDb.StringGetDeleteAsync(tokenKey);
        if (deletedUserId.IsNullOrEmpty)
        {
            return Unauthorized("Refresh token is invalid or expired");
        }
        await _redisDb.SetRemoveAsync(userTokensKey, refreshToken);
        var role = await _userService.GetUserRole(userId);
        if (role == null)
        {
            return Unauthorized("Can't find role! Refresh token is invalid?");
        }
        var newAToken = _tokenService.GenerateAccessToken(
            new AccessTokenPayload(userId, role));
        var newRefreshToken = _tokenService.GenerateRefreshToken(userId);
        return Ok(new TokenResponse(
            newAToken,
            newRefreshToken,
            _tokenService.GetAccessTokenExpireTime(),
            _tokenService.GetRefreshTokenExpireTime()
        ));
    }
    /// <summary>
    /// Завершает сессию и отзывает refresh-токен
    /// </summary>
    /// <remarks>Refresh-токен должен быть отправлен в заголовке "Refresh-token"</remarks>
    /// <response code="200">Сессия успешно завершена.</response>
    [HttpPost]
    public async Task<ActionResult> Logout()
    {
        string? refreshToken = Request.Headers["Refresh-token"];
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _redisDb.KeyDeleteAsync($"refresh_token:{refreshToken}");
        }
        return Ok();
    }
}

