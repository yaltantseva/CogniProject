using Cogni.Abstractions.Repositories;
using Cogni.Database.Context;
using Cogni.Database.Entities;
using Cogni.Models;
using Microsoft.EntityFrameworkCore;

namespace Cogni.Database.Repositories;
public class UserRepository : IUserRepository
{
    readonly CogniDbContext _context;

    readonly ILogger<UserRepository> _logger;


    public UserRepository(CogniDbContext context, ILogger<UserRepository> logger) 
    {
        _logger = logger;
        _context = context;
    }

    public async Task<bool> CheckUser(string login)
    {
        User? user = await _context.Users.FirstOrDefaultAsync(u=> u.Email==login);
        if (user == null)
        {
            return false;
        }
        return true;
    }

    public async Task<PrivateUserModel> CreateUser(User user)
    {
        // C каких пор Email стал неуникальным?
        await _context.Users.AddAsync(user);
        //await _context.Avatars.AddAsync(new Avatar {UserId = user.Id, AvatarUrl = placeholder, IsActive=true, DateAdded=DateTime.Now});
        await _context.SaveChangesAsync();
        _context.Entry(user).Reference(u => u.IdRoleNavigation).Load();
        _context.Entry(user).Reference(u => u.IdMbtiTypeNavigation).Load();
        _context.Entry(user).Collection(u => u.Avatars).Load();
        PrivateUserModel model = Converter(user);
        return model;
    }

    public async Task<PrivateUserModel?> GetPrivateUserByEmail(string email){
        User? user = await _context.Users
            .Include(u => u.IdMbtiTypeNavigation)
            .Include(u => u.IdRoleNavigation)
            .Include(u => u.Avatars)
            .FirstOrDefaultAsync(u=> u.Email==email);
        if (user == null){
            return null;
        }
        return Converter(user);
    }

    public async Task<UserCredsModel?> GetUserCredsById(int id){
        User? user = await _context.Users.FirstOrDefaultAsync(u=> u.Id==id);
        if (user == null){
            return null;
        }
        return new UserCredsModel{Id = user.Id, PasswordHash = user.PasswordHash, Salt = user.Salt};
    }

    public async Task SetMbtiType(int userId, int mbtiId)
    {
        var u = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (u != null)
        {
            u.IdMbtiType = mbtiId;
            await _context.SaveChangesAsync();
            return;
        }
        // ачо делать если нет юзера?
        // - ничего не делать)
        // todo: handle error
        return;
    }

    public async Task<PublicUserModel?> GetPublicUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.IdMbtiTypeNavigation)
            .Include(u => u.IdRoleNavigation)
            .Include(u => u.Avatars)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null){ return null;}
        var userModel = Converter(user);
        return userModel.ToPublic();
    }

    public async Task<bool> ChangeAvatar(int id, string picLink)
    {
        var user = await _context.Users
            .Include(u => u.Avatars)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return false;

        var avatar = user.Avatars.FirstOrDefault(r => r.IsActive == true);
        if (avatar != null) avatar.IsActive = false;

        user.Avatars.Add(new Avatar  { 
            AvatarUrl = picLink,
            UserId = id,
            IsActive = true,
            DateAdded = DateTime.Now,
        });

        await _context.SaveChangesAsync();  
        return true;
    }

    public async Task<string?> ChangeBanner(int id, string picLink)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null) return null;

        user.BannerImage = picLink;
        await _context.SaveChangesAsync();
        return user.BannerImage;
    }

    public async Task<bool> RemoveBanner(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return false;

        user.BannerImage = null;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangeName(int id, string name, string surname)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null) return false;

        user.Name = name;
        user.Surname = surname;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangeDescription(int id, string description)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null) return false;

        user.Description = description;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePassword(int id, string PasHash, byte[] salt)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null) return false;

        user.Salt = salt;
        user.PasswordHash = PasHash;
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<string?> GetUserRole(int id)
    {
        var role = await _context.Users
            .Include(u => u.IdRoleNavigation)
            .Where(u => u.Id == id)
            .Select(u => u.IdRoleNavigation.NameRole)
            .FirstOrDefaultAsync();
        return role;
    }

    public async Task<List<FriendDto>> GetRandomUsers(int userId, int startsFrom, int limit, bool sortByCompatibility = false)
    {
        // MBTI текущего пользователя (0 — если тип не задан)
        var currentMbtiId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IdMbtiType)
            .FirstOrDefaultAsync();

        var query = _context.Users
            .Where(u => u.Id != userId)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Surname,
                u.IdMbtiType,
                PicUrl = u.Avatars
                    .Where(a => a.IsActive == true)
                    .Select(a => a.AvatarUrl)
                    .FirstOrDefault(),
                Compatibility = sortByCompatibility && currentMbtiId != 0
                    ? _context.MbtiCompatibilities
                        .Where(c => c.SourceMbtiTypeId == currentMbtiId && c.TargetMbtiTypeId == u.IdMbtiType)
                        .Select(c => (int?)c.CompatibilityPercentage)
                        .FirstOrDefault()
                    : null
            });

        if (sortByCompatibility && currentMbtiId != 0)
        {
            query = query
                .OrderByDescending(x => x.Compatibility ?? 0)
                .ThenBy(x => x.Id);
        }
        else
        {
            query = query.OrderBy(x => x.Id);
        }

        return await query
            .Skip(startsFrom)
            .Take(limit)
            .Select(x => new FriendDto
            {
                Id = x.Id,
                Name = x.Name,
                Surname = x.Surname,
                PicUrl = x.PicUrl,
                Mbti = x.IdMbtiType,
                CompatibilityPercentage = x.Compatibility
            })
            .ToListAsync();
    }

    public async Task<List<FriendDto>> SearchUserByNameAndType(int userId, string NameSurname, int mbtiType, bool sortByCompatibility = false)
    {
        var currentMbtiId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IdMbtiType)
            .FirstOrDefaultAsync();

        var query = _context.Users
            .Where(u => u.Id != userId
                && (string.IsNullOrEmpty(NameSurname)
                    || ((u.Name + " " + u.Surname).ToLower()).Contains(NameSurname.Trim().ToLower()))
                && (mbtiType == 0 || u.IdMbtiType == mbtiType))
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Surname,
                u.IdMbtiType,
                PicUrl = u.Avatars
                    .Where(a => a.IsActive == true)
                    .Select(a => a.AvatarUrl)
                    .FirstOrDefault(),
                Compatibility = sortByCompatibility && currentMbtiId != 0
                    ? _context.MbtiCompatibilities
                        .Where(c => c.SourceMbtiTypeId == currentMbtiId && c.TargetMbtiTypeId == u.IdMbtiType)
                        .Select(c => (int?)c.CompatibilityPercentage)
                        .FirstOrDefault()
                    : null
            });

        if (sortByCompatibility && currentMbtiId != 0)
            query = query.OrderByDescending(x => x.Compatibility ?? 0).ThenBy(x => x.Id);
        else
            query = query.OrderBy(x => x.Id);

        return await query
            .Select(x => new FriendDto
            {
                Id = x.Id,
                Name = x.Name,
                Surname = x.Surname,
                PicUrl = x.PicUrl,
                Mbti = x.IdMbtiType,
                CompatibilityPercentage = x.Compatibility
            })
            .ToListAsync();
    }
    private PrivateUserModel Converter(User user)//метод конвертирующие из User-сущности в PrivateUserModel 
    {
        Avatar? avatar = user.Avatars.FirstOrDefault(i => i.IsActive == true);
        return new PrivateUserModel
        {
            Id = user.Id,
            Name = user.Name,
            Surname = user.Surname,
            Description = user.Description,
            Salt = user.Salt,
            PasswordHash = user.PasswordHash,
            Email = user.Email,
            BannerImage = user.BannerImage,
            IdRole = user.IdRole,
            IdMbtiType = user.IdMbtiType,
            MbtiType = user.IdMbtiTypeNavigation.NameOfType,
            RoleName = user.IdRoleNavigation.NameRole,
            LastLogin = user.LastLogin  == null ? null : (int)(user.LastLogin.Value.Subtract(new DateTime(1970, 1, 1))).TotalSeconds,
            ActiveAvatar = avatar == null ? "" : avatar.AvatarUrl
        };
    }
    public async Task<List<PublicUserModel>> GetUsersByIds(List<int> userIds) {
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Include(u => u.IdMbtiTypeNavigation)
            .Include(u => u.IdRoleNavigation)
            .Include(u => u.Avatars)
            .ToListAsync();

        return users.Select(u => new PublicUserModel
        {
            Id = u.Id,
            Name = u.Name,
            Surname = u.Surname,
            Description = u.Description,
            BannerImage = u.BannerImage,
            TypeMbti = u.IdMbtiTypeNavigation?.NameOfType,
            ActiveAvatar = u.Avatars.FirstOrDefault(a => a.IsActive == true)?.AvatarUrl,
            LastLogin = u.LastLogin  == null ? null : (int)(u.LastLogin.Value.Subtract(new DateTime(1970, 1, 1))).TotalSeconds,
        }).ToList();
    }
}
/*
public class PublicUserModel
{
    public int Id {get; set;}
    public string Name {get; set;}
    public string Surname {get; set;}
    public string? Description {get; set;}
    public string? Image {get; set;}
    public string? BannerImage {get; set;}
    public string TypeMbti {get; set;}
    public string? ActiveAvatar {get; set;}
    public int? LastLogin {get; set;}

}
*/