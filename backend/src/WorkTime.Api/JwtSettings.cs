// JwtSettings read out of the ASP.NET Core configuration (appsettings.json section "JwtSettings").
// Stage 2 keeps the same four values the wire contract fixes: SecretKey / Issuer / Audience /
// ExpiryMinutes. HS256 signing requires a key of at least 32 bytes, so the values are validated
// before the token pipeline is built.
namespace WorkTime.Api;

/// <summary>Настройки подписания JWT: ключ, issuer, audience и срок жизни токена (BR32).</summary>
public class JwtSettings
{
    // BR32: the token lifetime and the session inactivity limit are both 8 hours.
    /// <summary>Срок токена по умолчанию: восемь часов — он же окно неактивности сессии (BR32).</summary>
    public const int DefaultExpiryMinutes = 480;

    // Аудит 2.1: ключ подписи берётся ТОЛЬКО из конфигурации (appsettings, переменные окружения,
    // dotnet user-secrets). Дефолтный ключ из репозитория из кода удалён — код ничего не подставляет.
    /// <summary>Ключ подписи: берётся только из конфигурации, дефолтный ключ из кода удалён (аудит 2.1).</summary>
    public string SecretKey { get; set; } = "";
    /// <summary>Issuer токенов.</summary>
    public string Issuer { get; set; } = "";
    /// <summary>Audience токенов.</summary>
    public string Audience { get; set; } = "";
    /// <summary>Срок жизни токена в минутах.</summary>
    public int ExpiryMinutes { get; set; } = DefaultExpiryMinutes;
}

// Binds the "JwtSettings" section of the configuration and validates it (Stage 2).
/// <summary>Читает секцию настроек из конфигурации и проверяет её (аудит 2.1).</summary>
public class JwtSettingsFactory
{
    // HS256 needs a key of at least this many bytes (256 bits).
    /// <summary>Минимальная длина ключа HS256 в байтах (256 бит).</summary>
    public const int MinSecretKeyBytes = 32;

    // Точные имена из конфигурации: секция "JwtSettings", ключ "SecretKey".
    /// <summary>Точное имя секции конфигурации.</summary>
    public const string SettingsSectionName = "JwtSettings";
    /// <summary>Точное имя ключа конфигурации для подписывающего ключа.</summary>
    public const string SecretKeyConfigKey = "SecretKey";

    // Прежний dev-дефолт из репозитория. Он больше нигде не подставляется как fallback: этот
    // список только отвергает ключ (аудит 2.1), поэтому значение хранится как запрет, а не как подмена.
    /// <summary>Прежний dev-дефолт из репозитория: хранится как запрет — этот список только отвергает ключ и ничего не подставляет (аудит 2.1).</summary>
    public const string RejectedDevelopmentSecretKey =
        "work-time-development-signing-key-change-me-please-32+-bytes";

    /// <summary>Читает настройки из конфигурации; отсутствие секции — ошибка конфигурации при любом окружении (аудит 2.1).</summary>
    public static JwtSettings TryFromConfiguration(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment environment)
    {
        var settings = configuration.GetSection(SettingsSectionName).Get<JwtSettings>();

        if (settings == null)
        {
            // Отсутствие секции — ошибка конфигурации при любом окружении: тихая подмена
            // ключа недопустима и вне Development, и в Development (аудит 2.1).
            throw new ArgumentException(
                MissingSigningKeyMessage(),
                paramName: "CONFIGURATION");
        }

        // Молча не чиним ExpiryMinutes: невалидное значение отдаёт ValidateSettings как
        // явную ошибку конфигурации (правка аудита).
        TryValidateSigningKey(settings);
        ValidateSettings(settings);

        return settings;
    }

    // Жёсткая проверка ключа подписи (аудит 2.1). Вызывается на старте приложения, чтобы
    // пустой или прежний дефолтный ключ ронял процесс, а не проявлялся при первом выпуске токена.
    /// <summary>Жёсткая проверка подписывающего ключа на старте приложения: пустой или прежний дефолтный ключ роняет процесс, а не проявляется при первом выпуске токена (аудит 2.1).</summary>
    public static void TryValidateSigningKey(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SecretKey)
            || settings.SecretKey == RejectedDevelopmentSecretKey)
        {
            throw new ArgumentException(
                MissingSigningKeyMessage(),
                paramName: "CONFIGURATION");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(settings.SecretKey) < MinSecretKeyBytes)
        {
            throw new ArgumentException(
                "[RU] Ключ подписи JWT должен быть не короче " + MinSecretKeyBytes +
                " байт (256 бит для HS256). Задайте его в конфигурации: секция \"" +
                SettingsSectionName + "\", ключ \"" + SecretKeyConfigKey + "\" " +
                "(или переменная окружения " + SettingsSectionName + "__" + SecretKeyConfigKey + "). " +
                "Для локального запуска: dotnet user-secrets set \"" + SettingsSectionName + ":" +
                SecretKeyConfigKey + "\" \"<ключ>\".",
                paramName: "CONFIGURATION");
        }
    }

    private static string MissingSigningKeyMessage()
    {
        return "[RU] Ключ подписи JWT не задан: секция \"" + SettingsSectionName + "\" отсутствует, "
            + "ключ \"" + SecretKeyConfigKey + "\" пуст или состоит из пробелов, либо равен прежнему "
            + "дефолтному значению из репозитория. Задайте ключ через конфигурацию или переменную "
            + "окружения " + SettingsSectionName + "__" + SecretKeyConfigKey + ". "
            + "Для локального запуска: dotnet user-secrets set \"" + SettingsSectionName + ":" +
            SecretKeyConfigKey + "\" \"<ключ>\".";
    }

    private static void ValidateSettings(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new ArgumentException(
                "[RU] JwtSettings must define Issuer and Audience.",
                paramName: "CONFIGURATION");
        }

        if (settings.ExpiryMinutes <= 0)
        {
            throw new ArgumentException(
                "[RU] JwtSettings.ExpiryMinutes must be a positive number of minutes.",
                paramName: "CONFIGURATION");
        }
    }
}
