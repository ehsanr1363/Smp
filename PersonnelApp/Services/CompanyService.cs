using System.Collections.Generic;
using PersonnelApp.Data;
using PersonnelApp.Models;

namespace PersonnelApp.Services
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