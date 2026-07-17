# my fork of hypermenu which is a fork of malummenu
WARNING: ultra vibecoded

original repos:
https://github.com/scp222thj/MalumMenu
https://github.com/The-HyperMenu-Team/HyperMenu

# i can't read how do i install this without building it
install regular malum and replace the dll file under /Among Us/BepInEx/plugins with the one in the releases
honestly i don't know if that will cause issues. it probably will. i don't care

# what features does this fork add ??? 🤔
1. show player view - you can see peoples view boundaries as a coloured outline
2. show tp kill - adds a button when you're impostor that kills the nearest player on left click OR lets you pick a player on right click, and then teleports outside the map so you're out of view. combine it with teleport to cursor for actual usage
3. task complete - button that just compeltes the task ur in
4. xp farm - badly made xp farm, host a lobby before u turn it on and then afk
5. show impostors - just shows imps in the bottom left cause i sometimes need that
6. phantom invis - tps away when you go invis/appear as phantom, later ill make it just not show the appear anim. tps back when the anim is over do not fret you are not trapped in space forever
7. skip info - poorly named but it just skips the part where it shows you your role. i think it messes with kill cd so it might get u kicked if ur imp and u kill too fast

# how do i build this ?!?!?!??
open command prompt or terminal inside the folder and run this:::
```sh
AmongUs="/path/to/Among Us/" dotnet build src/MalumMenu.csproj -c Release
```
the path should be to the folder that amongus.exe is in or whatever the executable is called i forgot

on linux it will be

```sh
AmongUs="/home/user/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Among Us/" dotnet build src/MalumMenu.csproj -c Release
```

too lazy to boot windows to check. im sure you can copy a directory

# how do i contribute?
you don't

# will you update this when among us updates ?
no im lazy
