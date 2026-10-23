namespace Suzumetry.Core;

public enum Game { None = 0, AC = 1, ACC = 2, DR2 = 3 }

public sealed class Snap
{
    public long Seq;
    public Game Game;
    public string Car = "";
    public string Track = "";
    public int Status = 2;
    public int Session = -1;
    public float Throttle, Brake, Clutch, Steer;
    public int Gear, Rpm, MaxRpm;
    public float Speed, Fuel, MaxFuel, FuelPerLap, GLat, GLon;
    public readonly float[] TyreTemp = new float[4];
    public readonly float[] TyrePress = new float[4];
    public readonly float[] BrakeTemp = new float[4];
    public readonly float[] TyreWear = new float[4];
    public int Lap, Position, CurMs, LastMs, BestMs, Sector;
    public float LapPos, TcAct, AbsAct, BrakeBias, AirTemp, RoadTemp, Boost;
    public int TcLvl, AbsLvl, Map, Flag;
    public bool InPit, PitLimiter;
}
