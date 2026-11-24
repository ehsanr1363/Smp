using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SmpApi.Security;

namespace SmpApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly TokenService _tokenService;

        public AuthController(IConfiguration config, TokenService tokenService)
        {
            _connectionString = config.GetConnectionString("SmpDb")!;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            using var conn = new SqlConnection(_connectionString);
            string sql = "SELECT Username, PasswordHash, Role, ISNULL(IsActive, 1) AS IsActive FROM Users WHERE Username = @User";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@User", request.Username.Trim());

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized(new { message = "نام کاربری یا رمز عبور اشتباه است." });

            bool isActive = reader.GetInt32(3) == 1;
            if (!isActive)
                return Unauthorized(new { message = "حساب کاربری شما غیرفعال است." });

            string storedHash = reader.IsDBNull(1) ? "" : reader.GetString(1);
            string role = reader.IsDBNull(2) ? "Viewer" : reader.GetString(2);

            if (!SecurityHelper.VerifyPassword(request.Password, storedHash))
                return Unauthorized(new { message = "نام کاربری یا رمز عبور اشتباه است." });

            string token = _tokenService.CreateToken(request.Username, role);

            return Ok(new
            {
                token = token,
                username = request.Username,
                role = role
            });
        }
    }
}