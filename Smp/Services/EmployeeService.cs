using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Smp.Data;
using Smp.Models;

namespace Smp.Services;

public class EmployeeService
{
    private readonly EmployeeRepository _repository;

    public EmployeeService(EmployeeRepository repository)
    {
        _repository = repository;
    }

    // ==========================================================
    // Async methods (primary API)
    // ==========================================================
    public async Task<List<Employee>> GetEmployeesWithDetailsAsync(string searchText, bool showInactive, bool onlyWithActiveContract = false)
    {
        return await _repository.GetEmployeesWithDetailsAsync(searchText, showInactive, onlyWithActiveContract);
    }

    public async Task AddAsync(Employee e) => await _repository.AddAsync(e);

    public async Task UpdateAsync(Employee e, DateTime? terminationDate = null)
    {
        await _repository.UpdateAsync(e, terminationDate);
    }

    // ==========================================================
    // Sync methods (only for fast operations)
    // ==========================================================
    public Employee? GetById(int id) => _repository.GetById(id);
    public void Delete(int id) => _repository.Delete(id);
    public bool IsPersonnelCodeTaken(string code, int? id) => _repository.ExistsByPersonnelCode(code, id);
    public bool IsNationalCodeTaken(string code, int? id) => _repository.ExistsByNationalCode(code, id);
}
