using bulk_insertt_api;
using bulk_insertt_api.Hubs;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ImportExecutor>();
builder.Services.AddSignalR();
//builder.Services.AddResponseCompression(opt =>
//{
//    opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
//        ["application/octet-stream"]);
//});
builder.Services.AddCors(options =>
{
    options.AddPolicy("vueapp", builder => builder
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());
});
var app = builder.Build();

//app.UseResponseCompression();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.MapHub<ImportHub>("/hub/import");
app.MapControllers();
app.UseCors("vueapp");
app.Run();
