using System.Collections.Generic;
using System.Threading.Tasks;
using SMP.Data;
using SMP.Models;

namespace SMP.Services
{
    public class DependentService
    {
        private readonly DependentRepository _repository;

        public DependentService(DependentRepository repository)
        {
            _repository = repository;
        }

        // ==========================================================
        // Async methods (primary API)
        // ==========================================================
        public async Task<List<Dependent>> GetByEmployeeIdAsync(int employeeId) => await _repository.GetByEmployeeIdAsync(employeeId);
        public async Task<int?> GetEmployeeIdByNationalCodeAsync(string nc) => await _repository.GetEmployeeIdByNationalCodeAsync(nc);
        public async Task<string?> GetEmployeeFullNameByNationalCodeAsync(string nc) => await _repository.GetEmployeeFullNameByNationalCodeAsync(nc);
        public async Task<bool> IsDependentExistsAsync(string nc) => await _repository.IsDependentExistsAsync(nc);

        public async Task AddAsync(Dependent dep) => await _repository.AddAsync(dep);
        public async Task UpdateAsync(Dependent dep) => await _repository.UpdateAsync(dep);
        public async Task DeleteAsync(int id) => await _repository.DeleteAsync(id);

        // ✅ FIX STEP 2: sync wrappers حذف شدند — همگی Async هستند و هیچ caller sync وجود نداشت
    }
}