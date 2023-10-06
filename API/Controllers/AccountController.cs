using System.Net;
using System.Security.Claims;
using API.Contracts;
using API.Data;
using API.DTOs.Accounts;
using API.DTOs.Educations;
using API.Models;
using API.Utilities.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEducationRepository _educationRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IAccountRoleRepository _accountRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IEmailHandler _emailHandler;
    private readonly ITokenHandler _tokenHandler;

    public AccountController(IAccountRepository accountRepository, IEmployeeRepository employeeRepository,
                             IEducationRepository educationRepository, IUniversityRepository universityRepository,
                             IEmailHandler emailHandler, ITokenHandler tokenHandler, IAccountRoleRepository accountRoleRepository, IRoleRepository roleRepository)
    {
        _accountRepository = accountRepository;
        _employeeRepository = employeeRepository;
        _educationRepository = educationRepository;
        _universityRepository = universityRepository;
        _emailHandler = emailHandler;
        _tokenHandler = tokenHandler;
        _accountRoleRepository = accountRoleRepository;
        _roleRepository = roleRepository;
    }

    [HttpPut("change-password")]
    [AllowAnonymous]
    public IActionResult ChangePassword(ChangePasswordDto changePasswordDto)
    {
        try
        {
            var employee = _employeeRepository.GetByEmail(changePasswordDto.Email);
            if (employee is null)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "Email is invalid!"
                });
            }

            var account = _accountRepository.GetByGuid(employee.Guid);
            if (account is null)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "Email is invalid!"
                });
            }

            if (account.OTP != changePasswordDto.Otp)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "OTP is invalid!"
                });
            }

            if (account.ExpiredTime < DateTime.Now)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "OTP is expired!"
                });
            }

            if (account.IsUsed)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "OTP is already used!"
                });
            }

            account.Password = HashHandler.HashPassword(changePasswordDto.Password);
            account.IsUsed = true;
            _accountRepository.Update(account);

            return Ok(new ResponseOKHandler<string>("Password Changed"));
        }
        catch (ExceptionHandler ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ResponseErrorHandler {
                Code = StatusCodes.Status500InternalServerError,
                Status = HttpStatusCode.InternalServerError.ToString(),
                Message = "Failed to change password",
                Error = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpPost("forgot-password/{email}")]
    [AllowAnonymous]
    public IActionResult ForgotPassword(string email)
    {
        try
        {
            var employee = _employeeRepository.GetByEmail(email);
            if (employee is null)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "Email is invalid!"
                });
            }

            var account = _accountRepository.GetByGuid(employee.Guid);
            if (account is null)
            {
                return NotFound(new ResponseErrorHandler {
                    Code = StatusCodes.Status400BadRequest,
                    Status = HttpStatusCode.BadRequest.ToString(),
                    Message = "Email is invalid!"
                });
            }

            account.ExpiredTime = DateTime.Now.AddMinutes(5);
            account.IsUsed = false;
            account.OTP = new Random().Next(111111, 999999);
            _accountRepository.Update(account);
            
            _emailHandler.Send("Forgot Password", $"Your OTP is {account.OTP}", email);

            return Ok(new ResponseOKHandler<object>("OTP has been sent to your email"));
        }
        catch (ExceptionHandler ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ResponseErrorHandler {
                Code = StatusCodes.Status500InternalServerError,
                Status = HttpStatusCode.InternalServerError.ToString(),
                Message = "Failed to create OTP",
                Error = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login(LoginDto loginDto)
    {
        var employee = _employeeRepository.GetByEmail(loginDto.Email);
        if (employee is null)
        {
            return NotFound(new ResponseErrorHandler {
                Code = StatusCodes.Status400BadRequest,
                Status = HttpStatusCode.BadRequest.ToString(),
                Message = "Account or Password is invalid!"
            });
        }

        var account = _accountRepository.GetByGuid(employee.Guid);
        if (!HashHandler.VerifyPassword(loginDto.Password, account!.Password))
        {
            return BadRequest(new ResponseErrorHandler {
                Code = StatusCodes.Status400BadRequest,
                Status = HttpStatusCode.BadRequest.ToString(),
                Message = "Account or Password is invalid!"
            });
        }

        var claims = new List<Claim>();
        claims.Add(new Claim("Email", employee.Email));
        claims.Add(new Claim("FullName", string.Concat(employee.FirstName + " " + employee.LastName)));

        var getRoleName = from ar in _accountRoleRepository.GetAll()
                          join r in _roleRepository.GetAll() on ar.RoleGuid equals r.Guid
                          where ar.AccountGuid == account.Guid
                          select r.Name;

        foreach (var roleName in getRoleName)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));
        }

        var generateToken = _tokenHandler.Generate(claims);

        return Ok(new ResponseOKHandler<object>("Login Success", new {Token = generateToken}));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public IActionResult Register(RegisterDto registerDto)
    {
        using var context = _accountRepository.GetContext();
        using var transaction = context.Database.BeginTransaction();
        try
        {
            var university =
                _universityRepository.GetByCodeAndName(registerDto.UniversityCode, registerDto.UniversityName);
            if (university is null)
            {
                university = _universityRepository.Create(new University {
                    Code = registerDto.UniversityCode,
                    Name = registerDto.UniversityName
                });
            }

            var employee = _employeeRepository.Create(new Employee {
                Nik = GenerateHandler.Nik(_employeeRepository.GetLastNik()),
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                BirthDate = registerDto.BirthDate,
                Gender = registerDto.Gender,
                HiringDate = registerDto.HiringDate,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber
            });

            var education = _educationRepository.Create(new Education {
                Guid = employee.Guid,
                Major = registerDto.Major,
                Degree = registerDto.Degree,
                Gpa = registerDto.Gpa,
                UniversityGuid = university.Guid
            });

            var account = _accountRepository.Create(new Account {
                Guid = employee.Guid,
                Password = HashHandler.HashPassword(registerDto.Password)
            });

            var accountRole = _accountRoleRepository.Create(new AccountRole {
                AccountGuid = account.Guid,
                RoleGuid = _roleRepository.GetDefaultRoleGuid() ?? throw new Exception("Default Role Not Found")
            });
            
            transaction.Commit();

            return Ok(new ResponseOKHandler<string>("Account Created"));
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(StatusCodes.Status500InternalServerError, new ResponseErrorHandler {
                Code = StatusCodes.Status500InternalServerError,
                Status = HttpStatusCode.InternalServerError.ToString(),
                Message = "Failed to create data",
                Error = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var result = _accountRepository.GetAll();
        if (!result.Any())
        {
            return NotFound("Data Not Found");
        }

        var data = result.Select(x => (AccountDto)x);

        return Ok(data);
    }

    [HttpGet("{guid}")]
    public IActionResult GetByGuid(Guid guid)
    {
        var result = _accountRepository.GetByGuid(guid);
        if (result is null)
        {
            return NotFound("Id Not Found");
        }

        return Ok((AccountDto)result);
    }

    [HttpPost]
    public IActionResult Create(AccountDto accountDto)
    {
        Account toCreate = accountDto;
        toCreate.Password = HashHandler.HashPassword(accountDto.Password);
        var result = _accountRepository.Create(toCreate);
        if (result is null)
        {
            return BadRequest("Failed to create data");
        }

        return Ok((AccountDto)result);
    }

    [HttpPut]
    public IActionResult Update(AccountDto accountDto)
    {
        var entity = _accountRepository.GetByGuid(accountDto.Guid);
        if (entity is null)
        {
            return NotFound("Id Not Found");
        }

        Account toUpdate = accountDto;
        toUpdate.CreatedDate = entity.CreatedDate;
        toUpdate.Password = HashHandler.HashPassword(accountDto.Password);

        var result = _accountRepository.Update(toUpdate);
        if (!result)
        {
            return BadRequest("Failed to update data");
        }

        return Ok("Data Updated");
    }

    [HttpDelete("{guid}")]
    public IActionResult Delete(Guid guid)
    {
        var entity = _accountRepository.GetByGuid(guid);
        if (entity is null)
        {
            return NotFound("Id Not Found");
        }

        var result = _accountRepository.Delete(entity);
        if (!result)
        {
            return BadRequest("Failed to delete data");
        }

        return Ok("Data Deleted");
    }
}
