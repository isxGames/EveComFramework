﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EveCom;
using EveComFramework.Core;

namespace EveComFramework.Security
{
    #region Enums

    public enum FleeTrigger
    {
        Pod,
        NegativeStanding,
        NeutralStanding,
        Targeted,
        CapacitorLow,
        ShieldLow,
        ArmorLow
    }

    public enum FleeType
    {
        NearestStation,
        SecureBookmark,
        SafeBookmarks
    }

    #endregion

    #region Settings

    public class SecuritySettings : Settings
    {
        public List<FleeTrigger> Triggers = new List<FleeTrigger>
        {
            FleeTrigger.Pod,
            FleeTrigger.NegativeStanding,
            FleeTrigger.NeutralStanding,
            FleeTrigger.Targeted,
            FleeTrigger.CapacitorLow,
            FleeTrigger.ShieldLow,
            FleeTrigger.ArmorLow
        };
        public List<FleeType> Types = new List<FleeType>
        {
            FleeType.NearestStation,
            FleeType.SecureBookmark,
            FleeType.SafeBookmarks
        };
        public bool NegativeAlliance = false;
        public bool NegativeCorp = false;
        public bool NegativeFleet = false;
        public bool NeutralAlliance = false;
        public bool NeutralCorp = false;
        public bool NeutralFleet = false;
        public bool TargetAlliance = false;
        public bool TargetCorp = false;
        public bool TargetFleet = false;
        public int CapThreshold = 30;
        public int ShieldThreshold = 30;
        public int ArmorThreshold = 99;
        public string SafeSubstring = "Safe:";
        public string SecureBookmark = "";
        public int FleeWait = 5;
    }

    #endregion

    public class UIData : State
    {
        #region Instantiation

        static UIData _Instance;
        public static UIData Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new UIData();
                }
                return _Instance;
            }
        }

        private UIData() : base()
        {
            QueueState(Update);
        }

        #endregion

        #region Variables

        public List<string> Bookmarks { get; set; }

        #endregion

        #region States

        bool Update(object[] Params)
        {
            if (!Session.Safe || (!Session.InStation && !Session.InSpace)) return false;
            Bookmarks = Bookmark.All.Select(a => a.Title).ToList();
            return false;
        }

        #endregion
    }

    public class Security : State
    {
        #region Instantiation

        static Security _Instance;
        public static Security Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Security();
                }
                return _Instance;
            }
        }

        private Security() : base()
        {
        }


        #endregion

        #region Variables

        List<Bookmark> SafeSpots;
        public SecuritySettings Config = new SecuritySettings();
        Move.Move Move = EveComFramework.Move.Move.Instance;
        public Logger Log = new Logger("Security");

        #endregion

        #region Events

        public delegate void NewAlert(FleeTrigger Trigger);
        public event NewAlert Alert;
        public void TriggerAlert(FleeTrigger Trigger)
        {
            if (Alert != null)
            {
                Alert(Trigger);
            }
        }

        #endregion

        #region Actions

        public void Start()
        {
            if (Idle)
            {
                QueueState(CheckSafe);
            }

        }

        public void Stop()
        {
            Clear();
        }

        public void Flee()
        {
            QueueState(Flee);
        }

        public void Reset(int? Delay = null)
        {
            int iDelay = Delay ?? Config.FleeWait * 60000;
            QueueState(Blank, iDelay);
            QueueState(CheckSafe);
        }

        public void Configure()
        {
            UI.Security Configuration = new UI.Security();
            Configuration.Show();
        }

        #endregion

        #region States

        bool Blank(object[] Params)
        {
            Log.Log("Finished");
            return true;
        }

        bool CheckSafe(object[] Params)
        {
            if (!Standing.Ready)
            {
                Standing.LoadStandings();
                return false;
            }

            if (Entity.All.FirstOrDefault(a => a.IsWarpScrambling && a.IsTargetingMe) != null)
            {
                return false;
            }

            foreach (FleeTrigger Trigger in Config.Triggers)
            {
                switch (Trigger)
                {
                    case FleeTrigger.Pod:
                        if (MyShip.ToItem.GroupID == Group.Capsule)
                        {
                            TriggerAlert(FleeTrigger.Pod);
                            Log.Log("|rIn a pod!");
                            return true;
                        }
                        break;
                    case FleeTrigger.NegativeStanding:
                        List<Pilot> NegativePilots = Local.Pilots.Where(a => (a.ToAlliance.FromAlliance < 0 ||
                                                                                a.ToAlliance.FromCorp < 0 ||
                                                                                a.ToAlliance.FromChar < 0 ||
                                                                                a.ToCorp.FromAlliance < 0 ||
                                                                                a.ToCorp.FromCorp < 0 ||
                                                                                a.ToCorp.FromChar < 0 ||
                                                                                a.ToChar.FromAlliance < 0 ||
                                                                                a.ToChar.FromCorp < 0 ||
                                                                                a.ToChar.FromChar < 0
                                                                             ) &&
                                                                             a.ID != Me.CharID).ToList();
                        if (!Config.NegativeAlliance) { NegativePilots.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.NegativeCorp) { NegativePilots.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.NegativeFleet) { NegativePilots.RemoveAll(a => a.IsFleetMember); }
                        if (NegativePilots.Count > 0)
                        {
                            TriggerAlert(FleeTrigger.NegativeStanding);
                            Log.Log("|r{0} is negative standing", NegativePilots.FirstOrDefault().Name);
                            return true;
                        }
                        break;
                    case FleeTrigger.NeutralStanding:
                        List<Pilot> NeutralPilots = Local.Pilots.Where(a => (a.ToAlliance.FromAlliance <= 0 &&
                                                                                a.ToAlliance.FromCorp <= 0 &&
                                                                                a.ToAlliance.FromChar <= 0 &&
                                                                                a.ToCorp.FromAlliance <= 0 &&
                                                                                a.ToCorp.FromCorp <= 0 &&
                                                                                a.ToCorp.FromChar <= 0 &&
                                                                                a.ToChar.FromAlliance <= 0 &&
                                                                                a.ToChar.FromCorp <= 0 &&
                                                                                a.ToChar.FromChar <= 0
                                                                             ) &&
                                                                             a.ID != Me.CharID).ToList();
                        if (!Config.NeutralAlliance) { NeutralPilots.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.NeutralCorp) { NeutralPilots.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.NeutralFleet) { NeutralPilots.RemoveAll(a => a.IsFleetMember); }
                        if (NeutralPilots.Count > 0)
                        {
                            TriggerAlert(FleeTrigger.NeutralStanding);
                            Log.Log("|r{0} is neutral standing", NeutralPilots.FirstOrDefault().Name);
                            return true;
                        }
                        break;
                    case FleeTrigger.Targeted:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        List<Pilot> TargetingPilots = Local.Pilots.Where(a => Entity.All.FirstOrDefault(b => b.CharID == a.ID && b.IsTargetingMe) != null).ToList();
                        if (!Config.TargetAlliance) { TargetingPilots.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.TargetCorp) { TargetingPilots.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.TargetFleet) { TargetingPilots.RemoveAll(a => a.IsFleetMember); }
                        if (TargetingPilots.Count > 0)
                        {
                            TriggerAlert(FleeTrigger.Targeted);
                            Log.Log("|r{0} is targeting me", TargetingPilots.FirstOrDefault().Name);
                            return true;
                        }
                        break;
                    case FleeTrigger.CapacitorLow:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapThreshold)
                        {
                            TriggerAlert(FleeTrigger.CapacitorLow);
                            Log.Log("|rCapacitor is below threshold (|w{0}%|r)", Config.CapThreshold);
                            return true;
                        }
                        break;
                    case FleeTrigger.ShieldLow:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        if (MyShip.ToEntity.ShieldPct < Config.ShieldThreshold)
                        {
                            TriggerAlert(FleeTrigger.ShieldLow);
                            Log.Log("|rShield is below threshold (|w{0}%|r)", Config.ShieldThreshold);
                            return true;
                        }
                        break;
                    case FleeTrigger.ArmorLow:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        if (MyShip.ToEntity.ArmorPct < Config.ArmorThreshold)
                        {
                            TriggerAlert(FleeTrigger.ArmorLow);
                            Log.Log("|rArmor is below threshold (|w{0}%|r)", Config.ArmorThreshold);
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        bool Flee(object[] Params)
        {
            if (Session.InStation)
            {
                return true;
            }

            QueueState(Traveling);
            foreach (FleeType FleeType in Config.Types)
            {
                switch (FleeType)
                {
                    case FleeType.NearestStation:
                        if (Entity.All.FirstOrDefault(a => a.GroupID == Group.Station) != null)
                        {
                            Move.Object(Entity.All.FirstOrDefault(a => a.GroupID == Group.Station));
                            return true;
                        }
                        break;
                    case FleeType.SecureBookmark:
                        if (Bookmark.All.Count(a => a.Title == Config.SecureBookmark) > 0)
                        {
                            Move.Bookmark(Bookmark.All.FirstOrDefault(a => a.Title == Config.SecureBookmark));
                            return true;
                        }
                        break;
                    case FleeType.SafeBookmarks:
                        if (SafeSpots.Count == 0)
                        {
                            SafeSpots = Bookmark.All.Where(a => a.Title.Contains(Config.SafeSubstring) && a.LocationID == Session.SolarSystemID).ToList();
                        }
                        if (SafeSpots.Count > 0)
                        {
                            Move.Bookmark(SafeSpots.FirstOrDefault());
                            SafeSpots.Remove(SafeSpots.FirstOrDefault());
                            return true;
                        }
                        break;
                }
            }
            return true;
        }

        bool Traveling(object[] Params)
        {
            if (!Move.Idle || (Session.InSpace && MyShip.ToEntity.Mode == EntityMode.Warping))
            {
                return false;
            }
            return true;
        }
        #endregion
    }
}
