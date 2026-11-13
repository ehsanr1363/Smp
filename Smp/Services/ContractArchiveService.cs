using System.Collections.Generic;
using SMP.Data;
using SMP.Models;

namespace SMP.Services
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