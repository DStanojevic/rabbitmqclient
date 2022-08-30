using System;
using FluentValidation;

namespace RabbitMqClient.Configuration;

public class RabbitMqConfiguration
{
    public string HostName { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public String ClientName { get; set; }

    public void Validate()
    {
        var validator = new RabbitMqConfigurationValidator();
        var result = validator.Validate(this);
        if (!result.IsValid)
            throw new Exception($"Invalid configuration: {result}");
    }
}

public class RabbitMqConfigurationValidator : AbstractValidator<RabbitMqConfiguration>
{
    public RabbitMqConfigurationValidator()
    {
        RuleFor(x => x.HostName).NotEmpty();
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}