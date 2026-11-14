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

    /// <summary>
    /// Обновляет access токен пользователя
    /// </summary>
    /// <remarks>Refresh-токен должен быть отправлен в заголовке "Refresh-token"</remarks>
    /// <response code="200">Токен обновлен.</response>
    /// <response code="401">Рефреш токен невалиден. Разлогиньте пользователя.</response>
    [HttpGet]
    public async Task<ActionResult<TokenResponse>> Refresh()
    {
        string? refreshToken = Request.Headers["Refresh-token"];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized("Refresh token is missing");
        }

        var userIdStr = await _redisDb.StringGetDeleteAsync($"refresh_token:{refreshToken}");
        if (userIdStr.IsNullOrEmpty) {return Unauthorized("Refresh token is invalid or expired");}
        var userId = int.Parse(userIdStr!);
        var role = await _userService.GetUserRole(userId);
        if (role == null) {return Unauthorized("Can't find role! Refresh token is invalid?");} // Возможно только если пользователь удален
        var newAToken = _tokenService.GenerateAccessToken(new AccessTokenPayload(userId, role));
        var newRefreshToken = Guid.NewGuid().ToString();
        await _redisDb.StringSetAsync($"refresh_token:{newRefreshToken}", userId.ToString(), 
                            TimeSpan.FromMinutes(AuthOptions.RefreshTokenExpirationTime));
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

