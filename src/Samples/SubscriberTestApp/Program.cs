using RabbitMQ.Client;
using RabbitMqClient;
using SubscriberTestApp.BackgroundServices;
using SubscriberTestApp.Internal.Repositories;
using SubscriberTestApp.Internal.Services;
using SubscriberTestApp.Internal.Subscribers;

var builder = WebApplication.CreateBuilder(args);

//RabbitMQHelper services registration.
builder.Services.RegisterMessageSubscriber(builder.Configuration);

// Add services to the container.
builder.Services.AddSingleton<IMessagesRepository, InMemoryMessagesRepository>();
builder.Services.AddScoped<IMessageService, MessagesService>();
builder.Services.AddScoped<ISimpleMessageHandler, SimpleMessageHandler>();
builder.Services.AddScoped<IMessageIdPrependMessageHandler, MessageIdPrependMessageHandler>();
builder.Services.AddScoped<IRetryingMessageHandler, RetryingMessageHandler>();
builder.Services.AddScoped<IDelayedWorkMessageHandler, DelayedWorkMessageHandler>();

builder.Logging.AddConsole();
builder.Services.AddControllers();

//Add hosted services
builder.Services.AddHostedService<TestTopicSubscriberBackgroundService>();
builder.Services.AddHostedService<Worker1BackgroundService>();
builder.Services.AddHostedService<Worker2BackgroundService>();
builder.Services.AddHostedService<SimplePubSubSubscriberBackgroundService1>();
builder.Services.AddHostedService<SimplePubSubSubscriberBackgroundService2>();
builder.Services.AddHostedService<RetryingSubscriberBackgroundService>();
builder.Services.AddHostedService<DelayedWorker1BackgroundService>();
builder.Services.AddHostedService<DelayedWorker2BackgroundService>();
builder.Services.AddHostedService<TestReturnFromDleBackgroundService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
