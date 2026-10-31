using System.Collections.Generic;
using PersonnelApp.Data;
using PersonnelApp.Models;

namespace PersonnelApp.Services
{
    public class DepartmentService
    {
        private readonly DepartmentRepository _departmentRepository;

        public DepartmentService(DepartmentRepository departmentRepository)
        {
            _departmentRepository = departmentRepository;
        }

        public List<Department> GetAll()
        {
            return _departmentRepository.GetAll();
        }

        public void Add(Department dept)
        {
            _departmentRepository.Add(dept);
        }

        // 🌟 متد Update که خطای فعلی را برطرف می‌کند
        public void Update(Department dept)
        {
            _departmentRepository.Update(dept);
        }

        public void Delete(int id)
        {
            _departmentRepository.Delete(id);
        }
    }
}