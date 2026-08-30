using Microsoft.Extensions.Logging;
using FairyAI_Android.Services;
using FairyAI_Android.ViewModels;
using FairyAI_Android.Pages;

namespace FairyAI_Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<ILlmService, LlmService>();
        builder.Services.AddSingleton<IIntentAnalyzer, IntentAnalyzer>();
        builder.Services.AddSingleton<ISearchService, SearchService>();
        builder.Services.AddSingleton<ITtsApiService, TtsApiService>();
        builder.Services.AddSingleton<ITtsService, TtsService>();
        builder.Services.AddSingleton<IChatHistoryService, ChatHistoryService>();
        builder.Services.AddSingleton<ILocalSendService, LocalSendService>();
        builder.Services.AddSingleton<WakeWordService>();

        // ViewModels
        builder.Services.AddTransient<MainViewModel>();

        // Pages
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
