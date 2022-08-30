using RabbitMqClient;
using SenderTestApp.Internal;
using FluentValidation.AspNetCore;



var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Logging.AddConsole();
builder.Services.AddControllers().AddFluentValidation(conf =>
{
    conf.ImplicitlyValidateChildProperties = true;
    conf.DisableDataAnnotationsValidation = true;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//RabbitMqHelper message publisher.
builder.Services.RegisterMessagePublisher(builder.Configuration);

builder.Services.AddScoped<IMessagesService, MessagesService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
