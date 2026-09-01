using DiscoBox.Core.Jobs;
using DiscoBox.Core.Logging;
using DiscoBox.Core.Models;
using DiscoBox.Core.Proxy;
using DiscoBox.Core.Storage;

namespace DiscoBox.ProfileManager.Services;

public static class AppServices
{
    public static string DataDir { get; private set; } = "";

    public static SecretMasker Masker { get; private set; } = new();

    public static FileLogger Logger { get; private set; } = null!;

    public static IAccountStore AccountStore { get; private set; } = null!;

    public static IProxyStore ProxyStore { get; private set; } = null!;

    public static IJobStore JobStore { get; private set; } = null!;

    public static ISettingsStore SettingsStore { get; private set; } = null!;

    public static IGroupStore GroupStore { get; private set; } = null!;

    public static ProxyPool Pool { get; private set; } = null!;

    public static ProxyResolver Resolver { get; private set; } = null!;

    public static JobEngine Engine { get; private set; } = null!;

    public static AppSettings Settings { get; private set; } = new();

    public static List<Account> Accounts { get; } = new();

    public static (List<string> Groups, List<string> Tags) Groups { get; private set; } =
        (new List<string> { "default" }, new List<string>());

    public static void Init()
    {
        var bootstrapSettings = new SettingsStore(DataPaths.Root()).Load();
        DataDir = string.IsNullOrWhiteSpace(bootstrapSettings.DataDir)
            ? DataPaths.Root()
            : bootstrapSettings.DataDir!;

        Settings = bootstrapSettings;
        SettingsStore = new SettingsStore(DataDir);
        AccountStore = new AccountStore(DataDir);
        ProxyStore = new ProxyStore(DataDir);
        JobStore = new JobStore(DataDir);
        GroupStore = new GroupStore(DataDir);

        Logger = new FileLogger(DataDir, ParseLevel(bootstrapSettings.LogLevel), Masker);
        Pool = new ProxyPool(ProxyStore);
        Pool.Load();
        Resolver = new ProxyResolver(Pool, Logger);
        Engine = new JobEngine(JobStore, Resolver, Logger);
        Engine.Register(new CheckStatusAction());
        Engine.Register(new VerifyProxyAction(Pool));
        Engine.LoadExisting();

        Accounts.Clear();
        Accounts.AddRange(AccountStore.Load());
        Groups = GroupStore.Load();

        foreach (var account in Accounts)
        {
            Masker.Register(account.Token, account.Password);
        }

        foreach (var line in Pool.Snapshot())
        {
            Masker.Register(Core.Net.ProxyEndpoint.Parse(line.Url).Password);
        }

        Logger.Info("app", $"started, data dir {DataDir}, {Accounts.Count} accounts, {Pool.Snapshot().Count} proxy lines");
    }

    public static void PersistAccounts()
    {
        AccountStore.Save(Accounts);
        Logger.Info("storage", $"accounts saved: {Accounts.Count}");
    }

    public static void PersistGroups() => GroupStore.Save(Groups.Groups, Groups.Tags);

    public static void PersistSettings() => SettingsStore.Save(Settings);

    private static LogLevel ParseLevel(string value) =>
        Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : LogLevel.Info;
}
