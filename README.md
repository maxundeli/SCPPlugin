# MaxunPlugin

MaxunPlugin is an Exiled plugin for SCP: Secret Laboratory servers. It introduces new gameplay mechanics such as random facility blackouts, automatic warhead detonation, SCP-3114 spawns and player statistics collection with optional MySQL integration.

## Features

- **Random blackouts** – periodically turn off lights in selected zones.
- **Automatic warhead** – chance-based activation of the Dead Man's Switch late in the round.
- **SCP-3114 support** – randomly converts one SCP into SCP-3114 when the round starts.
- **Item spawns** – grant specific items to players based on their role.
- **Statistics tracking** – records kills, damage dealt, friendly fire, taken SCP objects and SCP kills.
- **MySQL database** – optional storage for persistent player statistics.

## Building

This project targets **.NET Framework 4.8**. Use the .NET SDK to compile:

```bash
# from the repository root
dotnet build -c Release
```

The resulting `MaxunPlugin.dll` will be in `bin/Release/net48`.

## Installation

1. Build the plugin or download a precompiled release.
2. Copy `MaxunPlugin.dll` into the `Exiled/Plugins` folder on your server.
3. Ensure the required dependencies from the `ExiledDependencies` directory are available in the plugin folder.
4. Restart the server to generate a configuration file.

## Configuration

After first launch, edit `MaxunPlugin.yml` in the `Exiled/Configs` directory. Below is an example configuration with default values:

```yaml
is_enabled: true
debug: false

database:
  enabled: true
  connection_string: "Server=localhost;Database=scp_db;User ID=scp_user;Password=scp_password;Pooling=true;"

blackout:
  enabled: true
  interval_min: 130
  interval_max: 170
  duration_min: 30
  duration_max: 90
  chance: 33

stats:
  enabled: true

autobomb:
  enabled: true

scp3114: true
scp3114chance: 20

spawn_items:
  ClassD:
    - item: Flashlight
      chance: 33
  Scientist:
    - item: Flashlight
      chance: 33
```

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
