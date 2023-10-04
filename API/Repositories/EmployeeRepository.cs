using API.Contracts;
using API.Data;
using API.Models;

namespace API.Repositories;

public class EmployeeRepository : GeneralRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(BookingManagementDbContext context) : base(context) { }
    
    public string? GetLastNik()
    {
        return _context.Set<Employee>().OrderBy(e => e.Nik).LastOrDefault()?.Nik;
    }
}
