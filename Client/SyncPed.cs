using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using Font = GTA.Font;

namespace GTACoOp
{
    public enum TrafficMode
    {
        None,
        Parked,
        All,
    }

    public class SyncPed
    {
        public long Host;
        public MaxMind.GeoIP2.Responses.CountryResponse geoIP;
        public Ped Character;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 VehicleVelocity;
        public bool IsInVehicle;
        public bool IsJumping;
        public int ModelHash;
        public int CurrentWeapon;
        public bool IsShooting;
        public bool IsAiming;
        public Vector3 AimCoords;
        public float Latency;
        public bool IsHornPressed;
        public Vehicle MainVehicle { get; set; }

        public int VehicleSeat;
        public int PedHealth;

        public int VehicleHealth;
        public int VehicleHash;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public string Name;
        public bool Siren;
        public bool IsEngineRunning;
        public bool IsInBurnout;
        public bool HighBeamsOn;
        public bool LightsOn;
        public VehicleLandingGear LandingGear;
        public int Livery;

        public float Steering;
        public float WheelSpeed;
        public string Plate;
        public int RadioStation;
        private DateTime _stopTime;
        private bool _lastBurnout;

        public float Speed
        {
            get { return _speed; }
            set
            {
                _lastSpeed = _speed;
                _speed = value;
            }
        }

        public bool IsParachuteOpen;

        public double AverageLatency
        {
            get { return _latencyAverager.Average(); }
        }

        public int LastUpdateReceived
        {
            get { return _lastUpdateReceived; }
            set
            {
                if (_lastUpdateReceived != 0)
                {
                    _latencyAverager.Enqueue(value - _lastUpdateReceived);
                    if (_latencyAverager.Count >= 10)
                        _latencyAverager.Dequeue();
                }

                _lastUpdateReceived = value;
            }
        }

        public int TicksSinceLastUpdate
        {
            get { return Environment.TickCount - LastUpdateReceived; }
        }

        public Dictionary<int, int> VehicleMods
        {
            get { return _vehicleMods; }
            set
            {
                if (value == null) return;
                _vehicleMods = value;
            }
        }

        public Dictionary<int, int> PedProps
        {
            get { return _pedProps; }
            set
            {
                if (value == null) return;
                _pedProps = value;
            }
        }

        private Vector3 _carPosOnUpdate;
        private Vector3 _lastVehiclePos;
        public Vector3 VehiclePosition
        {
            get { return _vehiclePosition; }
            set
            {
                _lastVehiclePos = _vehiclePosition;
                _vehiclePosition = value;
            }
        }

        private Vector3? _lastVehicleRotation;
        public Vector3 VehicleRotation
        {
            get { return _vehicleRotation; }
            set
            {
                _lastVehicleRotation = _vehicleRotation;
                _vehicleRotation = value;
            }
        }

        private bool _lastVehicle;
        private uint _switch;
        private bool _lastAiming;
        private float _lastSpeed;
        private DateTime _secondToLastUpdateReceived;
        private bool _lastShooting;
        private bool _lastJumping;
        private bool _blip;
        private bool _justEnteredVeh;
        private DateTime _lastHornPress = DateTime.Now;
        private int _relGroup;
        private DateTime _enterVehicleStarted;
        private Vector3 _vehiclePosition;
        private Vector3 _vehicleRotation;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        private Queue<double> _latencyAverager;

        private int _playerSeat;
        private bool _isStreamedIn;
        private Blip _mainBlip;
        private bool _lastHorn;
        private Prop _parachuteProp;

        public SyncPed(int hash, Vector3 pos, Vector3 rot, bool blip = true)
        {
            Position = pos;
            Rotation = rot;
            ModelHash = hash;
            _blip = blip;

            _latencyAverager = new Queue<double>();

            _relGroup = World.AddRelationshipGroup("SYNCPED");
            World.SetRelationshipBetweenGroups(Relationship.Neutral, _relGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Neutral, Game.Player.Character.RelationshipGroup, _relGroup);

            StartInterpolation();
        }

        public void SetBlipName(Blip blip, string text)
        {
            Function.Call(Hash._0xF9113A30DE5C6670, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._0xBC38B49BCB83BC9B, blip);
        }

        private int _modSwitch = 0;
        private int _clothSwitch = 0;
        private int _lastUpdateReceived;
        private float _speed;

        public void DisplayLocally()
        {
            try
            {
                var isPlane = Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, VehicleHash);
                float hRange = isPlane ? 1200f : 400f;

                var gPos = IsInVehicle ? VehiclePosition : Position;
                var inRange = isPlane ? true : Game.Player.Character.IsInRangeOf(gPos, hRange);

                if (inRange && !_isStreamedIn)
                {
                    _isStreamedIn = true;
                    if (_mainBlip != null)
                    {
                        _mainBlip.Remove();
                        _mainBlip = null;
                    }
                }
                else if(!inRange && _isStreamedIn)
                {
                    Clear();
                    _isStreamedIn = false;
                }

                if (!inRange)
                {
                    if (_mainBlip == null && _blip)
                    {
                        _mainBlip = World.CreateBlip(gPos);
                        _mainBlip.Color = BlipColor.White;
                        _mainBlip.Scale = 0.8f;
                        SetBlipName(_mainBlip, Name == null ? "<nameless>" : Name);
                    }
                    if(_blip && _mainBlip != null)
                        _mainBlip.Position = gPos;
                    return;
                }


                if (Character == null || !Character.Exists() || (!Character.IsInRangeOf(gPos, hRange) && Environment.TickCount - LastUpdateReceived < 5000) || Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
                {
                    if (Character != null) Character.Delete();

                    Character = World.CreatePed(new Model(ModelHash), gPos, Rotation.Z);
                    if (Character == null) return;

                    Character.BlockPermanentEvents = true;
                    Character.IsInvincible = true;
                    Character.CanRagdoll = false;
                    Character.RelationshipGroup = _relGroup;
                    if (_blip)
                    {
                        Character.AddBlip();
                        if (Character.CurrentBlip == null) return;
                        Character.CurrentBlip.Color = BlipColor.White;
                        Character.CurrentBlip.Scale = 0.8f;
                        SetBlipName(Character.CurrentBlip, Name);
                    }

                    if (PedProps != null)
                        foreach (KeyValuePair<int, int> pedprop in PedProps)
                            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, pedprop.Key, pedprop.Value, 0, 0);

                    return;
                }

                if (!Character.IsOccluded && Character.IsInRangeOf(Game.Player.Character.Position, 20f))
                {
                    Vector3 targetPos = Character.GetBoneCoord(Bone.IK_Head) + new Vector3(0, 0, 0.5f);

                    targetPos += Character.Velocity / Game.FPS;

                    Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);

                    float sizeOffset = Math.Max(1f - ((GameplayCamera.Position - Character.Position).Length() / 30f), 0.3f);

                    new UIResText(Name ?? "<Nameless>", new Point(0, 0), 0.4f * sizeOffset, Color.WhiteSmoke, Font.ChaletLondon, UIResText.Alignment.Centered)
                    {
                        Outline = true,
                    }.Draw();

                    Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                }

                if ((!_lastVehicle && IsInVehicle && VehicleHash != 0) || (_lastVehicle && IsInVehicle && (MainVehicle == null || !Character.IsInVehicle(MainVehicle) || MainVehicle.Model.Hash != VehicleHash || VehicleSeat != Util.GetPedSeat(Character))))
                {
                    if (MainVehicle != null && Util.IsVehicleEmpty(MainVehicle))
                        MainVehicle.Delete();

                    var vehs = World.GetAllVehicles().OrderBy(v =>
                    {
                        if (v == null) return float.MaxValue;
                        return (v.Position - Character.Position).Length();
                    }).ToList();


                    if (vehs.Any() && vehs[0].Model.Hash == VehicleHash && vehs[0].IsInRangeOf(gPos, 3f))
                    {
                        MainVehicle = vehs[0];
                        if (Game.Player.Character.IsInVehicle(MainVehicle) &&
                            VehicleSeat == Util.GetPedSeat(Game.Player.Character))
                        {
                            Game.Player.Character.Task.WarpOutOfVehicle(MainVehicle);
                            UI.Notify("~r~Car jacked!");
                        }
                    }
                    else
                    {
                        MainVehicle = World.CreateVehicle(new Model(VehicleHash), gPos, 0);
                    }

                    if (MainVehicle != null)
                    {
                        Function.Call(Hash.SET_VEHICLE_COLOURS, MainVehicle, VehiclePrimaryColor, VehicleSecondaryColor);
                        MainVehicle.Livery = Livery;

                        MainVehicle.Quaternion = VehicleRotation.ToQuaternion();
                        MainVehicle.IsInvincible = true;
                        Character.Task.WarpIntoVehicle(MainVehicle, (VehicleSeat)VehicleSeat);

                        /*if (_playerSeat != -2 && !Game.Player.Character.IsInVehicle(_mainVehicle))
                        { // TODO: Fix me.
                            Game.Player.Character.Task.WarpIntoVehicle(_mainVehicle, (VehicleSeat)_playerSeat);
                        }*/
                    }

                    _lastVehicle = true;
                    _justEnteredVeh = true;
                    _enterVehicleStarted = DateTime.Now;
                    return;
                }

                if (_lastVehicle && _justEnteredVeh && IsInVehicle && !Character.IsInVehicle(MainVehicle) && DateTime.Now.Subtract(_enterVehicleStarted).TotalSeconds <= 4)
                {
                    return;
                }
                _justEnteredVeh = false;

                if (_lastVehicle && !IsInVehicle && MainVehicle != null)
                {
                    if (Character != null) Character.Task.LeaveVehicle(MainVehicle, true);
                }

                Character.Health = PedHealth;

                _switch++;

                if (!inRange)
                {
                    if (Character != null && Environment.TickCount - LastUpdateReceived < 10000)
                    {
                        if (!IsInVehicle)
                        {
                            Character.PositionNoOffset = gPos;
                        }
                        else if (MainVehicle != null && GetResponsiblePed(MainVehicle).Handle == Character.Handle)
                        {
                            MainVehicle.Position = VehiclePosition;
                            MainVehicle.Rotation = VehicleRotation;
                        }
                    }
                    return;
                }

                if (IsInVehicle)
                {
                    if (GetResponsiblePed(MainVehicle).Handle == Character.Handle)
                    {
                        MainVehicle.Health = VehicleHealth;
                        if (MainVehicle.Health <= 0)
                        {
                            MainVehicle.IsInvincible = false;
                            //_mainVehicle.Explode();
                        }
                        else
                        {
                            MainVehicle.IsInvincible = true;
                            if (MainVehicle.IsDead)
                                MainVehicle.Repair();
                        }

                        MainVehicle.EngineRunning = IsEngineRunning;

                        if (Plate != null)
                        {
                            MainVehicle.NumberPlate = Plate;
                        }

                        var radioStations = Util.GetRadioStations();

                        if (radioStations?.ElementAtOrDefault(RadioStation) != null)
                        {
                            Function.Call(Hash.SET_VEH_RADIO_STATION, radioStations[RadioStation]);
                        }

                        if (VehicleMods != null && _modSwitch%50 == 0 &&
                            Game.Player.Character.IsInRangeOf(VehiclePosition, 30f))
                        {
                            var id = _modSwitch/50;

                            if (VehicleMods.ContainsKey(id) && VehicleMods[id] != MainVehicle.GetMod((VehicleMod) id))
                            {
                                Function.Call(Hash.SET_VEHICLE_MOD_KIT, MainVehicle.Handle, 0);
                                MainVehicle.SetMod((VehicleMod) id, VehicleMods[id], false);
                                Function.Call(Hash.RELEASE_PRELOAD_MODS, id);
                            }
                        }
                        _modSwitch++;

                        if (_modSwitch >= 2500)
                            _modSwitch = 0;

                        if (IsHornPressed && !_lastHorn)
                        {
                            _lastHorn = true;
                            MainVehicle.SoundHorn(99999);
                        }

                        if (!IsHornPressed && _lastHorn)
                        {
                            _lastHorn = false;
                            MainVehicle.SoundHorn(1);
                        }

                        if (IsInBurnout && !_lastBurnout)
                        {
                            Function.Call(Hash.SET_VEHICLE_BURNOUT, MainVehicle, true);
                            Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Character, MainVehicle, 23, 120000); // 30 - burnout
                        }
                        else if (!IsInBurnout && _lastBurnout)
                        {
                            Function.Call(Hash.SET_VEHICLE_BURNOUT, MainVehicle, false);
                            Character.Task.ClearAll();
                        }

                        _lastBurnout = IsInBurnout;

                        Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, MainVehicle, Speed > 0.2 && _lastSpeed > Speed);

                        if (MainVehicle.SirenActive && !Siren)
                            MainVehicle.SirenActive = Siren;
                        else if (!MainVehicle.SirenActive && Siren)
                            MainVehicle.SirenActive = Siren;

                        MainVehicle.LightsOn = LightsOn;
                        MainVehicle.HighBeamsOn = HighBeamsOn;
                        MainVehicle.SirenActive = Siren;
                        MainVehicle.SteeringAngle = (Steering > 5f || Steering < -5f) ? Steering : 0f;
                        Function.Call(Hash.SET_VEHICLE_LIVERY, MainVehicle, Livery);

                        Function.Call(Hash.SET_VEHICLE_COLOURS, MainVehicle, VehiclePrimaryColor, VehicleSecondaryColor);

                        if (MainVehicle.Model.IsPlane && LandingGear != MainVehicle.LandingGear)
                        {
                            MainVehicle.LandingGear = LandingGear;
                        }

                        if (Character.IsOnBike && MainVehicle.ClassType == VehicleClass.Cycles)
                        {
                            var isPedaling = IsPedaling(false);
                            var isFastPedaling = IsPedaling(true);
                            if (Speed < 2f)
                            {
                                if (isPedaling)
                                    StopPedalingAnim(false);
                                else if (isFastPedaling)
                                    StopPedalingAnim(true);
                            }
                            else if (Speed < 11f && !isPedaling)
                                StartPedalingAnim(false);
                            else if (Speed >= 11f && !isFastPedaling)
                                StartPedalingAnim(true);
                        }

                        if (Speed > 0.2f || IsInBurnout)
                        {
                            int currentTime = Environment.TickCount;
                            float alpha = Util.Unlerp(currentInterop.StartTime, currentTime, currentInterop.FinishTime);

                            alpha = Util.Clamp(0f, alpha, 1.5f);

                            float cAlpha = alpha - currentInterop.LastAlpha;
                            currentInterop.LastAlpha = alpha;

                            Vector3 comp = Util.Lerp(new Vector3(), cAlpha, currentInterop.vecError);

                            if (alpha == 1.5f)
                            {
                                currentInterop.FinishTime = 0;
                            }

                            Vector3 newPos = VehiclePosition + comp;

                            MainVehicle.Velocity = VehicleVelocity + (newPos - MainVehicle.Position);

                            _stopTime = DateTime.Now;
                            _carPosOnUpdate = MainVehicle.Position;
                        }
                        else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
                        {
                            var dir = VehiclePosition - _lastVehiclePos;
                            var posTarget = Util.LinearVectorLerp(_carPosOnUpdate, VehiclePosition + dir,
                                (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                                posTarget.Z, 0, 0, 0, 0);
                        }
                        else
                        {
                            MainVehicle.PositionNoOffset = VehiclePosition;
                        }

                        if (_lastVehicleRotation != null)
                        {
                            MainVehicle.Quaternion = Quaternion.Slerp(_lastVehicleRotation.Value.ToQuaternion(),
                                _vehicleRotation.ToQuaternion(),
                                Math.Min(1.5f, TicksSinceLastUpdate / (float)AverageLatency));
                        }
                        else
                        {
                            MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
                        }
                    }
                }
                else
                {
                    if (PedProps != null && _clothSwitch%50 == 0 && Game.Player.Character.IsInRangeOf(Position, 30f))
                    {
                        var id = _clothSwitch/50;

                        if (PedProps.ContainsKey(id) &&
                            PedProps[id] != Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Character.Handle, id))
                        {
                            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, id, PedProps[id], 0, 0);
                        }
                    }

                    _clothSwitch++;
                    if (_clothSwitch >= 750)
                        _clothSwitch = 0;

                    if (Character.Weapons.Current.Hash != (WeaponHash) CurrentWeapon)
                    {
                        var wep = Character.Weapons.Give((WeaponHash) CurrentWeapon, 9999, true, true);
                        Character.Weapons.Select(wep);
                    }

                    if (!_lastJumping && IsJumping)
                    {
                        Character.Task.Jump();
                    }

                    if (IsParachuteOpen)
                    {
                        if (_parachuteProp == null)
                        {
                            _parachuteProp = World.CreateProp(new Model(1740193300), Character.Position,
                                Character.Rotation, false, false);
                            _parachuteProp.FreezePosition = true;
                            Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);
                        }
                        Character.FreezePosition = true;
                        Character.Position = Position - new Vector3(0, 0, 1);
                        Character.Quaternion = Rotation.ToQuaternion();
                        _parachuteProp.Position = Character.Position + new Vector3(0, 0, 3.7f);
                        _parachuteProp.Quaternion = Character.Quaternion;

                        Character.Task.PlayAnimation("skydive@parachute@first_person", "chute_idle_right", 8f, 5000,
                            false, 8f);
                    }
                    else
                    {
                        var dest = Position;
                        Character.FreezePosition = false;

                        if (_parachuteProp != null)
                        {
                            _parachuteProp.Delete();
                            _parachuteProp = null;
                        }

                        const int threshold = 50;
                        if (IsAiming && !IsShooting && !Character.IsInRangeOf(Position, 0.5f) && _switch%threshold == 0)
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                                dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0,
                                (uint) FiringPattern.FullAuto);
                        }
                        else if (IsAiming && !IsShooting && Character.IsInRangeOf(Position, 0.5f))
                        {
                            Character.Task.AimAt(AimCoords, 100);
                        }

                        if (!Character.IsInRangeOf(Position, 0.5f) &&
                            ((IsShooting && !_lastShooting) ||
                             (IsShooting && _lastShooting && _switch%(threshold*2) == 0)))
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                                dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 1, 0x3F000000, 0x40800000, 1, 0, 0,
                                (uint) FiringPattern.FullAuto);
                        }
                        else if ((IsShooting && !_lastShooting) ||
                                 (IsShooting && _lastShooting && _switch%(threshold/2) == 0))
                        {
                            Function.Call(Hash.TASK_SHOOT_AT_COORD, Character.Handle, AimCoords.X, AimCoords.Y,
                                AimCoords.Z, 1500, (uint) FiringPattern.FullAuto);
                        }

                        if (!IsAiming && !IsShooting && !IsJumping)
                        {
                            float distance = Character.Position.DistanceTo(Position);
                            if (distance <= 0.15f || distance > 7.0f) // Still or to far away
                            {
                                Character.Position = dest - new Vector3(0, 0, 1f);
                                Character.Quaternion = Rotation.ToQuaternion();
                            }
                            else if (distance <= 1.25f) // Walking
                            {
                                Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character, Position.X, Position.Y, Position.Z, 1.0f, -1, Character.Heading, 0.0f);
                                Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 1.0f);
                            }
                            else if (distance > 1.75f) // Sprinting
                            {
                                Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character, Position.X, Position.Y, Position.Z, 3.0f, -1, Character.Heading, 2.0f);
                                Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Character, 1.49f);
                                Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 3.0f);
                            }
                            else // Running
                            {
                                Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character, Position.X, Position.Y, Position.Z, 4.0f, -1, Character.Heading, 1.0f);
                                Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 2.0f);
                            }
                        }
                    }
                    _lastJumping = IsJumping;
                    _lastShooting = IsShooting;
                    _lastAiming = IsAiming;
                }
                _lastVehicle = IsInVehicle;
            }
            catch (Exception ex)
            {
                UI.Notify("Sync error: " + ex.Message);
                Main.Logger.WriteException("Exception in SyncPed code", ex);
            }
        }

        struct Interpolation
        {
            public Vector3 vecTarget;
            public Vector3 vecError;
            public int StartTime;
            public int FinishTime;
            public float LastAlpha;
        }

        private Interpolation currentInterop;

        public void StartInterpolation()
        {
            currentInterop = new Interpolation();

            currentInterop.vecTarget = VehiclePosition;
            currentInterop.vecError = VehiclePosition - _lastVehiclePos;
            currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
            currentInterop.StartTime = Environment.TickCount;
            currentInterop.FinishTime = Environment.TickCount + 100;
            currentInterop.LastAlpha = 0f;
        }

        public static Ped GetResponsiblePed(Vehicle veh)
        {
            if (veh == null || veh.Handle == 0 || !veh.Exists()) return new Ped(0);

            if (veh.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle != 0) return veh.GetPedOnSeat(GTA.VehicleSeat.Driver);

            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (veh.GetPedOnSeat((VehicleSeat)i).Handle != 0) return veh.GetPedOnSeat((VehicleSeat)i);
            }

            return new Ped(0);
        }

        private string PedalingAnimDict()
        {
            string anim;
            switch ((VehicleHash)MainVehicle.Model.Hash)
            {
                case GTA.Native.VehicleHash.Bmx:
                    anim = "veh@bicycle@bmx@front@base";
                    break;
                case GTA.Native.VehicleHash.Cruiser:
                    anim = "veh@bicycle@cruiserfront@base";
                    break;
                case GTA.Native.VehicleHash.Scorcher:
                    anim = "veh@bicycle@mountainfront@base";
                    break;
                default:
                    anim = "veh@bicycle@roadfront@base";
                    break;
            }
            return anim;
        }

        private string PedalingAnimName(bool fast)
        {
            return fast ? "fast_pedal_char" : "cruise_pedal_char";
        }

        private bool IsPedaling(bool fast)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character.Handle, PedalingAnimDict(), PedalingAnimName(fast), 3);
        }

        private void StartPedalingAnim(bool fast)
        {
            Character.Task.PlayAnimation(PedalingAnimDict(), PedalingAnimName(fast), 8.0f, -8.0f, -1, AnimationFlags.Loop | AnimationFlags.AllowRotation, 5.0f);
        }

        private void StopPedalingAnim(bool fast)
        {
            Character.Task.ClearAnimation(PedalingAnimDict(), PedalingAnimName(fast));
        }

        public void Clear()
        {
            try
            {
                if (Character != null)
                {
                    Character.Model.MarkAsNoLongerNeeded();
                    Character.Delete();
                }
                if (_mainBlip != null)
                {
                    _mainBlip.Remove();
                    _mainBlip = null;
                }
                if (MainVehicle != null && Util.IsVehicleEmpty(MainVehicle))
                {
                    MainVehicle.Model.MarkAsNoLongerNeeded();
                    MainVehicle.Delete();
                }
                if (_parachuteProp != null)
                {
                    _parachuteProp.Delete();
                    _parachuteProp = null;
                }
            } catch (Exception ex)
            {
                UI.Notify("Clear sync error: " + ex.Message);
            }
        }
    }
}
