using GTANetworkAPI;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Data;
using Redage.SDK;
using MySqlConnector;
using System.Text.RegularExpressions;
using System.Threading;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Chars;
using NeptuneEvo.Fractions;
using NeptuneEvo.Functions;
using NeptuneEvo.Accounts;
using NeptuneEvo.Players.Models;
using NeptuneEvo.Players;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Character;
using Database;
using LinqToDB;
using System.Threading.Tasks;
using Localization;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Players.Phone.Messages.Models;
using NeptuneEvo.VehicleData.Data;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using Newtonsoft.Json;


namespace NeptuneEvo.Core
{
    class Admin : Script
    {
        public static readonly nLog Log = new nLog("Core.Admin");
        public static bool IsServerStoping = false;
        public static bool TimeChanged = false;
        public static short[] SetTime = new short[3] { 0, 0, 0 };

        public static Dictionary<string, (string, string, int)> ChangeNameQueue = new Dictionary<string, (string, string, int)>(); // CurName, (newName, admName, admLvl)
        public static Dictionary<ExtPlayer, (int, string, int)> SetLeaderQueue = new Dictionary<ExtPlayer, (int, string, int)>(); // targetId (fracid, admName, admLvl)
        public static Dictionary<ExtPlayer, (string, int)> SendCreatorQueue = new Dictionary<ExtPlayer, (string, int)>(); // targetId (admName, admLvl)
        public static Dictionary<int, (string, string, int, DateTime)> GlobalQueue = new Dictionary<int, (string, string, int, DateTime)>(); // QueueID (globaltext, admName, admLvl, globalexp)
        public static int GlobalID = 0;



        public static void RemoveQueue(ExtPlayer player, string text = "player's exit")

        {

            if (SetLeaderQueue.ContainsKey(player))

            {

                SetLeaderQueue.Remove(player);

                Trigger.SendToAdmins(2, $"{ChatColors.StrongOrange}[A] Request to grant leadership to {player.Name} ({player.Value}) was canceled due to {text}.");

            }

            if (SendCreatorQueue.ContainsKey(player))

            {

                SendCreatorQueue.Remove(player);

                Trigger.SendToAdmins(2, $"{ChatColors.StrongOrange}[A] Request to change appearance for {player.Name} ({player.Value}) was canceled due to {text}.");

            }

        }
        public static void AdminLog(int myadm, string message)
        {
            try
            {
                message = "!{#636363}[A] " + message;
                int hisadm = 0;
                foreach (ExtPlayer foreachPlayer in Main.AllAdminsOnline)
                {

                    var foreachCharacterData = foreachPlayer.GetCharacterData();

                    if (foreachCharacterData == null)
                        continue;

                    hisadm = foreachCharacterData.AdminLVL;



                    var foreachAdminConfig = foreachCharacterData.ConfigData.AdminOption;

                    if (foreachAdminConfig.ALog && hisadm >= 6 && hisadm >= myadm)
                        Trigger.SendChatMessage(foreachPlayer, message);
                }
            }
            catch (Exception e)
            {
                Log.Write($"AdminLog Exception: {e.ToString()}");
            }
        }

        public static void AdminsLog(int myadm, string message, byte levels_to = 2, string color = "#FFB833", bool highRankActionHide = true, byte hideAdminLevel = 8)
        {
            try
            {
                if (myadm >= hideAdminLevel && highRankActionHide == true) return; // Do not log actions in chat from special administrators and above
                message = "!{" + color + "}[A] " + message;// "!{#FFB833}[A] " + message;
                foreach (ExtPlayer foreachPlayer in Main.AllAdminsOnline)
                {

                    var foreachCharacterData = foreachPlayer.GetCharacterData();

                    if (foreachCharacterData == null) continue;
                    if (foreachCharacterData.AdminLVL >= levels_to) Trigger.SendChatMessage(foreachPlayer, message);
                }
            }
            catch (Exception e)
            {
                Log.Write($"AdminsLog Exception: {e.ToString()}");
            }
        }

        public static void ErrorLog(string message)
        {
            try
            {
                message = "!{#d35400}[A][ELOG] " + message;
                foreach (ExtPlayer foreachPlayer in Main.AllAdminsOnline)
                {

                    var foreachCharacterData = foreachPlayer.GetCharacterData();

                    if (foreachCharacterData == null)

                        continue;



                    var foreachAdminConfig = foreachCharacterData.ConfigData.AdminOption;

                    if (foreachAdminConfig.ELog && foreachCharacterData.AdminLVL >= 6)
                        Trigger.SendChatMessage(foreachPlayer, message);
                }
            }
            catch (Exception e)
            {
                Log.Write($"ErrorLog Exception: {e.ToString()}");
            }
        }

        public static void WinLog(string message)
        {
            try
            {
                message = "!{#EAEAB3}[A][WINLOG] " + message;
                foreach (ExtPlayer foreachPlayer in Main.AllAdminsOnline)
                {
                    var foreachCharacterData = foreachPlayer.GetCharacterData();
                    if (foreachCharacterData == null)
                        continue;

                    var foreachAdminConfig = foreachCharacterData.ConfigData.AdminOption;
                    if (foreachAdminConfig.WinLog && foreachCharacterData.AdminLVL >= 5)
                        Trigger.SendChatMessage(foreachPlayer, message);
                }
            }
            catch (Exception e)
            {
                Log.Write($"WinLog Exception: {e.ToString()}");
            }
        }
        public static void onPlayerDeathHandler(ExtPlayer player, ExtPlayer entityKiller, uint weapon, Vector3 pos)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null)
                    return;

                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;

                characterData.Deaths++;

                var killerCharacterData = entityKiller.GetCharacterData();
                if (killerCharacterData != null)
                {
                    killerCharacterData.Kills++;
                    sessionData.DeathData.LastDeath = $"{DateTime.Now.ToString("s")} | {entityKiller.Name} ({entityKiller.Value}) killed {player.Name} with {weapon}";
                    GameLog.Kills($"player({killerCharacterData.UUID})", weapon.ToString(), $"player({characterData.UUID})", $"{pos.X} {pos.Y} {pos.Z}");

                    var killerSessionData = entityKiller.GetSessionData();
                    if (killerSessionData != null && killerSessionData.KillData.DamageDisabled == true)
                    {
                        killerSessionData.KillData.Count++;
                        if (killerSessionData.KillData.Count == 1) Notify.Send(entityKiller, NotifyType.Info, NotifyPosition.BottomCenter, $"If you kill 1 more person before reaching level 1, you will be kicked", 10000);
                        else if (killerSessionData.KillData.Count >= 2) entityKiller.Kick();
                    }
                }
                else
                {
                    GameLog.Kills($"system", "", $"player({characterData.UUID})", $"{pos.X} {pos.Y} {pos.Z}");
                    //message = $"~r~{player.Name}({player.Value}) ~s~died";
                }

                foreach (ExtPlayer foreachPlayer in Main.AllAdminsOnline)
                {
                    var foreachCharacterData = foreachPlayer.GetCharacterData();
                    if (foreachCharacterData == null)
                        continue;

                    var foreachAdminConfig = foreachCharacterData.ConfigData.AdminOption;
                    if (foreachAdminConfig.KillList == 0)
                        continue;
                    if (foreachAdminConfig.KillList == 2 && foreachPlayer.Position.DistanceTo2D(player.Position) > 300)
                        continue;

                    SendKillLog(foreachPlayer, entityKiller, player, weapon);
                }
            }
            catch (Exception e)
            {
                Log.Write($"onPlayerDeathHandler Exception: {e.ToString()}");
            }
        }

        public static void SendKillLog(ExtPlayer player, ExtPlayer killer, ExtPlayer victim, uint weapon)
        {
            Trigger.ClientEvent(player, "hud.kill", killer != null ? killer.Id : -1, victim != null ? victim.Id : -1, weapon);
        }
        public static void sendRedbucks(ExtPlayer player, ExtPlayer target, int amount)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Givereds)) return;
                var targetAccountData = target.GetAccountData();
                if (targetAccountData == null) return;
                if (!target.IsCharacterData()) return;
                if (targetAccountData.RedBucks + amount < 0) amount = 0;
                UpdateData.RedBucks(target, amount, msg: "Sending RB");
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.RbOutcome, target.Name, amount), 3000);
                //Notify.Send(target, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.RbIncome, amount, player.Name), 3000);
                GameLog.Admin(player.Name, $"givereds({amount})", target.Name);
                Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.RedAge, LangFunc.GetText(LangType.En, DataName.RbIncome, amount, player.Name), DateTime.Now);
            }
            catch (Exception e)
            {
                Log.Write($"sendRedbucks Exception: {e.ToString()}");
            }
        }
        public static void giveFreeCase(ExtPlayer player, ExtPlayer target, byte caseid)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Givecase)) return;

                var targetAccountData = target.GetAccountData();
                if (targetAccountData == null) return;
                if (caseid < 0 || caseid > 2) return;
                Chars.Repository.AddNewItemWarehouse(target, ItemId.Case0 + caseid, 1);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"You gave {target.Name} ({target.Value}) one Free Case #{caseid}", 3000);
                Notify.Send(target, NotifyType.Success, NotifyPosition.BottomCenter, $"You received the opportunity from {player.Name} to open one Free Case #{caseid} in the Free-Roulette menu!", 3000);
                GameLog.Admin(player.Name, $"givecase{caseid}", target.Name);
            }
            catch (Exception e)
            {
                Log.Write($"giveFreeCase Exception: {e.ToString()}");
            }
        }
        public static void stopServer(ExtPlayer sender, string reason = "A scheduled server restart is in progress!")
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(sender, AdminCommands.Restart)) return;
                stopServer($"{sender.Name}", reason);
            }
            catch (Exception e)
            {
                Log.Write($"stopServer Exception: {e.ToString()}");
            }
        }

        public static async void SaveServer()
        {
            try
            {
                foreach (var foreachPlayer in Character.Repository.GetPlayers())
                    foreachPlayer.SavePlayerPosition();

                var vehiclesLocalData = RAGE.Entities.Vehicles.All.Cast<ExtVehicle>()
                    .Where(v => v.VehicleLocalData != null)
                    .Where(v => v.VehicleLocalData.Access == VehicleAccess.Garage || v.VehicleLocalData.Access == VehicleAccess.Personal)
                    .ToList();

                foreach (var vehicle in vehiclesLocalData)
                    vehicle.SavePosition();

                await NAPI.Task.WaitForMainThread();

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        Log.Write("Saving Database...");

                        await using var db = new ServerBD("MainDB");//On Separate Thread

                        foreach (ExtPlayer foreachPlayer in Character.Repository.GetPlayers())
                        {
                            try
                            {
                                var targetSessionData = foreachPlayer.GetSessionData();
                                if (targetSessionData == null) continue;
                                if (!targetSessionData.IsConnect)
                                    continue;

                                var targetCharacterData = foreachPlayer.GetCharacterData();
                                if (targetCharacterData == null)
                                    continue;

                                await Character.Save.Repository.SaveSql(db, foreachPlayer);
                                await Accounts.Save.Repository.SaveSql(db, foreachPlayer);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"saveDatabase Foreach #1 Exception: {e.ToString()}");
                            }
                        }
                        Log.Write("Players and Accounts has been saved to DB");
                        /*foreach (int acc in MoneySystem.Bank.Accounts.Keys)
                        {
                            try
                            {
                                if (!MoneySystem.Bank.Accounts.ContainsKey(acc)) continue;
                                MoneySystem.Bank.Save(acc);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"saveDatabase Foreach #2 Exception: {e.ToString()}");
                            }
                        }
                        Log.Write("Bank Saved");*/
                        foreach (var number in VehicleManager.Vehicles.Keys)
                        {
                            if (!VehicleData.LocalData.Repository.IsVehicleToNumber(VehicleAccess.Personal, number)) continue;
                            await VehicleManager.SaveSql(db, number);
                        }
                        Log.Write("Vehicles has been saved to DB");
                        BusinessManager.SavingBusiness();
                        await Organizations.Manager.SaveOrganizations(db);
                        Houses.HouseManager.SavingHouses();
                        await Stocks.SaveFractions(db);
                        WeaponRepository.SaveWeaponsDB();
                        AlcoFabrication.SaveAlco();
                        await Main.SaveDoorsControl(db);
                        await Players.Phone.Tinder.Repository.Saves(db);
                        //
                        Ban.Delete();

                        Log.Write("Database was saved");
                    }
                    catch (Exception e)
                    {
                        Log.Write($"saveDatabase Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"saveDatabase Exception: {e.ToString()}");
            }
        }


        public static void stopServer(string Admin = "server", string reason = "A scheduled server restart is in progress!")
        {
            try
            {
                Log.Write("Force saving database...", nLog.Type.Warn);
                IsServerStoping = true;
                GameLog.Admin(Admin, $"stopServer({reason})", "");

                Log.Write("Force kicking players...", nLog.Type.Warn);
                foreach (var foreachPlayer in RAGE.Entities.Players.All.Cast<ExtPlayer>())
                {
                    Ems.ReviveFunc(foreachPlayer, true);
                    Trigger.ClientEvent(foreachPlayer, "restart");
                    Trigger.Dimension(foreachPlayer, Dimensions.RequestPrivateDimension(foreachPlayer.Value));

                }


                foreach (var foreachPlayer in RAGE.Entities.Players.All.Cast<ExtPlayer>())
                    Players.Disconnect.Repository.OnPlayerDisconnect(foreachPlayer, DisconnectionType.Kicked, reason, isSave: false);

                Trigger.SetTask(async () =>
                {
                    var speedSave = DateTime.Now;
                    Log.Write($"[{DateTime.Now - speedSave}] All players has kicked!", nLog.Type.Success);

                    await using var db = new ServerBD("MainDB");//On Separate Thread

                    foreach (var foreachPlayer in RAGE.Entities.Players.All.Cast<ExtPlayer>())
                    {
                        try
                        {
                            var targetSessionData = foreachPlayer.GetSessionData();
                            if (targetSessionData == null)
                                continue;

                            await Character.Save.Repository.SaveSql(db, foreachPlayer);
                            await Accounts.Save.Repository.SaveSql(db, foreachPlayer);
                        }
                        catch (Exception e)
                        {
                            Log.Write($"saveDatabase Foreach #1 Exception: {e.ToString()}");
                        }
                    }

                    Log.Write($"[{DateTime.Now - speedSave}] All players saved!", nLog.Type.Success);

                    // Reset participation in the wheel of fortune
                    await Character.Save.Repository.ResetLuckyWheel();
                    //

                    await Accounts.Email.Repository.VerificationsDelete();

                    BusinessManager.SavingBusiness();

                    await Organizations.Manager.SaveOrganizations(db);

                    Houses.HouseManager.SavingHouses(true);

                    await Stocks.SaveFractions(db);

                    WeaponRepository.SaveWeaponsDB();

                    await Main.SaveDoorsControl(db);

                    await Players.Phone.Tinder.Repository.Saves(db);

                    //Log.Write($"[{DateTime.Now - speedSave}] Save property", nLog.Type.Success);

                    //await SaveAllPlayersServer();

                    Log.Write($"[{DateTime.Now - speedSave}] Restart All data has been saved!", nLog.Type.Success);

                    Timers.StartOnce(1000 * 60, () => { Environment.Exit(0); }, true);
                });


            }
            catch (Exception e)
            {
                Log.Write($"stopServer Exception: {e.ToString()}");
            }
        }

        private static async Task SaveAllPlayersServer()
        {
            var isSave = true;
            Console.WriteLine("SaveAllPlayersServer 1");

            do
            {
                //Thread.Sleep(1000 * 10);

                isSave = RAGE.Entities.Players.All.Cast<ExtPlayer>()
                    .Any(p => !p.IsRestartSaveAccountData || !p.IsRestartSaveCharacterData);

                await Task.Delay(1000);

            } while (isSave);

            Console.WriteLine("SaveAllPlayersServer 2");
        }
        public static void saveCoords(ExtPlayer player, string msg)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Save)) return;
                Vector3 pos = NAPI.Entity.GetEntityPosition(player);
                //pos.Z -= 1.12f;
                Vector3 rot = NAPI.Entity.GetEntityRotation(player);
                if (NAPI.Player.IsPlayerInAnyVehicle(player))
                {
                    var vehicle = (ExtVehicle)player.Vehicle;
                    pos = NAPI.Entity.GetEntityPosition(vehicle) + new Vector3(0, 0, 0.5);
                    rot = NAPI.Entity.GetEntityRotation(vehicle);
                }
                using (StreamWriter saveCoords = new StreamWriter("coords.txt", true, Encoding.UTF8))
                {
                    saveCoords.Write($"{msg}: new Vector3({pos.X}, {pos.Y}, {pos.Z}), new Vector3({rot.X}, {rot.Y}, {rot.Z})\r\n");
                    saveCoords.Close();
                }
            }
            catch (Exception e)
            {
                Log.Write($"saveCoords Exception: {e.ToString()}");
            }
        }
        public static void setPlayerAdminGroup(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Setadmin)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null)

                    return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null)
                    return;

                if (targetCharacterData.AdminLVL >= 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person already has admin rights", 3000);
                    return;
                }

                Character.Save.Repository.SaveAdminLvl(targetCharacterData.UUID, 1);

                targetCharacterData.AdminLVL = 1;

                Character.BindConfig.Repository.InitAdmin(target);

                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have granted admin rights to player {target.Name}", 3000);
                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"{player.Name} granted you admin rights", 3000);
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) granted {target.Name} admin rights level 1", 1, "#FFB833", false);
                GameLog.Admin($"{player.Name}", AdminCommands.Setadmin, $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"setPlayerAdminGroup Exception: {e.ToString()}");
            }
        }
        public static void OffdelPlayerAdminGroup(ExtPlayer player, string targetName)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Offdeladmin)) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null)
                    return;

                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;

                var target = (ExtPlayer)NAPI.Player.GetPlayerFromName(targetName);
                if (target.IsCharacterData())
                {
                    delPlayerAdminGroup(player, target);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offdeladmin was replaced with deladmin", 3000);
                    return;
                }
                if (!Main.PlayerUUIDs.ContainsKey(targetName))
                {
                    Notify.Send(target, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantFindMan), 3000);
                    return;
                }
                var targetuuid = Main.PlayerUUIDs[targetName];

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");//On Separate Thread

                        var character = await db.Characters
                            .Select(c => new
                            {
                                c.Uuid,
                                c.Firstname,
                                c.Lastname,
                                c.Adminlvl,
                            })
                            .Where(v => v.Uuid == targetuuid)
                            .FirstOrDefaultAsync();

                        if (character == null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Person not found", 3000);
                            return;
                        }

                        if (character.Adminlvl <= 0)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person does not have admin rights", 3000);
                            return;
                        }

                        if (character.Adminlvl >= characterData.AdminLVL)
                        {
                            Trigger.SendToAdmins(3, "!{#FF0000}" + $"[A] {sessionData.Name} ({sessionData.Value}) tried to remove {targetName} (offline), who has a higher administrator level.");
                            return;
                        }

                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have removed administrator rights from {targetName}", 3000);
                        AdminsLog(characterData.AdminLVL, $"{sessionData.Name} ({sessionData.Value}) removed admin rights from {targetName}", 1, "#FFB833", false);
                        GameLog.Admin($"{sessionData.Name}", $"OffDelAdmin", $"{targetName}");

                        await db.Characters
                            .Where(c => c.Uuid == character.Uuid)
                            .Set(c => c.Adminlvl, 0)
                            .UpdateAsync();
                    }
                    catch (Exception e)
                    {
                        Log.Write($"OffdelPlayerAdminGroup SetTask Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"OffdelPlayerAdminGroup Exception: {e.ToString()}");
            }
        }
        public static void delPlayerAdminGroup(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Deladmin)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null)
                    return;
                if (player == target)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot remove your own admin rights", 3000);
                    return;
                }

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null)
                    return;
                if (targetCharacterData.AdminLVL >= characterData.AdminLVL)
                {
                    Trigger.SendToAdmins(3, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to remove {target.Name} ({target.Value}), who has a higher administrator level.");
                    return;
                }
                if (targetCharacterData.AdminLVL < 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person does not have admin rights", 3000);
                    return;
                }

                Character.BindConfig.Repository.DeleteAdmin(target);

                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have removed administrator rights from {target.Name}", 3000);
                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"{player.Name} has removed your admin rights", 3000);
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) removed admin rights from {target.Name}", 1, "#FFB833", false);
                GameLog.Admin($"{player.Name}", $"delAdmin", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"delPlayerAdminGroup Exception: {e.ToString()}");
            }
        }
        public static void setPlayerAdminRank(ExtPlayer player, ExtPlayer target, int rank)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Arank)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (player == target)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot set your own rank", 3000);
                    return;
                }

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (targetCharacterData.AdminLVL < 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The person is not an administrator!", 3000);
                    return;
                }
                if (targetCharacterData.AdminLVL >= characterData.AdminLVL)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot change the rights level of this administrator", 3000);
                    return;
                }
                if (rank < 1 || rank >= characterData.AdminLVL)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"It's impossible to grant such a rank", 3000);
                    return;
                }
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have granted player {target.Name} admin rights level {rank}", 3000);
                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"{player.Name} has granted you admin rights level {rank}", 3000);
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) granted {target.Name} admin rights level {rank}", 1, "#FFB833", false);
                targetCharacterData.AdminLVL = rank;
                target.SetSharedData("ALVL", rank);
                Character.Save.Repository.SaveAdminLvl(targetCharacterData.UUID, rank);
                GameLog.Admin($"{player.Name}", $"setAdminRank({rank})", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"setPlayerAdminRank Exception: {e.ToString()}");
            }
        }

        public static void offSetPlayerAdminRank(ExtPlayer player, string targetName, int rank)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Offarank)) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null)
                    return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (rank < 1 || rank >= characterData.AdminLVL)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"It's impossible to grant such a rank", 3000);
                    return;
                }

                ExtPlayer target = (ExtPlayer)NAPI.Player.GetPlayerFromName(targetName);

                if (target.IsCharacterData())
                {
                    setPlayerAdminRank(player, target, rank);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offarank was replaced with arank", 3000);
                    return;
                }

                if (!Main.PlayerUUIDs.ContainsKey(targetName))
                {
                    Notify.Send(target, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantFindMan), 3000);
                    return;
                }
                var targetuuid = Main.PlayerUUIDs[targetName];

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");//On Separate Thread

                        var character = await db.Characters
                            .Select(c => new
                            {
                                c.Uuid,
                                c.Firstname,
                                c.Lastname,
                                c.Adminlvl,
                            })
                            .Where(v => v.Uuid == targetuuid)
                            .FirstOrDefaultAsync();

                        if (character == null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Person not found", 3000);
                            return;
                        }
                        if (character.Adminlvl <= 0)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person is not an administrator!", 3000);
                            return;
                        }

                        if (character.Adminlvl >= characterData.AdminLVL)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot change the rights level of this administrator", 3000);
                            return;
                        }

                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have granted player {targetName} admin rights level {rank}", 3000);
                        AdminsLog(characterData.AdminLVL, $"{sessionData.Name} ({sessionData.Value}) granted {targetName} admin rights level {rank}", 1, "#FFB833", false);
                        GameLog.Admin($"{sessionData.Name}", $"offSetAdminRank({rank})", $"{targetName}");

                        await db.Characters
                            .Where(c => c.Uuid == character.Uuid)
                            .Set(c => c.Adminlvl, rank)
                            .UpdateAsync();
                    }
                    catch (Exception e)
                    {
                        Log.Write($"offSetPlayerAdminRank SetTask Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"offSetPlayerAdminRank Exception: {e.ToString()}");
            }

        }
        public static void setPlayerVipLvl(ExtPlayer player, ExtPlayer target, int rank, ushort days)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Givevip)) return;



                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (rank > 5 || rank < 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"It's impossible to grant this level of VIP account", 3000);
                    return;
                }
                if (days > 1095)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Maximum VIP duration for granting is 3 years (1095 days)", 3000);
                    return;
                }

                var targetAccountData = target.GetAccountData();
                if (targetAccountData == null) return;
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have granted {target.Name} the {Group.GroupNames[rank]} status", 3000);
                EventSys.SendCoolMsg(target, "Administration", "VIP Grant", $"Administrator {player.Name} has granted you {Group.GroupNames[rank]}", "", 7000);
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) set {target.Name}'s status to {Group.GroupNames[rank]} for {days} days.");
                targetAccountData.VipLvl = rank;
                if (targetAccountData.VipDate > DateTime.Now) targetAccountData.VipDate = targetAccountData.VipDate.AddDays(days);
                else targetAccountData.VipDate = DateTime.Now.AddDays(days);
                GameLog.Admin($"{player.Name}", $"setVipLvl({rank},{days})", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"setPlayerVipLvl Exception: {e.ToString()}");
            }
        }

        public static void setFracLeader(ExtPlayer player, ExtPlayer target, int fractionId)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Setleader)) return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var fractionData = Manager.GetFractionData(fractionId);
                if (fractionData != null)
                {
                    if (!target.IsCharacterData()) return;

                    if (characterData.AdminLVL == 5 && player == target)
                    {
                        AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) granted leadership({fractionId}) to {target.Name} ({target.Value})");

                        GameLog.Admin($"{player.Name}", $"setFracLeader({fractionId})", $"{target.Name}");

                        if (SetLeaderQueue.ContainsKey(target))
                            SetLeaderQueue.Remove(target);

                        target.RemoveFractionMemberData();
                        target.ClearAccessories();
                        target.AddFractionMemberData(fractionId, fractionData.LeaderRank());
                        Customization.ApplyCharacter(target);

                        EventSys.SendCoolMsg(target, "Administration", "Leadership", $"You have become the leader of the {Manager.GetName(fractionId)} faction", "", 7000);
                        //Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"You have become the leader of the {Manager.GetName(fractionId)} faction", 3000);


                        if (Fractions.FractionClothingSets.FractionMainCloakrooms.ContainsKey(fractionId))
                        {
                            Notify.Send(target, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.StartWorkDay), 3000);
                            Manager.SetSkin(target);
                        }
                    }
                    else if (characterData.AdminLVL >= 5 || (SetLeaderQueue.ContainsKey(target) && SetLeaderQueue[target].Item1 == fractionId))
                    {
                        if (characterData.AdminLVL <= 4)
                        {
                            if (!SetLeaderQueue.ContainsKey(target)) return;
                            if (SetLeaderQueue[target].Item2 == player.Name)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Only another administrator can confirm!", 3000);
                                return;
                            }
                            AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) confirmed the leadership grant request from administrator {SetLeaderQueue[target].Item2}");
                            AdminsLog(SetLeaderQueue[target].Item3, $"{SetLeaderQueue[target].Item2} granted leadership({fractionId}) to {target.Name} ({target.Value})");
                            GameLog.Admin($"{player.Name}", $"setFracLeaderAccept({fractionId})", $"{SetLeaderQueue[target].Item2}");
                            GameLog.Admin($"{Admin.SetLeaderQueue[target].Item2}", $"setFracLeader({fractionId})", $"{target.Name}");
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "You have confirmed the leadership grant request!", 3000);
                        }
                        else
                        {
                            AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) granted leadership({fractionId}) to {target.Name} ({target.Value})");
                            GameLog.Admin($"{player.Name}", $"setFracLeader({fractionId})", $"{target.Name}");
                            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have made {target.Name} the leader of {Manager.GetName(fractionId)}", 3000);
                        }

                        if (SetLeaderQueue.ContainsKey(target))
                            SetLeaderQueue.Remove(target);

                        target.RemoveFractionMemberData();
                        target.ClearAccessories();
                        target.AddFractionMemberData(fractionId, fractionData.LeaderRank());
                        Customization.ApplyCharacter(target);

                        EventSys.SendCoolMsg(target, "Administration", "Leadership", $"You have become the leader of the {Manager.GetName(fractionId)} faction", "", 7000);
                        // Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"You have become the leader of the {Manager.GetName(fractionId)} faction", 3000);

                        if (Fractions.FractionClothingSets.FractionMainCloakrooms.ContainsKey(fractionId))
                        {
                            Notify.Send(target, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.StartWorkDay), 3000);
                            Manager.SetSkin(target);
                        }
                    }
                    else
                    {
                        if (SetLeaderQueue.ContainsKey(target) && SetLeaderQueue[target].Item1 != fractionId)
                        {
                            SetLeaderQueue.Remove(target);
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The old leadership grant request for this character has been deleted", 3000);
                        }
                        SetLeaderQueue.Add(target, (fractionId, player.Name, characterData.AdminLVL));
                        Trigger.SendToAdmins(2, "!{#FFB833}" + $"[A] Request from {player.Name} ({player.Value}) to grant leadership({fractionId}) to {target.Name} ({target.Value}). To confirm, type: /setleader {target.Value} {fractionId}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"setFracLeader Exception: {e.ToString()}");
            }
        }
        public static void delFracLeader(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Delleader)) return;

                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;

                var targetMemberFractionData = target.GetFractionMemberData();
                if (targetMemberFractionData == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person has no faction", 3000);
                    return;
                }

                var fractionData = Manager.GetFractionData(targetMemberFractionData.Id);
                if (fractionData == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person has no faction", 3000);
                    return;
                }


                if (targetMemberFractionData.Rank < fractionData.LeaderRank())
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person is not a leader", 3000);
                    return;
                }

                target.RemoveFractionMemberData();
                target.ClearAccessories();
                Customization.ApplyCharacter(target);

                EventSys.SendCoolMsg(target, "Administration", "Leadership", $"{player.Name.Replace('_', ' ')} has removed you from the post of faction leader", "", 7000);
                //Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"{player.Name.Replace('_', ' ')} has removed you from the post of faction leader", 3000);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have removed {target.Name.Replace('_', ' ')} from the post of faction leader", 3000);
                //Chars.Repository.PlayerStats(target);
                Admin.AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) removed the leadership from {target.Name} ({target.Value})");
                GameLog.Admin($"{player.Name}", $"delFracLeader", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"delFracLeader Exception: {e.ToString()}");
            }
        }
        public static void delJob(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Deljob)) return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();
                if (targetSessionData == null) return;

                var targetCharacterData = target.GetCharacterData();
                if (targetCharacterData == null) return;

                if (targetCharacterData.WorkID != 0)
                {
                    if (targetSessionData.WorkData.OnWork)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person must not be in their work uniform", 3000);
                        return;
                    }
                    UpdateData.Work(target, 0);
                    //Chars.Repository.PlayerStats(target);
                    Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"{player.Name.Replace('_', ' ')} has removed the employment from your character", 3000);
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have removed {target.Name.Replace('_', ' ')} from employment", 3000);
                    //Chars.Repository.PlayerStats(target);
                    Admin.AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) fired {target.Name} ({target.Value}) from their job");
                    GameLog.Admin($"{player.Name}", $"delJob", $"{target.Name}");
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person does not have a job", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"delJob Exception: {e.ToString()}");
            }
        }
        public static void delFrac(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Delfrac)) return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var targetMemberFractionData = target.GetFractionMemberData();
                if (targetMemberFractionData == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person has no faction", 3000);
                    return;
                }

                var fractionData = Manager.GetFractionData(targetMemberFractionData.Id);
                if (fractionData == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person has no faction", 3000);
                    return;
                }

                if (targetMemberFractionData.Rank >= fractionData.LeaderRank())
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The person is a faction leader", 3000);
                    return;
                }

                target.RemoveFractionMemberData();
                target.ClearAccessories();
                Customization.ApplyCharacter(target);

                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"Administrator {player.Name.Replace('_', ' ')} has kicked you from the faction", 3000);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have kicked {target.Name.Replace('_', ' ')} from the faction", 3000);
                //Chars.Repository.PlayerStats(target);
                Admin.AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) kicked {target.Name} ({target.Value}) from the faction");
                GameLog.Admin($"{player.Name}", $"delFrac", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"delFrac Exception: {e.ToString()}");
            }
        }

        public static void giveBankMoney(ExtPlayer player, int bankid, int amount)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Givemoney)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!MoneySystem.Bank.Accounts.ContainsKey(bankid))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "A player with this ID/bank account number was not found", 3000);
                    return;
                }
                MoneySystem.Bank.Data data = MoneySystem.Bank.Accounts[bankid];
                if (data.Type != 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The bank account you are trying to use does not belong to a person.", 3000);
                    return;
                }
                MoneySystem.Bank.Change(bankid, amount, false);
                GameLog.Money($"player({characterData.UUID})", $"bank({bankid})", amount, "admin");
                GameLog.Admin($"{player.Name}", $"giveBankMoney({amount})", $"{data.Holder}");
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"You gave person {data.Holder} ${amount} to bank account {bankid}", 3000);

                //if (amount >= 1000000)
                //    Admin.AdminsLog(1, $"[WARNING] Player {data.Holder} received ${amount} in a single transaction from {player.Name}({player.Value}) (giveBankMoney)", 1, "#FF0000");

                // var target = (ExtPlayer)NAPI.Player.GetPlayerFromName(data.Holder);
                // if (target.IsCharacterData())
                // {
                //     var targetSessionData = target.GetSessionData();
                //     if (targetSessionData == null) return;
                //     
                //     if (amount >= 10000 && targetSessionData.LastBankOperationSum == amount)
                //     {
                //         Admin.AdminsLog(1, $"[WARNING] Player {target.Name}({target.Value}) received ${amount} twice in a row from {player.Name}({player.Value}) (giveBankMoney)", 1, "#FF0000");
                //         targetSessionData.LastBankOperationSum = 0;
                //     }
                //     else
                //     {
                //         targetSessionData.LastBankOperationSum = amount;
                //     }
                // }
            }
            catch (Exception e)
            {
                Log.Write($"giveBankMoney Exception: {e.ToString()}");
            }
        }
        public static void giveMoney(ExtPlayer player, ExtPlayer target, int amount)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Givemoney)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                MoneySystem.Wallet.Change(target, amount);
                GameLog.Money($"player({characterData.UUID})", $"player({targetCharacterData.UUID})", amount, "admin");
                GameLog.Admin($"{player.Name}", $"giveMoney({amount})", $"{target.Name}");
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"You gave person {target.Name} ${amount}", 3000);
                Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.Bank, LangFunc.GetText(LangType.En, DataName.MoneyIncome, amount), DateTime.Now);
            }
            catch (Exception e)
            {
                Log.Write($"giveMoney Exception: {e.ToString()}");
            }
        }
        public static void OffGiveMoney(ExtPlayer player, string name, int amount)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Offgivemoney)) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null)
                    return;

                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;

                if (!Main.PlayerNames.Values.Contains(name) || !Main.PlayerUUIDs.ContainsKey(name))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "A person with this name was not found", 3000);
                    return;
                }
                ExtPlayer target = (ExtPlayer)NAPI.Player.GetPlayerFromName(name);
                if (target.IsCharacterData())
                {
                    giveMoney(player, target, amount);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offgivemoney was replaced with givemoney", 3000);
                    return;
                }
                int targetuuid = Main.PlayerUUIDs[name];
                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");//On Separate Thread

                        var character = await db.Characters
                            .Select(c => new
                            {
                                c.Uuid,
                                c.Money,
                            })
                            .Where(v => v.Uuid == targetuuid)
                            .FirstOrDefaultAsync();

                        if (character == null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Person not found", 3000);
                            return;
                        }

                        var money = Convert.ToInt32(character.Money) + amount;
                        if (money < 0)
                            money = 0;

                        GameLog.Money($"player({characterData.UUID})", $"player({character.Uuid})", amount, "admin");
                        GameLog.Admin($"{sessionData.Name}", $"offGiveMoney({amount})", $"{name}");
                        //Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.Bank, LangFunc.GetText(LangType.En, DataName.MoneyIncome, amount), DateTime.Now); //DOES NOT WORK
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"You have set {name}'s money to ${money} (+${amount})", 3000);

                        await db.Characters
                            .Where(c => c.Uuid == character.Uuid)
                            .Set(c => c.Money, money)
                            .UpdateAsync();
                    }
                    catch (Exception e)
                    {
                        Log.Write($"OffGiveMoney SetTask Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"OffGiveMoney Exception: {e.ToString()}");
            }
        }
        public static void mutePlayer(ExtPlayer player, ExtPlayer target, int time, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Mute)) return;
                if (!player.IsCharacterData()) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (player == target) return;
                if (time < 5 || time > 10080)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot issue a mute for more than 10080 minutes", 3000);
                    return;
                }
                if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                {
                    Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");
                    Character.BindConfig.Repository.DeleteAdmin(player);
                    return;
                }
                if (!CheckMe(player, 0)) return;
                int firstTime = time * 60;
                string deTimeMsg = " minutes";
                if (time > 60)
                {
                    deTimeMsg = " hours";
                    time /= 60;
                    if (time > 24)
                    {
                        deTimeMsg = " days";
                        time /= 24;
                    }
                }
                targetCharacterData.Unmute = firstTime;
                targetCharacterData.VoiceMuted = true;
                if (targetSessionData.TimersData.MuteTimer != null) Timers.Stop(targetSessionData.TimersData.MuteTimer);
                targetSessionData.TimersData.MuteTimer = Timers.Start(1000, () => timer_mute(target));
                target.SetSharedData("vmuted", true);
                Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} muted player {target.Name}({target.Value}) for {time}{deTimeMsg}. Reason: {reason}", target);
                GameLog.Admin($"{player.Name}", $"mutePlayer({time}{deTimeMsg}, {reason})", $"{target.Name}");
                Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.RedAge, $"{player.Name} muted you for {time}{deTimeMsg}. Reason: {reason}", DateTime.Now);
            }
            catch (Exception e)
            {
                Log.Write($"mutePlayer Exception: {e.ToString()}");
            }
        }
        public static void unmutePlayer(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!player.IsCharacterData()) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (targetCharacterData.Unmute >= 0)
                {
                    targetCharacterData.Unmute = 1;
                    Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} unmuted player {target.Name}({target.Value})");
                    GameLog.Admin($"{player.Name}", $"unmutePlayer", $"{target.Name}");
                    Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.RedAge, $"{player.Name} unmuted you.", DateTime.Now);
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The player is not muted", 1500);
            }
            catch (Exception e)
            {
                Log.Write($"unmutePlayer Exception: {e.ToString()}");
            }
        }

        public static void OffMutePlayer(ExtPlayer player, string targetName, int time, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Offmute)) return;
                ExtPlayer target = (ExtPlayer)NAPI.Player.GetPlayerFromName(targetName);
                if (target.IsCharacterData())
                {
                    mutePlayer(player, target, time, reason);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offmute was replaced with mute", 3000);
                    return;
                }
                if (!Main.PlayerNames.Values.Contains(targetName) || !Main.PlayerUUIDs.ContainsKey(targetName))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "A person with this name was not found", 3000);
                    return;
                }
                if (time < 5 || time > 10080)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot issue a mute for more than 10080 minutes", 3000);
                    return;
                }
                if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                {
                    Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");
                    Character.BindConfig.Repository.DeleteAdmin(player);
                    return;
                }
                if (player.Name.Equals(targetName)) return;
                if (!CheckMe(player, 0)) return;
                int firstTime = time * 60;
                string deTimeMsg = " minutes";
                if (time > 60)
                {
                    deTimeMsg = " hours";
                    time /= 60;
                    if (time > 24)
                    {
                        deTimeMsg = " days";
                        time /= 24;
                    }
                }
                int targetuuid = Main.PlayerUUIDs[targetName];
                string[] split = targetName.Split('_');

                Character.Save.Repository.SaveUnMute(targetuuid, firstTime);

                Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} muted player {targetName} offline for {time}{deTimeMsg}. Reason: {reason}");
                GameLog.Admin($"{player.Name}", $"mutePlayer({time}{deTimeMsg}, {reason})", $"{targetName}");
            }
            catch (Exception e)
            {
                Log.Write($"OffMutePlayer Exception: {e.ToString()}");
            }

        }
        public static void OffUnMutePlayer(ExtPlayer player, string targetName)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Offunmute)) return;
                ExtPlayer target = (ExtPlayer)NAPI.Player.GetPlayerFromName(targetName);
                if (target.IsCharacterData())
                {
                    unmutePlayer(player, target);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offunmute was replaced with unmute", 3000);
                    return;
                }
                if (!Main.PlayerNames.Values.Contains(targetName) || !Main.PlayerUUIDs.ContainsKey(targetName))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "A person with this name was not found", 3000);
                    return;
                }
                if (player.Name.Equals(targetName)) return;
                int targetuuid = Main.PlayerUUIDs[targetName];
                string[] split = targetName.Split('_');

                Character.Save.Repository.SaveUnMute(targetuuid, 0);

                Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} unmuted player {targetName} offline.");
                GameLog.Admin($"{player.Name}", $"unmutePlayer", $"{targetName}");
            }
            catch (Exception e)
            {
                Log.Write($"OffUnMutePlayer Exception: {e.ToString()}");
            }
        }



        public static void getRb(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Getlogin)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetAccountData = target.GetAccountData();

                if (targetAccountData == null) return;
                int targetRb = targetAccountData.RedBucks;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Player {target.Name} ({target.Value}) has {targetRb} RedBucks", 5000);
                AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) checked the RB amount of {target.Name} ({target.Value}) - {targetRb}");
            }
            catch (Exception e)
            {
                Log.Write($"getRb Exception: {e.ToString()}");
            }
        }

        public static void getVip(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.GetVip)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetAccountData = target.GetAccountData();

                if (targetAccountData == null) return;
                int targetVip = targetAccountData.VipLvl;
                DateTime targetVipDate = targetAccountData.VipDate;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Player {target.Name} ({target.Value}) has VIP level {targetVip} until {targetVipDate} ", 5000);
                AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) checked the VIP status of {target.Name} ({target.Value}) - {targetVip}");
            }
            catch (Exception e)
            {
                Log.Write($"getRb Exception: {e.ToString()}");
            }
        }

        public static void getLogin(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Getlogin)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (player == target) return;
                string targetlogin = target.GetLogin();
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Player {target.Name}'s ({target.Value}) login is - {targetlogin}", 5000);
                AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) checked the login of {target.Name} ({target.Value}) - {targetlogin}");
            }
            catch (Exception e)
            {
                Log.Write($"getLogin Exception: {e.ToString()}");
            }
        }
        public static bool CheckMe(ExtPlayer player, byte type)
        {
            try
            {

                var sessionData = player.GetSessionData();

                if (sessionData == null) return false;

                var characterData = player.GetCharacterData();

                if (characterData == null) return false;
                if (characterData.AdminLVL == 0) return false;
                if (Main.ServerNumber == 0 || characterData.AdminLVL == 9) return true;
                int alvl = characterData.AdminLVL;
                int limit = (alvl >= 1 && alvl <= 5) ? 5 : 10;
                switch (type)
                {
                    case 0:
                        if (sessionData.AdminData.MuteCount == limit)
                        {
                            Trigger.SendToAdmins(3, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the security system for exceeding the punishment limit");
                            BanMe(player, 1);
                            return false;
                        }
                        sessionData.AdminData.MuteCount++;
                        break;
                    case 1:
                        if (sessionData.AdminData.KickCount == limit)
                        {
                            Trigger.SendToAdmins(3, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the security system for exceeding the punishment limit");
                            BanMe(player, 1);
                            return false;
                        }
                        sessionData.AdminData.KickCount++;
                        break;
                    case 2:
                        if (sessionData.AdminData.JailCount == limit)
                        {
                            Trigger.SendToAdmins(3, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the security system for exceeding the punishment limit");
                            BanMe(player, 1);
                            return false;
                        }
                        sessionData.AdminData.JailCount++;
                        break;
                    case 3:
                        if (sessionData.AdminData.WarnCount == limit)
                        {
                            Trigger.SendToAdmins(3, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the security system for exceeding the punishment limit");
                            BanMe(player, 1);
                            return false;
                        }
                        sessionData.AdminData.WarnCount++;
                        break;
                    case 4:
                        if (sessionData.AdminData.BansCount == limit)
                        {
                            Trigger.SendToAdmins(3, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the security system for exceeding the punishment limit");
                            BanMe(player, 1);
                            return false;
                        }
                        sessionData.AdminData.BansCount++;
                        break;
                    case 5:
                        if (sessionData.AdminData.AclearCount == 5)
                        {
                            Trigger.SendToAdmins(3, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the security system for exceeding the punishment limit");
                            BanMe(player, 1);
                            return false;
                        }
                        sessionData.AdminData.AclearCount++;
                        break;
                    default:
                        break;
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Write($"CheckMe Exception: {e.ToString()}");
                return false;
            }
        }
        public static void BanMe(ExtPlayer player, byte type)
        {
            try
            {

                var accountData = player.GetAccountData();
                if (accountData == null) return;



                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                string msg = "Banned by the system ";
                switch (type)
                {
                    case 0:
                        msg += "for attempting to ban unbannable characters";
                        break;
                    case 1:
                        msg += "for exceeding the punishment limit per minute";
                        break;
                    default:
                        break;
                }
                Character.BindConfig.Repository.DeleteAdmin(player);
                Ban.Online(player, DateTime.MaxValue, true, msg, "server");
                GameLog.Ban(-2, characterData.UUID, accountData.Login, DateTime.MaxValue, msg, true);
                player.Kick(msg);
            }
            catch (Exception e)
            {
                Log.Write($"BanMe Exception: {e.ToString()}");
            }
        }

        public static void banPlayer(ExtPlayer player, ExtPlayer target, int time, string reason, bool isSBan = false)
        {
            try
            {

                var accountData = player.GetAccountData();
                if (accountData == null) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (isSBan == true && !CommandsAccess.CanUseCmd(player, AdminCommands.Sban)) return;

                if (!isSBan && !CommandsAccess.CanUseCmd(player, AdminCommands.Ban)) return;
                if (player == target) return;
                int tadmlvl = targetCharacterData.AdminLVL;
                if (tadmlvl == 9)
                {
                    Trigger.SendToAdmins(1, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to ban {target.Name} ({target.Value}).");
                    BanMe(player, 0);
                    return;
                }
                else if (tadmlvl != 0 && tadmlvl >= characterData.AdminLVL)
                {
                    Trigger.SendToAdmins(3, $"{ChatColors.StrongOrange}[A] {player.Name} ({player.Value}) banned {target.Name} ({target.Value}) and was banned by the system.");

                    Character.BindConfig.Repository.DeleteAdmin(target);
                    Character.BindConfig.Repository.DeleteAdmin(player);

                    Ban.Online(target, DateTime.MaxValue, false, reason, player.Name);
                    Ban.Online(player, DateTime.MaxValue, false, $"Banned by the system for banning administrator {target.Name}", "server");

                    Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"You have been permanently banned by administrator {player.Name}.", 30000);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.Center, $"You have been permanently banned by the system for banning administrator {target.Name}.", 30000);

                    GameLog.Ban(characterData.UUID, targetCharacterData.UUID, target.GetLogin(), DateTime.MaxValue, reason, false);
                    GameLog.Ban(-2, characterData.UUID, accountData.Login, DateTime.MaxValue, $"Banned by the system for banning administrator {target.Name}", false);

                    target.Kick(reason);
                    player.Kick("Banned by the system for banning an administrator");
                }
                else
                {
                    if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                    {
                        Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");
                        Character.BindConfig.Repository.DeleteAdmin(player);
                        return;
                    }
                    if (!CheckMe(player, 4)) return;
                    DateTime unbanTime = (time >= 3650) ? DateTime.MaxValue : DateTime.Now.AddDays(time);



                    if (time >= 3650)

                    {

                        if (isSBan == true) AdminsLog(characterData.AdminLVL, $"{player.Name} silently banned player {target.Name}({target.Value}) forever. Reason: {reason}", 1, "#FFB833", false);

                        else Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} has permanently banned character {target.Name}({target.Value}). Reason: {reason}", target);

                    }

                    else

                    {

                        if (isSBan == true) AdminsLog(characterData.AdminLVL, $"{player.Name} silently banned player {target.Name}({target.Value}) for {time} days. Reason: {reason}", 1, "#FFB833", false);

                        else Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} has banned character {target.Name}({target.Value}) for {time} days. Reason: {reason}.", target);

                    }



                    Ban.Online(target, unbanTime, false, reason, player.Name);

                    Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"You are banned until {unbanTime.Day} {unbanTime.ToString("MMMM")} {unbanTime.Year} {unbanTime.Hour}:{unbanTime.Minute}:{unbanTime.Second} (UTC+3).", 30000);
                    Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"Reason: {reason}", 30000);

                    GameLog.Ban(characterData.UUID, targetCharacterData.UUID, target.GetLogin(), unbanTime, reason, false);
                    target.Kick(reason);
                }
            }
            catch (Exception e)
            {
                Log.Write($"banPlayer Exception: {e.ToString()}");
            }
        }
        public static void banLoginPlayer(ExtPlayer player, string login, int time, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Banlogin)) return;



                var characterData = player.GetCharacterData();

                if (characterData == null)
                    return;

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        var ban = await Ban.GetBanToLogin(login);

                        if (ban != null)
                        {
                            var hard = (ban.Ishard > 0) ? "hard " : "";
                            Notify.Send(player, NotifyType.Warning, NotifyPosition.Center, $"The player is already {hard}banned.", 3000);
                            return;
                        }

                        NAPI.Task.Run(() =>
                        {
                            try
                            {

                                Notify.Send(player, NotifyType.Warning, NotifyPosition.Center, $"You have banned the login {login} offline with the reason {reason}.", 3000);

                                DateTime unbanTime = DateTime.Now.AddDays(time);

                                Ban.OfflineBanToLogin(login, unbanTime, true, reason, "System");

                                GameLog.Ban(-1, -1, login, unbanTime, reason, true);

                                var target = Accounts.Repository.GetPlayerToLogin(login);

                                if (target.IsAccountData())

                                {

                                    AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) banned the login({login}) of {target.Name} ({target.Value}) offline for the reason {reason}");

                                    Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"You are banned until {unbanTime.Day} {unbanTime.ToString("MMMM")} {unbanTime.Year} {unbanTime.Hour}:{unbanTime.Minute}:{unbanTime.Second} (UTC+3).", 30000);

                                    Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"Reason: {reason}", 30000);

                                    target.Kick(reason);

                                }

                                else AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) banned the login({login}) offline for the reason {reason}");

                            }

                            catch (Exception e)

                            {

                                Log.Write($"banLoginPlayer NAPI.Task Exception: {e.ToString()}");

                            }



                        });

                    }

                    catch (Exception e)

                    {

                        Log.Write($"banLoginPlayer Task Exception: {e.ToString()}");

                    }

                });
            }
            catch (Exception e)
            {
                Log.Write($"banLoginPlayer Exception: {e.ToString()}");
            }
        }

        public static void unbanLoginPlayer(ExtPlayer player, string login)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Unbanlogin)) return;



                var characterData = player.GetCharacterData();

                if (characterData == null)
                    return;

                if (!Main.Usernames.ContainsKey(login))
                {
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "This login was not found in the system.", 3000);
                    return;
                }
                Trigger.SetTask(async () =>
                {
                    try
                    {
                        var isBan = await Ban.PardonLogin(login);

                        if (!isBan)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"{login} is not banned!", 3000);
                            return;
                        }

                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) unbanned the login({login})");

                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Login unblocked!", 3000);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"unbanLoginPlayer Task.Run Exception: {e.ToString()}");
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        Log.Write($"unbanLoginPlayer Task.Run Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"unbanLoginPlayer Exception: {e.ToString()}");
            }
        }
        public static void hardbanPlayer(ExtPlayer player, ExtPlayer target, int time, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Hardban)) return;

                var accountData = player.GetAccountData();
                if (accountData == null) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (player == target) return;
                int tadmlvl = targetCharacterData.AdminLVL;
                string targetLogin = target.GetLogin();
                if (tadmlvl == 9)
                {
                    Trigger.SendToAdmins(1, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to hardban {target.Name} ({target.Value}).");
                    BanMe(player, 0);
                    return;
                }
                else if (tadmlvl != 0 && tadmlvl >= characterData.AdminLVL)
                {
                    Trigger.SendToAdmins(3, $"{ChatColors.StrongOrange}[A] {player.Name} ({player.Value}) hardbanned {target.Name} ({target.Value}) and was banned by the system.");

                    Character.BindConfig.Repository.DeleteAdmin(target);
                    Character.BindConfig.Repository.DeleteAdmin(player);

                    Ban.Online(target, DateTime.MaxValue, true, reason, player.Name);
                    Ban.Online(player, DateTime.MaxValue, true, $"Banned by the system for banning administrator {target.Name}", "server");

                    Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"You have received a permanent banhammer from administrator {player.Name}.", 30000);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.Center, $"You have received a permanent banhammer from the system for banning administrator {target.Name}.", 30000);

                    int AUUID = characterData.UUID;
                    GameLog.Ban(AUUID, targetCharacterData.UUID, targetLogin, DateTime.MaxValue, reason, true);
                    GameLog.Ban(-2, AUUID, accountData.Login, DateTime.MaxValue, $"Banned by the system for hardbanning administrator {target.Name}", true);

                    target.Kick(reason);
                    player.Kick("Banned by the system for hardbanning an administrator");
                }
                else
                {
                    if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                    {
                        Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");
                        Character.BindConfig.Repository.DeleteAdmin(player);
                        return;
                    }
                    if (Character.Repository.LoginsBlck.Contains(targetLogin))
                    {
                        Trigger.SendToAdmins(3, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to hardban {target.Name} ({target.Value}).");
                        BanMe(player, 0);
                        return;
                    }
                    if (!CheckMe(player, 4)) return;
                    DateTime unbanTime = (time >= 3650) ? DateTime.MaxValue : DateTime.Now.AddDays(time);
                    if (time >= 3650) Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} issued a permanent banhammer to player {target.Name}({target.Value}). Reason: {reason}", target);
                    else Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} issued a banhammer to player {target.Name}({target.Value}) for {time} days. Reason: {reason}", target);
                    Ban.Online(target, unbanTime, true, reason, player.Name);
                    EventSys.SendCoolMsg(target, "Administration", $"BANHAMMER", $"You have received a banhammer until {unbanTime.ToString()}. Reason: {reason}. See you later!", "", 15000);
                    //Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"You have received a banhammer until {unbanTime.ToString()}", 30000);
                    //Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"Reason: {reason}", 30000);
                    int AUUID = characterData.UUID;
                    int TUUID = targetCharacterData.UUID;
                    GameLog.Ban(AUUID, TUUID, targetLogin, unbanTime, reason, true);
                    target.Kick(reason);
                }
            }
            catch (Exception e)
            {
                Log.Write($"hardbanPlayer Exception: {e.ToString()}");
            }
        }
        public static void isHardPlayer(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Ishard)) return;
                if (player == target) return;

                if (!target.IsCharacterData()) return;
                Trigger.SetTask(async () =>
                {
                    try
                    {
                        var targetSessionData = target.GetSessionData();

                        if (targetSessionData == null) return;

                        var ban = await Ban.GetIsHard(targetSessionData.RealHWID);

                        if (ban != null)
                        {
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"The character's HWID matches the banned account {ban.Account}", 3000);
                            return;
                        }

                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Character's HWID not found in the ban list", 3000);

                    }
                    catch (Exception e)
                    {
                        Log.Write($"isHardPlayer Task.Run Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"isHardPlayer Exception: {e.ToString()}");
            }
        }
        public static void offBanPlayer(ExtPlayer player, string name, int time, string reason, bool isHard = false)
        {
            try
            {
                if (!isHard && !CommandsAccess.CanUseCmd(player, AdminCommands.Offban)) return;
                else if (isHard && !CommandsAccess.CanUseCmd(player, AdminCommands.Offhardban)) return;



                var accountData = player.GetAccountData();
                if (accountData == null) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (player.Name == name) return;



                ExtPlayer target = (ExtPlayer)NAPI.Player.GetPlayerFromName(name);

                if (target.IsCharacterData())
                {
                    if (isHard)

                    {

                        hardbanPlayer(player, target, time, reason);

                        Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offhardban was replaced with hardban", 3000);

                    }
                    else

                    {

                        hardbanPlayer(player, target, time, reason);

                        Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "The player was online, so offhardban was replaced with hardban", 3000);

                    }
                    return;
                }


                string prefix = "";
                if (isHard)
                    prefix = "hard";

                string[] split = name.Split("_");



                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");//On Separate Thread

                        var targetCharacter = await db.Characters
                            .Select(v => new
                            {
                                v.Uuid,
                                v.Firstname,
                                v.Lastname,
                                v.Adminlvl,
                            })
                            .Where(c => c.Firstname == split[0] && c.Lastname == split[1])
                            .FirstOrDefaultAsync();

                        Banneds ban = null;

                        if (targetCharacter != null)
                            ban = await Ban.GetBanToUUID(db, targetCharacter.Uuid);

                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                if (targetCharacter == null)
                                {
                                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Player not found", 3000);
                                    return;
                                }
                                if (targetCharacter.Adminlvl == 9)
                                {
                                    Trigger.SendToAdmins(1, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to {prefix}ban {name} (offline).");
                                    BanMe(player, 0);
                                    return;
                                }
                                if (targetCharacter.Adminlvl != 0 && targetCharacter.Adminlvl >= characterData.AdminLVL)
                                {

                                    string login = Main.GetLoginFromUUID(targetCharacter.Uuid);

                                    if (login != null && Character.Repository.LoginsBlck.Contains(login))
                                    {

                                        Trigger.SendToAdmins(3, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to {prefix}ban {name} (offline).");

                                        BanMe(player, 0);

                                        return;
                                    }

                                    Trigger.SendToAdmins(3, $"{ChatColors.StrongOrange}[A] {player.Name} ({player.Value}) banned {name} offline and was banned by the system.");



                                    Character.BindConfig.Repository.DeleteAdmin(player);



                                    Ban.OfflineBanToNickName(name, DateTime.MaxValue, isHard, reason, player.Name);

                                    Ban.Online(player, DateTime.MaxValue, isHard, $"Banned by the system for banning administrator {name}", "server");



                                    Notify.Send(player, NotifyType.Warning, NotifyPosition.Center, $"You have been permanently banned by the system for off{prefix}banning administrator {name}.", 30000);



                                    if (login != null) GameLog.Ban(characterData.UUID, targetCharacter.Uuid, login, DateTime.MaxValue, reason, isHard);

                                    else GameLog.Ban(characterData.UUID, targetCharacter.Uuid, "-", DateTime.MaxValue, reason, isHard);



                                    GameLog.Ban(-2, characterData.UUID, accountData.Login, DateTime.MaxValue, $"Banned by the system for off{prefix}banning administrator {name}", isHard);



                                    player.Kick("Banned by the system for banning an administrator");

                                    return;

                                }



                                if (ban != null)

                                {

                                    string hard = (ban.Ishard > 0) ? "hard " : "";

                                    Notify.Send(player, NotifyType.Warning, NotifyPosition.Center, $"The player is already {hard}banned", 3000);

                                    return;

                                }



                                if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))

                                {

                                    Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");

                                    Character.BindConfig.Repository.DeleteAdmin(player);

                                    return;

                                }



                                if (!CheckMe(player, 4)) return;

                                DateTime unbanTime = (time >= 3650) ? DateTime.MaxValue : DateTime.Now.AddDays(time);

                                if (isHard)
                                {
                                    if (time >= 3650) Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} issued a permanent banhammer to player {name} offline. Reason: {reason}", target);
                                    else Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} issued a banhammer to player {name} offline for {time} days. Reason: {reason}", target);
                                }
                                else
                                {
                                    if (time >= 3650) Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} banned character {name} offline forever. Reason: {reason}");
                                    else Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} banned character {name} offline for {time} days. Reason: {reason}");
                                }



                                Ban.OfflineBanToNickName(name, unbanTime, isHard, reason, player.Name);

                                string login1 = Main.GetLoginFromUUID(targetCharacter.Uuid);

                                if (login1 != null)
                                    GameLog.Ban(characterData.UUID, targetCharacter.Uuid, login1, unbanTime, reason, isHard);
                                else
                                    GameLog.Ban(characterData.UUID, targetCharacter.Uuid, "-", unbanTime, reason, isHard);

                            }
                            catch (Exception e)
                            {
                                Log.Write($"offBanPlayer NAPI.Task.Run Exception: {e.ToString()}");

                            }

                        });
                    }
                    catch (Exception e)
                    {
                        Log.Write($"offBanPlayer NAPI.Task.Run 1 Exception: {e.ToString()}");

                    }




                });
            }
            catch (Exception e)
            {
                Log.Write($"offBanPlayer Exception: {e.ToString()}");
            }
        }
        public static void unbanPlayer(ExtPlayer player, string name)
        {
            try
            {
                if (!Main.PlayerNames.Values.Contains(name))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "No name like this!", 3000);
                    return;
                }
                Trigger.SetTask(async () => {
                    try
                    {
                        var isBan = await Ban.Pardon(name);
                        if (!isBan)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"{name} is not banned!", 3000);
                            return;
                        }

                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                Trigger.SendToAdmins(1, $"~r~[A] {player.Name} ({player.Value}) unbanned {name}");

                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Player unbanned!", 3000);

                                GameLog.Admin($"{player.Name}", $"unban", $"{name}");
                            }
                            catch (Exception e)
                            {
                                Log.Write($"unbanPlayer NAPI.Task.Run Exception: {e.ToString()}");
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        Log.Write($"unbanPlayer Task.Run Exception: {e.ToString()}");
                    }

                });
            }
            catch (Exception e)
            {
                Log.Write($"unbanPlayer Exception: {e.ToString()}");
            }
        }
        public static void unhardbanPlayer(ExtPlayer player, string name)
        {
            try
            {
                if (!Main.PlayerNames.Values.Contains(name))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "No name like this!", 3000);
                    return;
                }

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        var isBan = await Ban.PardonHard(name);
                        if (!isBan)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"{name} is not hardbanned!", 3000);
                            return;
                        }

                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                Trigger.SendToAdmins(1, $"~r~[A] {player.Name} ({player.Value}) removed the hardban from {name}");

                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "The hardban has been removed from the player!", 3000);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"unhardbanPlayer NAPI.Task.Run Exception: {e.ToString()}");
                            }
                        });

                    }
                    catch (Exception e)
                    {
                        Log.Write($"unhardbanPlayer Task.Run Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"unhardbanPlayer Exception: {e.ToString()}");
            }
        }
        public static async void unbanIp(ExtPlayer player, string ip)
        {
            try
            {
                await using var db = new ServerBD("MainDB");//On Separate Thread

                var ban = await db.Banned
                    .Where(v => v.Ip == ip)
                    .FirstOrDefaultAsync();

                if (ban != null)
                {
                    await db.Banned
                        .Where(b => b.Ip == ip)
                        .Set(b => b.Ip, "-")
                        .UpdateAsync();

                    NAPI.Task.Run(() =>
                    {
                        Trigger.SendToAdmins(1, $"~r~[A] {player.Name} ({player.Value}) unblocked IP address: {ip}");
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "You have unblocked the IP address.", 3000);
                    });
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "This IP is not blocked.", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"unbanIp Exception: {e.ToString()}");
            }
        }
        public static void kickPlayer(ExtPlayer player, ExtPlayer target, string reason, bool isSilence)
        {
            try
            {

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (target == player) return;
                if (target.IsCharacterData())
                {
                    if (targetCharacterData.AdminLVL >= characterData.AdminLVL)
                    {
                        Trigger.SendToAdmins(3, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to kick {target.Name} ({target.Value}), who has a higher administrator level.");
                        return;
                    }
                }

                if (isSilence == true && Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                {
                    Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");
                    Character.BindConfig.Repository.DeleteAdmin(player);
                    return;
                }

                if (!CheckMe(player, 1)) return;
                if (!isSilence)
                {
                    Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} kicked player {target.Name}({target.Value}). Reason: {reason}", target);
                    GameLog.Admin($"{player.Name}", $"kickPlayer({reason})", $"{target.Name}");
                }
                else
                {
                    Trigger.SendToAdmins(1, "!{#FFB833}" + $"[A] {player.Name} silently kicked player {target.Name}({target.Value}).");

                    GameLog.Admin($"{player.Name}", $"skickPlayer", $"{target.Name}");
                }
                NAPI.Player.KickPlayer(target, reason);
            }
            catch (Exception e)
            {
                Log.Write($"kickPlayer Exception: {e.ToString()}");
            }
        }
        public static void warnPlayer(ExtPlayer player, ExtPlayer target, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Warn)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (player == target) return;
                if (targetCharacterData.AdminLVL >= characterData.AdminLVL)
                {
                    Trigger.SendToAdmins(3, "!{#FF0000}" + $"[A] {player.Name} ({player.Value}) tried to warn {target.Name} ({target.Value}), who has a higher administrator level.");
                    return;
                }
                if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                {
                    Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the punishment: {reason}");
                    Character.BindConfig.Repository.DeleteAdmin(player);
                    return;
                }
                if (!CheckMe(player, 3)) return;
                targetCharacterData.WarnInfo.Admin[targetCharacterData.Warns] = player.Name;
                targetCharacterData.WarnInfo.Reason[targetCharacterData.Warns] = reason;

                targetCharacterData.Warns++;
                targetCharacterData.Unwarn = DateTime.Now.AddDays(14);

                if (target.GetFractionId() > 0)
                    Fractions.Table.Logs.Repository.AddLogs(target, FractionLogsType.UnInvite, "Received a warning");

                target.RemoveFractionMemberData();
                target.ClearAccessories();
                Customization.ApplyCharacter(target);

                Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} issued a warning to player {target.Name}({target.Value}). | {targetCharacterData.Warns}/3. Reason: {reason}", target);

                if (targetCharacterData.Warns >= 3)
                {
                    DateTime unbanTime = DateTime.Now.AddMinutes(43200);
                    targetCharacterData.Warns = 0;
                    targetCharacterData.WarnInfo = new WarnInfo();
                    Ban.Online(target, unbanTime, false, "Warns 3/3", "Server");
                }
                GameLog.Admin($"{player.Name}", $"warnPlayer({reason})", $"{target.Name}");
                Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.RedAge, $"{player.Name} issued you a WARN | {targetCharacterData.Warns}/3. Reason: {reason}", DateTime.Now);
                target.Kick("Warning");
            }
            catch (Exception e)
            {
                Log.Write($"warnPlayer Exception: {e.ToString()}");
            }
        }

        public static void killTarget(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Kill)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (!target.IsCharacterData()) return;
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) killed player {target.Name} ({target.Value})", 1, "#636363", hideAdminLevel: 9);
                if (!targetSessionData.DeathData.IsDying) NAPI.Player.SetPlayerHealth(target, 0);
                else Ems.ReviveFunc(target);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You killed player {target.Name}({target.Value})", 3000);
                GameLog.Admin($"{player.Name}", $"killPlayer", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"killTarget Exception: {e.ToString()}");
            }
        }
        public static void ReviveMe(ExtPlayer player)
        {
            try
            {

                var sessionData = player.GetSessionData();

                if (sessionData == null) return;
                if (!sessionData.DeathData.IsDying) return;
                sessionData.DeathData.IsReviving = false;
                Ems.ReviveFunc(player, true);
            }
            catch (Exception e)
            {
                Log.Write($"ReviveMe Exception: {e.ToString()}");
            }
        }

        public static void reviveTarget(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Revive)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (!targetSessionData.DeathData.IsDying)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The player is not dead.", 3000);
                    return;
                }
                int id = target.Value;
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) revived player {target.Name} ({target.Value})", 2, "#636363");
                GameLog.Admin($"{player.Name}", $"revivePlayer", $"{target.Name}");
                EventSys.SendCoolMsg(target, "Administration", "Revival", $"Administrator {player.Name} revived you", "", 7000);
                //Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"Administrator {player.Name} revived you", 3000);
                ReviveMe(target);
                NAPI.Player.SetPlayerHealth(target, 100);
            }
            catch (Exception e)
            {
                Log.Write($"reviveTarget Exception: {e.ToString()}");
            }
        }
        public static void healTarget(ExtPlayer player, ExtPlayer target, int hp)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Sethp)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (!target.IsCharacterData() || targetSessionData.DeathData.IsDying || hp < 0 || hp > 100) return;
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) set HP({hp}) for {target.Name} ({target.Value})", 1, "#636363", hideAdminLevel: 9);
                NAPI.Player.SetPlayerHealth(target, hp);
                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"Administrator {player.Name} changed your health level to {hp}.", 3000);
                GameLog.Admin($"{player.Name}", $"healPlayer({hp})", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"healTarget Exception: {e.ToString()}");
            }
        }
        public static void armorTarget(ExtPlayer player, ExtPlayer target, int ar)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Setar)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (ar <= 0 || ar > 100) return;
                if (Chars.Repository.itemCount(target, "inventory", ItemId.BodyArmor) < Chars.Repository.maxItemCount)
                {
                    Chars.Repository.AddNewItem(target, $"char_{targetCharacterData.UUID}", "inventory", ItemId.BodyArmor, 1, ar.ToString());
                    Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) gave Armor({ar}) to {target.Name} ({target.Value})");
                    GameLog.Admin($"{player.Name}", $"armorPlayer({ar})", $"{target.Name}");
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The player already has armor in their inventory.", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"armorTarget Exception: {e.ToString()}");
            }
        }
        public static void checkGodmode(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Gm)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (!target.IsCharacterData() || targetSessionData.DeathData.IsDying) return;
                int targetHealth = target.Health;
                AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) checked {target.Name} ({target.Value}) for GM");
                target.Eval($"global.localplayer.applyDamageTo({targetHealth - 1}, true);");
                NAPI.Task.Run(() =>
                {
                    try
                    {
                        if (!target.IsCharacterData()) return;
                        if (target.Health >= targetHealth) Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, $"{target.Name} ({target.Value}) might have GM", 3000);
                        else
                        {
                            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, $"{target.Name} ({target.Value}) does not have GM", 3000);
                            target.Health = targetHealth;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Write($"checkGodmode Task Exception: {e.ToString()}");
                    }
                }, 500);
                GameLog.Admin($"{player.Name}", $"checkGm", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"checkGodmode Exception: {e.ToString()}");
            }
        }

        public static void slapPlayer(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Slap)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (!target.IsCharacterData() || targetSessionData.DeathData.IsDying) return;
                NAPI.Entity.SetEntityPosition(target, target.Position + new Vector3(0, 0, 5));
                Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) slapped {target.Name} ({target.Value})");
                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"Administrator {player.Name} slapped you", 3000);
                GameLog.Admin($"{player.Name}", $"Slap", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"slapPlayer Exception: {e.ToString()}");
            }
        }

        public static void checkMoney(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Checkmoney)) return;

                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;

                var targetCharacterData = target.GetCharacterData();
                if (targetCharacterData == null)
                    return;

                MoneySystem.Bank.Data bankAcc = MoneySystem.Bank.Get(Main.PlayerBankAccs[target.Name]);

                int bankMoney = 0;
                if (bankAcc != null)
                    bankMoney = (int)bankAcc.Balance;

                Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, $"{target.Name} has ${MoneySystem.Wallet.Format(targetCharacterData.Money)} | Bank: ${MoneySystem.Wallet.Format(bankMoney)}", 3000);
                Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) checked the funds of {target.Name} ({target.Value})");
                GameLog.Admin($"{player.Name}", $"checkMoney", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"checkMoney Exception: {e.ToString()}");
            }
        }

        public static void teleportTargetToPlayer(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Metp)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null)

                    return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null)
                    return;

                if (targetCharacterData.AdminLVL >= 6)
                {

                    var targetAdminConfig = targetCharacterData.ConfigData.AdminOption;
                    if (targetAdminConfig.HideMe && characterData.AdminLVL < targetCharacterData.AdminLVL)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Player with this ID not found", 3000);
                        Notify.Send(target, NotifyType.Alert, NotifyPosition.BottomCenter, $"{player.Name} ({player.Value}) tried to teleport you to them (/metp)", 3000);
                        return;
                    }
                }
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) teleported {target.Name} ({target.Value}) to themselves", 1, "#636363");
                GameLog.Admin($"{player.Name}", $"metp({player.Position.X}, {player.Position.Y}, {player.Position.Z})", $"{target.Name}");
                NAPI.Entity.SetEntityPosition(target, player.Position);
                Trigger.Dimension(target, UpdateData.GetPlayerDimension(player));
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You teleported {target.Name} to you", 3000);
                EventSys.SendCoolMsg(target, "Teleport", "Teleport", $"{player.Name} teleported you to them", "", 7000);
                //Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"{player.Name} teleported you to them", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"teleportTargetToPlayer Exception: {e.ToString()}");
            }
        }

        public static void freezeTarget(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Fz)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!target.IsCharacterData()) return;
                Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) froze {target.Name} ({target.Value})");
                Trigger.ClientEvent(target, "freeze", true);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have frozen player {target.Name}", 3000);
                GameLog.Admin($"{player.Name}", $"freeze", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"freezeTarget Exception: {e.ToString()}");
            }
        }
        public static void unFreezeTarget(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.UnFz)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!target.IsCharacterData()) return;
                Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) unfroze {target.Name} ({target.Value})");
                Trigger.ClientEvent(target, "freeze", false);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You have unfrozen player {target.Name}", 3000);
                GameLog.Admin($"{player.Name}", $"unfreeze", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"unFreezeTarget Exception: {e.ToString()}");
            }
        }

        public static void giveTargetGun(ExtPlayer player, ExtPlayer target, string weapon, string serial)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Givegun)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!target.IsCharacterData()) return;
                if (serial.Length != 9)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The serial number must be 9 characters long", 3000);
                    return;
                }
                Regex rg = new Regex(@"^[a-z0-9]+$", RegexOptions.IgnoreCase);
                if (!rg.IsMatch(serial))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Invalid serial number input!", 3000);
                    return;
                }
                ItemId wType = (ItemId)Enum.Parse(typeof(ItemId), weapon);
                if (wType == ItemId.CarCoupon) return;
                if ((Chars.Repository.StrongWeapons.Contains(wType) || Chars.Repository.HeavyWeapons.Contains(wType)) && characterData.AdminLVL <= 5) return;
                if (characterData.AdminLVL >= 8 || (characterData.AdminLVL <= 7 && (Chars.Repository.ItemsInfo[wType].functionType == newItemType.Weapons || Chars.Repository.ItemsInfo[wType].functionType == newItemType.MeleeWeapons)))
                {
                    serial = serial.Replace('\\', '\0');
                    serial = serial.Replace('\'', '\0');
                    serial = serial.Replace('/', '\0');
                    if (WeaponRepository.GiveWeapon(target, wType, serial) == -1) return;
                    AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) gave weapon ({weapon}|{serial}) to {target.Name} ({target.Value})");
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You gave player {target.Name} a weapon ({weapon})", 3000);
                    GameLog.Admin($"{player.Name}", $"giveGun({weapon},{serial})", $"{target.Name}");
                }
                else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Available from admin level 8.", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"giveTargetGun Exception: {e.ToString()}");
            }
        }

        public static void giveTargetCar(ExtPlayer player, ExtPlayer target, string vehicle)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Carcoupon)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (Chars.Repository.isFreeSlots(target, ItemId.CarCoupon) != 0) return;
                Chars.Repository.AddNewItem(target, $"char_{targetCharacterData.UUID}", "inventory", ItemId.CarCoupon, 1, vehicle);
                AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) gave a car coupon ({vehicle}) to {target.Name} ({target.Value})");
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You gave player {target.Name} a car coupon ({vehicle})", 3000);
                GameLog.Admin($"{player.Name}", $"giveCarC({vehicle})", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"giveTargetCar Exception: {e.ToString()}");
            }
        }

        public static void giveTargetSkin(ExtPlayer player, ExtPlayer target, string pedModel)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Setskin)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (pedModel.Equals("-1"))
                {
                    if (targetSessionData.AdminSkin)
                    {
                        targetSessionData.AdminSkin = false;
                        target.SetDefaultSkin();
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "You have restored the player's appearance", 3000);
                    }
                    else
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The player's appearance was not changed", 3000);
                        return;
                    }
                }
                else
                {
                    PedHash pedHash = NAPI.Util.PedNameToModel(pedModel);
                    if (pedHash != 0)
                    {
                        targetSessionData.AdminSkin = true;
                        target.SetSkin(pedHash);
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You changed player {target.Name}'s appearance to ({pedModel})", 3000);
                    }
                    else
                    {
                        if (characterData.AdminLVL <= 7) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "An appearance with this name was not found", 3000);
                        else
                        {
                            targetSessionData.AdminSkin = true;
                            target.SetSkin(NAPI.Util.GetHashKey(pedModel));
                            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You changed player {target.Name}'s appearance to ({pedModel})", 3000);
                        }
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"giveTargetSkin Exception: {e.ToString()}");
            }
        }
        public static void giveTargetClothes(ExtPlayer player, ExtPlayer target, string weapon, string serial)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Giveclothes)) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (serial.Length < 6 || serial.Length > 12)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The serial number must be 6-12 characters long", 3000);
                    return;
                }
                ItemId wType = (ItemId)Enum.Parse(typeof(ItemId), weapon);
                if (Chars.Repository.ItemsInfo[wType].functionType != newItemType.Clothes)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "This command can only be used to give clothing items", 3000);
                    return;
                }
                try
                {
                    serial = serial.Replace('\\', '\0');
                    serial = serial.Replace('\'', '\0');
                    serial = serial.Replace('/', '\0');
                }
                catch { Log.Write("giveTargetClothes ERROR"); }
                if (Chars.Repository.AddNewItem(target, $"char_{targetCharacterData.UUID}", "inventory", wType, 1, serial) == -1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"The player does not have enough inventory space", 3000);
                    return;
                }
                GameLog.Admin($"{player.Name}", $"giveClothes({weapon},{serial})", $"{target.Name}");
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You gave player {target.Name} clothing ({weapon.ToString()})", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"giveTargetClothes Exception: {e.ToString()}");
            }
        }
        public static void takeTargetGun(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Delgun)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!target.IsCharacterData()) return;
                Chars.Repository.RemoveAllWeapons(target, true);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"You took all weapons from player {target.Name}", 3000);
                Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) took all weapons from {target.Name} ({target.Value})");
                GameLog.Admin($"{player.Name}", $"takeGuns", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"takeTargetGun Exception: {e.ToString()}");
            }
        }
        #region HCmds
        public static void CMD_Cnum(ExtPlayer player, ExtPlayer target, string a)
        {
            try
            {

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!target.IsCharacterData()) return;
                if (characterData.AdminLVL <= 8 || !NewCasino.Roullete.Roulette.ContainsValue(a) || !NewCasino.Roullete.PlayerData.ContainsKey(target)) return;
                if (NewCasino.Roullete.RouletteTables.Count <= NewCasino.Roullete.PlayerData[target].SelectedTable) return;
                NewCasino.Table table = NewCasino.Roullete.RouletteTables[NewCasino.Roullete.PlayerData[target].SelectedTable];
                if (table.Process) return;
                KeyValuePair<int, string> pair = NewCasino.Roullete.Roulette.FirstOrDefault(p => p.Value.Equals(a));
                if (pair.Equals(default(KeyValuePair<int, string>))) return;
                table.Win = pair.Key;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"{pair.Value}", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Cnum Exception: {e.ToString()}");
            }
        }
        public static void CMD_Chnum(ExtPlayer player, int a)
        {
            try
            {

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (characterData.AdminLVL <= 8 || a < 1 || a > 6 || NewCasino.Horses.curentScreen != 0) return;
                NewCasino.Horses.WinHorse = a;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Horse #{a}", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Chnum Exception: {e.ToString()}");
            }
        }
        public static void CMD_Cinum(ExtPlayer player, int a, byte b, byte c)
        {
            try
            {

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (characterData.AdminLVL <= 8) return;
                ExtPlayer target = Main.GetPlayerByID(a);

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (!target.IsCharacterData()) return;
                if (b == 255 || c == 255)
                {
                    targetSessionData.CaseWin = 255;
                    targetSessionData.CaseItemWin = 255;
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "RESET", 1000);
                    return;
                }
                if (b >= Chars.Repository.RouletteCasesData.Count || Chars.Repository.RouletteCasesData[b].RouletteItemsData.Count <= c) return;
                RouletteItemData p = Chars.Repository.RouletteCasesData[b].RouletteItemsData[c];
                if (p == null) return;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"{Chars.Repository.RouletteCasesData[b].Name} - {p.Name}", 1000);
                targetSessionData.CaseWin = b;
                targetSessionData.CaseItemWin = c;
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Cinum Exception: {e.ToString()}");
            }
        }
        #endregion
        public static void adminSMS(ExtPlayer player, ExtPlayer target, string message)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Asms)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!target.IsCharacterData() || player == target) return;
                Trigger.SendChatMessage(target, $"{ChatColors.Report}Administrator {player.Name} ({player.Value}): {message}");
                Trigger.SendToAdmins(characterData.AdminLVL, $"{ChatColors.Report}[A][ASMS] {player.Name} ({player.Value}) to {target.Name} ({target.Value}): {message}");
                //Notify.Send(target, NotifyType.Info, NotifyPosition.TopCenter, $"Administrator {player.Name}: {message}", 8000);
                EventSys.SendCoolMsg(target, "Administration", $"{player.Name}", $"{message}", "", 8000);
                GameLog.Admin($"{player.Name}", $"aSMS({message})", $"{target.Name}");
                Trigger.ClientEvent(target, "StartDangerButtonSound_client", "sounds/icq.mp3");
            }
            catch (Exception e)
            {
                Log.Write($"adminSMS Exception: {e.ToString()}");
            }
        }
        public static void checkKill(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Checkkill)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;

                var targetSessionData = target.GetSessionData();

                if (targetSessionData == null) return;
                if (targetSessionData.DeathData.LastDeath == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"{target.Name} has no last death data.", 2500);
                    return;
                }
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, targetSessionData.DeathData.LastDeath, 7000);
                Admin.AdminLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) checked the last death data of {target.Name} ({target.Value})");
                GameLog.Admin($"{player.Name}", $"checkKill", $"{target.Name}");
            }
            catch (Exception e)
            {
                Log.Write($"checkKill Exception: {e.ToString()}");
            }
        }
        public static void adminChat(ExtPlayer player, string message)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.A)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                GameLog.AddInfo($"(AChat) player({characterData.UUID}) {message}");

                message = (characterData.AdminLVL >= 6) ? $"{ChatColors.AdminChat}[A] {ChatColors.HAdmin}{player.Name}{ChatColors.AdminChat} ({player.Value}): {message}" : $"{ChatColors.AdminChat}[A] {ChatColors.LAdmin}{player.Name}{ChatColors.AdminChat} ({player.Value}): {message}";

                foreach (ExtPlayer foreachPlayer in Main.AllAdminsOnline)
                {

                    var foreachCharacterData = foreachPlayer.GetCharacterData();

                    if (foreachCharacterData == null) continue;
                    if (foreachCharacterData.AdminLVL >= 1) Trigger.SendChatMessage(foreachPlayer, message);
                }
            }
            catch (Exception e)
            {
                Log.Write($"adminChat Exception: {e.ToString()}");
            }
        }
        public static void adminGlobal(ExtPlayer player, string message)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Global)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (characterData.AdminLVL <= 6)
                {
                    if (Main.stringGlobalBlock.Any(c => message.Contains(c)))
                    {
                        Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {player.Name} ({player.Value}) was removed by the system for the reason in the global announcement: {message}");
                        Character.BindConfig.Repository.DeleteAdmin(player);
                        return;
                    }
                }
                if (characterData.AdminLVL >= 6)
                {
                    NAPI.Chat.SendChatMessageToAll($"{CommandsAccess.AdminPrefixChat}{player.Name.Replace('_', ' ')}: {message}");
                    GameLog.Admin($"{player.Name}", $"global({message})", "null");
                    EventSys.SendPlayersToEvent("Administration", $"{player.Name}", $"{message}", "", 10000);
                }
                else
                {
                    GlobalQueue.Add(GlobalID, (message, player.Name, characterData.AdminLVL, DateTime.Now.AddSeconds(60)));
                    Trigger.SendToAdmins(2, "!{#FFB833}" + $"[A] Request from {player.Name} ({player.Value}) for global({message}). To confirm, type: /accept {GlobalID}");
                    GlobalID++;
                }
            }
            catch (Exception e)
            {
                Log.Write($"adminGlobal Exception: {e.ToString()}");
            }
        }

        public static void adminGlobalAccept(ExtPlayer player, int id)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Accept)) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (!GlobalQueue.ContainsKey(id))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Unfortunately, a global confirmation request with this ID does not exist.", 3000);
                    return;
                }
                if (GlobalQueue[id].Item4 < DateTime.Now)
                {
                    GlobalQueue.Remove(id);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The request time has expired (60 seconds).", 3000);
                    return;
                }
                if (GlobalQueue[id].Item2 == player.Name)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Only another administrator can confirm!", 3000);
                    return;
                }
                AdminsLog(characterData.AdminLVL, $"{player.Name} ({player.Value}) confirmed the request for global({GlobalQueue[id].Item2})");
                NAPI.Chat.SendChatMessageToAll($"{CommandsAccess.AdminPrefixChat}{GlobalQueue[id].Item2.Replace('_', ' ')}: {GlobalQueue[id].Item1}");
                GameLog.Admin($"{GlobalQueue[id].Item2}", $"global({GlobalQueue[id].Item1})", "null");
                GameLog.Admin($"{player.Name}", $"globalAccept({GlobalQueue[id].Item2})", "null");
                GlobalQueue.Remove(id);
            }
            catch (Exception e)
            {
                Log.Write($"adminGlobalAccept Exception: {e.ToString()}");
            }
        }
        public static void sendPlayerToDemorgan(ExtPlayer admin, ExtPlayer target, int time, string reason)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(admin, AdminCommands.Jail)) return;
                var targetSessionData = target.GetSessionData();
                if (targetSessionData == null) return;
                if (!target.IsCharacterData()) return;
                if (admin == target) return;

                if (time < 5 || time > 10080)
                {
                    Notify.Send(admin, NotifyType.Error, NotifyPosition.BottomCenter, $"You cannot issue a Jail for more than 10080 minutes", 3000);
                    return;
                }
                if (Main.stringGlobalBlock.Any(c => reason.Contains(c)))
                {
                    Trigger.SendToAdmins(1, $"{ChatColors.Red}[A] {admin.Name} ({admin.Value}) was removed by the system for the reason in the punishment: {reason}");
                    Character.BindConfig.Repository.DeleteAdmin(admin);
                    return;
                }
                if (!CheckMe(admin, 2)) return;
                int firstTime = time * 60;
                string deTimeMsg = " minutes";
                if (time > 60)
                {
                    deTimeMsg = " hours";
                    time /= 60;
                    if (time > 24)
                    {
                        deTimeMsg = " days";
                        time /= 24;
                    }
                }
                var targetCharacterData = target.GetCharacterData();

                Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{admin.Name} jailed {target.Name}({target.Value}) for {time}{deTimeMsg}. Reason: {reason}", target);

                targetCharacterData.DemorganInfo.Admin = admin.Name;
                targetCharacterData.DemorganInfo.Reason = reason;
                targetCharacterData.ArrestTime = 0;
                targetCharacterData.ArrestType = 0;
                targetCharacterData.DemorganTime = firstTime;
                //
                VehicleManager.WarpPlayerOutOfVehicle(target, false);
                //
                FractionCommands.unCuffPlayer(target);
                //
                targetSessionData.CuffedData.CuffedByCop = false;
                targetSessionData.CuffedData.CuffedByMafia = false;
                //
                target.Position = DemorganPositions[Main.rnd.Next(55)] + new Vector3(0, 0, 1.5);
                target.SetSkin(DemorganSkins[Main.rnd.Next(14)]);
                //
                if (targetSessionData.TimersData.ArrestTimer != null) Timers.Stop(targetSessionData.TimersData.ArrestTimer);
                targetSessionData.TimersData.ArrestTimer = Timers.Start(1000, () => timer_demorgan(target));
                targetCharacterData.VoiceMuted = true;
                target.SetSharedData("vmuted", true);
                //
                Trigger.ClientEvent(target, "client.demorgan", true);
                target.Eval($"mp.game.audio.playSoundFrontend(-1, \"Deliver_Pick_Up\", \"HUD_FRONTEND_MP_COLLECTABLE_SOUNDS\", true);");
                target.SetSharedData("HideNick", true);
                //
                NAPI.Player.SetPlayerHealth(target, 3);
                Chars.Repository.RemoveAllWeapons(target, true, true, armour: true);
                GameLog.Admin(admin.Name, $"demorgan({time}{deTimeMsg},{reason})", target.Name);
                //Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.RedAge,$"{admin.Name} sent you to demorgan for {time}{deTimeMsg}. Reason: {reason}", DateTime.Now); 
                EventSys.SendCoolMsg(target, "Administration", $"{admin.Name}", $"Sent you to demorgan for {time}{deTimeMsg}. Reason: {reason}", "", 15000);
                //
            }
            catch (Exception e)
            {
                Log.Write($"sendPlayerToDemorgan Exception: {e.ToString()}");
            }
        }
        public static void releasePlayerFromDemorgan(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Unjail)) return;

                var targetCharacterData = target.GetCharacterData();

                if (targetCharacterData == null) return;
                if (targetCharacterData.DemorganTime <= 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "The player is not in the special jail.", 3000);
                    return;
                }
                targetCharacterData.DemorganTime = 1;
                Trigger.SendPunishment($"{CommandsAccess.AdminPrefix}{player.Name} released {target.Name} ({target.Value}) from demorgan");
                GameLog.Admin($"{player.Name}", $"undemorgan", $"{target.Name}");
                //Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.RedAge,$"{player.Name} released you from demorgan.", DateTime.Now);
                EventSys.SendCoolMsg(target, "Administration", $"{player.Name}", $"Released you from demorgan. Enjoy the game and don't break the rules again! :)", "", 15000);
            }
            catch (Exception e)
            {
                Log.Write($"releasePlayerFromDemorgan Exception: {e.ToString()}");
            }
        }

        public static PedHash[] DemorganSkins = new PedHash[14]
        {
            //Land Animals
            PedHash.Cat,
            PedHash.Chop,
            PedHash.Husky,
            PedHash.Poodle,
            PedHash.Pug,
            PedHash.Rabbit,
            PedHash.Retriever,
            PedHash.Rottweiler,
            PedHash.Shepherd,
            PedHash.Westy,
            //Birds
            PedHash.Seagull,
            PedHash.ChickenHawk,
            PedHash.Crow,
            PedHash.Pigeon
        };

        public static Vector3[] DemorganPositions = new Vector3[55]
        {
            new Vector3(-323.5328, 1370.41, 346.4068),
            new Vector3(791.201, 2311.601, 47.13044),
            new Vector3(383.2664, 773.5862, 183.8618),
            new Vector3(1301.362, 1447.964, 98.23167),
            new Vector3(-78.97045, 6597.737, 28.42492),
            new Vector3(1266.669, 1911.738, 77.65078),
            new Vector3(-1852.087, 1931.88, 149.0496),
            new Vector3(125.2143, 6593.171, 30.85273),
            new Vector3(-2599.767, 1689.187, 139.9411),
            new Vector3(1404.253, 2169.581, 96.66694),
            new Vector3(-2291.825, 2485.236, 1.914838),
            new Vector3(-44.12902, 6299.675, 30.50067),
            new Vector3(-98.26251, 2801.208, 52.12306),
            new Vector3(-248.8021, 6200.921, 30.36921),
            new Vector3(-396.2312, 6053.582, 30.38009),
            new Vector3(1212.61, 2651.768, 36.69186),
            new Vector3(143.9709, 1669.224, 227.5427),
            new Vector3(-514.4248, 6275.275, 8.819883),
            new Vector3(1471.917, 1318.306, 113.9579),
            new Vector3(1469.517, 2718.285, 36.45533),
            new Vector3(1316.378, 653.1463, 81.45456),
            new Vector3(2497.466, 1582.359, 31.60028),
            new Vector3(-214.658, 6561.28, 9.756557),
            new Vector3(2531.433, 2039.337, 18.69318),
            new Vector3(2062.655, 3481.989, 42.38651),
            new Vector3(2545.573, 2637.896, 36.82483),
            new Vector3(112.0649, 6842.445, 15.012),
            new Vector3(2682.535, 2803.837, 39.21159),
            new Vector3(2155.056, 3018.603, 44.15971),
            new Vector3(291.8854, 6793.59, 14.57676),
            new Vector3(1876.9, 3967.318, 35.13688),
            new Vector3(2446.471, 3768.721, 39.95733),
            new Vector3(778.8936, 6439.555, 30.84892),
            new Vector3(-1543.886, 1366.205, 124.9902),
            new Vector3(410.5818, 6457.222, 27.68898),
            new Vector3(-788.4865, 1672.407, 198.4508),
            new Vector3(2538.595, 4477.067, 36.10785),
            new Vector3(-262.9102, 2608.396, 62.22501),
            new Vector3(221.4821, 2961.687, 41.5982),
            new Vector3(485.4037, 3501.335, 33.06005),
            new Vector3(1557.167, 3753.676, 33.27285),
            new Vector3(2852.622, 3714.04, 47.53786),
            new Vector3(2732.895, 4414.241, 46.5253),
            new Vector3(2883.424, 347.9111, 1.930261),
            new Vector3(2943.639, 4655.853, 47.42477),
            new Vector3(2762.99, 1264.434, 2.839344),
            new Vector3(2083.99, 3838.34, 30.67446),
            new Vector3(2374.62, 2427.864, 61.85823),
            new Vector3(2872.061, 4873.417, 61.47282),
            new Vector3(1544.658, 3911.407, 30.57072),
            new Vector3(1801.522, 4524.824, 30.87487),
            new Vector3(-133.3322, 6336.445, 30.37035),
            new Vector3(-179.7547, 4295.339, 31.85094),
            new Vector3(-261.8268, 6303.704, 31.166),
            new Vector3(-345.2491, 6150.061, 30.363)
        };
        public static void timer_demorgan(ExtPlayer player)
        {
            try
            {

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (characterData.DemorganTime-- <= 0) Fractions.FractionCommands.freePlayer(player, true);
            }
            catch (Exception e)
            {
                Log.Write($"timer_demorgan Exception: {e.ToString()}");
            }
        }
        public static void timer_mute(ExtPlayer player)
        {
            try
            {

                var sessionData = player.GetSessionData();

                if (sessionData == null) return;

                var characterData = player.GetCharacterData();

                if (characterData == null) return;
                if (characterData.Unmute-- <= 0)
                {
                    if (sessionData.TimersData.MuteTimer != null)
                    {
                        Timers.Stop(sessionData.TimersData.MuteTimer);
                        sessionData.TimersData.MuteTimer = null;
                        characterData.Unmute = 0;
                        if (characterData.DemorganTime <= 0)
                        {
                            NAPI.Task.Run(() =>
                            {
                                try
                                {
                                    if (characterData == null || player == null) return;
                                    characterData.VoiceMuted = false;
                                    player.SetSharedData("vmuted", false);
                                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Mute has been removed, don't break the rules anymore!", 3000);
                                }
                                catch (Exception e)
                                {
                                    Log.Write($"timer_mute Task Exception: {e.ToString()}");
                                }
                            });
                        }
                        else Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Mute has been removed, but you will be able to use voice chat after your other punishment expires.", 10000);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"timer_mute Exception: {e.ToString()}");
            }
        }
        /*
        public static void respawnAllCars(Player player)
        {
            try
            {
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Spvehall)) return;
                List<Vehicle> all_vehicles = NAPI.Pools.GetAllVehicles();

                foreach (Vehicle vehicle in all_vehicles)
                {
                    if (VehicleManager.GetVehicleOccupants(vehicle).Count >= 1) continue;
                    if (vehicleLocalData != null)
                    {
                        VehicleStreaming.VehiclesData data = vehicle.GetVehicleLocalData();

                        switch (vehicleLocalData.Access)
                        {
                            case "FRACTION":
                                RespawnFractionCar(vehicle);
                                break;
                            case "GANGDELIVERY":
                            case "MAFIADELIVERY":
                            case "BIKERDELIVERY":
                                VehicleStreaming.DeleteVehicle(vehicle);
                                break;
                            default:
                                continue;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"respawnAllCars Exception: {e.ToString()}");
            }
        }
        */

        [RemoteEvent("saveEspState")]
        public void ClientEvent_SaveEspState(ExtPlayer player, byte state)
        {
            try
            {

                var characterData = player.GetCharacterData();

                if (characterData == null)

                    return;



                var adminConfig = characterData.ConfigData.AdminOption;
                adminConfig.ESP = state;
            }
            catch (Exception e)
            {
                Log.Write($"ClientEvent_SaveEspState Exception: {e.ToString()}");
            }
        }
        public static void RespawnFractionCar(ExtVehicle vehicle)
        {
            try
            {
                var vehicleLocalData = vehicle.GetVehicleLocalData();
                if (vehicleLocalData != null)
                {
                    if (vehicleLocalData.LoaderMats != null)
                    {
                        ExtPlayer loader = vehicleLocalData.LoaderMats;
                        var loaderSessionData = loader.GetSessionData();

                        if (loaderSessionData != null)
                        {
                            Notify.Send(loader, NotifyType.Warning, NotifyPosition.BottomCenter, $"Material loading canceled because the vehicle left the checkpoint", 3000);

                            if (loaderSessionData.TimersData.LoadMatsTimer != null)
                            {
                                Timers.Stop(loaderSessionData.TimersData.LoadMatsTimer);
                                loaderSessionData.TimersData.LoadMatsTimer = null;
                            }
                        }
                        vehicleLocalData.LoaderMats = null;
                    }
                    Fractions.Configs.RespawnFractionCar(vehicle);
                }
            }
            catch (Exception e)
            {
                Log.Write($"RespawnFractionCar Exception: {e.ToString()}");
            }
        }
    }

    public class Group
    {
        public static string[] GroupNames = new string[6]
        {
            "Player",
            "Silver VIP",
            "Gold VIP",
            "Platinum VIP",
            "Diamond VIP",
            "Media VIP",
        };
        public static float[] GroupPayAdd = new float[6]
        {
            1.0f,
            1.0f,
            1.15f,
            1.25f,
            1.35f,
            1.35f,
        };
        public static int[] GroupAddPayment = new int[6]
        {
            0,
            50,
            70,
            110,
            200,
            200,
        };

        public static int[] GroupMaxBusinesses = new int[6]
        {
            1,
            1,
            1,
            1,
            1,
            1,
        };
        public static int[] GroupEXP = new int[6]
        {
            1,
            1,
            2,
            2,
            3,
            3,
        };
    }
}
