using Automatic_Bluray_Ripping.Components;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

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

            builder.Services.AddScoped<DefaultSettings>();

            DefaultSettings settings = new DefaultSettings();
            
            TranscodeQueueService transcodeQueue = new TranscodeQueueService();
            ThumbnailQueue thumbnailQueue = new ThumbnailQueue();
            ThumbnailManager thumbnailManager = new ThumbnailManager(thumbnailQueue);
            SubtitleQueue subtitleQueue = new SubtitleQueue();
            SubtitleManager subtitleManager = new SubtitleManager(subtitleQueue);

            OpticalDriveManager driveManager = new OpticalDriveManager(settings);
            MakeMKVManager mkvManager = new MakeMKVManager(driveManager, settings);
            MediaScannerManager mediaScannerManager = new MediaScannerManager(mkvManager, thumbnailQueue, subtitleQueue, transcodeQueue, settings);
            TranscodeManager transcodeManager = new TranscodeManager(transcodeQueue);
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await driveManager.ReadOpticalDrives();
                    await mkvManager.ScanForBackups();
                    mediaScannerManager.LoadHandbrakePresets();
                    mediaScannerManager.LoadMKVBackups();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Something went wrong lmao {ex.Message}");
                }
                
                //transcodeManager.LoadHandbrakePresets();
                //transcodeManager.ScanBackups();
            });

            builder.Services.AddSingleton<DefaultSettings>(settings);
            builder.Services.AddSingleton<TranscodeQueueService>(transcodeQueue);
            builder.Services.AddSingleton<OpticalDriveManager>(driveManager);
            builder.Services.AddSingleton<MakeMKVManager>(mkvManager);
            builder.Services.AddSingleton<TranscodeManager>(transcodeManager);
            builder.Services.AddSingleton<ThumbnailQueue>(thumbnailQueue);
            builder.Services.AddSingleton<MediaScannerManager>(mediaScannerManager);
            builder.Services.AddSingleton<SubtitleQueue>(subtitleQueue);
            builder.Services.AddHostedService<TranscodeManager>(provider => transcodeManager);
            builder.Services.AddHostedService<ThumbnailManager>(provider => thumbnailManager);
            builder.Services.AddHostedService<SubtitleManager>(provider => subtitleManager);


            var app = builder.Build();

            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".mkv"] = "video/x-matroska";

            AppServices.Provider = app.Services;

            string transcodesPath = Path.Combine(AppContext.BaseDirectory, "Transcodes");

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(transcodesPath),
                RequestPath = "/transcodes",
                ContentTypeProvider = provider
            });

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
