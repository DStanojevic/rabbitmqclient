using FluentValidation;

namespace SenderTestApp.Models.Validators;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Header).NotNull();
        RuleFor(x => x.Body).NotEmpty();
    }
}