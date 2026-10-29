<div align="center">

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:1a1f2b,100:ff4d6d&height=180&section=header&text=Suzumetry&fontSize=52&fontColor=ffffff&fontAlignY=40&desc=Telemetry%20HUD%20for%20DiRT%20Rally%202.0%2C%20Assetto%20Corsa%20and%20ACC&descSize=16&descAlignY=62" alt="Suzumetry" />

![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Games](https://img.shields.io/badge/Games-DR2%20%C2%B7%20AC%20%C2%B7%20ACC-ff4d6d?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-Early%20Development-orange?style=for-the-badge)
[![License](https://img.shields.io/badge/License-See%20file-blue?style=for-the-badge)](LICENSE.txt)

</div>

---

## About

Suzumetry shows basic telemetry from **DiRT Rally 2.0**, **Assetto Corsa** and **Assetto Corsa Competizione** on a movable HUD overlay, plus a live mouse pad display. It is in a very early stage of development, so expect rough edges and changes.

## Preview

<div align="center">
<img width="734" height="405" alt="image" src="https://github.com/user-attachments/assets/b3440b9d-60e4-4619-8f7b-b69515d5a913" />

<br/><br/>
<img width="1012" height="666" alt="image" src="https://github.com/user-attachments/assets/9edef25e-fa09-477c-a205-b1e6bfd550eb" />

  
<img width="600" alt="Mouse pad settings" src="https://github.com/user-attachments/assets/8fb2e7ab-6ddc-4070-8a29-2ae3a188abba" />
</div>

## Features

- **HUD overlay:** gear, speed, pedals, tyres, lap times, fuel, position, stage progress, G-force meter, steering bar, air and road temperature
- **Live mouse pad:** raw input preview with trail, grid, sensitivity, auto-centering and per-axis inversion
- **Adjustable HUD:** scale, opacity, individually toggled elements, drag to move, mph option
- **Telemetry settings:** read rate, stream rate, game selection, UDP port and packet forwarding (DR2)
- **Local server** for the web dashboard

### Hotkeys

| Keys | Action |
|---|---|
| `Ctrl+Alt+H` | Show or hide the HUD |
| `Ctrl+Alt+L` | Lock or unlock the HUD for moving |
| `Ctrl+Alt+R` | Recenter the mouse pad |

## DiRT Rally 2.0 setup

Telemetry is sent over UDP. Use the **Enable UDP in game config** button in the app, then make sure the port matches (`20777` by default).

## Building

Requirements:

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```
git clone https://github.com/FryingHoshino/Suzumetry.git
cd Suzumetry
dotnet build -c Release
```

The project is WPF and has no external NuGet dependencies. To run it directly: `dotnet run -c Release`.

## License

See [LICENSE.txt](LICENSE.txt).
