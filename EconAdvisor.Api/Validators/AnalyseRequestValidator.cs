using EconAdvisor.Api.Models;
using FluentValidation;

namespace EconAdvisor.Api.Validators;

public sealed class AnalyseRequestValidator : AbstractValidator<AnalyseRequest>
{
    public AnalyseRequestValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required.")
            .MaximumLength(500).WithMessage("Question must not exceed 500 characters.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required.")
            .Matches(@"^[A-Z]{2}$").WithMessage("Country must be exactly 2 uppercase letters (e.g. SE).");
    }
}
