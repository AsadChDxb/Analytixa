using AdHocReporting.Application.DTOs.Auth;
using AdHocReporting.Application.DTOs.Datasources;
using FluentValidation;

namespace AdHocReporting.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class CreateDatasourceRequestValidator : AbstractValidator<CreateDatasourceRequest>
{
    public CreateDatasourceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SqlDefinitionOrObjectName).NotEmpty();
        RuleForEach(x => x.Parameters).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.DataType).NotEmpty().MaximumLength(50);
        });
    }
}
