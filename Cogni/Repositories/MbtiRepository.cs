using Cogni.Abstractions.Repositories;
using Cogni.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace Cogni.Database.Repositories
{
    public class MbtiRepository : IMbtiRepository
    {
        private readonly CogniDbContext _context;

        public MbtiRepository(CogniDbContext context)
        {
            _context = context;
        }
        public async Task<int> GetMbtiTypeByName(string nameOfType)
        {
            var type = await _context.MbtiTypes.FirstOrDefaultAsync(t => t.NameOfType == nameOfType);
            if (type == null) 
            {
                return 0;
            }
            return type.Id;
        }
        public async Task<int> GetCompatibilityAsync(int sourceTypeId, int targetTypeId)
        {
            var row = await _context.MbtiCompatibilities
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.SourceMbtiTypeId == sourceTypeId &&
                    c.TargetMbtiTypeId == targetTypeId);
            return row?.CompatibilityPercentage ?? 0;
        }

        public async Task<int?> GetUserMbtiTypeIdAsync(int userId)
        {
            return await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => (int?)u.IdMbtiType)
                .FirstOrDefaultAsync();
        }
    }
}
