using GTANetworkAPI;
using NeptuneEvo.Handles;
using MySqlConnector;
using NeptuneEvo.Accounts;
using NeptuneEvo.Players.Models;
using NeptuneEvo.Players;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Chars;
using NeptuneEvo.Events;
using NeptuneEvo.Functions;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using Localization;
using NeptuneEvo.VehicleData.LocalData.Models;

namespace NeptuneEvo.Core
{
    class EventSys : Script
    {
        public class PlayerData
        {
            public Vector3 Position { get; set; }
            public Vector3 Rotation { get; set; }
            public int Health { get; set; }
            public uint Dimension { get; set; }
        }
        public class CustomEvent
        {
            public string Name { get; set; }
            public ExtPlayer Admin { get; set; }
            public Vector3 Position { get; set; }
            public uint Dimension { get; set; }
            public ushort MembersLimit { get; set; }
            public ExtPlayer Winner { get; set; }
            public uint Reward { get; set; }
            public ExtColShape Zone { get; set; } = null;
            public byte EventState { get; set; } = 0; // 0 - MP not created, 1 - Created but not started, 2 - Created and started.
            public DateTime Started { get; set; }
            public int RewardLimit { get; set; } = 0;
            public Dictionary<ExtPlayer, PlayerData> EventMembers = new Dictionary<ExtPlayer, PlayerData>();
        }
        public static CustomEvent AdminEvent = new CustomEvent(); // Only one event can be created at a time.
        private static readonly nLog Log = new nLog("Core.EventSys");

        public static void Init()
        {
            AdminEvent.RewardLimit = Main.ServerSettings.EventRewardLimit;
        }
        
        private static void DeleteClientFromEvent(ExtPlayer player)
        {
            AdminEvent.EventMembers.Remove(player);
        }

        public static void OnPlayerDisconnected(ExtPlayer player)
        {
            try
            {
                if (player == null) return;
                if (AdminEvent.EventState != 0)
                {
                    if (AdminEvent.EventMembers.ContainsKey(player))
                    {
                        DeleteClientFromEvent(player);
                        if (AdminEvent.EventState == 2)
                        {
                            if (AdminEvent.EventMembers.Count == 0) CloseAdminEvent(AdminEvent.Admin, 0, -1, 0, -1, 0);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnPlayerDisconnected Exception: {e.ToString()}");
            }
        }

        public static bool ExitFromMP(ExtPlayer player, bool check = true)
        {
            try
            {
                var publicSessionData = player.GetSessionData();
                if (publicSessionData == null) return true;
                if (AdminEvent.EventMembers.ContainsKey(player))
                {
                    if (publicSessionData.AdminSkin)
                    {
                        publicSessionData.AdminSkin = false;
                        player.SetDefaultSkin();
                    }
                    
                    PlayerData sessionData = AdminEvent.EventMembers[player];
                    DeleteClientFromEvent(player);
                    if (check && AdminEvent.EventState == 2)
                    {
                        if (AdminEvent.EventMembers.Count == 0) CloseAdminEvent(AdminEvent.Admin, 0, -1, 0, -1, 0);
                    }
                    NAPI.Player.SpawnPlayer(player, new Vector3(sessionData.Position.X, sessionData.Position.Y, sessionData.Position.Z));
                    player.Position = sessionData.Position;
                    player.Health = sessionData.Health;
                    Trigger.Dimension(player, sessionData.Dimension);
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Write($"ExitFromMP Exception: {e.ToString()}");
            }
            return false;
        }

        [Command(AdminCommands.Createmp, "Usage: /createmp [Participant limit] [Zone radius] [Event name]", GreedyArg = true)]
        public static void CreateEvent(ExtPlayer player, ushort members, float radius, string eventname)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Createmp)) return;
                if (AdminEvent.EventState == 0)
                {
                    if (eventname.Length < 50)
                    {
                        if (radius >= 10) AdminEvent.Zone = CustomColShape.CreateSphereColShape(player.Position, radius, UpdateData.GetPlayerDimension(player), ColShapeEnums.AdminMP);
                        AdminEvent.EventState = 1;
                        AdminEvent.EventMembers = new Dictionary<ExtPlayer, PlayerData>();
                        if (members >= NAPI.Server.GetMaxPlayers()) members = 0;
                        AdminEvent.Started = DateTime.Now;
                        AdminEvent.MembersLimit = members;
                        AdminEvent.Name = eventname;
                        AdminEvent.Winner = null;
                        AdminEvent.Position = player.Position;
                        AdminEvent.Dimension = UpdateData.GetPlayerDimension(player);
                        AdminEvent.Admin = player;
                        AddAdminEventLog();

                        //SendPlayersToEvent("Мероприятие", eventname, $"Type /mp to participate!", "", 8000);
                        NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.EventStart, Chars.Models.ChatColors.AMP, eventname));
                        if (members != 0) NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.EventMemberLimit, Chars.Models.ChatColors.AMP, members));
                        else NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.LimitMemberNoStart, Chars.Models.ChatColors.AMP));
                        if (AdminEvent.Zone != null) NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpRadiusTp, Chars.Models.ChatColors.AMP, radius));
                        NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.TpMpTp, Chars.Models.ChatColors.AMP));
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.TooLongMpName));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.OneMpExists));
            }
            catch (Exception e)
            {
                Log.Write($"CreateEvent Exception: {e.ToString()}");
            }
        }

        public static void SendPlayersToEvent(string subTitle, string title, string desc, string image, ushort timeWait)
        {

            foreach (ExtPlayer foreachPlayer in NeptuneEvo.Character.Repository.GetPlayers())
            {
                if (!foreachPlayer.IsCharacterData())
                    continue;
                
                Trigger.ClientEvent(foreachPlayer, "hud.event", subTitle, title, desc, image, timeWait);
            }
        }
        
        public static void SendCoolMsg(ExtPlayer player, string subTitle, string title, string desc, string image, ushort timeWait)
        {

            foreach (ExtPlayer foreachPlayer in NeptuneEvo.Character.Repository.GetPlayers())
            {
                if (!foreachPlayer.IsCharacterData())
                    continue;
                
                Trigger.ClientEvent(player, "hud.event.cool", subTitle, title, desc, image, timeWait);
            }
        }
        
        [Interaction(ColShapeEnums.AdminMP, Out: true)]
        public static void OutAdminMP(ExtPlayer player)
        {
            if (!player.IsCharacterData()) return;
            if (AdminEvent.EventState == 2)
            { // Runs the check only after the event has started and the teleport is closed

                if (AdminEvent.EventMembers.ContainsKey(player))
                {
                    ExitFromMP(player);
                    Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.LeftMpTer));
                }
            }
        }
        [Command(AdminCommands.Startmp)]
        public static void StartEvent(ExtPlayer player)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Startmp)) return;
                if (AdminEvent.EventState == 1)
                {
                    if (AdminEvent.EventMembers.Count >= 1)
                    {
                        AdminEvent.EventState = 2;
                        NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpNameStarted, Chars.Models.ChatColors.AMP, AdminEvent.Name));
                        //SendPlayersToEvent("Event", AdminEvent.Name, $"The event has started, teleport is closed. Participants: {AdminEvent.EventMembers.Count}. Good luck to everyone!", "", 10000);
                        NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpPlayers, Chars.Models.ChatColors.AMP, AdminEvent.EventMembers.Count));
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.CantStartMpWithout));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.MpNotStarted));
            }
            catch (Exception e)
            {
                Log.Write($"StartEvent Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Stopmp, "Usage: /stopmp [Winner ID] [Reward #1] [Winner ID 2?] [Reward #2?] [Winner ID 3?] [Reward #3?]")] // didn't work out
        public static void MPReward(ExtPlayer player, int pid, uint reward, int pid2 = -1, uint reward2 = 0, int pid3 = -1, uint reward3 = 0)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Stopmp) || pid < 0) return;
                if(pid2 != -1 && (pid == pid2 || (pid3 != -1 && (pid2 == pid3 || pid == pid3))))
                {
                    Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.OneWinnerMpCant));
                    return;
                }
                if (AdminEvent.EventState == 2)
                {
                    if (reward <= AdminEvent.RewardLimit)
                    {
                        if (AdminEvent.Winner == null)
                        {
                            ExtPlayer target = Main.GetPlayerByID(pid);
                            if (target != null)
                            {
                                if (AdminEvent.EventMembers.ContainsKey(target) || AdminEvent.Admin == target) CloseAdminEvent(target, reward, pid2, reward2, pid3, reward3);
                                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.PlayerNotFoundMp));
                            }
                            else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.CantFindPlayerWithId));
                        }
                        else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.WinnerBilNaznachen));
                    }
                    else Trigger.SendChatMessage(player, "The reward cannot exceed the set limit: " + AdminEvent.RewardLimit + ".");
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.MpNotStarted));
            }
            catch (Exception e)
            {
                Log.Write($"MPReward Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Mpveh, "Usage: /mpveh [Model name] [Color] [Color] [Number of vehicles]")]
        public static void CreateMPVehs(ExtPlayer player, string model, byte c1, byte c2, byte count)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mpveh)) return;

                if (UpdateData.GetPlayerDimension(player) == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantUse0Dim), 3000);
                    return;
                }
                
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                
                if (AdminEvent.EventState >= 1)
                {
                    if (count >= 1 && count <= 10)
                    {
                        var vehHash = (uint)NAPI.Util.VehicleNameToModel(model);
                        if (vehHash != 0)
                        {
                            for (byte i = 0; i != count; i++)
                            {
                                var vehicle = VehicleStreaming.CreateVehicle(vehHash, new Vector3((player.Position.X + (4 * i)), player.Position.Y, player.Position.Z), player.Rotation.Z, c1, c2, "EVENTCAR", acc: VehicleAccess.Event, numb: -2, dimension: UpdateData.GetPlayerDimension(player), petrol: 9999, engine: true);
                                vehicle.PrimaryColor = c1;
                                vehicle.SecondaryColor = c2;
                            }
                            AdminEvent.Admin = player;
                        }
                        else
                        {
                            if (characterData.AdminLVL <= 5) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoVehThatName), 3000);
                            else
                            {
                                uint model1 = NAPI.Util.GetHashKey(model);
                                for (byte i = 0; i != count; i++)
                                {
                                    var vehicle = VehicleStreaming.CreateVehicle(model1, new Vector3((player.Position.X + (4 * i)), player.Position.Y, player.Position.Z), player.Rotation.Z, c1, c2, "EVENTCAR", acc: VehicleAccess.Event, numb: -2, dimension: UpdateData.GetPlayerDimension(player), petrol: 9999, engine: true);
                                    vehicle.PrimaryColor = c1;
                                    vehicle.SecondaryColor = c2;
                                }
                                AdminEvent.Admin = player;
                            }
                            return;
                        }
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.OneTime10vehs));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.VehOnlyMp));
            }
            catch (Exception e)
            {
                Log.Write($"CreateMPVehs Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.mpskin, "Usage: /mpskin [ID] [SKIN]")]
        public static void GiveMPSkin(ExtPlayer player, int pid, string pedModel)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.mpskin)) return;

                if (UpdateData.GetPlayerDimension(player) == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantUse0Dim), 3000);
                    return;
                }
                
                ExtPlayer target = Main.GetPlayerByID(pid);
                if (target != null)
                {
                    var targetSessionData = target.GetSessionData();
                    if (targetSessionData == null) return;
                    
                    if (AdminEvent.EventMembers.ContainsKey(target)) 
                    {
                        AdminEvent.Admin = player;
                        Trigger.SendToAdmins(1, LangFunc.GetText(LangType.En, DataName.AdminMpChangedSkin, Chars.Models.ChatColors.AMP, AdminEvent.Name, player.Name, target.Name, pedModel));
                        
                        if (pedModel.Equals("-1"))
                        {
                            if (targetSessionData.AdminSkin)
                            {
                                targetSessionData.AdminSkin = false;
                                target.SetDefaultSkin();
                                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VisualPlayerReturned), 3000);
                            }
                            else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VisualPlayerNotChanged), 3000);
                        }
                        else
                        {
                            PedHash pedHash = NAPI.Util.PedNameToModel(pedModel);
                            if (pedHash != 0)
                            {
                                targetSessionData.AdminSkin = true;
                                target.SetSkin(pedHash);
                            }
                            else
                            {
                                targetSessionData.AdminSkin = true;
                                target.SetSkin(NAPI.Util.GetHashKey(pedModel));
                            }
                        }
                    }
                    else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerIdFoundButNotMp), 3000);
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantFindPlayerWithId), 3000);
            }
            catch (Exception e)
            {
                Log.Write($"GiveMPSkin Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.mpskins, "Usage: /mpskins [SKIN] [RADIUS]")]
        public static void GiveMPSkins(ExtPlayer player, string pedModel, int radius)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.mpskins)) return;

                if (UpdateData.GetPlayerDimension(player) == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantUse0Dim), 3000);
                    return;
                }
                
                AdminEvent.Admin = player;
                
                if (pedModel.Equals("-1"))
                {
                    foreach (ExtPlayer foreachPlayer in Main.GetPlayersInRadiusOfPosition(player.Position, (float)radius, UpdateData.GetPlayerDimension(player)))
                    {
                        var foreachPlayerSessionData = foreachPlayer.GetSessionData();
                        if (foreachPlayerSessionData == null) continue;
                    
                        if (foreachPlayer.Value != player.Value && foreachPlayerSessionData.AdminSkin)
                        {
                            foreachPlayerSessionData.AdminSkin = false;
                            foreachPlayer.SetDefaultSkin();
                        }
                    }
                }
                else
                {
                    PedHash pedHash = NAPI.Util.PedNameToModel(pedModel);
                    foreach (ExtPlayer foreachPlayer in Main.GetPlayersInRadiusOfPosition(player.Position, (float)radius, UpdateData.GetPlayerDimension(player)))
                    {
                        var foreachPlayerSessionData = foreachPlayer.GetSessionData();
                        if (foreachPlayerSessionData == null) continue;
                    
                        if (foreachPlayer.Value != player.Value)
                        {
                            if (pedHash != 0)
                            {
                                foreachPlayerSessionData.AdminSkin = true;
                                foreachPlayer.SetSkin(pedHash);
                            }
                            else
                            {
                                foreachPlayerSessionData.AdminSkin = true;
                                foreachPlayer.SetSkin(NAPI.Util.GetHashKey(pedModel));
                            }
                        }
                    }
                }
                    
                Trigger.SendToAdmins(1, LangFunc.GetText(LangType.En, DataName.AdminMpChangedSkinRadius, Chars.Models.ChatColors.AMP, AdminEvent.Name, player.Name, pedModel, radius));
            }
            catch (Exception e)
            {
                Log.Write($"GiveMPSkins Exception: {e.ToString()}");
            }
        }
        
        [Command(AdminCommands.mppos)]
        public static void MPAdminPos(ExtPlayer player)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.mppos)) return;

                if (UpdateData.GetPlayerDimension(player) == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantUse0Dim), 3000);
                    return;
                }
                
                if (AdminEvent.EventState == 1)
                {
                    AdminEvent.Admin = player;
                    
                    foreach (ExtPlayer foreachPlayer in AdminEvent.EventMembers.Keys)
                    {
                        if (foreachPlayer == null) continue;
                        Trigger.ClientEvent(foreachPlayer, "createMPWaypoint", player.Value, player.Position.X, player.Position.Y, UpdateData.GetPlayerDimension(player));
                    }
                    
                    Trigger.ClientEvent(player, "createMPWaypoint", player.Value, player.Position.X, player.Position.Y, UpdateData.GetPlayerDimension(player));
                    Trigger.SendToAdmins(1, LangFunc.GetText(LangType.En, DataName.PostavilMetkuVsem, Chars.Models.ChatColors.AMP, AdminEvent.Name, player.Name));
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.MetkuCanPostaviTposleMp), 3000);
            }
            catch (Exception e)
            {
                Log.Write($"MPAdminPos Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Mpreward, "Usage: /mpreward [New limit]")]
        public static void SetMPReward(ExtPlayer player, int newreward)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mpreward)) return;
                if (newreward <= 999999)
                {
                    AdminEvent.RewardLimit = newreward;
                    try
                    {
                        using MySqlCommand cmd = new MySqlCommand
                        {
                            CommandText = "UPDATE `eventcfg` SET `RewardLimit`=@val0"
                        };
                        cmd.Parameters.AddWithValue("@val0", newreward);
                        MySQL.Query(cmd);
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.MpReward, newreward), 3000);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"SetMPReward #2 Exception: {e.ToString()}");
                    }
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.MpRewardLimit));
            }
            catch (Exception e)
            {
                Log.Write($"SetMPReward Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Mpo, GreedyArg = true)]
        public static void CMD_MPGlobal(ExtPlayer player, string text)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mpo)) return;
                if (AdminEvent.EventState >= 1)
                {
                    //Trigger.SendToAdmins(1, $"{Chars.Models.ChatColors.AMP}[A][{AdminEvent.Name}] {player.Name}: {text}");
                    Trigger.SendChatMessage(player, $"{Chars.Models.ChatColors.AMP}[{AdminEvent.Name}] {player.Name}: {text}");
                    lock (AdminEvent.EventMembers)
                    {
                        foreach (ExtPlayer foreachPlayer in AdminEvent.EventMembers.Keys)
                        {
                            if(foreachPlayer == null) continue;
                            Trigger.SendChatMessage(foreachPlayer, $"{Chars.Models.ChatColors.AMP}[{AdminEvent.Name}] {player.Name}: {text}");
                            //Notify.Send(foreachPlayer, NotifyType.Info, NotifyPosition.TopCenter, $"[{AdminEvent.Name}] {player.Name}: {text}", 10000);
                            SendCoolMsg(foreachPlayer,AdminEvent.Name, player.Name, $"{text}", "", 10000);
                        }
                    }
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.MpGlobalrError));
            }
            catch (Exception e)
            {
                Log.Write($"CMD_MPGlobal Exception: {e.ToString()}");
            }
        }

        [Command("mp")]
        public static void TpToMp(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (characterData.IsBannedMP == true)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.BlacklistedMp, characterData.BanMPReason), 5000);
                    return;
                }
                
                if (characterData.InsideHouseID == -1 && player.Dimension == 0 && characterData.DemorganTime <= 0 && characterData.ArrestTime <= 0 && !sessionData.CuffedData.Cuffed && !sessionData.DeathData.InDeath && sessionData.SitPos == -1 && sessionData.InTanksLobby == -1)
                {
                    if (AdminEvent.EventState == 1)
                    {
                        if (!AdminEvent.EventMembers.ContainsKey(player))
                        {
                            if (AdminEvent.MembersLimit == 0 || AdminEvent.EventMembers.Count < AdminEvent.MembersLimit)
                            {
                                AdminEvent.EventMembers.Add(player, new PlayerData 
                                { 
                                    Position = player.Position,
                                    Rotation = player.Rotation,
                                    Health = player.Health,
                                    Dimension = player.Dimension
                                });
                                player.Position = AdminEvent.Position;
                                Trigger.Dimension(player, AdminEvent.Dimension);
                                Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.YouTpedMp, AdminEvent.Name));
                            }
                            else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.MpListFull));
                        }
                        else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.YouAlreadyMped));
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.TpClosed));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.TpUnavaible));
            }
            catch (Exception e)
            {
                Log.Write($"TpToMp Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Mpkick, "Usage: /mpkick [Player ID] [Reason]", GreedyArg = true)]
        public static void MPKick(ExtPlayer player, int pid, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mpkick)) return;
                if (AdminEvent.EventState == 1)
                {
                    ExtPlayer target = Main.GetPlayerByID(pid);
                    if (target != null)
                    {
                        if (AdminEvent.EventMembers.ContainsKey(target))
                        {
                            AdminEvent.Admin = player;
                            Trigger.SendToAdmins(1, LangFunc.GetText(LangType.En, DataName.AdminKickedMpS, Chars.Models.ChatColors.AMP, AdminEvent.Name, player.Name, target.Name, reason));
                            lock (AdminEvent.EventMembers)
                            {
                                foreach (ExtPlayer foreachPlayer in AdminEvent.EventMembers.Keys)
                                {
                                    if (foreachPlayer == null) continue;
                                    Trigger.SendChatMessage(foreachPlayer, LangFunc.GetText(LangType.En, DataName.AdminKickedMpS, Chars.Models.ChatColors.AMP, AdminEvent.Name, player.Name, target.Name, reason));
                                }
                            }
                            ExitFromMP(target);
                        }
                        else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.PlayerIdFoundButNotMp));
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.CantFindPlayerWithId));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.VignatMozhno));
            }
            catch (Exception e)
            {
                Log.Write($"MPKick Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Banmp, "Usage: /banmp [Player ID] [Reason]", GreedyArg = true)]
        public static void BanMP(ExtPlayer player, int pid, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Banmp)) return;
                
                ExtPlayer target = Main.GetPlayerByID(pid);
                var targetCharacterData = target.GetCharacterData();
                if (target != null && targetCharacterData != null)
                {
                    if (targetCharacterData.IsBannedMP == true)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerBlocked), 5000);
                        return;
                    }
                    
                    Trigger.SendPunishment(LangFunc.GetText(LangType.En, DataName.AdminBlockedMpS, CommandsAccess.AdminPrefix, player.Name, target.Name, target.Value, reason), target);
                    GameLog.Admin($"{player.Name}", $"banmp({reason})", $"{target.Name}");
                    targetCharacterData.IsBannedMP = true;
                    targetCharacterData.BanMPReason = reason;
                    
                    if (AdminEvent.EventState == 1 && AdminEvent.EventMembers.ContainsKey(target))
                        ExitFromMP(target);
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantFindPlayerWithId), 5000);
            }
            catch (Exception e)
            {
                Log.Write($"BanMP Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Unbanmp, "Usage: /unbanmp [Player ID]")]
        public static void UnbanMP(ExtPlayer player, int pid)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Unbanmp)) return;
                
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                
                ExtPlayer target = Main.GetPlayerByID(pid);
                var targetCharacterData = target.GetCharacterData();
                if (target != null && targetCharacterData != null)
                {
                    if (targetCharacterData.IsBannedMP == false)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerHasntBlocked), 5000);
                        return;
                    }
                    
                    Trigger.SendToAdmins(1, LangFunc.GetText(LangType.En, DataName.AdminUnblockedMp, player.Name, player.Value, target.Name, target.Value));
                    Trigger.SendChatMessage(target, LangFunc.GetText(LangType.En, DataName.AdminBlockedMp, player.Name));
                    targetCharacterData.IsBannedMP = false;
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantFindPlayerWithId), 5000);
            }
            catch (Exception e)
            {
                Log.Write($"UnbanMP Exception: {e.ToString()}");
            }
        }

        [Command("mpleave")]
        public static void MPLeave(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                if (AdminEvent.EventState != 0)
                {
                    if (AdminEvent.EventMembers.ContainsKey(player))
                    {
                        ExitFromMP(player);
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SucLeaveMp), 5000);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"MPLeave Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Mphp, "Usage: /mphp [Amount of HP]")]
        public static void MPHeal(ExtPlayer player, byte health)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mphp)) return;
                if (AdminEvent.EventState >= 1)
                {
                    if (health >= 1 && health <= 100)
                    {
                        AdminEvent.Admin = player;
                        foreach (ExtPlayer foreachPlayer in AdminEvent.EventMembers.Keys)
                        {
                            if(foreachPlayer == null) continue;
                            NAPI.Player.SetPlayerHealth(foreachPlayer, health);
                        }
                        Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.YouFilledHp, health));
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.HpOneTo100));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.HpOnlyUnderStart));
            }
            catch (Exception e)
            {
                Log.Write($"MPHeal Exception: {e.ToString()}");
            }
        }

        [Command(AdminCommands.Mpplayers)]
        public static void MpPlayerList(ExtPlayer player)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mpplayers)) return;
                if (AdminEvent.EventState != 0)
                {
                    short memcount = Convert.ToInt16(AdminEvent.EventMembers.Count);
                    if (memcount > 0)
                    {
                        if (memcount <= 15)
                        {
                            foreach (ExtPlayer foreachPlayer in AdminEvent.EventMembers.Keys)
                            {
                                if(foreachPlayer == null) continue;
                                Trigger.SendChatMessage(player, "ID: " + foreachPlayer.Value + " | Имя: " + foreachPlayer.Name);
                            }
                            Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.PlayersOnMp, memcount));
                        }
                        else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.PlayersOnMp, memcount));
                    }
                    else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.NoMpPlayers));
                }
                else Trigger.SendChatMessage(player, LangFunc.GetText(LangType.En, DataName.MpNotSozdano));
            }
            catch (Exception e)
            {
                Log.Write($"MpPlayerList Exception: {e.ToString()}");
            }
        }
        
        public static void Event_PlayerDeath(ExtPlayer player, ExtPlayer entityKiller, uint weapon)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                if (sessionData.AdminSkin)
                {
                    sessionData.AdminSkin = false;
                    player.SetDefaultSkin();
                }
            }
            catch (Exception e)
            {
                Log.Write($"Event_PlayerDeath Exception: {e.ToString()}");
            }
        }
        
        private static void AddAdminEventLog()
        {
            GameLog.EventLogAdd(AdminEvent.Admin.Name, AdminEvent.Name, AdminEvent.MembersLimit, MySQL.ConvertTime(AdminEvent.Started));
        }

        private static void UpdateLastAdminEventLog()
        {
            GameLog.EventLogUpdate(AdminEvent.Admin.Name, AdminEvent.EventMembers.Count, AdminEvent.Winner.Name, AdminEvent.Reward, MySQL.ConvertTime(DateTime.Now), AdminEvent.RewardLimit, AdminEvent.MembersLimit, AdminEvent.Name);
        }

        public static void CloseAdminEvent(ExtPlayer winner, uint reward, int winnerid2, uint reward2, int winnerid3, uint reward3)
        {
            try
            {
                CustomColShape.DeleteColShape(AdminEvent.Zone);
                AdminEvent.Zone = null;
                
                var vehiclesLocalData = RAGE.Entities.Vehicles.All.Cast<ExtVehicle>()
                    .Where(v => v.VehicleLocalData != null)
                    .Where(v => v.VehicleLocalData.Access == VehicleAccess.Event)
                    .ToList();
                
                foreach (var vehicleEvent in vehiclesLocalData)
                    VehicleStreaming.DeleteVehicle(vehicleEvent);
                
                AdminEvent.Winner = winner;
                AdminEvent.Reward = reward;
                AdminEvent.EventState = 0;
                UpdateLastAdminEventLog();
                NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpEnded, Chars.Models.ChatColors.AMP, AdminEvent.Name));
                //SendPlayersToEvent("Event", AdminEvent.Name, "The event has ended, thank you for participating!", "", 10000);

                if (winner != AdminEvent.Admin)
                {
                    if (reward != 0)
                    {
                        NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpWinner1, Chars.Models.ChatColors.AMP, winner.Name, MoneySystem.Wallet.Format(reward)));
                        MoneySystem.Wallet.Change(winner, (int)reward);
                    }
                    else NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.WinnerMp1, Chars.Models.ChatColors.AMP, winner.Name));

                    int winnernum = 2;
                    if (winnerid2 != -1)
                    {
                        ExtPlayer winner2 = Main.GetPlayerByID(winnerid2);
                        if(winner2 != null && winner2 != AdminEvent.Admin)
                        {
                            if(reward2 != 0 && reward2 <= AdminEvent.RewardLimit)
                            {
                                NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpWinner, Chars.Models.ChatColors.AMP, winnernum, winner2.Name, MoneySystem.Wallet.Format(reward2)));
                                MoneySystem.Wallet.Change(winner2, (int)reward2);
                            }
                            else NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.WinnerMp, Chars.Models.ChatColors.AMP, winnernum, winner2.Name));
                            winnernum++;
                        }
                    }

                    if (winnerid3 != -1)
                    {
                        ExtPlayer winner3 = Main.GetPlayerByID(winnerid3);
                        if (winner3 != null && winner3 != AdminEvent.Admin)
                        {
                            if (reward3 != 0 && reward3 <= AdminEvent.RewardLimit)
                            {
                                NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.MpWinner, Chars.Models.ChatColors.AMP, winnernum, winner3.Name, MoneySystem.Wallet.Format(reward3)));
                                MoneySystem.Wallet.Change(winner3, (int)reward3);
                            }
                            else NAPI.Chat.SendChatMessageToAll(LangFunc.GetText(LangType.En, DataName.WinnerMp, Chars.Models.ChatColors.AMP, winnernum, winner3.Name));
                        }
                    }
                }
                if (AdminEvent.EventMembers.Count != 0)
                {
                    lock(AdminEvent.EventMembers)
                    {
                        foreach (ExtPlayer foreachPlayer in AdminEvent.EventMembers.Keys)
                        {
                            if (foreachPlayer == null) continue;
                            ExitFromMP(foreachPlayer, false);
                            //Notify.Send(foreachPlayer, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SpsMp), 3000);
                            SendCoolMsg(foreachPlayer,"Мероприятие", AdminEvent.Name, $"{LangFunc.GetText(LangType.En, DataName.SpsMp)}", "", 10000);
                        }
                    }
                }
                AdminEvent = new CustomEvent();
                AdminEvent.RewardLimit = Main.ServerSettings.EventRewardLimit;
            }
            catch (Exception e)
            {
                Log.Write($"CloseAdminEvent Exception: {e.ToString()}");
            }
        }
    }
}
