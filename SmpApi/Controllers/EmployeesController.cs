using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SmpApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // این خط کل کنترلر را قفل می‌کند
    public class EmployeesController : ControllerBase
    {
        private readonly string _connectionString;

        public EmployeesController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("SmpDb")!;
        }

        [HttpGet]
        public async Task<ActionResult<List<EmployeeDto>>> GetEmployees()
        {
            var list = new List<EmployeeDto>();

            using var conn = new SqlConnection(_connectionString);
            string sql = @"
                SELECT 
                    e.Id,
                    ISNULL(e.PersonnelCode, '') AS PersonnelCode,
                    ISNULL(e.FirstName, N'') + N' ' + ISNULL(e.LastName, N'') AS FullName,
                    ISNULL(e.Position, '') AS Position,
                    ISNULL(d.Name, N'---') AS DepartmentName,
                    ISNULL(e.IsActive, 1) AS IsActive
                FROM Employees e
                LEFT JOIN Departments d ON e.DepartmentId = d.Id
                ORDER BY e.Id DESC";

            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new EmployeeDto
                {
                    Id = reader.GetInt32(0),
                    PersonnelCode = reader.GetString(1),
                    FullName = reader.GetString(2).Trim(),
                    Position = reader.GetString(3),
                    DepartmentName = reader.GetString(4),
                    IsActive = reader.GetInt32(5) == 1
                });
            }

            return Ok(list);
        }
    }
}