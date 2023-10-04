using API.DTOs.Employees;
using FluentValidation;

namespace API.Utilities.Validations.Employees;

public class CreateEmployeeValidator : AbstractValidator<CreateEmployeeDto>
{
    public CreateEmployeeValidator()
    {
        RuleFor(e => e.FirstName)
           .NotEmpty();

        RuleFor(e => e.BirthDate)
           .NotEmpty();
        //.LessThanOrEqualTo("01/01/2003"); // 18 years old

        RuleFor(e => e.Gender)
           .NotEmpty()
           .IsInEnum();

        RuleFor(e => e.HiringDate).NotEmpty();

        RuleFor(e => e.Email)
           .NotEmpty().WithMessage("Tidak Boleh Kosong")
           .EmailAddress().WithMessage("Format Email Salah");

        RuleFor(e => e.PhoneNumber)
           .NotEmpty()
           .MinimumLength(10)
           .MaximumLength(20);
    }
}
