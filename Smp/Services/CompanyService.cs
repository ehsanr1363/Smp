using System.Collections.Generic;
using SMP.Data;
using SMP.Models;

namespace SMP.Services
{
    public class CompanyService
    {
        private readonly CompanyRepository _repository;
        public CompanyService(CompanyRepository repository) => _repository = repository;

        public List<Company> GetAll() => _repository.GetAll();
        public void Add(Company c) => _repository.Add(c);
        public void Update(Company c) => _repository.Update(c);
        public void Delete(int id) => _repository.Delete(id);
    }
}