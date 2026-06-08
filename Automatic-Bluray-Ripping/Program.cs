using Automatic_Bluray_Ripping.Components;

namespace Automatic_Bluray_Ripping
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            //builder.Services.AddScoped<TranscodeStateService>();
            builder.Services.AddScoped<DefaultSettings>();

            TranscodeQueueService transcodeQueue = new TranscodeQueueService();
            OpticalDriveManager driveManager = new OpticalDriveManager();

            _ = Task.Run(async () =>
            {
                await driveManager.ReadOpticalDrives();
            });

            builder.Services.AddSingleton<TranscodeQueueService>(transcodeQueue);
            builder.Services.AddSingleton<OpticalDriveManager>(driveManager);
            builder.Services.AddHostedService<TranscodeBackgroundWorker>(provider => new TranscodeBackgroundWorker(transcodeQueue));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
