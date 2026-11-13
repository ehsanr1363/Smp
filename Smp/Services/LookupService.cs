using System.Collections.Generic;
using SMP.Data;
using SMP.Models;
using System.Threading.Tasks;

namespace SMP.Services
{
    public class LookupService
    {
        private readonly LookupRepository _lookupRepository;

        public LookupService(LookupRepository lookupRepository)
        {
            _lookupRepository = lookupRepository;
        }

        public List<LookupItem> GetByCategory(string category)
        {
            return _lookupRepository.GetByCategory(category);
        }

        // 👈 متد جدید Async
        public async Task<List<LookupItem>> GetByCategoryAsync(string category)
        {
            return await _lookupRepository.GetByCategoryAsync(category);
        }

        public void Add(LookupItem lookupItem)
        {
            _lookupRepository.Add(lookupItem);
        }

        public void Delete(int id)
        {
            _lookupRepository.Delete(id);
        }
    }
}