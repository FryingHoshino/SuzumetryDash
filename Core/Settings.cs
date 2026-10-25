using System.Text.Json;

namespace Suzumetry.Core;

public sealed class Settings
{
    public int ReadHz { get; set; } = 120;
    public int StreamHz { get; set; } = 60;
    public int Port { get; set; } = 8080;
    public int Dr2Port { get; set; } = 20777;
    public int ForceGame { get; set; } = 0;
    public int Dr2ForwardPort { get; set; } = 0;
    public string Dr2ForwardHost { get; set; } = "127.0.0.1";
    public bool HudEnabled { get; set; } = true;
    public double HudX { get; set; } = 40;
    public double HudY { get; set; } = 40;
    public double HudScale { get; set; } = 1;
    public double HudOpacity { get; set; } = 0.92;
    public bool HudPedals { get; set; } = true;
    public bool HudTyres { get; set; } = true;
    public bool HudLaps { get; set; } = true;
    public bool HudFuel { get; set; } = true;
    public bool HudPos { get; set; } = true;
    public bool HudHeader { get; set; } = true;
    public bool HudProgress { get; set; } = true;
    public bool HudGForce { get; set; } = true;
    public bool HudSteer { get; set; } = true;
    public bool HudWeather { get; set; } = true;
    public bool HudMph { get; set; } = false;
    public int HudFps { get; set; } = 60;
    public bool HudPad { get; set; } = true;
    public int PadSide { get; set; } = 1;
    public int PadSize { get; set; } = 190;
    public int PadRange { get; set; } = 600;
    public double PadSens { get; set; } = 1.0;
    public bool PadInvertX { get; set; } = false;
    public bool PadInvertY { get; set; } = false;
    public int PadCenter { get; set; } = 0;
    public bool PadCenterY { get; set; } = true;
    public bool PadMidReset { get; set; } = false;
    public int PadHz { get; set; } = 240;
    public int PadTrailMs { get; set; } = 800;
    public int PadDot { get; set; } = 6;
    public int PadDivs { get; set; } = 10;
    public bool PadGrid { get; set; } = true;
    public bool PadLabels { get; set; } = true;
    public bool PadCross { get; set; } = true;
    public bool PadRaw { get; set; } = true;
    public bool PadExtra { get; set; } = true;
    public bool PadGame { get; set; } = true;
    public bool PadAlways { get; set; } = false;

    public string PadCardColor { get; set; } = "#0B0D11";
    public double PadCardOpacity { get; set; } = 0.82;
    public string PadCapColor { get; set; } = "#7F8798";
    public double PadCapOpacity { get; set; } = 1;
    public double PadCapSize { get; set; } = 10;
    public string PadBgColor { get; set; } = "#10131A";
    public double PadBgOpacity { get; set; } = 1;
    public string PadGridColor { get; set; } = "#1E2430";
    public double PadGridOpacity { get; set; } = 1;
    public double PadGridThick { get; set; } = 1;
    public string PadAxisColor { get; set; } = "#FF5A7A";
    public double PadAxisOpacity { get; set; } = 1;
    public double PadAxisThick { get; set; } = 1.4;
    public string PadArrowColor { get; set; } = "#FF5A7A";
    public double PadArrowOpacity { get; set; } = 1;
    public double PadArrowSize { get; set; } = 4;
    public string PadNumColor { get; set; } = "#7F8798";
    public double PadNumOpacity { get; set; } = 1;
    public double PadNumSize { get; set; } = 8;
    public string PadLetterColor { get; set; } = "#7F8798";
    public double PadLetterOpacity { get; set; } = 1;
    public double PadLetterSize { get; set; } = 10;
    public string PadCrossColor { get; set; } = "#5A6275";
    public double PadCrossOpacity { get; set; } = 1;
    public double PadCrossThick { get; set; } = 1;
    public string PadTrailColor { get; set; } = "#FF5A7A";
    public double PadTrailOpacity { get; set; } = 1;
    public double PadTrailThick { get; set; } = 2.4;
    public string PadDotColor { get; set; } = "#FF5A7A";
    public double PadDotOpacity { get; set; } = 1;
    public string PadRingColor { get; set; } = "#FFFFFF";
    public double PadRingOpacity { get; set; } = 1;
    public double PadRingThick { get; set; } = 1.2;
    public string PadSteerColor { get; set; } = "#4DA6FF";
    public double PadSteerOpacity { get; set; } = 1;
    public double PadSteerThick { get; set; } = 2;
    public string PadTextColor { get; set; } = "#C9CEDA";
    public double PadTextOpacity { get; set; } = 1;
    public double PadTextSize { get; set; } = 11;
    public string PadExtraColor { get; set; } = "#C9CEDA";
    public double PadExtraOpacity { get; set; } = 1;
    public double PadExtraSize { get; set; } = 11;

    public bool PadCapInfo { get; set; } = true;
    public int PadCapDetail { get; set; } = 2;

    public bool HudExtra { get; set; } = true;
    public string HsBgColor { get; set; } = "#0B0D11";
    public double HsBgOpacity { get; set; } = 0.82;
    public string HsHeadColor { get; set; } = "#7F8798";
    public double HsHeadOpacity { get; set; } = 1;
    public string HsGearColor { get; set; } = "#F2F4F8";
    public double HsGearOpacity { get; set; } = 1;
    public string HsSpeedColor { get; set; } = "#F2F4F8";
    public double HsSpeedOpacity { get; set; } = 1;
    public string HsUnitColor { get; set; } = "#7F8798";
    public double HsUnitOpacity { get; set; } = 1;
    public string HsRpmTextColor { get; set; } = "#7F8798";
    public double HsRpmTextOpacity { get; set; } = 1;
    public string HsRpmTrackColor { get; set; } = "#2A2F3A";
    public double HsRpmTrackOpacity { get; set; } = 1;
    public string HsRpmColor { get; set; } = "#5CFF8A";
    public double HsRpmOpacity { get; set; } = 1;
    public string HsRedlineColor { get; set; } = "#FF5A5A";
    public double HsRedlineOpacity { get; set; } = 1;
    public string HsPosColor { get; set; } = "#F2F4F8";
    public double HsPosOpacity { get; set; } = 1;
    public string HsStateColor { get; set; } = "#FFB020";
    public double HsStateOpacity { get; set; } = 1;
    public string HsSteerTrackColor { get; set; } = "#2A2F3A";
    public double HsSteerTrackOpacity { get; set; } = 1;
    public string HsSteerCenterColor { get; set; } = "#7F8798";
    public double HsSteerCenterOpacity { get; set; } = 1;
    public string HsSteerMarkColor { get; set; } = "#F2F4F8";
    public double HsSteerMarkOpacity { get; set; } = 1;
    public string HsProgTrackColor { get; set; } = "#2A2F3A";
    public double HsProgTrackOpacity { get; set; } = 1;
    public string HsProgColor { get; set; } = "#FF5A7A";
    public double HsProgOpacity { get; set; } = 1;
    public string HsPedalTrackColor { get; set; } = "#2A2F3A";
    public double HsPedalTrackOpacity { get; set; } = 1;
    public string HsCltColor { get; set; } = "#4DA6FF";
    public double HsCltOpacity { get; set; } = 1;
    public string HsBrkColor { get; set; } = "#FF5A5A";
    public double HsBrkOpacity { get; set; } = 1;
    public string HsThrColor { get; set; } = "#5CFF8A";
    public double HsThrOpacity { get; set; } = 1;
    public string HsTempCapColor { get; set; } = "#7F8798";
    public double HsTempCapOpacity { get; set; } = 1;
    public string HsColdColor { get; set; } = "#4DA6FF";
    public double HsColdOpacity { get; set; } = 1;
    public string HsGoodColor { get; set; } = "#5CFF8A";
    public double HsGoodOpacity { get; set; } = 1;
    public string HsHotColor { get; set; } = "#FF5A5A";
    public double HsHotOpacity { get; set; } = 1;
    public string HsIdleColor { get; set; } = "#7F8798";
    public double HsIdleOpacity { get; set; } = 1;
    public string HsBrakeBgColor { get; set; } = "#2A2F3A";
    public double HsBrakeBgOpacity { get; set; } = 0.9;
    public string HsTyreTextColor { get; set; } = "#0B0D11";
    public double HsTyreTextOpacity { get; set; } = 1;
    public string HsGRingColor { get; set; } = "#2A2F3A";
    public double HsGRingOpacity { get; set; } = 1;
    public string HsGDotColor { get; set; } = "#FF5A7A";
    public double HsGDotOpacity { get; set; } = 1;
    public string HsCurColor { get; set; } = "#F2F4F8";
    public double HsCurOpacity { get; set; } = 1;
    public string HsLastColor { get; set; } = "#7F8798";
    public double HsLastOpacity { get; set; } = 1;
    public string HsBestColor { get; set; } = "#FF5A7A";
    public double HsBestOpacity { get; set; } = 1;
    public string HsInfoColor { get; set; } = "#7F8798";
    public double HsInfoOpacity { get; set; } = 1;
    public string HsExtraTextColor { get; set; } = "#C9CEDA";
    public double HsExtraTextOpacity { get; set; } = 1;
    public double HsTyreBgOpacity { get; set; } = 0.9;
    public double HsTyreCold { get; set; } = 60;
    public double HsTyreHot { get; set; } = 105;
    public double HsBrakeCold { get; set; } = 150;
    public double HsBrakeHot { get; set; } = 550;
    public double HsExtraSize { get; set; } = 11;

    public int PadSource { get; set; } = 0;
    public int PadDevice { get; set; } = 0;
    public int PadAxisX { get; set; } = 0;
    public int PadAxisY { get; set; } = 1;
    public double PadDeadzone { get; set; } = 0.04;

    public bool HudShift { get; set; } = true;
    public bool HudWarn { get; set; } = true;
    public bool PresetAuto { get; set; } = false;
    public Dictionary<string, string> PresetMap { get; set; } = new();
    public string HsShiftOffColor { get; set; } = "#2A2F3A";
    public double HsShiftOffOpacity { get; set; } = 1;
    public string HsShiftLowColor { get; set; } = "#5CFF8A";
    public double HsShiftLowOpacity { get; set; } = 1;
    public string HsShiftMidColor { get; set; } = "#FFD21F";
    public double HsShiftMidOpacity { get; set; } = 1;
    public string HsShiftHighColor { get; set; } = "#FF5A5A";
    public double HsShiftHighOpacity { get; set; } = 1;
    public double HsShiftCount { get; set; } = 12;
    public double HsShiftHeight { get; set; } = 10;
    public double HsShiftStart { get; set; } = 0.6;
    public double HsShiftFlash { get; set; } = 0.97;
    public double HsBrakeWarn { get; set; } = 700;
    public double HsTyreWarn { get; set; } = 125;
    public double HsFuelWarn { get; set; } = 5;

    static readonly Settings Def = new();

    static readonly object gate = new();
    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Suzumetry");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                s.Clamp();
                return s;
            }
        }
        catch (Exception e) { Log.Write(e); }
        return new Settings();
    }

    public void Clamp()
    {
        ReadHz = Math.Clamp(ReadHz, 10, 500);
        StreamHz = Math.Clamp(StreamHz, 1, 240);
        Port = Math.Clamp(Port, 1024, 65535);
        Dr2Port = Math.Clamp(Dr2Port, 1024, 65535);
        ForceGame = Math.Clamp(ForceGame, 0, 3);
        Dr2ForwardPort = Math.Clamp(Dr2ForwardPort, 0, 65535);
        if (string.IsNullOrWhiteSpace(Dr2ForwardHost)) Dr2ForwardHost = "127.0.0.1";
        HudScale = Math.Clamp(HudScale, 0.4, 3);
        HudOpacity = Math.Clamp(HudOpacity, 0.2, 1);
        HudFps = Math.Clamp(HudFps, 10, 240);
        PadSide = Math.Clamp(PadSide, 0, 3);
        PadSize = Math.Clamp(PadSize, 120, 360);
        PadRange = Math.Clamp(PadRange, 100, 5000);
        PadSens = Math.Clamp(PadSens, 0.05, 10);
        PadCenter = Math.Clamp(PadCenter, 0, 30);
        PadHz = Math.Clamp(PadHz, 60, 1000);
        PadTrailMs = Math.Clamp(PadTrailMs, 0, 4000);
        PadDot = Math.Clamp(PadDot, 2, 16);
        PadDivs = Math.Clamp(PadDivs, 2, 20);

        PadCardColor = Col(PadCardColor, Def.PadCardColor);
        PadCapColor = Col(PadCapColor, Def.PadCapColor);
        PadBgColor = Col(PadBgColor, Def.PadBgColor);
        PadGridColor = Col(PadGridColor, Def.PadGridColor);
        PadAxisColor = Col(PadAxisColor, Def.PadAxisColor);
        PadArrowColor = Col(PadArrowColor, Def.PadArrowColor);
        PadNumColor = Col(PadNumColor, Def.PadNumColor);
        PadLetterColor = Col(PadLetterColor, Def.PadLetterColor);
        PadCrossColor = Col(PadCrossColor, Def.PadCrossColor);
        PadTrailColor = Col(PadTrailColor, Def.PadTrailColor);
        PadDotColor = Col(PadDotColor, Def.PadDotColor);
        PadRingColor = Col(PadRingColor, Def.PadRingColor);
        PadSteerColor = Col(PadSteerColor, Def.PadSteerColor);
        PadTextColor = Col(PadTextColor, Def.PadTextColor);
        PadExtraColor = Col(PadExtraColor, Def.PadExtraColor);
        PadCardOpacity = Math.Clamp(PadCardOpacity, 0, 1);
        PadCapOpacity = Math.Clamp(PadCapOpacity, 0, 1);
        PadBgOpacity = Math.Clamp(PadBgOpacity, 0, 1);
        PadGridOpacity = Math.Clamp(PadGridOpacity, 0, 1);
        PadAxisOpacity = Math.Clamp(PadAxisOpacity, 0, 1);
        PadArrowOpacity = Math.Clamp(PadArrowOpacity, 0, 1);
        PadNumOpacity = Math.Clamp(PadNumOpacity, 0, 1);
        PadLetterOpacity = Math.Clamp(PadLetterOpacity, 0, 1);
        PadCrossOpacity = Math.Clamp(PadCrossOpacity, 0, 1);
        PadTrailOpacity = Math.Clamp(PadTrailOpacity, 0, 1);
        PadDotOpacity = Math.Clamp(PadDotOpacity, 0, 1);
        PadRingOpacity = Math.Clamp(PadRingOpacity, 0, 1);
        PadSteerOpacity = Math.Clamp(PadSteerOpacity, 0, 1);
        PadTextOpacity = Math.Clamp(PadTextOpacity, 0, 1);
        PadExtraOpacity = Math.Clamp(PadExtraOpacity, 0, 1);
        PadGridThick = Math.Clamp(PadGridThick, 0.5, 6);
        PadAxisThick = Math.Clamp(PadAxisThick, 0.5, 6);
        PadCrossThick = Math.Clamp(PadCrossThick, 0.5, 6);
        PadTrailThick = Math.Clamp(PadTrailThick, 0.5, 10);
        PadRingThick = Math.Clamp(PadRingThick, 0.5, 6);
        PadSteerThick = Math.Clamp(PadSteerThick, 0.5, 8);
        PadArrowSize = Math.Clamp(PadArrowSize, 2, 14);
        PadCapSize = Math.Clamp(PadCapSize, 6, 24);
        PadNumSize = Math.Clamp(PadNumSize, 6, 20);
        PadLetterSize = Math.Clamp(PadLetterSize, 6, 24);
        PadTextSize = Math.Clamp(PadTextSize, 8, 28);
        PadExtraSize = Math.Clamp(PadExtraSize, 8, 28);

        PadCapDetail = Math.Clamp(PadCapDetail, 0, 2);
        HsBgColor = Col(HsBgColor, Def.HsBgColor);
        HsBgOpacity = Math.Clamp(HsBgOpacity, 0, 1);
        HsHeadColor = Col(HsHeadColor, Def.HsHeadColor);
        HsHeadOpacity = Math.Clamp(HsHeadOpacity, 0, 1);
        HsGearColor = Col(HsGearColor, Def.HsGearColor);
        HsGearOpacity = Math.Clamp(HsGearOpacity, 0, 1);
        HsSpeedColor = Col(HsSpeedColor, Def.HsSpeedColor);
        HsSpeedOpacity = Math.Clamp(HsSpeedOpacity, 0, 1);
        HsUnitColor = Col(HsUnitColor, Def.HsUnitColor);
        HsUnitOpacity = Math.Clamp(HsUnitOpacity, 0, 1);
        HsRpmTextColor = Col(HsRpmTextColor, Def.HsRpmTextColor);
        HsRpmTextOpacity = Math.Clamp(HsRpmTextOpacity, 0, 1);
        HsRpmTrackColor = Col(HsRpmTrackColor, Def.HsRpmTrackColor);
        HsRpmTrackOpacity = Math.Clamp(HsRpmTrackOpacity, 0, 1);
        HsRpmColor = Col(HsRpmColor, Def.HsRpmColor);
        HsRpmOpacity = Math.Clamp(HsRpmOpacity, 0, 1);
        HsRedlineColor = Col(HsRedlineColor, Def.HsRedlineColor);
        HsRedlineOpacity = Math.Clamp(HsRedlineOpacity, 0, 1);
        HsPosColor = Col(HsPosColor, Def.HsPosColor);
        HsPosOpacity = Math.Clamp(HsPosOpacity, 0, 1);
        HsStateColor = Col(HsStateColor, Def.HsStateColor);
        HsStateOpacity = Math.Clamp(HsStateOpacity, 0, 1);
        HsSteerTrackColor = Col(HsSteerTrackColor, Def.HsSteerTrackColor);
        HsSteerTrackOpacity = Math.Clamp(HsSteerTrackOpacity, 0, 1);
        HsSteerCenterColor = Col(HsSteerCenterColor, Def.HsSteerCenterColor);
        HsSteerCenterOpacity = Math.Clamp(HsSteerCenterOpacity, 0, 1);
        HsSteerMarkColor = Col(HsSteerMarkColor, Def.HsSteerMarkColor);
        HsSteerMarkOpacity = Math.Clamp(HsSteerMarkOpacity, 0, 1);
        HsProgTrackColor = Col(HsProgTrackColor, Def.HsProgTrackColor);
        HsProgTrackOpacity = Math.Clamp(HsProgTrackOpacity, 0, 1);
        HsProgColor = Col(HsProgColor, Def.HsProgColor);
        HsProgOpacity = Math.Clamp(HsProgOpacity, 0, 1);
        HsPedalTrackColor = Col(HsPedalTrackColor, Def.HsPedalTrackColor);
        HsPedalTrackOpacity = Math.Clamp(HsPedalTrackOpacity, 0, 1);
        HsCltColor = Col(HsCltColor, Def.HsCltColor);
        HsCltOpacity = Math.Clamp(HsCltOpacity, 0, 1);
        HsBrkColor = Col(HsBrkColor, Def.HsBrkColor);
        HsBrkOpacity = Math.Clamp(HsBrkOpacity, 0, 1);
        HsThrColor = Col(HsThrColor, Def.HsThrColor);
        HsThrOpacity = Math.Clamp(HsThrOpacity, 0, 1);
        HsTempCapColor = Col(HsTempCapColor, Def.HsTempCapColor);
        HsTempCapOpacity = Math.Clamp(HsTempCapOpacity, 0, 1);
        HsColdColor = Col(HsColdColor, Def.HsColdColor);
        HsColdOpacity = Math.Clamp(HsColdOpacity, 0, 1);
        HsGoodColor = Col(HsGoodColor, Def.HsGoodColor);
        HsGoodOpacity = Math.Clamp(HsGoodOpacity, 0, 1);
        HsHotColor = Col(HsHotColor, Def.HsHotColor);
        HsHotOpacity = Math.Clamp(HsHotOpacity, 0, 1);
        HsIdleColor = Col(HsIdleColor, Def.HsIdleColor);
        HsIdleOpacity = Math.Clamp(HsIdleOpacity, 0, 1);
        HsBrakeBgColor = Col(HsBrakeBgColor, Def.HsBrakeBgColor);
        HsBrakeBgOpacity = Math.Clamp(HsBrakeBgOpacity, 0, 1);
        HsTyreTextColor = Col(HsTyreTextColor, Def.HsTyreTextColor);
        HsTyreTextOpacity = Math.Clamp(HsTyreTextOpacity, 0, 1);
        HsGRingColor = Col(HsGRingColor, Def.HsGRingColor);
        HsGRingOpacity = Math.Clamp(HsGRingOpacity, 0, 1);
        HsGDotColor = Col(HsGDotColor, Def.HsGDotColor);
        HsGDotOpacity = Math.Clamp(HsGDotOpacity, 0, 1);
        HsCurColor = Col(HsCurColor, Def.HsCurColor);
        HsCurOpacity = Math.Clamp(HsCurOpacity, 0, 1);
        HsLastColor = Col(HsLastColor, Def.HsLastColor);
        HsLastOpacity = Math.Clamp(HsLastOpacity, 0, 1);
        HsBestColor = Col(HsBestColor, Def.HsBestColor);
        HsBestOpacity = Math.Clamp(HsBestOpacity, 0, 1);
        HsInfoColor = Col(HsInfoColor, Def.HsInfoColor);
        HsInfoOpacity = Math.Clamp(HsInfoOpacity, 0, 1);
        HsExtraTextColor = Col(HsExtraTextColor, Def.HsExtraTextColor);
        HsExtraTextOpacity = Math.Clamp(HsExtraTextOpacity, 0, 1);
        HsTyreBgOpacity = Math.Clamp(HsTyreBgOpacity, 0, 1);
        HsTyreCold = Math.Clamp(HsTyreCold, 0, 600);
        HsTyreHot = Math.Clamp(HsTyreHot, 0, 600);
        HsBrakeCold = Math.Clamp(HsBrakeCold, 0, 1500);
        HsBrakeHot = Math.Clamp(HsBrakeHot, 0, 1500);
        HsExtraSize = Math.Clamp(HsExtraSize, 8, 24);

        PadSource = Math.Clamp(PadSource, 0, 2);
        PadDevice = Math.Clamp(PadDevice, 0, 15);
        PadAxisX = Math.Clamp(PadAxisX, 0, 6);
        PadAxisY = Math.Clamp(PadAxisY, 0, 6);
        PadDeadzone = Math.Clamp(PadDeadzone, 0, 0.4);
        PresetMap ??= new();
        HsShiftOffColor = Col(HsShiftOffColor, Def.HsShiftOffColor);
        HsShiftOffOpacity = Math.Clamp(HsShiftOffOpacity, 0, 1);
        HsShiftLowColor = Col(HsShiftLowColor, Def.HsShiftLowColor);
        HsShiftLowOpacity = Math.Clamp(HsShiftLowOpacity, 0, 1);
        HsShiftMidColor = Col(HsShiftMidColor, Def.HsShiftMidColor);
        HsShiftMidOpacity = Math.Clamp(HsShiftMidOpacity, 0, 1);
        HsShiftHighColor = Col(HsShiftHighColor, Def.HsShiftHighColor);
        HsShiftHighOpacity = Math.Clamp(HsShiftHighOpacity, 0, 1);
        HsShiftCount = Math.Clamp(HsShiftCount, 4, 24);
        HsShiftHeight = Math.Clamp(HsShiftHeight, 4, 30);
        HsShiftStart = Math.Clamp(HsShiftStart, 0.3, 0.95);
        HsShiftFlash = Math.Clamp(HsShiftFlash, 0.8, 1);
        HsBrakeWarn = Math.Clamp(HsBrakeWarn, 200, 1500);
        HsTyreWarn = Math.Clamp(HsTyreWarn, 60, 250);
        HsFuelWarn = Math.Clamp(HsFuelWarn, 0, 40);
    }

    static string Col(string? v, string d) => PadStyle.TryParse(v, out var c) ? PadStyle.Hex(c) : d;

    public void ResetPad()
    {
        var d = new Settings();
        foreach (var p in typeof(Settings).GetProperties())
            if (p.Name.StartsWith("Pad") && p.CanWrite) p.SetValue(this, p.GetValue(d));
    }

    public void ResetHudStyle()
    {
        var d = new Settings();
        foreach (var p in typeof(Settings).GetProperties())
            if (p.Name.StartsWith("Hs") && p.CanWrite) p.SetValue(this, p.GetValue(d));
        HudExtra = true;
        HudShift = true;
        HudWarn = true;
    }

    public void Save()
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Dir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, true);
            }
        }
        catch (Exception e) { Log.Write(e); }
    }
}
