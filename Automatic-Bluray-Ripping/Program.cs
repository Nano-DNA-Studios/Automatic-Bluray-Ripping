using Automatic_Bluray_Ripping.Components;

namespace Automatic_Bluray_Ripping
{
    public static class AppServices
    {
        public static IServiceProvider? Provider { get; set; }

        public static T? Get<T>() where T : class
        {
            return Provider?.GetService(typeof(T)) as T;
        }
    }

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
            MakeMKVManager mkvManager = new MakeMKVManager();
            TranscodeManager transcodeManager = new TranscodeManager(transcodeQueue);

            _ = Task.Run(() =>
            {
                driveManager.ReadOpticalDrives();
                mkvManager.ScanForBackups();
                transcodeManager.LoadHandbrakePresets();
                transcodeManager.ScanBackups();
            });

            builder.Services.AddSingleton<TranscodeQueueService>(transcodeQueue);
            builder.Services.AddSingleton<OpticalDriveManager>(driveManager);
            builder.Services.AddSingleton<MakeMKVManager>(mkvManager);
            builder.Services.AddSingleton<TranscodeManager>(transcodeManager);
            builder.Services.AddHostedService<TranscodeManager>(provider => new TranscodeManager(transcodeQueue));

            var app = builder.Build();

            AppServices.Provider = app.Services;

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
