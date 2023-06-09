﻿using HttpCache.Database;
using HttpCache.Services;
using HttpCache.Settings;

namespace HttpCache;

public static class Startup
{
    private static readonly ThreadLocal<Dictionary<Type, string>> SettingsNames =
        new(
            () => new()
        );

    public static IServiceCollection AddHttpCache(this IServiceCollection services) =>
        services
            .AddSettings(() => HttpSettings.Default)
            .AddSettings(() => CacheSettings.Default)
            .AddSettings(() => PulsarSettings.Default)
            .AddSingleton<RequestHandler>()
            .AddRedis();

    public static IServiceCollection AddRedis(this IServiceCollection services) =>
        services
            .AddRequiredSettings<RedisSettings>()
            .AddSingleton<ICacheDatabase, RedisCacheDatabase>();

    public static IServiceCollection AddPulsar(this IServiceCollection services) =>
        services
            .AddRequiredSettings<PulsarSettings>()
            .AddHostedService<PulsarRequestConsumer>();

    public static IServiceCollection AddRequiredSettings<T>(
        this IServiceCollection collection
    ) where T : class, ISettings, new() => collection.AddSettings<T>(
        defaultValueFactory: () => throw new Exception(
            $"Could not find required settings section {GetSettingsName<T>()}"
        )
    );

    public static T? GetSettings<T>(this IConfiguration configuration) where T : class, ISettings, new() =>
        configuration
            .GetSection(GetSettingsName<T>())
            .Get<T>();

    public static IServiceCollection AddSettings<T>(
        this IServiceCollection collection,
        Func<T>? defaultValueFactory = null
    )
        where T : class, ISettings, new() =>
        collection
            .AddSingleton<T>(provider =>
                GetOrDefault(
                    provider
                        .GetRequiredService<IConfiguration>()
                        .GetSettings<T>(),
                    defaultValueFactory
                )
            );

    private static T GetOrDefault<T>(T? instance, Func<T>? defaultValueFactory = null) where T : class =>
        instance ?? (defaultValueFactory?.Invoke() ?? throw new Exception("Could not find settings section."));

    private static string GetSettingsName<T>() where T : ISettings, new()
    {
        var dict = SettingsNames.Value!;
        if (dict.TryGetValue(typeof(T), out var name))
            return name;

        return dict[typeof(T)] = new T().SectionName;
    }
}