using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Newtonsoft.Json;

using CSTimers = CounterStrikeSharp.API.Modules.Timers;

public class Config : BasePluginConfig
{
    public string ToggleCommand { get; set; } = "damage, showdamage, damagedisplay";
    public bool DisplayDamage { get; set; } = true;
    public bool DisplayGrenadeDamage { get; set; } = true;
    public float DisplayTimer { get; set; } = 5;
}

public partial class Plugin : BasePlugin, IPluginConfig<Config>
{
    private Dictionary<CCSPlayerController, string> playerMessages = new Dictionary<CCSPlayerController, string>();

    private Dictionary<CCSPlayerController, CSTimers.Timer> playerMessageTimers = new Dictionary<CCSPlayerController, CSTimers.Timer>();

    private Dictionary<CCSPlayerController, int> grenadeDamage = new Dictionary<CCSPlayerController, int>();

    private Dictionary<CCSPlayerController, byte> playerTeams = new Dictionary<CCSPlayerController, byte>();

    private Dictionary<string, bool> showDamageEnabled = new Dictionary<string, bool>();

    private string? showDamageFilePath;

    private void LoadShowDamageConfig()
    {
        if (File.Exists(showDamageFilePath))
        {
            string json = File.ReadAllText(showDamageFilePath);
            showDamageEnabled = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
        }
        else showDamageEnabled = new Dictionary<string, bool>();
    }

    private void SaveShowDamageConfig()
    {
        string json = JsonConvert.SerializeObject(showDamageEnabled);
        File.WriteAllText(showDamageFilePath!, json);
    }

    public override string ModuleName => "ShowDamage";
    public override string ModuleVersion => "1.1";
    public override string ModuleAuthor => "ABKAM, continued by exkludera";

    public override void Load(bool hotReload)
    {
        showDamageFilePath = Path.Combine(ModuleDirectory, "commandsave.json"); 

        LoadShowDamageConfig();

        foreach (var command in Config.ToggleCommand.Split(','))
            AddCommand($"css_{command}", "Open Trails Menu", ToggleShowDamage!);

        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
    }
    public override void Unload(bool hotReload)
    {
        playerMessages.Clear();
        playerMessageTimers.Clear();
        grenadeDamage.Clear();
        playerTeams.Clear();
        showDamageEnabled.Clear();

        RemoveListener<Listeners.OnTick>(OnTick);
        DeregisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        DeregisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
    }

    public Config Config { get; set; } = new Config();
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    private void OnTick()
    {
        foreach (var kvp in playerMessages)
        {
            var player = kvp.Key;
            var message = kvp.Value;

            if (player != null && player.IsValid)
                player.PrintToCenterHtml(message);
        }
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;

        var player = @event.Userid;

        var newTeam = (byte)@event.Team;

        playerTeams[player!] = newTeam;

        return HookResult.Continue;
    }
    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim = @event.Userid;

        if (attacker != null && victim != null && playerTeams.TryGetValue(attacker, out var attackerTeam) && playerTeams.TryGetValue(victim, out var victimTeam))
        {
            if (attackerTeam != victimTeam)
            {
                var playerName = @event.Userid!.PlayerName;
                var remainingHP = @event.Health;
                var damageHP = @event.DmgHealth;
                var hitgroup = @event.Hitgroup;

                string attackerSteamId = attacker.SteamID.ToString();
                if (!showDamageEnabled.ContainsKey(attackerSteamId))
                {
                    if (@event.Weapon == "hegrenade")
                    {
                        if (!grenadeDamage.ContainsKey(attacker))
                            grenadeDamage[attacker] = 0;

                        grenadeDamage[attacker] += damageHP;

                        if (Config.DisplayGrenadeDamage) ShowTotalGrenadeDamage(attacker);
                    }
                    else
                    {
                        var message = Localizer["DamageHTML", playerName, damageHP, remainingHP, HitGroupToString(hitgroup)];
                        if (Config.DisplayDamage) UpdateCenterMessage(attacker, message, Config.DisplayTimer);
                    }
                }
            }
        }

        return HookResult.Continue;
    }

    private void ToggleShowDamage(CCSPlayerController player, CommandInfo command)
    {
        string playerSteamId = player.SteamID.ToString();

        if (showDamageEnabled.ContainsKey(playerSteamId))
        {
            showDamageEnabled.Remove(playerSteamId);
            player.PrintToChat(Localizer["EnabledMessage"]);
        }
        else
        {
            showDamageEnabled[playerSteamId] = true;
            player.PrintToChat(Localizer["DisableMessage"]);
        }

        SaveShowDamageConfig();
    }

    private void ShowTotalGrenadeDamage(CCSPlayerController attacker)
    {
        if (grenadeDamage.TryGetValue(attacker, out var totalDamage))
        {
            var message = Localizer["GrenadeDamageHTML", totalDamage];

            playerMessages[attacker] = message;

            if (playerMessageTimers.TryGetValue(attacker, out var existingTimer))
            {
                existingTimer.Kill(); 
            }

            var messageTimer = AddTimer(Config.DisplayTimer, () => 
            {
                playerMessages.Remove(attacker);
            });

            playerMessageTimers[attacker] = messageTimer;
        }
    }

    private void UpdateCenterMessage(CCSPlayerController player, string message, float durationInSeconds)
    {
        playerMessages[player] = message;

        if (playerMessageTimers.TryGetValue(player, out var existingTimer))
            existingTimer.Kill();

        var messageTimer = AddTimer(durationInSeconds, () =>
        {
            playerMessages.Remove(player);
        });

        playerMessageTimers[player] = messageTimer;
    }

    private string HitGroupToString(int hitGroup)
    {
        switch (hitGroup)
        {
            case 1:
                return "Head";
            case 2:
                return "Chest";
            case 3:
                return "Stomach";
            case 4:
                return "Left Arm";
            case 5:
                return "Right Arm";
            case 6:
                return "Left Leg";
            case 7:
                return "Right Leg";
            case 10:
                return "Gear";
            default:
                return "Unknown";
        }
    }
}
