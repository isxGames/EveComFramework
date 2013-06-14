﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EveCom;
using EveComFramework.Core;
using LavishScriptAPI;

namespace EveComFramework.GroupControl
{
     
    public enum Role { Combat, Miner, Hauler , Booster };
    public enum GroupType { Mining, AnomalyCombat };

    #region Persistence Classes

    [Serializable]
    public class GroupSettings
    {
        public string FriendlyName;
        public Guid ID = Guid.NewGuid();
        public GroupType GroupType;
        public List<string> MemberProfiles = new List<string>();
    }

    public class GroupControlGlobalSettings : EveComFramework.Core.Settings
    {
        public GroupControlGlobalSettings() : base("GroupControl") {}
        public List<GroupSettings> Groups = new List<GroupSettings>();
    }

    public class GroupControlSettings : EveComFramework.Core.Settings
    {
        public Guid CurrentGroup = new Guid();
        public Role Role = Role.Miner;
    }
    #endregion

    public class ActiveMember
    {
        public bool Active = false;
        public bool Available = false;
        public string ProfileName;
        public string CharacterName;
        public int LeadershipValue = 0;
        public bool InFleet = false;
        public Role Role;
    }

    public class ActiveGroup
    {
        public GroupSettings GroupSettings;
        public List<ActiveMember> ActiveMembers;    
    }

    public class GroupControl : State
    {
        #region Variables

        public bool FinishedCycle = false;
        public GroupControlGlobalSettings GlobalConfig = new GroupControlGlobalSettings();
        public GroupControlSettings Config = new GroupControlSettings();
        public Logger Log = new Logger("GroupControl");
        public ActiveMember Self = new ActiveMember();
        public ActiveGroup CurrentGroup;
        public ActiveMember Leader;
        string[] GenericLSkills = new string[] { "Leadership", "Wing Command", "Fleet Command", "Warfare Link Specialist" };
        string[] CombatLSkills = new string[] { "Information Warfare", "Armored Warfare", "Siege Warfare", "Skirmish Warfare" };
        string[] MiningLSkills = new string[] { "Mining Director", "Mining Foreman" };

        #endregion  
    
        #region Instantiation

        static GroupControl _Instance;
        public static GroupControl Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new GroupControl();
                }
                return _Instance;
            }
        }

        private GroupControl()
            : base()
        {
            DefaultFrequency = 5000;
            LavishScript.Commands.AddCommand("UpdateGroupControl", UpdateGroupControl);
            LoadConfig();
           
        }
        
        #endregion

        #region LSCommands

        public int UpdateGroupControl(string[] args)
        {
            foreach (string st in args)
            {
                //Log.Log("UpdategroupControl {0}", st);
            }
            switch (args[1])
            {
                case "active":
                    ActiveMember activeMember = CurrentGroup.ActiveMembers.FirstOrDefault(a => a.ProfileName == args[2]);
                    if (activeMember != null)
                    {
                        activeMember.Active = true;
                        activeMember.LeadershipValue = Convert.ToInt32(args[3]);
                        activeMember.Role = (Role)Enum.Parse(typeof(Role), args[4]);
                        activeMember.CharacterName = args[5];
                    }
                    break;
                case "available":
                    ActiveMember availableMember = CurrentGroup.ActiveMembers.FirstOrDefault(a => a.ProfileName == args[2]);
                    if (availableMember != null)
                    {
                        availableMember.Available = Convert.ToBoolean(args[3]);
                    }
                    break;
                case "joinedfleet":
                    ActiveMember joinedFleet = CurrentGroup.ActiveMembers.FirstOrDefault(a => a.ProfileName == args[2]);
                    if (joinedFleet != null)
                    {
                        joinedFleet.InFleet = true;
                    }
                    break;
                case "reloadConfig":
                    LoadConfig();
                    break;
                case "forceupdate":
                    if (Self.CharacterName != null)
                    {
                        RelayAll("active", Self.ProfileName, Self.LeadershipValue.ToString(), Self.Role.ToString(), Self.CharacterName);
                        RelayAll("available", Self.Available.ToString());
                        if (Self.InFleet)
                        {
                            RelayAll("joinedfleet", Self.ProfileName);
                        }
                    }
                    break;

            }
            return 0;
        }
        #endregion

        #region Actions

        public bool IsLeader
        {
            get
            {
                if (Leader != null)
                {
                    if (Leader.ProfileName == Self.ProfileName)
                    {
                        return true;
                    }
                    return false;
                }
                return true;            
            }
        }

        public bool InGroup
        {
            get
            {
                if (CurrentGroup != null) return true;
                return false;
            }
        }

        public string LeaderName
        {
            get
            {
                if (Leader != null) return Leader.CharacterName;
                return "";
            }
        }

        public void Start()
        {
            if (Idle)
            {
                QueueState(InitializeSelf);
            }
        }

        public void Stop()
        {
            Clear();
        }

        public void Configure()
        {
            UI.GroupControl Configuration = new UI.GroupControl();
            Configuration.ShowDialog();
            RelayAll("reloadConfig", null);
            LoadConfig();

        }

        public void SetUnavailable()
        {
            Self.Available = false;
            RelayAll("available", "false");
        }

        public void SetAvailable()
        {
            Self.Available = true;
            RelayAll("available", "true");
        }

        #endregion 

        #region Helper Functions

        public void RelayAll(string Command , params string[] Args)
        {
            string msg = "relay \"all other\" -noredirect UpdateGroupControl \"" + Command + "\" ";
            if (Args != null)
            {
                foreach (string arg in Args)
                {
                    msg = msg + " \"" + arg + "\"";
                }            
            }        
            LavishScriptAPI.LavishScript.ExecuteCommand(msg);
        }

        public void LoadConfig()
        {
            Config.Load();
            GlobalConfig.Load();
            Self.Active = true;
            Self.Available = true;
            Self.Role = Config.Role;
            
            Self.ProfileName = Core.Config.Instance.DefaultProfile;

            CurrentGroup = new ActiveGroup();
            CurrentGroup.ActiveMembers = new List<ActiveMember>();
            CurrentGroup.GroupSettings = GlobalConfig.Groups.FirstOrDefault(a => a.ID == Config.CurrentGroup);
            if (CurrentGroup.GroupSettings != null)
            {
                foreach (string member in CurrentGroup.GroupSettings.MemberProfiles)
                {
                    if (member == Self.ProfileName)
                    {
                        CurrentGroup.ActiveMembers.Add(Self);
                    }
                    else
                    {
                        CurrentGroup.ActiveMembers.Add(new ActiveMember());
                        CurrentGroup.ActiveMembers.Last().ProfileName = member;
                    }
                }
            }
            else
            {
                CurrentGroup = null;
            }
        }

        #endregion

        

        #region States

        #region Utility

        bool Blank(object[] Params)
        {
            return true;
        }

        #endregion

        public bool InitializeSelf(object[] Params)
        {
            if (CurrentGroup != null)
            {
                Self.CharacterName = Me.Name;

                foreach (string skill in GenericLSkills)
                {
                    Skill s = Skill.All.FirstOrDefault(a => a.Type == skill);
                    if (s != null)
                    {
                        Self.LeadershipValue += s.SkillLevel;
                    }
                }

                if (Self.Role == Role.Combat && CurrentGroup.GroupSettings.GroupType == GroupType.AnomalyCombat)
                {
                    foreach (string skill in CombatLSkills)
                    {
                        Skill s = Skill.All.FirstOrDefault(a => a.Type == skill);
                        if (s != null)
                        {
                            Self.LeadershipValue += s.SkillLevel;
                        }
                    }
                }
                if (Self.Role == Role.Miner && CurrentGroup.GroupSettings.GroupType == GroupType.Mining)
                {
                    foreach (string skill in MiningLSkills)
                    {
                        Skill s = Skill.All.FirstOrDefault(a => a.Type == skill);
                        if (s != null)
                        {
                            Self.LeadershipValue += s.SkillLevel;
                        }
                    }
                }
                if (Self.Role == Role.Booster)
                {
                    Self.LeadershipValue += 10000;
                }
                QueueState(Organize);
                RelayAll("forceupdate", "");
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Organize(object[] Params)
        {
            FinishedCycle = false; 
            if (CurrentGroup != null)
            {
                try
                {
                    //check for group members who haven't checked it, keep waiting if there are

                    RelayAll("active", Core.Config.Instance.DefaultProfile, Self.LeadershipValue.ToString(), Self.Role.ToString(), Me.Name);
                    RelayAll("available", Self.ProfileName, Self.Available.ToString());

                    if (!Session.InFleet)
                    {
                        //i'm not in a fleet, should i wait for an invite or create a fleet?
                        if (CurrentGroup.ActiveMembers.Count(a => a.InFleet) < 1)
                        {
                            //nobody else is in a fleet, i can make one
                            Log.Log("|oCreating fleet");
                            Fleet.CreateFleet();
                            RelayAll("joinedfleet", Self.ProfileName);
                            Self.InFleet = true;
                            return false;
                        }
                        else
                        {
                            //someone else is in a fleet , wait for an invite from another group member
                            if (CurrentGroup.ActiveMembers.Any(a => Window.All.OfType<PopupWindow>().Any(b => b.Message.Contains(a.CharacterName))))
                            {
                                Log.Log("|oAccepting fleet invite");
                                Window.All.OfType<PopupWindow>().FirstOrDefault(a => CurrentGroup.ActiveMembers.Any(b => a.Message.Contains(b.CharacterName))).ClickButton(Window.Button.Yes);
                                RelayAll("joinedfleet", Self.ProfileName);
                                Self.InFleet = true;
                                return false;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    else if (!Self.InFleet)
                    {
                        RelayAll("joinedfleet", Self.ProfileName);
                        Self.InFleet = true;
                        return false;
                    }
                    //am i the only person in the fleet?
                    if (Fleet.Members.Count == 1)
                    {
                        //hand out invites!
                        Pilot ToInvite = Local.Pilots.FirstOrDefault(a => CurrentGroup.ActiveMembers.Any(b => b.CharacterName == a.Name && !b.InFleet));
                        if (ToInvite != null)
                        {
                            Log.Log("|oInviting fleet member");
                            Log.Log(" |-g{0}", ToInvite.Name);
                            Fleet.Invite(Local.Pilots.FirstOrDefault(a => CurrentGroup.ActiveMembers.Any(b => b.CharacterName == a.Name && !b.InFleet)), Fleet.Wings[0], Fleet.Wings[0].Squads[0], FleetRole.SquadMember);
                        }
                        return false;
                    }

                    //who should be squad leader
                    ActiveMember newLeader = CurrentGroup.ActiveMembers.Where(a => a.Active).OrderByDescending(a => a.LeadershipValue).ThenBy(b => b.ProfileName).FirstOrDefault(a => a.Active && a.Available && a.InFleet);
                    if (newLeader != null)
                    {
                        if (Leader != newLeader)
                        {
                            //oh shit we got a new leader , if it's not me i should check i wasn't the old one
                            Log.Log("|oSelecting new leader");
                            Log.Log(" |-g{0}", newLeader.CharacterName);
                            //check if the new leader isnt me
                            if (newLeader.CharacterName != Self.CharacterName)
                            {
                                //it's not me check if i have to hand boss over
                                if (Fleet.Members.FirstOrDefault(a => a.Boss).Name == Self.CharacterName)
                                {
                                    //i'm the squad leader but no the leader!! better give boss to new leader
                                    Log.Log("|oPassing boss to new leader");
                                    Log.Log(" |-g{0}", newLeader.CharacterName);
                                    Fleet.Members.First(a => a.Name == newLeader.CharacterName).MakeBoss();
                                    Leader = newLeader;
                                    return false;
                                }
                            }
                            Leader = newLeader;
                        }

                        //Am I the leader?
                        if (Leader.ProfileName == Self.ProfileName)
                        {
                            //am I da boss
                            if (Fleet.Members.Any(a => a.Boss && a.Name == Me.Name))
                            {
                                //am i the squad leader?
                                FleetMember commander = Fleet.Wings[0].Members.FirstOrDefault(a => a.Role == FleetRole.SquadCommander);
                                if (commander != null)
                                {
                                    //someone is squad leader, is it me?
                                    if (commander.Name != Me.Name)
                                    {
                                        //it's not me! , demote that guy
                                        commander.Move(Fleet.Wings[0], Fleet.Wings[0].Squads[0], FleetRole.SquadMember);
                                        return false;
                                    }
                                }
                                else
                                {
                                    //nobody is squad leader, make me squad leader!
                                    Fleet.Members.First(a => a.Name == Me.Name).Move(Fleet.Wings[0], Fleet.Wings[0].Squads[0], FleetRole.SquadCommander);
                                    return false;
                                }

                                //are there invites to do?
                                if (CurrentGroup.ActiveMembers.Any(a => !a.InFleet))
                                {
                                    Pilot ToInvite = Local.Pilots.FirstOrDefault(a => CurrentGroup.ActiveMembers.Any(b => b.CharacterName == a.Name && !b.InFleet));
                                    Log.Log("|oInviting fleet member");
                                    Log.Log(" |-g{0}", ToInvite.Name);
                                    Fleet.Invite(ToInvite, Fleet.Wings[0], Fleet.Wings[0].Squads[0], FleetRole.SquadMember);
                                    return false;
                                }
                            }
                            else
                            {
                                //im not the boss, hopefully the old boss will make me the boss
                                return false;
                            }
                        }
                    }
                    else
                    {
                    }
                    FinishedCycle = true;
                    return false;
                }
                catch (Exception e)
                {
                    Log.Log(e.Message);
                    return false;
                }
            }
            else
            {
                QueueState(InitializeSelf);
                return true;
            }
        }

        #endregion
    }

}
