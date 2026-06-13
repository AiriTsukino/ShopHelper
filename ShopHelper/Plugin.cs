using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ShopHelper.Services;
using ShopHelper.UI;

namespace ShopHelper;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/shophelper";
    private const string SettingsCommandName = "/shophelpersettings";
    private readonly WindowSystem windowSystem = new("ShopHelper");
    private readonly Configuration config;
    private readonly PersistenceService persistence;
    private readonly ShopService shopService;
    private readonly MainWindow mainWindow;
    private readonly SettingsWindow settingsWindow;
    private bool mainWindowOpenedByShop;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        DalamudServices.Initialize(pluginInterface);
        config = DalamudServices.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.Clamp();
        persistence = new PersistenceService(config);
        shopService = new ShopService(config);

        mainWindow = new MainWindow(config, persistence, shopService, OpenSettingsWindow) { IsOpen = config.WindowVisible };
        settingsWindow = new SettingsWindow(config, persistence) { IsOpen = config.SettingsWindowVisible };
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(settingsWindow);

        DalamudServices.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = "Toggle ShopHelper window." });
        DalamudServices.CommandManager.AddHandler(SettingsCommandName, new CommandInfo(OnSettingsCommand) { HelpMessage = "Toggle ShopHelper settings window." });
        DalamudServices.PluginInterface.UiBuilder.Draw += DrawUi;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        foreach (var addonName in new[] { "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop", "FreeShop", "GrandCompanySupplyList" })
            DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, addonName, OnShopAddonSetup);

        persistence.SaveNow();
    }

    private void OnCommand(string command, string arguments) => ToggleMainUi();
    private void OnSettingsCommand(string command, string arguments) => ToggleConfigUi();

    private void OnShopAddonSetup(AddonEvent type, AddonArgs args)
    {
        if (!config.AutoOpenWithShop)
            return;

        mainWindowOpenedByShop = true;
        config.WindowVisible = true;
        mainWindow.IsOpen = true;
        persistence.SaveNow();
    }

    private void OpenSettingsWindow()
    {
        config.SettingsWindowVisible = true;
        settingsWindow.IsOpen = true;
        persistence.SaveNow();
    }

    private void ToggleMainUi()
    {
        mainWindowOpenedByShop = false;
        config.WindowVisible = !config.WindowVisible;
        mainWindow.IsOpen = config.WindowVisible;
        persistence.SaveNow();
    }

    private void ToggleConfigUi()
    {
        config.SettingsWindowVisible = !config.SettingsWindowVisible;
        settingsWindow.IsOpen = config.SettingsWindowVisible;
        persistence.SaveNow();
    }

    private void DrawUi()
    {
        if (mainWindowOpenedByShop && mainWindow.IsOpen && !shopService.IsShopOpen)
        {
            mainWindowOpenedByShop = false;
            mainWindow.IsOpen = false;
            config.WindowVisible = false;
            persistence.SaveNow();
        }

        windowSystem.Draw();
        if (config.WindowVisible != mainWindow.IsOpen || config.SettingsWindowVisible != settingsWindow.IsOpen)
        {
            config.WindowVisible = mainWindow.IsOpen;
            config.SettingsWindowVisible = settingsWindow.IsOpen;
            persistence.SaveNow();
        }
    }

    public void Dispose()
    {
        persistence.SaveNow();
        foreach (var addonName in new[] { "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop", "FreeShop", "GrandCompanySupplyList" })
            DalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, addonName, OnShopAddonSetup);

        DalamudServices.PluginInterface.UiBuilder.Draw -= DrawUi;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        DalamudServices.CommandManager.RemoveHandler(CommandName);
        DalamudServices.CommandManager.RemoveHandler(SettingsCommandName);
        windowSystem.RemoveAllWindows();
        shopService.Dispose();
        persistence.Dispose();
    }
}
