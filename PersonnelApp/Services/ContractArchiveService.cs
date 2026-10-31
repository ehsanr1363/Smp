using System.Collections.Generic;
using PersonnelApp.Data;
using PersonnelApp.Models;

namespace PersonnelApp.Services
{
    public class ContractArchiveService
    {
        private readonly ContractArchiveRepository _repository;

        public ContractArchiveService(ContractArchiveRepository repository)
        {
            _repository = repository;
        }

        public List<ContractArchive> GetByEmployeeId(int employeeId) => _repository.GetByEmployeeId(employeeId);
        public void Add(ContractArchive archive) => _repository.Add(archive);
        public void Update(ContractArchive archive) => _repository.Update(archive);
        public ContractArchive? GetByContractNumberAndEmployeeId(string contractNumber, int employeeId) => _repository.GetByContractNumberAndEmployeeId(contractNumber, employeeId);
        public void Delete(int id) => _repository.Delete(id);
    }
}