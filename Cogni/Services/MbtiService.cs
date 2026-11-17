using Cogni.Abstractions.Repositories;
using Cogni.Abstractions.Services;
using Cogni.Database.Context;

namespace Cogni.Services
{
    public class MbtiService : IMbtiService
    {
        private readonly IMbtiRepository _mbtiRepository;

        public MbtiService(IMbtiRepository mbtiRepository) 
        {
            _mbtiRepository = mbtiRepository;
        }

        public async Task<int> GetMbtiTypeIdByName(string nameOfType)
        {
            return await _mbtiRepository.GetMbtiTypeByName(nameOfType);
        }
        public async Task<int> GetCompatibilityAsync(string typeA, string typeB)
        {
            if (string.IsNullOrWhiteSpace(typeA) || string.IsNullOrWhiteSpace(typeB))
                throw new ArgumentException("MBTI types must be provided.");

            var idA = await _mbtiRepository.GetMbtiTypeByName(typeA.Trim().ToUpperInvariant());
            var idB = await _mbtiRepository.GetMbtiTypeByName(typeB.Trim().ToUpperInvariant());
            if (idA == 0 || idB == 0)
                throw new ArgumentException($"Unknown MBTI type. Got '{typeA}' and '{typeB}'.");
            return await _mbtiRepository.GetCompatibilityAsync(idA, idB);
        }

        public async Task<int> GetUserCompatibilityAsync(int currentUserId, int targetUserId)
        {
            var idA = await _mbtiRepository.GetUserMbtiTypeIdAsync(currentUserId)
                ?? throw new InvalidOperationException("Current user has no MBTI type or does not exist.");
            var idB = await _mbtiRepository.GetUserMbtiTypeIdAsync(targetUserId)
                ?? throw new InvalidOperationException("Target user has no MBTI type or does not exist.");
            return await _mbtiRepository.GetCompatibilityAsync(idA, idB);
        }
    }
}
