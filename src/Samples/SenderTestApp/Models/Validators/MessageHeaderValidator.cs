using FluentValidation;
using FluentValidation.Results;

namespace SenderTestApp.Models.Validators;

public class MessageHeaderValidator : AbstractValidator<MessageHeader>
{
    public MessageHeaderValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
    }
}