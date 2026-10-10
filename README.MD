# GlazeExtensions

Simple tool that adds a few GlazeWM commands that I was missing from Komorebi.
Important: This is mostly just a personal tool and as such highly customized to fit my use-case. I only made this
open-source because it might be helpful for a few users.

# Workspace Config (GlazeWM)

My setup revolves around having 10 workspaces per monitor.
The config for it looks something like this:
```yaml
workspaces:
  - name: '1-0' # Monitor Index = 1, Workspace Index = 0
    bind_to_monitor: 1
    keep_alive: true
...
  - name: '1-9' # Monitor Index = 1, Workspace Index = 9
    bind_to_monitor: 1
    keep_alive: true
```

The naming scheme for every workspace is `{MonitorIndex}-{WorkspaceIndex}`. This is required for determining which is the logical next & previous workspace for a monitor.

You can find my full GlazeWM config here: https://github.com/Lumanyx/windows-dotfiles/tree/main/glazewm

# Setup

Build the Client & Server module with Visual Studio or Rider.

Start `Server.ex[Server.csproj](Server/Server.csproj)e`. 

`Client.exe` can now run commands like this:

`Client.exe move_active_window_to_workspace 1`

# Commands

The following commands are supported:

- `move_active_window_to_workspace` (parameter: absolute workspace index of monitor (0-9))
  - Moves the currently focused window to the nth workspace on the current monitor.
- `move_active_window_to_workspace_relative`: (parameter: workspace index delta (-1 or 1))
  - Moves the currently focused window to the desired relative workspace on the current monitor.
- `focus_workspace`: (parameter: absolute workspace index of monitor (0-9))
  - Focuses the nth workspace on the current monitor.
- `focus_workspace_relative`: (parameter: workspace index delta (-1 or 1))
  - Focuses the next or previous workspace on the current monitor.
 
## License

MIT, see [LICENSE](LICENSE.MD).