using GTANetworkAPI;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.GUI;
using NeptuneEvo.Core;
using System.Collections.Generic;
using NeptuneEvo.Houses;
using Newtonsoft.Json;
using System.Data;
using System.Linq;
using NeptuneEvo.Fractions;
using NeptuneEvo.MoneySystem;
using MySqlConnector;
using System.Threading;
using NeptuneEvo.Events;
using Database;
using LinqToDB;
using NeptuneEvo.Functions;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using NeptuneEvo.Accounts;
using NeptuneEvo.Players.Models;
using NeptuneEvo.Players;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Character;
using System.Threading.Tasks;
using Localization;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players.Phone.Messages.Models;
using NeptuneEvo.Players.Popup.List.Models;
using NeptuneEvo.Quests;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;


namespace NeptuneEvo.Chars
{
    /// <summary>
    /// Handling character data
    /// </summary>
    class Repository : Script
    {
        /// <summary>
        /// Logger
        /// </summary>
        private static readonly nLog Log = new nLog("Chars.Repository");

        [ServerEvent(Event.PlayerDeath)]
        public void onPlayerDeathHandler(ExtPlayer player, ExtPlayer entityKiller, uint weapon)
        {
            try
            {
                RouletteClose(player);
                ChangeAutoNumberClear(player);
            }
            catch (Exception e)
            {
                Log.Write($"onPlayerDeathHandler Exception: {e.ToString()}");
            }
        }

        #region trade
        /// <summary>
        /// Trade
        /// </summary>
        /// <param name="player">Player</param>
        /// <param name="target">Target login</param>
        /// <param name="type">Type of trade</param>
        /// <returns>Trade event</returns>
        public static TradeCharacterResponse TradeCharacter(ExtPlayer player, ExtPlayer target, string type)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return TradeCharacterResponse.Error; 
                var characterData = player.GetCharacterData();
                if (characterData == null) return TradeCharacterResponse.Error;
                else if (type == "business" && characterData.BizIDs.Count == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouHaveNoBusiness), 3000);
                    return TradeCharacterResponse.Error;
                }
                var targetSessionData = target.GetSessionData();
                if (targetSessionData == null) return TradeCharacterResponse.Error;
                var targetCharacterData = target.GetCharacterData();
                if (targetCharacterData == null) return TradeCharacterResponse.Error;
                else if (type == "business" && targetCharacterData.BizIDs.Count == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PersonHaveNoBusiness), 3000);
                    return TradeCharacterResponse.Error;
                }
                else if(targetSessionData.SellItemData.Seller != null || targetSessionData.SellItemData.Buyer != null) return TradeCharacterResponse.ErrorTrade;
                else if (TradeGet(player) || TradeGet(target)) return TradeCharacterResponse.ErrorTrade;
                else if (Main.IHaveDemorgan(player, true)) return TradeCharacterResponse.Error;
                sessionData.TradePropertyData = new TradePropertyData(target, DateTime.Now.AddSeconds(10));
                targetSessionData.TradePropertyData = new TradePropertyData(player, DateTime.Now.AddSeconds(10));
                return TradeCharacterResponse.Fine;
            }
            catch (Exception e)
            {
                Log.Write($"TradeCharacter Exception: {e.ToString()}");
                return TradeCharacterResponse.Error;
            }
        }
        public static void TradeClear(ExtPlayer player, bool message = true)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                if (dataTrade != null)
                {
                    var targetSessionData = dataTrade.Player.GetSessionData();
                    if (targetSessionData != null && targetSessionData.TradePropertyData != null)
                    {
                        if (message) Notify.Send(dataTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCancel), 3000);
                        targetSessionData.TradePropertyData = null;
                    }
                    dataTrade = null;
                }
            }
            catch (Exception e)
            {
                Log.Write($"TradeClear Exception: {e.ToString()}");
            }
        }
        public static bool TradeGet(ExtPlayer player)
        {
            try
            {
                /*if (!player.IsCharacterData()) return true;
                else if (PlayerTrade.ContainsKey(player.Value) && DateTime.Now >= PlayerTrade[player.Value].Time)
                {
                    TradeCharacterData dataTrade = PlayerTrade[player.Value];
                    if (isOnlineCharacter(dataTrade.Player) && PlayerTrade.ContainsKey(dataTrade.Player.Value))
                    {
                        if (DateTime.Now >= PlayerTrade[dataTrade.Player.Value].Time)
                        {
                            Notify.Send(dataTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCancel), 3000);
                            if (PlayerTrade.ContainsKey(dataTrade.Player.Value)) PlayerTrade.Remove(dataTrade.Player.Value);
                            if (PlayerTrade.ContainsKey(player.Value)) PlayerTrade.Remove(player.Value);
                            return false;
                        }
                        Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, "Человек еще продалжает трейд", 1500);
                        return true;
                    }
                    else
                    {
                        if (PlayerTrade.ContainsKey(player.Value)) PlayerTrade.Remove(player.Value);
                        //Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, "Время предложения истекло", 1500);
                        return false;

                    }
                }
                else if (PlayerTrade.ContainsKey(player.Value)) return true;
                return false;*/

                if (!player.IsCharacterData()) return true;
                var sessionData = player.GetSessionData();
                if (sessionData.TradePropertyData != null)
                {
                    TradePropertyData dataTrade = sessionData.TradePropertyData;
                    var targetSessionData = dataTrade.Player.GetSessionData();
                    if (targetSessionData == null || player.Position.DistanceTo(dataTrade.Player.Position) > 2 || DateTime.Now >= dataTrade.Time)
                    {
                        if (targetSessionData != null && targetSessionData.TradePropertyData != null)
                        {
                            Notify.Send(dataTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCancel), 3000);
                            targetSessionData.TradePropertyData = null;
                        }
                        dataTrade = null;
                        return false;
                    }
                    return true;
                }

                if (sessionData.SellItemData.Buyer != null && (!sessionData.SellItemData.Buyer.IsCharacterData() || player.Position.DistanceTo(sessionData.SellItemData.Buyer.Position) > 2))///* || DateTime.Now >= targetSessionData.SellItemData.Time)*/)
                {
                    sessionData.SellItemData = new SellItemData();
                }
                else if (sessionData.SellItemData.Seller != null && (!sessionData.SellItemData.Seller.IsCharacterData() || player.Position.DistanceTo(sessionData.SellItemData.Seller.Position) > 2))// || DateTime.Now >= targetSessionData.SellItemData.Time))
                {
                    sessionData.SellItemData = new SellItemData();
                }
                else if (sessionData.SellItemData.Buyer != null || sessionData.SellItemData.Seller != null) return true;

                return false;
            }
            catch (Exception e)
            {
                Log.Write($"TradeGet Exception: {e.ToString()}");
                return true;
            }

        }
        public static void TradeStart(ExtPlayer player, ExtPlayer target, string type)
        {
            try
            {
                if (!player.IsCharacterData() || !target.IsCharacterData()) return;
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.FunctionOffByAdmins), 3000);
                /*else if (sessionData.RequestData.IsRequested)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Подождите немного, у вас есть активные предложения.", 3000);
                    return;
                }
                else if (targetSessionData.RequestData.IsRequested)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Подождите немного, этому человеку уже предлагают что-то.", 3000);
                    return;
                }
                TradeCharacterResponse result = TradeCharacter(player, target, type);
                if (result == TradeCharacterResponse.ErrorTrade)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "В данный момент Вы не можете торговаться", 3000);
                }
                else if (result == TradeCharacterResponse.Fine)
                {
                    if (type == "vehicle")
                    {
                        TradeVehicleStart(player);
                        TradeVehicleStart(target);
                    }
                    else if (type == "house")
                    {
                        TradeHouseConfirm(player);
                        TradeHouseConfirm(target);
                    }
                    else if (type == "business")
                    {
                        TradeBusinessesStart(player);
                        TradeBusinessesStart(target);
                    }
                }
                else
                {
                    TradeClear(player);
                    TradeClear(target);
                }*/
            }
            catch (Exception e)
            {
                Log.Write($"TradeStart Exception: {e.ToString()}");
            }
        }
        #region Business
        /*public static void TradeBusinessesStart(ExtPlayer player)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null)
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                else if (characterData.BizIDs.Count == 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouHaveNoBusiness), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                dataTrade.Time = DateTime.Now.AddSeconds(10);
                Menu menu = new Menu("tradebiz", false, true);
                menu.Callback = CallbackTradeBusinesses;

                Menu.Item menuItem = new Menu.Item("header", Menu.MenuItem.Header);
                menuItem.Text = LangFunc.GetText(LangType.En, DataName.YouBusinesses);
                menu.Add(menuItem);

                foreach (int id in characterData.BizIDs)
                {
                    menuItem = new Menu.Item(id.ToString(), Menu.MenuItem.Button);
                    menuItem.Text = BusinessManager.BusinessTypeNames[BusinessManager.BizList[id].Type];
                    menu.Add(menuItem);
                }

                menuItem = new Menu.Item("close", Menu.MenuItem.Button);
                menuItem.Text = LangFunc.GetText(LangType.En, DataName.Close);
                menu.Add(menuItem);

                menu.Open(player);
            }
            catch (Exception e)
            {
                Log.Write($"TradeBusinessesStart Exception: {e.ToString()}");
            }
        }

        private static void CallbackTradeBusinesses(ExtPlayer player, Menu menu, Menu.Item item, string eventName, dynamic data)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                MenuManager.Close(player);
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                if (item.ID == "close")
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                dataTrade.Number = item.ID;
                dataTrade.Status = TradeStage.choose;
                dataTrade.Time = DateTime.Now.AddSeconds(10);
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;
                if (dataTargetTrade.Status == TradeStage.choose) //Если второй выбрал авто как и мы то 
                {
                    TradeBusinessesConfirm(player);
                    TradeBusinessesConfirm(dataTargetTrade.Player);
                }
                else
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerChosingBiz), 3000);
                }
            }
            catch (Exception e)
            {
                Log.Write($"CallbackTradeBusinesses Exception: {e.ToString()}");
            }
        }
        public static void TradeBusinessesConfirm(ExtPlayer player)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null)
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                var targetCharacterData = sessionData.TradePropertyData.Player.GetCharacterData();
                if (targetSessionData == null || targetCharacterData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }

                TradePropertyData dataTrade = sessionData.TradePropertyData;
                if (!characterData.BizIDs.Contains(Convert.ToInt32(dataTrade.Number)))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoBusiness), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;
                if (!targetCharacterData.BizIDs.Contains(Convert.ToInt32(dataTargetTrade.Number)))
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ErrorChelBiz), 3000);
                    return;
                }
                Business biz = BusinessManager.BizList[Convert.ToInt32(dataTrade.Number)];
                Business tbiz = BusinessManager.BizList[Convert.ToInt32(dataTargetTrade.Number)];
                dataTrade.Time = DateTime.Now.AddSeconds(10);
                if (dataTargetTrade.Player == player) Trigger.ClientEvent(player, "openDialog", "CONFIRM_BUSINESSES_TRADE", $"Вы предлагаете обменять свой бизнес {BusinessManager.BusinessTypeNames[biz.Type]} №{Convert.ToInt32(dataTrade.Number)} на бизнес {BusinessManager.BusinessTypeNames[tbiz.Type]} №{Convert.ToInt32(dataTargetTrade.Number)} человека {dataTargetTrade.Player.Name}[{dataTargetTrade.Player.Value}]"); // NE SMOG
                else Trigger.ClientEvent(player, "openDialog", "CONFIRM_BUSINESSES_TRADE", $"{dataTargetTrade.Player.Name}[{dataTargetTrade.Player.Value}] предлагает обменять свой бизнес {BusinessManager.BusinessTypeNames[tbiz.Type]} №{Convert.ToInt32(dataTargetTrade.Number)} на Ваш бизнес {BusinessManager.BusinessTypeNames[biz.Type]} №{Convert.ToInt32(dataTrade.Number)}."); // NE SMOG
            }
            catch (Exception e)
            {
                Log.Write($"TradeBusinessesConfirm Exception: {e.ToString()}");
            }
        }
        public static void TradeBusinessesConfirmed(ExtPlayer player, bool toggled)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null)
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                var targetCharacterData = sessionData.TradePropertyData.Player.GetCharacterData();
                if (targetSessionData == null || targetCharacterData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;

                if (!toggled)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeSucCancelled), 3000);
                    Notify.Send(dataTargetTrade.Player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerCancelTrade), 3000);
                    TradeClear(player);
                    TradeClear(dataTargetTrade.Player);
                }
                else
                {
                    dataTrade.Status = TradeStage.confirmed;
                    if (dataTargetTrade.Status == TradeStage.confirmed) //Если второй выбрал авто как и мы то 
                    {
                        if (player.Position.DistanceTo(dataTargetTrade.Player.Position) > 3)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerTooFar), 3000);
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerTooFar), 3000);
                            TradeClear(player);
                            TradeClear(dataTargetTrade.Player);
                            return;
                        }
                        else if (!characterData.BizIDs.Contains(Convert.ToInt32(dataTrade.Number)))
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoBusiness), 3000);
                            Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ErrorChelBiz), 3000);
                            TradeClear(player);
                            TradeClear(dataTargetTrade.Player);
                            return;
                        }
                        else if (!targetCharacterData.BizIDs.Contains(Convert.ToInt32(dataTargetTrade.Number)))
                        {
                            Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoBusiness), 3000);
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ErrorChelBiz), 3000);
                            TradeClear(player);
                            TradeClear(dataTargetTrade.Player);
                            return;
                        }
                        TradeUpdate(player, "business", dataTargetTrade);
                        TradeUpdate(dataTargetTrade.Player, "business", dataTrade);
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                        Notify.Send(dataTargetTrade.Player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                        TradeClear(player, false);
                        TradeClear(dataTargetTrade.Player, false);
                    }
                    else
                    {
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeBusinessesConfirmed, dataTargetTrade.Player.Name, dataTargetTrade.Player.Value), 3000);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"TradeBusinessesConfirmed Exception: {e.ToString()}");
            }
        }*/
        #endregion
        #region Vehicle
        /*public static void TradeVehicleStart(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                dataTrade.Time = DateTime.Now.AddSeconds(10);
                var vehiclesNumber = VehicleManager.GetVehiclesCarNumberToPlayer(player.Name);
                if (vehiclesNumber.Count == 0)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouHaveNoCar), 3000);
                    return;
                }
                Menu menu = new Menu("tradecar", false, true);
                menu.Callback = CallbackTradeVehicle;

                Menu.Item menuItem = new Menu.Item("header", Menu.MenuItem.Header);
                menuItem.Text = "Выбор машины";
                menu.Add(menuItem);
                foreach (string number in vehiclesNumber)
                {
                    var vehicleData = VehicleManager.GetVehicleToNumber(number);
                    if (vehicleData == null) continue;
                    menuItem = new Menu.Item(number, Menu.MenuItem.Button);
                    menuItem.Text = vehicleData.Model + " - " + number;
                    menu.Add(menuItem);
                }

                menuItem = new Menu.Item("close", Menu.MenuItem.Button);
                menuItem.Text = LangFunc.GetText(LangType.En, DataName.Close);
                menu.Add(menuItem);

                menu.Open(player);
            }
            catch (Exception e)
            {
                Log.Write($"TradeVehicleStart Exception: {e.ToString()}");
            }
        }

        private static void CallbackTradeVehicle(ExtPlayer player, Menu menu, Menu.Item item, string eventName, dynamic data)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                MenuManager.Close(player);
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                if (item.ID == "close")
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    return;
                }
                dataTrade.Number = item.ID;
                dataTrade.Status = TradeStage.choose;
                dataTrade.Time = DateTime.Now.AddSeconds(10);
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;
                if (dataTargetTrade.Status == TradeStage.choose) //Если второй выбрал авто как и мы то 
                {
                    TradeVehicleConfirm(player);
                    TradeVehicleConfirm(dataTargetTrade.Player);
                }
                else
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerChosingVeh), 3000);
                }
            }
            catch (Exception e)
            {
                Log.Write($"CallbackTradeVehicle Exception: {e.ToString()}");
            }
        }
        public static void TradeVehicleConfirm(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }

                TradePropertyData dataTrade = sessionData.TradePropertyData;
                var vehicleData = VehicleManager.GetVehicleToNumber(dataTrade.Number);
                if (vehicleData == null)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouNeVladeetVehTrade), 3000);
                    return;
                }
                string vName = vehicleData.Model;
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;
                var vehicleDataTarget = VehicleManager.GetVehicleToNumber(dataTargetTrade.Number);
                if (vehicleDataTarget == null)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerNeVladeetVehTrade), 3000);
                    return;
                }

                string vTargetName = vehicleDataTarget.Model;
                dataTrade.Time = DateTime.Now.AddSeconds(10);
                if (dataTargetTrade.Player == player) Trigger.ClientEvent(player, "openDialog", "CONFIRM_VEHICLE_TRADE", $"Вы предлагаете обменять свой {vName} (Номер: {dataTrade.Number}) на {vTargetName} (Номер: {dataTargetTrade.Number}) человека {dataTargetTrade.Player.Name}[{dataTargetTrade.Player.Value}]"); //NE SMOG  
                else Trigger.ClientEvent(player, "openDialog", "CONFIRM_VEHICLE_TRADE", $"{dataTargetTrade.Player.Name}[{dataTargetTrade.Player.Value}] предлагает обменять свой {vTargetName} (Номер: {dataTargetTrade.Number}) на Ваш {vName} (Номер: {dataTrade.Number})."); // NE SMOG
            }
            catch (Exception e)
            {
                Log.Write($"TradeVehicleConfirm Exception: {e.ToString()}");
            }
        }
        public static void TradeVehicleConfirmed(ExtPlayer player, bool toggled)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;

                if (!toggled)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeSucCancelled), 3000);
                    TradeClear(player);
                }
                else
                {
                    dataTrade.Status = TradeStage.confirmed;
                    if (dataTargetTrade.Status == TradeStage.confirmed) //Если второй выбрал авто как и мы то 
                    {
                        if (player.Position.DistanceTo(dataTargetTrade.Player.Position) > 3)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerTooFar), 3000);
                            Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerTooFar), 3000);
                            TradeClear(player, false);
                            return;
                        }
                        var vehicleData = VehicleManager.GetVehicleToNumber(dataTrade.Number);
                        if (vehicleData == null || vehicleData.Holder != player.Name)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouNeVladeetVehTrade), 3000);
                            Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerNeVladeetVehTrade), 3000);
                            TradeClear(player, false);
                            return;
                        }
                        var vehicleDataTarget = VehicleManager.GetVehicleToNumber(dataTargetTrade.Number);
                        if (vehicleDataTarget == null || vehicleDataTarget.Holder != dataTargetTrade.Player.Name)
                        {
                            Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouNeVladeetVehTrade), 3000);
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerNeVladeetVehTrade), 3000);
                            TradeClear(player, false);
                            return;
                        }
                        TradeUpdate(player, "vehicle", dataTargetTrade);
                        TradeUpdate(dataTargetTrade.Player, "vehicle", dataTrade);
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                        Notify.Send(dataTargetTrade.Player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                        TradeClear(player, false);
                    }
                    else
                    {
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeBusinessesConfirmed, dataTargetTrade.Player.Name, dataTargetTrade.Player.Value), 3000);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"TradeVehicleConfirmed Exception: {e.ToString()}");
            }
        }*/
        #endregion
        #region House
        /*public static void TradeHouseConfirm(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;

                var house = HouseManager.GetHouse(player, true);
                if (house == null || house.Owner != player.Name)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoHome), 3000);
                    return;
                }
                var garage = house.GetGarageData();
                if (garage == null)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoHome), 3000);
                    return;
                }
                var garageType = GarageManager.GarageTypes[garage.BDType];
                
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;

                var houseTarget = HouseManager.GetHouse(dataTrade.Player, true);

                if (houseTarget == null)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PersonNoHome), 3000);
                    return;
                }

                var garageTarget = houseTarget.GetGarageData();

                if (garageTarget == null)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PersonNoHome), 3000);
                    return;
                }
                
                var garageTargetType = GarageManager.GarageTypes[garageTarget.BDType];
                
                int vehiclesCount = VehicleManager.GetVehiclesCarCountToPlayer(player.Name);
                if (vehiclesCount > garageTargetType.MaxCars)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ErrorMaxGarageCars, garageTargetType.MaxCars), 3000);
                    Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerErrorMaxGarageCars, garageTargetType.MaxCars), 3000);
                    TradeClear(player, false);
                    return;
                }
                dataTrade.Time = DateTime.Now.AddSeconds(10);

                if (dataTrade.Player == player) 
                    Trigger.ClientEvent(player, "openDialog", "CONFIRM_HOUSE_TRADE", $"Вы предлагаете обменять свою недвижимость №{house.ID} ({HouseManager.HouseTypeList[house.Type].Name}, {garageType.MaxCars} г.м.) на недвижимость №{houseTarget.ID} ({HouseManager.HouseTypeList[houseTarget.Type].Name}, {garageTargetType.MaxCars} г.м.) человека {dataTrade.Player.Name}[{dataTrade.Player.Value}]"); // NE SMOG
                else 
                    Trigger.ClientEvent(player, "openDialog", "CONFIRM_HOUSE_TRADE", $"{dataTrade.Player.Name}[{dataTrade.Player.Value}] предлагает обменять свою недвижимость №{houseTarget.ID} ({HouseManager.HouseTypeList[houseTarget.Type].Name}, {garageTargetType.MaxCars} г.м.) на Вашу недвижимость №{house.ID} ({HouseManager.HouseTypeList[house.Type].Name}, {garageType.MaxCars} г.м.)"); // NE SMOG
            }
            catch (Exception e)
            {
                Log.Write($"TradeHouseConfirm Exception: {e.ToString()}");
            }
        }
        public static void TradeHouseConfirmed(ExtPlayer player, bool toggled)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = sessionData.TradePropertyData.Player.GetSessionData();
                if (targetSessionData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                TradePropertyData dataTrade = sessionData.TradePropertyData;
                TradePropertyData dataTargetTrade = targetSessionData.TradePropertyData;

                if (!toggled)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeSucCancelled), 3000);
                    TradeClear(player);
                    return;
                }
                else
                {
                    dataTrade.Status = TradeStage.confirmed;
                    if (dataTargetTrade.Status == TradeStage.confirmed) //Если второй выбрал авто как и мы то 
                    {
                        if (player.Position.DistanceTo(dataTargetTrade.Player.Position) > 3)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.PlayerTooFar), 3000);
                            TradeClear(player);
                            return;
                        }
                        var house = HouseManager.GetHouse(player, true);
                        if (house == null || house.Owner != player.Name)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoreHouse), 3000);
                            TradeClear(player);
                            return;
                        }
                        House houseTarget = HouseManager.GetHouse(dataTrade.Player, true);
                        if (houseTarget == null || houseTarget.Owner != dataTrade.Player.Name)
                        {
                            Notify.Send(dataTargetTrade.Player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoreHouse), 3000);
                            TradeClear(player);
                            return;
                        }
                        TradeUpdate(player, "house", dataTargetTrade);
                        TradeUpdate(dataTrade.Player, "house", dataTrade);
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                        Notify.Send(dataTrade.Player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                        TradeClear(player, false);
                    }
                    else
                    {
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeBusinessesConfirmed, dataTrade.Player.Name, dataTrade.Player.Value), 3000);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"TradeHouseConfirmed Exception: {e.ToString()}");
            }
        }*/
        #endregion
        public static void TradeUpdate(ExtPlayer player, string type, TradePropertyData dataTrade)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null)
                {
                    TradeClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.TradePropertyData == null) return;
                var targetSessionData = dataTrade.Player.GetSessionData();
                var targetCharacterData = dataTrade.Player.GetCharacterData();
                if (targetSessionData == null || targetCharacterData == null || targetSessionData.TradePropertyData == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCanceled), 3000);
                    TradeClear(player);
                    return;
                }
                if (type == "vehicle")
                {
                    string number = dataTrade.Number;
                    var vehicleData = VehicleManager.GetVehicleToNumber(number);
                    vehicleData.Holder = player.Name;

                    var house = HouseManager.GetHouse(dataTrade.Player, true);
                    if (house != null)
                    {
                        var sellerGarage = house.GetGarageData();
                        if (sellerGarage != null)
                            sellerGarage.DeleteCar(number, isRevive: true);
                    }
                    house = HouseManager.GetHouse(player, true);
                    if (house != null)
                    {
                        var garage = house.GetGarageData();
                        if (garage != null)
                        {
                            if (garage.Type != -1 && garage.Type != 6)
                                garage.SpawnCar(number);
                            else
                                garage.GetVehicleFromGarage(number);
                        }
                    }

                    VehicleManager.SaveHolder(number);
                }
                else if (type == "house")
                {
                    var house = HouseManager.GetHouse(dataTrade.Player, true);
                    HouseManager.CheckAndKick(player);
                    if (house.Type != 7)
                        house.PetName = characterData.PetName;
                    house.SetOwner(player.Name);
                }
                else if (type == "business")
                {
                    int Number = Convert.ToInt32(dataTrade.Number);

                    Business biz = BusinessManager.BizList[Number];

                    if (!Main.PlayerUUIDs.ContainsKey(player.Name))
                    {
                        TradeClear(player);
                        return;
                    }

                    biz.SetOwner(player.Name);

                    targetCharacterData.BizIDs.Remove(Number);
                    characterData.BizIDs.Add(Number);
                    
                    Character.Save.Repository.SaveBiz(player);
                }
            }
            catch (Exception e)
            {
                Log.Write($"TradeUpdate Exception: {e.ToString()}");
            }
        }

        #endregion

        #region Релетка


        public static RouletteCategory[] RoulleteCategoryList =
        {
            new RouletteCategory("Free", "keys_2", new int[] {0, 1, 2}),
            new RouletteCategory("Standard", "keys_5", new int[] {3, 4, 5, 6, 7}),
            new RouletteCategory("Elite", "keys_7", new int[] {8, 9, 12, 14, 11, 10, 13}),
        };

        public static string RoulleteCategoryListJson = "";


        public static List<RouletteCaseData> RouletteCasesData = new List<RouletteCaseData> { 
             /* 
             Case 'Free Common' - 10 items 
             Blue    25.00% 
             Yellow  40.00% 
             Pink    30.00% 
             Red     5.00% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case0, "Free Daily Case", 0, new List<RouletteItemData>(), "keys_0", "A free case, can be opened after 3 hours of playtime."), 
             /* 
             Case 'Free Weapon' - 20 items 
             Blue    40.00% 
             Yellow  30.00% 
             Pink    27.00% 
             Red     3.00% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case1, "Free Weapon Case", 0, new List<RouletteItemData>(), "keys_1", "A free case with weapons, can be opened after 5 hours of playtime."), 
             /* 
             Case 'Free Vehicle' - 28 items 
             Blue    40.00% 
             Yellow  47.00% 
             Pink    10.00% 
             Red     3.00% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case2, "Free Vehicle Case", 0, new List<RouletteItemData>(), "keys_2", "A free case with cars, can be opened after 8 hours of playtime."), 
             /* 
             Case 'Standart' - 16 items 
             Blue    48.40% 
             Yellow  27.50% 
             Pink    15.00% 
             Red     9.10% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case3, "Standard Case", 99, new List<RouletteItemData>(), "keys_3", "A standard case with standard prizes."),             
             /* 
             Case 'VIP' - 19 items 
             Blue    40.00% 
             Yellow  14.80% 
             Pink    35.00% 
             Red     10.20% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case4, "Strange Case", 249, new List<RouletteItemData>(), "keys_4", "A decent case for beginners."), 
             /* 
             Case 'Premium' - 20 items 
             Blue    40.00% 
             Yellow  0.00% 
             Pink    47.20% 
             Red     12.80% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case5, "Special Case", 499, new List<RouletteItemData>(), "keys_5", "A juicy case, with a chance for a serious profit!"), 
             /* 
             Case 'Rare' - 15 items 
             Blue    29.00% 
             Yellow  40.00% 
             Pink    25.00% 
             Red     6.00% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case6, "Rare Case", 999, new List<RouletteItemData>(), "keys_6", "A case with items and vehicles of higher rarity."), 
             /* 
             Case 'Exclusive' - 16 items 
             Blue    38.00% 
             Yellow  15.75% 
             Pink    35.00% 
             Red     11.25% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case7, "Fierce Case", 2499, new List<RouletteItemData>(), "keys_7", "A very fierce case. The grand prize is a Bugatti, the fastest car."), 
             /* 
             Case 'Case M' - 12 items 
             Blue    38.00% 
             Yellow  15.75% 
             Pink    35.00% 
             Red     11.25% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case8, "Men's Case", 2999, new List<RouletteItemData>(), "keys_9", "A case with MEN'S clothing from the donation store. Test your luck and be stylish, or sell it and be rich. Or you could even gift it..."), 
             /* 
             Case 'Case JE' - 12 items 
             Blue    38.00% 
             Yellow  15.75% 
             Pink    35.00% 
             Red     11.25% 
             Total   10000 - 100.00% 
             */ 
             new RouletteCaseData(ItemId.Case9, "Women's Case", 2999, new List<RouletteItemData>(), "keys_10", "A case with WOMEN'S clothing from the donation store. A great option if you want to give a gift."), 
             /* 
             Case 'Exotic' - 12 items 
             Blue    38.00% 
             Yellow  15.75% 
             Pink    35.00% 
             Red     11.25% 
             Total   10000 - 100.00% 
             */ 
            new RouletteCaseData(ItemId.Case10, "Exotic Case", 7777, new List<RouletteItemData>(), "keys_8", "A case with cars from the Donate Room Autoroom. Donata Redbucksovna approves!"),
            new RouletteCaseData(ItemId.Case11, "Legendary Case", 5555, new List<RouletteItemData>(), "keys_11", "All or nothing! Want to risk it and become the owner of the legendary RedAge sweater? Then spin the case!"),
            new RouletteCaseData(ItemId.Case12, "Interesting Case", 3999, new List<RouletteItemData>(), "keys_12", "Been wanting a unique car that isn't sold anywhere? Here you go! The Dodge Charger - used by the police, with fairly high specs."),
            new RouletteCaseData(ItemId.Case13, "Helicopter Case", 8888, new List<RouletteItemData>(), "keys_13", "Love looking down on people? A helicopter is probably perfect for that!"),
            new RouletteCaseData(ItemId.Case14, "Armored Case", 5000, new List<RouletteItemData>(), "keys_14", "A cool new case with trendy armored cars!"),
            new RouletteCaseData(ItemId.Case15, "Exclusive Case", 100, new List<RouletteItemData>(), "keys_15", "A cool new case with placeable items!"),
            new RouletteCaseData(ItemId.Case16, "Snow Case [M]", 5000, new List<RouletteItemData>(), "keys_16", "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. How quickly can you collect your set?"),
            new RouletteCaseData(ItemId.Case17, "Amethyst Case [M]", 5000, new List<RouletteItemData>(), "keys_17", "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. How quickly can you collect your set?"),
            new RouletteCaseData(ItemId.Case18, "Holiday Case [M]", 5000, new List<RouletteItemData>(), "keys_18", "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. How quickly can you collect your set?"),
            new RouletteCaseData(ItemId.Case19, "Snow Case [F]", 5000, new List<RouletteItemData>(), "keys_19", "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. How quickly can you collect your set?"),
            new RouletteCaseData(ItemId.Case20, "Amethyst Case [F]", 5000, new List<RouletteItemData>(), "keys_20", "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. How quickly can you collect your set?"),
            new RouletteCaseData(ItemId.Case21, "Holiday Case [F]", 5000, new List<RouletteItemData>(), "keys_21", "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. How quickly can you collect your set?"),
        };

        public static List<List<object>> RouletteCasesDataJson = new List<List<object>>();
        
        public static async void InitRoulette()
        {
            RoulleteCategoryList = Settings.ReadAsync("roulleteCategoryList", RoulleteCategoryList);

            var jsonData = new List<List<object>>();
            foreach (var roulleteCategoryList in RoulleteCategoryList)
            {
                var itemData = new List<object>();
                
                itemData.Add(roulleteCategoryList.Name);
                itemData.Add(roulleteCategoryList.Image);
                itemData.Add(roulleteCategoryList.CaseList);
                
                jsonData.Add(itemData);
            }

            RoulleteCategoryListJson = JsonConvert.SerializeObject(jsonData);
            jsonData.Clear();
            
            //
            
            RouletteCasesData = Settings.ReadAsync("rouletteCasesData", RouletteCasesData);

            //
            
            await using var db = new ConfigBD("ConfigDB");

            var rouletteList = await db.Roulette
                .ToListAsync();
            
            foreach (var rouletteItem in rouletteList)
            {
                if (RouletteCasesData.Count <= (int) rouletteItem.CaseId)
                    continue;

                var rouletteCaseData = RouletteCasesData [(int) rouletteItem.CaseId];
                
                if (rouletteCaseData == null)
                    continue;
                
                //RouletteItemData(int Id, string Name, int ValueMin, int ValueMax, int ReturnRB, int Percent, RouletteColor Color = RouletteColor.Blue, bool IsChatMessage = false, bool IsHudMessage = false, ItemId ItemId = ItemId.Debug, string ItemData = "")
                rouletteCaseData.RouletteItemsData.Add(new RouletteItemData(
                    rouletteItem.Name,
                    rouletteItem.Desc,
                    rouletteItem.Image,
                    (int) rouletteItem.ValueMin,
                    (int) rouletteItem.ValueMax,
                    (int) rouletteItem.ReturnRB,
                    (int) rouletteItem.Percent,
                    (RouletteColor) rouletteItem.Color,
                    rouletteItem.IsChatMessage,
                    rouletteItem.IsHudMessage,
                    (ItemId) rouletteItem.ItemId,
                    rouletteItem.ItemData
                ));
                
            }
            
            //
            var index = 0;
            foreach (var rouletteCasesData in RouletteCasesData)
            {
                var itemData = new List<object>();
                
                itemData.Add(index);
                itemData.Add(rouletteCasesData.Price);
                itemData.Add(rouletteCasesData.Image);
                itemData.Add(rouletteCasesData.Name);
                itemData.Add(rouletteCasesData.Desc);
                
                //
                
                
                var itemsData = new List<List<object>>();
                foreach (var rouletteItemsData in rouletteCasesData.RouletteItemsData)
                {
                    var item = new List<object>();
                    
                    item.Add($"{rouletteItemsData.Name} | {rouletteItemsData.Desc}");
                    item.Add(rouletteItemsData.Image);
                    item.Add(rouletteItemsData.Color);
                    
                    itemsData.Add(item);
                }
                itemData.Add(itemsData);
                //
                
                jsonData.Add(itemData);
                index++;
            }

            RouletteCasesDataJson = jsonData;
            //


            /*var index = 0;
            foreach (var rouletteCaseData in RouletteCasesData1)
            {
                foreach (var rouletteItemData in rouletteCaseData.RouletteItemsData)
                {
                    db.InsertAsync(new Roulettes
                    {
                        CaseId = (int)index,
                        Id = rouletteItemData.Id,
                        Name = rouletteItemData.Name,
                        ValueMin = rouletteItemData.ValueMin,
                        ValueMax = rouletteItemData.ValueMax,
                        ReturnRB = rouletteItemData.ReturnRB,
                        Percent = rouletteItemData.Count,
                        Color = (int)rouletteItemData.Color,
                        IsChatMessage = rouletteItemData.IsChatMessage,
                        IsHudMessage = rouletteItemData.IsHudMessage,
                        ItemId = (short)rouletteItemData.ItemId,
                        ItemData = rouletteItemData.ItemData,

                    });
                }
                index++;
            }*/

        }
        
        [RemoteEvent("server.donate.roulette.loadCase")]
        public void LoadCase(ExtPlayer player, int caseId)
        {
            if (!player.IsCharacterData()) return;
            
            Trigger.ClientEvent(player, "client.donate.roulette.initCase", JsonConvert.SerializeObject(RouletteCasesDataJson[caseId]));
        }

        
        private static string RouletteItemColorByCase(RouletteColor Color)
        {
            try
            {
                switch (Color)
                {
                    case RouletteColor.Blue: // Blue
                        return "!{#4593ff}";
                    case RouletteColor.Yellow: // Yellow
                        return "!{#ccc300}";
                    case RouletteColor.Pink: // Pink
                        return "!{#9c7cbd}";
                    case RouletteColor.Red: // Red
                        return "!{#DF5353}";
                    default: // White
                        return "!{#d4d4d4}";
                }
            }
            catch (Exception e)
            {
                Log.Write($"RouletteItemColorByCase Exception: {e.ToString()}");
                return "!{#d4d4d4}";
            }
        }
        public static string RouletteToItemsText(ExtPlayer player, string Name, int Amount, int Price, bool BtnTake)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return "";

                var accountData = player.GetAccountData();

                if (accountData == null) return "";

                string returnText = "";
                switch (Name)
                {
                    case "VIP Silver":
                    case "VIP Gold":
                    case "VIP Platinum":
                    case "VIP Diamond":
                        int rank = 0;
                        switch (Name)
                        {
                            case "VIP Silver":
                                rank = 1;
                                break;
                            case "VIP Gold":
                                rank = 2;
                                break;
                            case "VIP Platinum":
                                rank = 3;
                                break;
                            case "VIP Diamond":
                                rank = 4;
                                break;
                            default:
                                // Not supposed to end up here. 
                                break;
                        }
                        if (accountData.VipLvl > 0 && accountData.VipLvl < rank)//Если выйгранная выше нынешнеей
                        {
                            returnText = LangFunc.GetText(LangType.En, DataName.CaseWinVip, Group.GroupNames[rank], Amount, Group.GroupNames[accountData.VipLvl]);
                        }
                        else if (accountData.VipLvl > 0 && accountData.VipLvl > rank)//Если нынешняя випка выше выйграной
                        {
                            returnText = LangFunc.GetText(LangType.En, DataName.CaseWinVipHueviy, Group.GroupNames[rank], Amount);
                        }
                        else returnText = LangFunc.GetText(LangType.En, DataName.CaseWinVipNoVip, Group.GroupNames[rank], Amount);//если нет випки
                        break;
                    case "Игровая валюта":
                        returnText = LangFunc.GetText(LangType.En, DataName.CaseWinMoney, Wallet.Format(Amount));
                        break;
                    case "EXP":
                        returnText = LangFunc.GetText(LangType.En, DataName.CaseWinExp, Amount);
                        break;
                    case "RedBucks":
                        returnText = LangFunc.GetText(LangType.En, DataName.CaseWinRB, Wallet.Format(Amount));
                        break;
                    case "Маска":
                        returnText = LangFunc.GetText(LangType.En, DataName.CaseWinMask);
                        break;
                    case "Лицензия на вертолёт":
                        if (characterData.Licenses[4]) returnText = LangFunc.GetText(LangType.En, DataName.CaseWinFlylicEst);
                        else returnText = LangFunc.GetText(LangType.En, DataName.CaseWinFlylic);
                        break;
                    case "Лицензия на самолёт":
                        if (characterData.Licenses[5]) returnText = LangFunc.GetText(LangType.En, DataName.CaseWinSamoletEst);
                        else returnText = LangFunc.GetText(LangType.En, DataName.CaseWinSamolet);
                        break;
                    case "Лицензия парамедика":
                        if (characterData.Licenses[8]) returnText = LangFunc.GetText(LangType.En, DataName.CaseWinParamedic);
                        else returnText = LangFunc.GetText(LangType.En, DataName.CaseWinParamedicEst);
                        break;
                    case "Сим-карта":
                        if (characterData.Sim != -1) returnText = LangFunc.GetText(LangType.En, DataName.CaseWinSimCard, Amount);
                        else returnText = LangFunc.GetText(LangType.En, DataName.CaseWinSimCard, Amount);
                        break;
                    case "Чай":
                        returnText = LangFunc.GetText(LangType.En, DataName.CaseWinTea, Amount);
                        break;
                    default:
                        returnText = LangFunc.GetText(LangType.En, DataName.CaseWinItem, Name);
                        break;
                }

                if (Price > 0)
                {
                    returnText += LangFunc.GetText(LangType.En, DataName.CaseSellRB, Price);
                    if (BtnTake) returnText += LangFunc.GetText(LangType.En, DataName.CaseOstavitPriz);
                }
                else if (BtnTake) returnText += LangFunc.GetText(LangType.En, DataName.CaseGetPrize);
                return returnText;
            }
            catch (Exception e)
            {
                Log.Write($"RouletteToItemsText Exception: {e.ToString()}");
                return LangFunc.GetText(LangType.En, DataName.CaseWinItem, Name);
            }
        }

        public static bool RouletteGetWallet(ExtPlayer player, int amount, bool typeCurrency)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return false;

                var accountData = player.GetAccountData();

                if (accountData == null) return false;
                else if (!typeCurrency && accountData.RedBucks < amount)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NetRB), 3000);
                    return false;
                }
                else if (typeCurrency && characterData.Money < (amount * Convert.ToInt32(100 * Main.DonateSettings.Convert)))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoney), 3000);
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Write($"RouletteGetWallet Exception: {e.ToString()}");
                return false;
            }
        }

        public static void RouletteUpdateWallet(ExtPlayer player, int amount, bool typeCurrency, int caseid = -1)
        {
            try
            {
                var accountData = player.GetAccountData();
                if (accountData == null) return;
                if (!typeCurrency)
                {
                    if (caseid == -1) 
                        UpdateData.RedBucks(player, amount, msg: "Открытие кейса");
                    else 
                        Donate.UpdatePrice(player, "cases", caseid, amount);
                }
                else
                {
                    Wallet.Change(player, +(amount * Convert.ToInt32(10 * Main.DonateSettings.Convert)));
                    //PlayerStats(player);
                }
            }
            catch (Exception e)
            {
                Log.Write($"RouletteUpdateWallet Exception: {e.ToString()}");
            }
        }
        public static void RouletteBuyCase(ExtPlayer player, int caseid, int count)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var accountData = player.GetAccountData();
            if (accountData == null) return;
            if (caseid >= RouletteCasesData.Count)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CaseNeDostupen), 3000);
                return;
            }
            RouletteCaseData caseData = RouletteCasesData[caseid];
            if (caseData.Price == 0) return;
            else if (!RouletteGetWallet(player, Donate.GetPrice(accountData.Unique, "cases", caseid, RouletteCasesData[caseid].Price * count), false)) return;

            RouletteUpdateWallet(player, -(caseData.Price * count), false, caseid);

            if (isFreeSlots(player, caseData.ItemId, count, send: false) != 0)
            {
                Chars.Repository.AddNewItemWarehouse(player, caseData.ItemId, count);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CaseGetSklad), 3000);
                return;
            }     

            AddNewItem(player, $"char_{characterData.UUID}", "inventory", caseData.ItemId, count);
            Notify.Send(player, NotifyType.Success, NotifyPosition.Top, LangFunc.GetText(LangType.En, DataName.CaseGetInventory), 2000);
        }
        public static void RouletteOpenCase(ExtPlayer player, int caseid, int count, bool typeCurrency = false)
        {
            try
            {
                if (!FunctionsAccess.IsWorking("opencase"))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.FunctionOffByAdmins), 3000);
                    return;
                }
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var accountData = player.GetAccountData();
                if (accountData == null) return;
                else if (caseid >= RouletteCasesData.Count)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CaseNeDostupen), 3000);
                    return;
                }
                /*else if (caseid == 0 && accountData.FreeCase[0] == 0)
                {
                    if (characterData.Time.TodayTime < 180) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы должны отыграть 3 часа за сегодня, чтобы открыть кейс.", 3000);
                    else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы уже открывали этот кейс сегодня, приходите завтра!", 3000);
                    return;
                }
                else if (caseid == 1 && accountData.FreeCase[1] == 0)
                {
                    if (characterData.Time.TodayTime < 300) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы должны отыграть 5 часов за сегодня, чтобы открыть кейс.", 3000);
                    else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы уже открывали этот кейс сегодня, приходите завтра!", 3000);
                    return;
                }
                else if (caseid == 2 && accountData.FreeCase[2] == 0)
                {
                    if (characterData.Time.TodayTime < 480) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы должны отыграть 8 часов за сегодня, чтобы открыть кейс.", 3000);
                    else Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы уже открывали этот кейс сегодня, приходите завтра!", 3000);
                    return;
                }
                else if (!RouletteGetWallet(player, Donate.GetPrice(accountData.Unique, "cases", caseid, RouletteCasesData[caseid].Price * count), typeCurrency)) return;*/

                RouletteCaseData caseData = RouletteCasesData[caseid];

                ItemStruct aItem = Repository.isItem(player, "inventory", caseData.ItemId);
                if (aItem == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouHaveNoCase), 3000);
                    return;
                }
                else if (count > aItem.Item.Count)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter,LangFunc.GetText(LangType.En, DataName.YouHaveCases, aItem.Item.Count), 3000);
                    return;
                }


                int itemIndex = -1;
                if (sessionData.CaseWin != 255 && sessionData.CaseWin == caseid)
                {
                    sessionData.CaseWin = 255;
                    itemIndex = sessionData.CaseItemWin;
                    sessionData.CaseItemWin = 255;
                }

                List<RouletteData> returnPlayerData = RouletteCreate(player, caseid, caseData, itemIndex, count, typeCurrency);
                if (returnPlayerData == null) return;
                /*if (caseid == 0)
                {
                    accountData.FreeCase[0] -= 1;
                    Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, $"У Вас осталось {accountData.FreeCase[0]} Free Common Case для открытия.", 3000);
                    if (!characterData.Achievements[19])
                    {
                        characterData.Achievements[19] = true;
                        Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, "Следующий раз получить этот кейс можно будет только завтра, отыграв 3 часа!", 3000);
                    }
                }
                else if (caseid == 1)
                {
                    accountData.FreeCase[1] -= 1;
                    Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, $"У Вас осталось {accountData.FreeCase[1]} Free Weapon Case для открытия.", 3000);
                    if (!characterData.Achievements[25])
                    {
                        characterData.Achievements[25] = true;
                        Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, "Следующий раз получить этот кейс можно будет только завтра, отыграв 5 часов!", 3000);
                    }
                }
                else if (caseid == 2)
                {
                    accountData.FreeCase[2] -= 1;
                    Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, $"У Вас осталось {accountData.FreeCase[2]} Free Vehicle Case для открытия.", 3000);
                    if (!characterData.Achievements[26])
                    {
                        characterData.Achievements[26] = true;
                        Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, "Следующий раз получить этот кейс можно будет только завтра, отыграв 8 часов!", 3000);
                    }
                }*/
                Repository.RemoveIndex(player, aItem.Location, aItem.Index, count);

                Trigger.ClientEvent(player, "client.roullete.start", JsonConvert.SerializeObject(returnPlayerData));
                
                BattlePass.Repository.UpdateReward(player, 50);
                
                //if (caseData.Price > 0) RouletteUpdateWallet(player, -(caseData.Price * count), typeCurrency, caseid);
            }
            catch (Exception e)
            {
                Log.Write($"RouletteOpenCase Exception: {e.ToString()}");
            }
        }
        public static List<RouletteData> RouletteCreate(ExtPlayer player, int caseid, RouletteCaseData caseData, int indexItem, int count = 1, bool typeCurrency = false)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return null;

                var accountData = player.GetAccountData();
                if (accountData == null) return null;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return null;

                if (sessionData.RouletteData.Count > 0)
                {
                    if (sessionData.RouletteData[0].CreateTime < DateTime.Now) Events.RoulleteConfirm(player, false, -1);
                    else
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.WaitTillNextTry), 3000);
                        return null;
                    }
                }

                List<RouletteData> _RoulettePlayerDatas = new List<RouletteData>();
                Random rand = new Random();


                for(int i = 0; i < count; i++)
                {
                    RouletteItemData randomItem = null;
                    var itemIndex = 0;
                    if (indexItem == -1/* || !caseData.RouletteItemsData.Contains(indexItem)*/)
                    {

                        int maxCount = 0;
                        foreach (RouletteItemData items in caseData.RouletteItemsData)
                        {
                            maxCount += items.Count;
                        }

                        int idItem = rand.Next(0, maxCount);

                        maxCount = 0;
                        foreach (RouletteItemData items in caseData.RouletteItemsData)
                        {
                            maxCount += items.Count;
                            if (maxCount >= idItem)
                            {
                                randomItem = items;
                                break;
                            }

                            itemIndex++;
                        }
                    }
                    else
                    {
                        randomItem = caseData.RouletteItemsData[indexItem];
                        indexItem = -1;
                    }

                    int randomAmount = randomItem.ValueMax == 0 ? randomItem.ValueMin : rand.Next(randomItem.ValueMin, randomItem.ValueMax);
                    int price = randomItem.ReturnRB;
                    bool btnTake = true;
                    switch (randomItem.Name)
                    {
                        case "Сим-карта":
                            price = randomItem.ReturnRB;
                            var number = Players.Phone.Sim.Repository.GenerateSimCard(randomItem.ValueMin, randomItem.ValueMax);
                            Players.Phone.Sim.Repository.Add(number);
                            randomAmount = number;
                            break;
                        case "VIP Silver":
                        case "VIP Gold":
                        case "VIP Platinum":
                        case "VIP Diamond":
                            price = randomItem.ReturnRB;
                            int rank = 0;
                            switch (randomItem.Name)
                            {
                                case "VIP Silver":
                                    rank = 1;
                                    break;
                                case "VIP Gold":
                                    rank = 2;
                                    break;
                                case "VIP Platinum":
                                    rank = 3;
                                    break;
                                case "VIP Diamond":
                                    rank = 4;
                                    break;
                                default:
                                    // Not supposed to end up here. 
                                    break;
                            }
                            if (accountData.VipLvl > rank) btnTake = false;
                            break;
                        case "Лицензия на вертолёт":
                            if (characterData.Licenses[4]) btnTake = false;
                            break;
                        case "Лицензия на самолёт":
                            if (characterData.Licenses[5]) btnTake = false;
                            break;
                        case "Лицензия парамедика":
                            if (characterData.Licenses[8]) btnTake = false;
                            break;
                        default:
                            // Not supposed to end up here. 
                            break;
                            /*default:
                                bool success = false;
                                foreach (KeyValuePair<ItemId, ItemsInfo> itemData in ItemsInfo)
                                {
                                    if (itemData.Value.Name == randomItem.Name)
                                    {
                                        ItemId nameWeapon = itemData.Key;
                                        if (btnTake && nameWeapon == ItemId.BodyArmor && isItem(player, "inventory", ItemId.BodyArmor) != null) btnTake = false;
                                        if (btnTake && isFreeSlots(player, nameWeapon, send: false) != 0) btnTake = false;
                                        success = true;
                                        break;
                                    }
                                }
                                if (!success && BusinessManager.ProductsOrderPrice.ContainsKey(randomItem.Name))
                                {
                                    int mycars = VehicleManager.getAllPlayerVehicles(player.Name).Count;
                                    var house = HouseManager.GetHouse(player, true);
                                    if (house != null)
                                    {
                                        var garage = house.GetGarageData();
                                        if (mycars >= GarageManager.GarageTypes[garage.Type].MaxCars) btnTake = false;
                                    }
                                    else if (mycars >= GarageManager.MaxGarageCars) btnTake = false;
                                }
                                break;*/
                    }
                    RouletteData _ReturnPlayerData = new RouletteData(randomItem, itemIndex, rand.Next(10, 40), randomAmount, price, RouletteToItemsText(player, randomItem.Name, randomAmount, price, btnTake), btnTake, typeCurrency, DateTime.Now.AddSeconds(20), caseid, (byte)rand.Next(100));
                    _ReturnPlayerData.IndexList = i;
                    _RoulettePlayerDatas.Add(_ReturnPlayerData);
                }
                sessionData.RouletteData = _RoulettePlayerDatas;
                return _RoulettePlayerDatas;
            }
            catch (Exception e)
            {
                Log.Write($"RouletteCreate Exception: {e.ToString()}");
                return null;
            }
        }

        private static List<int> freeOpenCaseToPer = new List<int>()
        {
            3,
            3,
            3,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5,
            5
        };
        private static void RouletteWinAnnounce(ExtPlayer player, int caseid, RouletteItemData RouletteItem, int amount, bool type, byte bonuscase)
        {
            try
            {
                var accountData = player.GetAccountData();

                if (accountData == null) return;
                switch (RouletteItem.Name)
                {
                    case "Игровая валюта":
                        Commands.RPChat("sb", player, "выиграл «" + RouletteItemColorByCase(RouletteItem.Color) + RouletteItem.Name + "!{#d4d4d4}» в количестве " + Wallet.Format(amount) + $"$ из {RouletteCasesData[caseid].Name}."); // NE SMOG
                        break;
                    case "RedBucks":
                        Commands.RPChat("sb", player, "выиграл «" + RouletteItemColorByCase(RouletteItem.Color) + RouletteItem.Name + "!{#d4d4d4}» в количестве " + Wallet.Format(amount) + $"RB из {RouletteCasesData[caseid].Name}.");
                        break;
                    default:
                        Commands.RPChat("sb", player, "выиграл «" + RouletteItemColorByCase(RouletteItem.Color) + RouletteItem.Name + "!{#d4d4d4}» из " + $"{RouletteCasesData[caseid].Name}.");
                        break;
                }
                if (type) Admin.WinLog($"[{RouletteCasesData[caseid].Name}] {player.Name} ({player.Value}) выиграл {RouletteItem.Name} в количестве {Wallet.Format(amount)}, но продал.");
                else Admin.WinLog($"[{RouletteCasesData[caseid].Name}] {player.Name} ({player.Value}) выиграл {RouletteItem.Name} в количестве {Wallet.Format(amount)}!");


                if (RouletteItem.IsChatMessage)
                {
                    switch (RouletteItem.Name)
                    {
                        case "Игровая валюта":
                            NAPI.Chat.SendChatMessageToAll($"~g~[ПРИЗ С КЕЙСА] Ура! {player.Name} получает крайне редкий приз - {Wallet.Format(amount)} {RouletteItem.Name} из {RouletteCasesData[caseid].Name}!");
                            break;
                        case "RedBucks":
                            NAPI.Chat.SendChatMessageToAll($"~g~[ПРИЗ С КЕЙСА] Вот это да! Кармашек {player.Name} пополнился хрустящими редбаксами на сумму {Wallet.Format(amount)} {RouletteItem.Name}! Всему виной {RouletteCasesData[caseid].Name}!");
                            break;
                        default:
                            NAPI.Chat.SendChatMessageToAll($"~g~[ПРИЗ С КЕЙСА] Невероятно! {player.Name} получает редкий приз - {RouletteItem.Name} из {RouletteCasesData[caseid].Name}!");
                            break;
                    }
                }
                if (RouletteItem.IsHudMessage)
                {
                    switch (RouletteItem.Name)
                    {
                        case "Игровая валюта":
                            SupperWin($"{Wallet.Format(amount)} RedBucks из {RouletteCasesData[caseid].Name}!", RouletteItem.Name, RouletteItem.Image, player.Name);
                            break;
                        case "RedBucks":
                            SupperWin($"{Wallet.Format(amount)} RedBucks из {RouletteCasesData[caseid].Name}!", RouletteItem.Name, RouletteItem.Image, player.Name);// NE SMOG
                            break;
                        default:
                            SupperWin($"{RouletteItem.Name} из {RouletteCasesData[caseid].Name}!", RouletteItem.Name, RouletteItem.Image, player.Name);
                            break;
                    }
                }
                if (bonuscase <= freeOpenCaseToPer[caseid]) FreeCaseWinBonus(player);
            }
            catch (Exception e)
            {
                Log.Write($"RouletteWinAnnounce Exception: {e.ToString()}");
            }
        }
        private static void SupperWin(string _title, string _name, string _image, string _desc)
        {
            try
            {
                foreach (ExtPlayer foreachPlayer in Character.Repository.GetPlayers())
                {
                    if (!foreachPlayer.IsCharacterData()) continue;
                    Trigger.ClientEvent(foreachPlayer, "client.roullete.updateWin", _title, _name, _image, _desc);
                }
            }
            catch (Exception e)
            {
                Log.Write($"SupperWin Exception: {e.ToString()}");
            }
        }

        private static void FreeCaseWinBonus(ExtPlayer player)
        {
            try
            {
                var accountData = player.GetAccountData();

                if (accountData == null) return;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.WinOneFreeCase), 3000);
                Commands.RPChat("sb", player, LangFunc.GetText(LangType.En, DataName.WonOneFreeCase));
                AddNewItemWarehouse(player, ItemId.Case0, 1);
            }
            catch (Exception e)
            {
                Log.Write($"FreeCaseWinBonus Exception: {e.ToString()}");
            }
        }

        public static void RouletteConfirmCase(ExtPlayer player, bool type, int listIndex)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                var accountData = player.GetAccountData();
                if (accountData == null) return;
                var sessionData = player.GetSessionData();
                if (sessionData == null || sessionData.RouletteData.Count == 0) return;
                else if (listIndex < 0 || sessionData.RouletteData.Count <= listIndex) return;
                else if (sessionData.RouletteData[listIndex].Done) return;
                RouletteData returnPlayerData = sessionData.RouletteData[listIndex];
                returnPlayerData.Done = true;
                bool isAllDone = true;
                foreach (RouletteData playerData in sessionData.RouletteData)
                {
                    if (!playerData.Done)
                        isAllDone = false;
                }
                if (isAllDone) sessionData.RouletteData.Clear();
                RouletteWinAnnounce(player, returnPlayerData.CaseID, returnPlayerData.Item, returnPlayerData.Amount, type, returnPlayerData.FreeCaseBonus);
                if (type)
                {
                    switch (returnPlayerData.Item.Name)
                    {
                        case "Игровая валюта":
                            Wallet.Change(player, +returnPlayerData.Amount);
                            GameLog.Money($"system", $"player({characterData.UUID})", returnPlayerData.Amount, $"caseWin");
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonMoneyAmount, MoneySystem.Wallet.Format(returnPlayerData.Amount)), 3000);
                            break;
                        case "RedBucks":
                            RouletteUpdateWallet(player, returnPlayerData.Amount, returnPlayerData.TypeCurrency);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonRbAmount, MoneySystem.Wallet.Format(returnPlayerData.Amount)), 3000);
                            break;
                        default:
                            if (returnPlayerData.Item.Name == "Сим-карта")
                                Players.Phone.Sim.Repository.Add(returnPlayerData.Amount);
                            
                            RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonItemRBAmount, returnPlayerData.Item.Name, MoneySystem.Wallet.Format(returnPlayerData.Price)), 3000);
                            break;
                    }
                }
                else
                {
                    switch (returnPlayerData.Item.Name)
                    {
                        case "Игровая валюта":
                            Wallet.Change(player, +returnPlayerData.Amount);
                            GameLog.Money($"system", $"player({characterData.UUID})", returnPlayerData.Amount, $"caseWin");
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetMoneyAmount, MoneySystem.Wallet.Format(returnPlayerData.Amount)), 3000);
                            break;
                        case "EXP":
                            UpdateData.Exp(player, returnPlayerData.Amount);
                            GameLog.Money($"system", $"player({characterData.UUID})", 0, $"caseWinExp({returnPlayerData.Amount})");
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetItemAmount, returnPlayerData.Item.Name, returnPlayerData.Amount), 3000);
                            break;
                        case "VIP Silver":
                        case "VIP Gold":
                        case "VIP Platinum":
                        case "VIP Diamond":
                            int rank = 0;
                            switch (returnPlayerData.Item.Name)
                            {
                                case "VIP Silver":
                                    rank = 1;
                                    break;
                                case "VIP Gold":
                                    rank = 2;
                                    break;
                                case "VIP Platinum":
                                    rank = 3;
                                    break;
                                case "VIP Diamond":
                                    rank = 4;
                                    break;
                                default:
                                    // Not supposed to end up here. 
                                    break;
                            }
                            UpdateVipStatus(player, rank, returnPlayerData.Amount, true);

                            GameLog.Money($"system", $"player({characterData.UUID})", 0, $"caseWinVIP({rank}lvl, {returnPlayerData.Amount}d, стало до {accountData.VipDate.ToString("s")})");
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetVipAmount, returnPlayerData.Item.Name, returnPlayerData.Amount), 3000);
                            break;
                        case "RedBucks":
                            RouletteUpdateWallet(player, returnPlayerData.Amount, returnPlayerData.TypeCurrency);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonRbAmount, MoneySystem.Wallet.Format(returnPlayerData.Amount)), 3000);
                            break;
                        case "Маска":
                            Random rand = new Random();
                            Clothes maskRand = Customization.Masks[rand.Next(0, Customization.Masks.Count)];
                            //if (!ChangeAccessoriesItem(player, 1, $"{maskRand.Variation}_{maskRand.Colors[0]}_{characterData.Gender}"))
                            if (AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId.Mask, 1, $"{maskRand.Variation}_{maskRand.Colors[0]}_{characterData.Gender}") == -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                                RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                                return;
                            }
                            GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinMask({maskRand.Variation})");
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetNewMask), 3000);
                            break;
                        case "Лицензия на вертолёт":
                            if (characterData.Licenses[4])
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouAlreadyHaveLic), 3000);
                                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonRbAmount, MoneySystem.Wallet.Format(returnPlayerData.Price)), 3000);
                                RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                                return;
                            }
                            GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinLic(4)");
                            characterData.Licenses[4] = true;
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouActivatedLicFly), 3000);
                            break;
                        case "Лицензия на самолёт":
                            if (characterData.Licenses[5])
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouAlreadyHaveLic), 3000);
                                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonRbAmount, MoneySystem.Wallet.Format(returnPlayerData.Price)), 3000);
                                RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                                return;
                            }
                            GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinLic(5)");
                            characterData.Licenses[5] = true;
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouActivatedLicSamolet), 3000);
                            break;
                        case "Лицензия парамедика":
                            if (characterData.Licenses[8])
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouAlreadyHaveLic), 3000);
                                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouWonRbAmount, MoneySystem.Wallet.Format(returnPlayerData.Price)), 3000);
                                RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                                return;
                            }
                            GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinLic(8)");
                            characterData.Licenses[8] = true;
                            //PlayerStats(player);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouActivatedPMLic), 3000);
                            break;
                        case "Сим-карта":
                            AddNewItemWarehouse(player, ItemId.SimCard, 1, returnPlayerData.Amount.ToString());
                            GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinItem({ItemId.SimCard},{returnPlayerData.Item.Name})");
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetSimCard, returnPlayerData.Amount), 3000);
                            break;

                        default:
                            bool success = false;
                            if (returnPlayerData.Item.ItemId != ItemId.Debug)
                            {
                                ItemId nameWeapon = returnPlayerData.Item.ItemId;
                                success = true;
                                AddNewItemWarehouse(player, nameWeapon, returnPlayerData.Amount, returnPlayerData.Item.ItemData);
                                GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinItem({nameWeapon},{returnPlayerData.Item.Name})");
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetItemSclad, returnPlayerData.Item.Name), 3000);
                            }
                            else
                            {
                                foreach (KeyValuePair<ItemId, ItemsInfo> itemData in ItemsInfo)
                                {
                                    if (itemData.Value.Name == returnPlayerData.Item.Name)
                                    {
                                        ItemId nameWeapon = itemData.Key;
                                        success = true;
                                        AddNewItemWarehouse(player, nameWeapon, returnPlayerData.Amount, nameWeapon == ItemId.BodyArmor ? 100.ToString() : "DRoulette");
                                        GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinItem({nameWeapon},{returnPlayerData.Item.Name})");
                                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetItemSclad, returnPlayerData.Item.Name), 3000);
                                        break;
                                    }
                                }
                            }
                            if (!success && BusinessManager.BusProductsData.ContainsKey(returnPlayerData.Item.Name))
                            {
                                ItemId nameWeapon = ItemId.CarCoupon;
                                AddNewItemWarehouse(player, nameWeapon, 1, returnPlayerData.Item.Name);
                                GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinItem({nameWeapon},{returnPlayerData.Item.Name})");
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetItemSclad, returnPlayerData.Item.Name), 3000);
                            }
                            /*if (!success && BusinessManager.ProductsOrderPrice.ContainsKey(returnPlayerData.Item.Name))
                            {
                                int mycars = VehicleManager.getAllPlayerVehicles(player.Name).Count;
                                if (mycars >= GarageManager.MaxGarageCars)
                                {
                                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У Вас уже максимальное количество машин, приз будет продан в систему за {returnPlayerData.Price} RedBucks.", 6000);
                                    RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                                    return;
                                }
                                var house = HouseManager.GetHouse(player, true);
                                if (house != null)
                                {
                                    var garage = house.GetGarageData();
                                    if (mycars >= GarageManager.GarageTypes[garage.Type].MaxCars)
                                    {
                                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У Вас уже максимальное количество машин в гараже, приз будет продан в систему за {returnPlayerData.Price} RedBucks.", 3000);
                                        RouletteUpdateWallet(player, returnPlayerData.Price, returnPlayerData.TypeCurrency);
                                        return;
                                    }
                                }
                                string vNumber = VehicleManager.Create(player.Name, returnPlayerData.Item.Name, new Color(225, 225, 225), new Color(225, 225, 225));
                                GameLog.Money($"system", $"player({characterData.UUID})", 1, $"caseWinCar({returnPlayerData.Item.Name}, {vNumber})");
                                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Вы активировали {returnPlayerData.Item.Name} с номером {vNumber}", 3000);
                                AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId.CarKey, 1, $"{vNumber}_0");
                                if (house != null)
                                {
                                    var garage = house.GetGarageData();
                                    if (garage == null) return;
                                    if (garage.Type != -1 && garage.Type != 6) garage.SpawnCar(vNumber);
                                    else garage.GetVehicleFromGarage(vNumber);
                                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"В скором времени она будет доставлена в Ваш гараж", 5000);
                                }
                            }*/
                            break;

                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"RouletteConfirmCase Exception: {e.ToString()}");
            }
        }
        public static void UpdateVipStatus(ExtPlayer player, int rank, int days, bool force = false, bool logWrite = false, string logMessagePrefix = "RefVIP")
        {
            try
            {
                var accountData = player.GetAccountData();
                if (accountData == null) return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (accountData.VipLvl > 0)
                {
                    double unixTime = TimeSpan.FromTicks(accountData.VipDate.Ticks - DateTime.Now.Ticks).TotalSeconds;
                    if (accountData.VipLvl < rank)
                    {
                        if (!force)
                        {
                            double addTime = (days * 86400) * ((rank + 1) - accountData.VipLvl);
                            if (addTime < 86400) addTime = 86400;
                            accountData.VipDate = DateTime.Now.AddSeconds(unixTime + addTime);
                        }
                        else
                        {
                            accountData.VipLvl = rank;
                            accountData.VipDate = DateTime.Now.AddDays(days);
                        }
                    }
                    else if (accountData.VipLvl > rank)
                    {
                        double addTime = (days * 86400) / (accountData.VipLvl - (rank - 1));
                        if (addTime < 86400) addTime = 86400;
                        accountData.VipDate = DateTime.Now.AddSeconds(unixTime + addTime);
                    }
                    else accountData.VipDate = accountData.VipDate.AddDays(days);
                }
                else
                {
                    accountData.VipLvl = rank;
                    accountData.VipDate = DateTime.Now.AddDays(days);
                }

                if (logWrite)
                    GameLog.Money($"server", $"player({characterData.UUID})", 1, $"{logMessagePrefix}({accountData.VipLvl}lvl, стало до: {accountData.VipDate.ToString("s")}, RefId: {accountData.RefferalId})");
            }
            catch (Exception e)
            {
                Log.Write($"UpdateVipStatus Exception: {e.ToString()}");
            }
        }
        public static void RouletteClose(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData != null && sessionData.RouletteData.Count > 0)
                    Events.RoulleteConfirm(player, false, -1);
            }
            catch (Exception e)
            {
                Log.Write($"RouletteClose Exception: {e.ToString()}");
            }
        }

        #endregion

        #region Реффералка
        public static readonly RefferalData[] RefferalsData = new RefferalData[16]
        {
            new RefferalData("Время начать?", 0, 0, 0, true, 200, 5), // Первый, нулевой человек
            new RefferalData("Первые шаги", 1, 4, 1, true, 250, 7), // 1 - 4 человек
            new RefferalData("Начинающий", 5, 4, 1, true, 300, 10), // 5 - 9
            new RefferalData("Минивэнщик", 10, 4, 1, true, 375, 13), // 10 - 14
            new RefferalData("Каптурщик младший", 15, 4, 1, true, 400, 15), // 15 - 19
            new RefferalData("Мясовозчик", 20, 4, 1, true, 425, 17), // 20 - 29
            new RefferalData("Дикий коршун", 30, 4, 1, true, 500, 20), // 30 - 39
            new RefferalData("Боевая единица", 40, 4, 1, true, 550, 22), // 40 - 49
            new RefferalData("Василий мафиозник", 50, 4, 1, true, 600, 25), // 50 - 60
            new RefferalData("Спелый торговец", 60, 4, 1, true, 625, 27), // 60 - 70
            new RefferalData("Владимир II", 70, 4, 1, true, 700, 30), // 70 - 89
            new RefferalData("Известность", 80, 4, 1, true, 750, 32), // 80 - 99
            new RefferalData("Генеральный директор", 100, 4, 1, true, 800, 35), // 100 - 119
            new RefferalData("Генералиссимус", 120, 4, 1, true, 850, 37), // 120 - 149
            new RefferalData("Вице-Эль Президенте", 150, 4, 2, true, 900, 40), // 150 - 200
            new RefferalData("Эль Президенте", 200, 4, 3, true, 1000, 50), // 201+
        };
        #endregion

        #region Обмен номерами

        public static void ChangeAutoNumberInit()
        {
            //Main.CreateBlip(new Main.BlipData(811, "Смена номеров", new Vector3(-2286.025, 354.21, 174.6016), 47, true, 1.5f));
            //NAPI.Marker.CreateMarker(1, new Vector3(-2286.025, 354.21, 172.2016), new Vector3(), new Vector3(), 3f, new Color(255, 255, 255, 220), false, 0);
            //NAPI.TextLabel.CreateTextLabel("~w~Смена номеров", new Vector3(-2286.025, 354.21, 174.6016), 8f, 0.3f, 0, new Color(255, 255, 255), true, 0);
            //CustomColShape.CreateSphereColShape(new Vector3(-1288.142, -579.7515, 30.573048), 2f, 0, ColShapeEnums.ChangeAutoNumber); // Смена номеров
            PedSystem.Repository.CreateQuest("s_m_m_fiboffice_02", new Vector3(-1288.142, -579.7515, 30.573048), 27.65183f, title: "~y~NPC~w~ Эдвард Дэниелс\nСмена номеров", colShapeEnums: ColShapeEnums.ChangeAutoNumber);

        }
        [Interaction(ColShapeEnums.ChangeAutoNumber)]
        public static void OnChangeAutoNumber(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                var vehiclesNumber = VehicleManager.GetVehiclesCarNumberToPlayer(player.Name);
                if (vehiclesNumber.Count == 0)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouNeedNot2Cars), 3000);
                    return;
                }
                int count = 0;
                foreach (string number in vehiclesNumber)
                {
                    var vehicleData = VehicleManager.GetVehicleToNumber(number);
                    if (vehicleData == null) continue;
                    if (!BusinessManager.CarsNames[2].Contains(vehicleData.Model) && 
                        !BusinessManager.CarsNames[3].Contains(vehicleData.Model)) count++;
                }
                if (count <= 1)
                {
                    TradeClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouNeedNot2CarsPremium), 3000);
                    return;
                }
                string Number1 = null;
                var sessionData = player.GetSessionData();
                if (sessionData.ChangeAutoNumber != null) 
                    Number1 = sessionData.ChangeAutoNumber.Number1;


                var frameList = new FrameListData();

                frameList.Header = "Выбор машины";
                frameList.Callback = CallbackChangeAutoNumber;
                
                foreach (string number in vehiclesNumber)
                {
                    var vehicleData = VehicleManager.GetVehicleToNumber(number);
                    if (vehicleData == null) continue;
                    if (BusinessManager.CarsNames[2].Contains(vehicleData.Model) || 
                        BusinessManager.CarsNames[3].Contains(vehicleData.Model)) continue;
                    if (Number1 == number) continue;
                    
                    frameList.List.Add(new ListData($"{vehicleData.Model} - {number}", number));
                }
                
                Players.Popup.List.Repository.Open(player, frameList);
            }
            catch (Exception e)
            {
                Log.Write($"OnChangeAutoNumber Exception: {e.ToString()}");
            }
        }

        private static void CallbackChangeAutoNumber(ExtPlayer player, object listItem)
        {
            try
            {
                if (!player.IsCharacterData())
                {
                    ChangeAutoNumberClear(player);
                    return;
                }
                
                if (!(listItem is string) || listItem == null)
                {
                    ChangeAutoNumberClear(player);
                    return;
                }

                var number = Convert.ToString(listItem);
                
                var vehicleData1 = VehicleManager.GetVehicleToNumber(number);
                if (vehicleData1 == null)
                {
                    ChangeAutoNumberClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CarBoleeNetu), 3000);
                    return;
                }
                if (BusinessManager.CarsNames[2].Contains(vehicleData1.Model) || BusinessManager.CarsNames[3].Contains(vehicleData1.Model))
                {
                    ChangeAutoNumberClear(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VehNePodhoditDlyaObmena), 3000);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData.ChangeAutoNumber == null)
                {
                    sessionData.ChangeAutoNumber = new ChangeAutoNumber(number);
                    OnChangeAutoNumber(player);
                }
                else
                {
                    var vehicleData2 = VehicleManager.GetVehicleToNumber(sessionData.ChangeAutoNumber.Number1);
                    if (vehicleData2 == null)
                    {
                        ChangeAutoNumberClear(player);
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CarBoleeNetu), 3000);
                        return;
                    }
                    if (BusinessManager.CarsNames[2].Contains(vehicleData2.Model) || BusinessManager.CarsNames[3].Contains(vehicleData2.Model))
                    {
                        ChangeAutoNumberClear(player);
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VehNePodhoditDlyaObmena), 3000);
                        return;
                    }
                    sessionData.ChangeAutoNumber.Number2 = number;
                    Trigger.ClientEvent(player, "openDialog", "ChangeAutoNumberConfirm", $"Вы уверены что хотите поменять номера местами автомобилей {vehicleData1.Model} и {vehicleData2.Model}?<br/><br/>Стоимость обмена 70.000$");
                }
            }
            catch (Exception e)
            {
                Log.Write($"CallbackChangeAutoNumber Exception: {e.ToString()}");
            }
        }
        public static void ChangeAutoNumberConfirm(ExtPlayer player, bool toggled)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null)
                {
                    ChangeAutoNumberClear(player);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (sessionData.ChangeAutoNumber == null) return;
                else if (!toggled)
                {
                    ChangeAutoNumberClear(player);
                    return;
                }

                if (characterData.Money < 70000)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoney), 3000);
                    return;
                }
                var changeAutoNumber = sessionData.ChangeAutoNumber;
                ChangeAutoNumberClear(player);
                var vehicle1Data = VehicleManager.GetVehicleToNumber(changeAutoNumber.Number1);
                var vehicle2Data = VehicleManager.GetVehicleToNumber(changeAutoNumber.Number2);
                if (vehicle1Data == null || vehicle2Data == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VehDoesntExist), 3000);
                    return;
                }
                Wallet.Change(player, -70000);

                GameLog.Money($"player({characterData.UUID})", $"system", 70000, $"vNumSwap({changeAutoNumber.Number1},{vehicle1Data.Model}<->{changeAutoNumber.Number2},{vehicle2Data.Model})");

                var house = HouseManager.GetHouse(player, true);
                var garage = house?.GetGarageData();
                garage?.DeleteCar(changeAutoNumber.Number1);
                garage?.DeleteCar(changeAutoNumber.Number2);     

                ChangeAutoNumberUpdate(changeAutoNumber.Number2, vehicle1Data, garage);
                ChangeAutoNumberUpdate(changeAutoNumber.Number1, vehicle2Data, garage);

                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SucNumberTrade), 3000);
            }
            catch (Exception e)
            {
                Log.Write($"ChangeAutoNumberConfirm Exception: {e.ToString()}");
            }
        }
        public static void ChangeAutoNumberUpdate(string newNum, VehicleData.Models.VehicleData vehicleData, Garage garage)
        {
            try
            {
                if (!VehicleManager.IsVehicleToNumber(newNum)) return;

                vehicleData.Number = newNum;
                VehicleManager.Vehicles[newNum] = vehicleData;
                VehicleManager.VehiclesSqlIdToNumber[vehicleData.SqlId] = newNum;
                
                garage?.SpawnCar(newNum);
                
                VehicleManager.SaveNumber(newNum);
            }
            catch (Exception e)
            {
                Log.Write($"ChangeAutoNumberUpdate Exception: {e.ToString()}");
            }
        }
        public static void ChangeAutoNumberClear(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                sessionData.ChangeAutoNumber = null;
            }
            catch (Exception e)
            {
                Log.Write($"ChangeAutoNumberClear Exception: {e.ToString()}");
            }
        }
        #endregion

        #region Инвентарь
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<int, InventoryItemData>>> ItemsData = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<int, InventoryItemData>>>();

        public static ConcurrentDictionary<string, List<ExtPlayer>> InventoryOtherPlayers = new ConcurrentDictionary<string, List<ExtPlayer>>();

        /*
 *
public static IReadOnlyDictionary<ClothesComponent, ItemId> ClothesComponentToItemId = new Dictionary<ClothesComponent, ItemId>() 
{ 
{ ClothesComponent.Hat, ItemId.Hat }, 
{ ClothesComponent.Masks, ItemId.Mask }, 
{ ClothesComponent.Ears, ItemId.Ears }, 
{ ClothesComponent.Glasses, ItemId.Glasses }, 
{ ClothesComponent.Accessories, ItemId.Jewelry },// 
{ ClothesComponent.Tops, ItemId.Top }, 
{ ClothesComponent.Undershort, ItemId.Undershit }, 
{ ClothesComponent.BodyArmors, ItemId.BodyArmor }, 
{ ClothesComponent.Bugs, ItemId.Bag }, 
{ ClothesComponent.Legs, ItemId.Leg }, 
{ ClothesComponent.Bracelets, ItemId.Bracelets }, 
{ ClothesComponent.Watches, ItemId.Watches }, 
{ ClothesComponent.Torsos, ItemId.Gloves }, 
{ ClothesComponent.Shoes, ItemId.Feet }, 
{ ClothesComponent.Decals, ItemId.Decals }, 
}; 
 */
        
        public static IReadOnlyDictionary<int, ItemId> AccessoriesInfo = new Dictionary<int, ItemId>()
        {
            { 0, ItemId.Hat },
            { 1, ItemId.Mask },
            { 2, ItemId.Ears },
            { 3, ItemId.Glasses },
            { 4, ItemId.Jewelry },
            { 5, ItemId.Top },
            { 6, ItemId.Undershit },
            { 7, ItemId.BodyArmor },
            { 8, ItemId.Bag },
            { 9, ItemId.Leg },
            { 10, ItemId.Bracelets },
            { 11, ItemId.Watches },
            { 12, ItemId.Gloves },
            { 13, ItemId.Feet },
            { 14, ItemId.Decals },
        };
        
        public static IReadOnlyDictionary<ClothesComponent, ClothesComponentId> ClothesComponentToPropId = new Dictionary<ClothesComponent, ClothesComponentId>()
        {
            { ClothesComponent.Hat, new ClothesComponentId(0, ItemId.Hat, 0) },
            { ClothesComponent.Ears, new ClothesComponentId(2, ItemId.Ears, 2) },
            { ClothesComponent.Glasses, new ClothesComponentId(1, ItemId.Glasses, 3) },
            { ClothesComponent.Bracelets, new ClothesComponentId(7, ItemId.Bracelets, 10) },
            { ClothesComponent.Watches, new ClothesComponentId(6, ItemId.Watches, 11) },
        };
        
        
        public static IReadOnlyDictionary<ClothesComponent, ClothesComponentId> ClothesComponentToComponentId = new Dictionary<ClothesComponent, ClothesComponentId>()
        {
            { ClothesComponent.Masks, new ClothesComponentId(1, ItemId.Mask, 1) },
            { ClothesComponent.Accessories, new ClothesComponentId(7, ItemId.Jewelry, 4) },//
            { ClothesComponent.Tops, new ClothesComponentId(11, ItemId.Top, 5) },
            { ClothesComponent.Undershort, new ClothesComponentId(8, ItemId.Undershit, 6) },
            { ClothesComponent.BodyArmors, new ClothesComponentId(9, ItemId.BodyArmor, 7) },
            { ClothesComponent.Bugs, new ClothesComponentId(5, ItemId.Bag, 8) },
            { ClothesComponent.Legs, new ClothesComponentId(4, ItemId.Leg, 9) },
            { ClothesComponent.Torsos, new ClothesComponentId(3, ItemId.Gloves, 12) },
            { ClothesComponent.Shoes, new ClothesComponentId(6, ItemId.Feet, 13) },
            { ClothesComponent.Decals, new ClothesComponentId(10, ItemId.Decals, 14) },
        };

        public static IReadOnlyDictionary<ItemId, ItemsInfo> ItemsInfo = new Dictionary<ItemId, ItemsInfo>()
        {
            { ItemId.Mask, new ItemsInfo("Mask", "Helps to hide your identity, but also serves as a decoration. Can be torn off by a police officer.", "inv-item-mask", "Clothing", 3887136870, 1, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Clothes) },
            { ItemId.Gloves, new ItemsInfo("Gloves", "Will keep you warm on a cold winter day.", "inv-item-glove", "Clothing", 3125389411, 1, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Clothes) },
            { ItemId.Ears, new ItemsInfo("Headphones", ".", "inv-item-ears", "Clothing", 3125389411, 1, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Clothes) },
            { ItemId.Leg, new ItemsInfo("Pants", "They won't let you freeze.", "inv-item-shorts", "Clothing", 2086911125, 1, new Vector3(0.0,0.0,-0.85), new Vector3(), newItemType.Clothes) },

            { ItemId.Bag, new ItemsInfo("Bag", "Allows you to carry any items inside.", "inv-item-backpack", "Clothing", NAPI.Util.GetHashKey("prop_cs_heist_bag_02"), 1, new Vector3(0, 0, -0.85), new Vector3(), newItemType.Clothes) },

            { ItemId.Feet, new ItemsInfo("Footwear", "A modern style that will never let you down.", "inv-item-sneakers", "Clothing", 1682675077, 1, new Vector3(0.0,0.0,-0.95), new Vector3(), newItemType.Clothes) },
            { ItemId.Jewelry, new ItemsInfo("Accessory", "Allows you to improve your character's appearance.", "inv-item-necklace", "Clothing", 2329969874, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.Clothes) },
            { ItemId.Undershit, new ItemsInfo("Undershirt", "Can be worn under outerwear, gives your character a stylish look.", "inv-item-shirt", "Clothing", 578126062, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.Clothes) },
            { ItemId.BodyArmor, new ItemsInfo("Body Armor", "Serves as protection for the character, able to absorb 100 damage before it breaks.", "inv-item-armor", "Clothing", 701173564, 1, new Vector3(0.0,0.0,-0.88), new Vector3(90, 90, 0), newItemType.Clothes) },
            { ItemId.Decals, new ItemsInfo("Decals", "Allows you to improve your character's appearance.", "inv-item-clock", "Clothing", 0, 1, new Vector3(), new Vector3(), newItemType.Clothes) },
            { ItemId.Top, new ItemsInfo("Top/Jacket", "Can be worn over an undershirt, gives your character a stylish look.", "inv-item-jacket", "Clothing", 3038378640, 1, new Vector3(0.0,0.0,-0.96), new Vector3(), newItemType.Clothes) },
            { ItemId.Hat, new ItemsInfo("Headwear", "Will save you from sunstroke on a hot day.", "inv-item-cap", "Clothing", 1619813869, 1, new Vector3(0.0,0.0,-0.93), new Vector3(), newItemType.Clothes) },
            { ItemId.Glasses, new ItemsInfo("Glasses", "Will protect you from the sun on the sunniest day.", "inv-item-glasses", "Clothing", 2329969874, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.Clothes) },
            { ItemId.Bracelets, new ItemsInfo("Bracelet", "Allows you to improve your character's appearance.", "inv-item-bracelet", "Clothing", 2329969874, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.Clothes) },
            { ItemId.Watches, new ItemsInfo("Watch", "Shows the status of your character.", "inv-item-clock", "Clothing", 2329969874, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.Clothes) },

            { ItemId.Debug, new ItemsInfo("None", "", "", "", 0, 0, new Vector3(), new Vector3(), newItemType.None) },
            { ItemId.BagWithDrill, new ItemsInfo("Bag with Drill", "Used to break into the vault.","inv-item-Bag-drill", "Other", NAPI.Util.GetHashKey("prop_cs_heist_bag_02"), 1, new Vector3(0, 0, -0.85), new Vector3(), newItemType.None) },
            { ItemId.GasCan, new ItemsInfo("Gas Can", "Can be used to refuel a vehicle.","inv-item-gasoline", "Tools", 786272259, 2, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.None) },
            { ItemId.Crisps, new ItemsInfo("Chips", "A snack, restores 30% health.","inv-item-Chips", "Food", 2564432314, 4, new Vector3(0.0,0.0,-1.0), new Vector3(90, 90, 0), newItemType.Eat) },
            { ItemId.Beer, new ItemsInfo("Beer", "A cold one for the road!","inv-item-beer", "Water", 1940235411, 5, new Vector3(0.0,0.0,-0.97), new Vector3(-80, 0, 0), newItemType.Water) },
            { ItemId.Pizza, new ItemsInfo("Pizza", "Mmm... Pizza... Restores 30% health.","inv-item-pizza", "Food", 604847691, 3, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Eat) },
            { ItemId.Burger, new ItemsInfo("Burger", "A bun with a patty, restores 30% health.","inv-item-burger", "Food", 2240524752, 4, new Vector3(0.0,0.0,-0.97), new Vector3(), newItemType.Eat) },
            { ItemId.HotDog, new ItemsInfo("Hot Dog", "A bun with a sausage, restores 30% health.","inv-item-hot-dog", "Food", 2565741261, 5, new Vector3(0.0,0.0,-0.97), new Vector3(), newItemType.Eat) },
            { ItemId.Sandwich, new ItemsInfo("Sandwich", "A few slices of bread and meat, restores 30% health.","inv-item-sandwich", "Food", 987331897, 7, new Vector3(0.0,0.0,-0.99), new Vector3(), newItemType.Eat) },
            { ItemId.eCola, new ItemsInfo("eCola", "A non-alcoholic carbonated drink, restores 10% health.","inv-item-eCola", "Water", 144995201, 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Water) },
            { ItemId.Sprunk, new ItemsInfo("Sprunk", "A highly carbonated soft drink, restores 10% health.","inv-item-eCola", "Water", 2973713592, 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Water) },
            { ItemId.Lockpick, new ItemsInfo("Lockpick", "A tool for opening locks without a key.","inv-item-picklock", "Tool", 977923025, 10, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            { ItemId.BagWithMoney, new ItemsInfo("Bag with Money", "You can put all the money in here after robbing a 24/7.","inv-item-briefcase", "Other", NAPI.Util.GetHashKey("p_ld_heist_bag_s_pro"), 1, new Vector3(0, 0, -1.1), new Vector3(0, 30, 110), newItemType.None) },
            { ItemId.Material, new ItemsInfo("Materials", "Needed to create weapons and ammunition.","inv-item-fraction", "Other", 3045218749, 4000, new Vector3(0.0,0.0,-0.6), new Vector3(), newItemType.None) },
            { ItemId.Drugs, new ItemsInfo("Drugs", "This magical tea produces a narcotic effect.","inv-item-marijuana", "Other", 4293279169, 50, new Vector3(0.0,0.0,-0.95), new Vector3(), newItemType.None) },
            { ItemId.HealthKit, new ItemsInfo("First-Aid Kit","Needed for healing, can be used once every 5 minutes.","inv-item-medical-kit", "Other", 678958360, 10, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.ArmyLockpick, new ItemsInfo("Military Lockpick", "A tool for opening the locks of military vehicles.","inv-item-picklock", "Tool", 977923025, 10, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            { ItemId.Pocket, new ItemsInfo("Sack", "A regular sack. They say you can put it on someone's head...","inv-item-bag", "Tool", 3887136870, 5, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            { ItemId.Cuffs, new ItemsInfo("Zip Ties", "Allows you to tie up a person with their hands up.","inv-item-stretching", "Tool", 3887136870, 5, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            { ItemId.CarKey, new ItemsInfo("Car Keys", "A tool that allows you to lock/unlock and start a car.","inv-item-key", "Tool", 977923025, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            { ItemId.Present, new ItemsInfo("Gift", "Contains something interesting! Open it quickly!","inv-item-gift", "Tool", 1580014892, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            { ItemId.KeyRing, new ItemsInfo("Key Ring", "Gathers all your keys into one large ring and saves space in your pockets!","inv-item-key-2", "Tool", 977923025, 1, new Vector3(0.0,0.0,-0.98), new Vector3(), newItemType.None) },
            /* Drinks */
            { ItemId.RusDrink1, new ItemsInfo("<On a Lemon Peel>", "An alcoholic drink with notes of lemon, causes an intoxicating effect.","inv-item-On-lemon-peel", "Alcohol", NAPI.Util.GetHashKey("prop_rum_bottle"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.RusDrink2, new ItemsInfo("<On Lingonberry>", "An alcoholic drink with a light note of lingonberry, causes an intoxicating effect.","inv-item-The-cranberries", "Alcohol", NAPI.Util.GetHashKey("h4_prop_h4_t_bottle_01a"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.RusDrink3, new ItemsInfo("<Russian Standard>", "Good old Russian vodka, causes an intoxicating effect.","inv-item-Russian-standard", "Alcohol", NAPI.Util.GetHashKey("prop_vodka_bottle"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.YakDrink1, new ItemsInfo("<Asahi>", "A beer with low malt content, causes an intoxicating effect.","inv-item-Asahi", "Alcohol", NAPI.Util.GetHashKey("prop_bottle_brandy"), 5, new Vector3(0.0,0.0,-0.87), new Vector3(), newItemType.Alco) },
            { ItemId.YakDrink2, new ItemsInfo("<Midori>", "A sweet melon liqueur, causes an intoxicating effect.","inv-item-Midori", "Alcohol", NAPI.Util.GetHashKey("prop_bottle_cognac"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.YakDrink3, new ItemsInfo("<Yamazaki>", "A Japanese whiskey, causes an intoxicating effect.","inv-item-Yamazaki", "Alcohol", NAPI.Util.GetHashKey("prop_bottle_macbeth"), 5, new Vector3(0.0,0.0,-0.87), new Vector3(), newItemType.Alco) },
            { ItemId.LcnDrink1, new ItemsInfo("<Martini Asti>", "A sparkling wine, causes an intoxicating effect.","inv-item-Martini-Asti", "Alcohol", NAPI.Util.GetHashKey("p_amb_bag_bottle_01"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.LcnDrink2, new ItemsInfo("<Sambuca>", "A strong liqueur with a pleasant anise flavor, causes an intoxicating effect.","inv-item-Sambuca", "Alcohol", NAPI.Util.GetHashKey("prop_cs_whiskey_bottle"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.LcnDrink3, new ItemsInfo("<Campari>", "A bitter liqueur based on aromatic herbs and fruits.","inv-item-Campari", "Alcohol", NAPI.Util.GetHashKey("prop_bottle_richard"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.ArmDrink1, new ItemsInfo("<Jivan>", "An Armenian cognac, causes an intoxicating effect.","inv-item-alcohol", "Alcohol", NAPI.Util.GetHashKey("h4_prop_h4_t_bottle_02a"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.ArmDrink2, new ItemsInfo("<Ararat>", "An Armenian cognac with fruity-floral notes, causes an intoxicating effect.","inv-item-ararat", "Alcohol", NAPI.Util.GetHashKey("p_cs_bottle_01"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            { ItemId.ArmDrink3, new ItemsInfo("<Noyan Tapan>", "An Armenian wine drink, causes an intoxicating effect.","inv-item-Noyan-Tapan", "Alcohol", NAPI.Util.GetHashKey("prop_tequila_bottle"), 5, new Vector3(0.0,0.0,-1.0), new Vector3(), newItemType.Alco) },
            /* Weapons */
            /* Pistols */
            { ItemId.Pistol, new ItemsInfo("Pistol", "Standard pistol, magazine holds 12 rounds.","inv-item-Pistol", "Weapon", 1467525553, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CombatPistol, new ItemsInfo("Combat Pistol", "Combat pistol, magazine holds 12 rounds.","inv-item-Pistol", "Weapon", 403140669, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Pistol50, new ItemsInfo("Pistol .50", "Powerful .50 caliber pistol, magazine holds 9 rounds.","inv-item-Pistol-50", "Weapon", 4116483281, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SNSPistol, new ItemsInfo("SNS Pistol", "Pocket-sized .25 caliber pistol, magazine holds 6 rounds.","inv-item-SNS-Pistol", "Weapon", 339962010, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.HeavyPistol, new ItemsInfo("Heavy Pistol", "Heavy pistol, magazine holds 18 rounds.","inv-item-Heavy-Pistol", "Weapon", 1927398017, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.VintagePistol, new ItemsInfo("Vintage Pistol", "Vintage pistol, magazine holds 7 rounds.","inv-item-Vintage-Pistol", "Weapon", 3170921020, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MarksmanPistol, new ItemsInfo("Marksman Pistol", "Marksman pistol, holds 1 round.","inv-item-Marksman-Pistol", "Weapon", 4191177435, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Revolver, new ItemsInfo("Revolver", "Revolver, holds 6 rounds.","inv-item-Heavy-Revolver", "Weapon", 914615883, 1, new Vector3(0.0,0.0,-0.95), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.APPistol, new ItemsInfo("AP Pistol", "Armor-piercing pistol, magazine holds 18 rounds.","inv-item-AP-Pistol", "Weapon", 905830540, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.StunGun, new ItemsInfo("Stun Gun", "An electroshock weapon, holds 1 charge.","inv-item-Stun-Gun", "Melee Weapon", 1609356763, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.FlareGun, new ItemsInfo("Flare Gun", "A signal pistol.","inv-item-Flare-Gun", "Weapon", 1349014803, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.DoubleAction, new ItemsInfo("Double Action Revolver", "Self-cocking revolver, holds 6 rounds.","inv-item-Double-Action-Revolver", "Weapon", 1393678102, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.PistolMk2, new ItemsInfo("Pistol Mk2", "An improved version of the standard pistol, magazine holds 12 rounds.","inv-item-Pistol-Mk-II", "Weapon", 995074671, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SNSPistolMk2, new ItemsInfo("SNS Pistol Mk2", "An improved version of the pocket pistol, magazine holds 6 rounds.","inv-item-SNS-Pistol-Mk-II", "Weapon", 4221916961, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.RevolverMk2, new ItemsInfo("Heavy Revolver Mk2", "An improved version of the standard revolver, holds 6 rounds.","inv-item-Heavy-Revolver-Mk-II", "Weapon", 4065179617, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            /* SMG */
            { ItemId.MicroSMG, new ItemsInfo("Micro SMG", "Compact submachine gun, magazine holds 16 rounds.","inv-item-Micro-SMG", "Weapon", 3238253642, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MachinePistol, new ItemsInfo("Machine Pistol", "Automatic pistol, magazine holds 12 rounds.","inv-item-Machine-Pistol", "Weapon", 3963421467, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SMG, new ItemsInfo("SMG", "Submachine gun, magazine holds 30 rounds.","inv-item-SMG", "Weapon", 3794909300, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.AssaultSMG, new ItemsInfo("Assault SMG", "Assault submachine gun, magazine holds 30 rounds.","inv-item-Assault-SMG", "Weapon", 3821393119, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CombatPDW, new ItemsInfo("Combat PDW", "Compact assault rifle, magazine holds 30 rounds.","inv-item-Combat-PDW", "Weapon", 2901952492, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MG, new ItemsInfo("MG", "Heavy machine gun, magazine holds 54 rounds.","inv-item-MG", "Weapon", 2238602894, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CombatMG, new ItemsInfo("Combat MG", "Special purpose machine gun, magazine holds 100 rounds.","inv-item-Combat-MG", "Weapon", 3555572849, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Gusenberg, new ItemsInfo("Gusenberg Sweeper", "Submachine gun, magazine holds 30 rounds.","inv-item-Gusenberg-Sweeper", "Weapon", 574348740, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MiniSMG, new ItemsInfo("Mini SMG", "Compact submachine gun, magazine holds 20 rounds.","inv-item-Mini-SMG", "Weapon", 3322144245, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SMGMk2, new ItemsInfo("SMG Mk2", "Improved submachine gun, magazine holds 30 rounds.","inv-item-SMG-Mk-II", "Weapon", 2547423399, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CombatMGMk2, new ItemsInfo("Combat MG Mk2", "Upgraded general-purpose machine gun, holds 100 rounds.", "inv-item-Combat-MG-Mk-II", "Weapon", 2969831089, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            /* Rifles */
            { ItemId.AssaultRifle, new ItemsInfo("Assault Rifle", "Assault rifle, magazine holds 30 rounds.","inv-item-Assault-Rifle", "Weapon", 273925117, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CarbineRifle, new ItemsInfo("Carbine Rifle", "American semi-automatic rifle, magazine holds 30 rounds.","inv-item-Carbine-Rifle", "Weapon", 1026431720, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.AdvancedRifle, new ItemsInfo("Advanced Rifle", "Advanced assault rifle, magazine holds 30 rounds.","inv-item-Advanced-Rifle", "Weapon", 2587382322, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SpecialCarbine, new ItemsInfo("Special Carbine",  "Assault rifle with less recoil, magazine holds 30 rounds.","inv-item-Special-Carbine", "Weapon", 2549323539, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.BullpupRifle, new ItemsInfo("Bullpup Rifle", "Chinese assault rifle, magazine holds 30 rounds.","inv-item-Bullpup-Rifle", "Weapon", 3006407723, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CompactRifle, new ItemsInfo("Compact Rifle", "Shortened rifle, magazine holds 30 rounds.","inv-item-Compact-Rifle", "Weapon", 1931114084, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.AssaultRifleMk2, new ItemsInfo("Assault Rifle Mk2", "Improved assault rifle, magazine holds 30 rounds.","inv-item-Assault-Rifle-Mk-II", "Weapon", 1762764713, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CarbineRifleMk2, new ItemsInfo("Carbine Rifle Mk2", "Improved semi-automatic rifle, magazine holds 30 rounds.","inv-item-Carbine-Rifle-Mk-II", "Weapon", 1520780799, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SpecialCarbineMk2, new ItemsInfo("Special Carbine Mk2", "Improved assault rifle with a shorter barrel, magazine holds 30 rounds.","inv-item-Special-Carbine-Mk-II", "Weapon", 2379721761, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.BullpupRifleMk2, new ItemsInfo("Bullpup Rifle Mk2", "Improved Chinese assault rifle, magazine holds 30 rounds.","inv-item-Bullpup-Rifle-Mk-II", "Weapon", 1415744902, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MilitaryRifle, new ItemsInfo("Military Rifle", "Advanced assault rifle, magazine holds 30 rounds.","inv-item-militaryrifle", "Weapon", 1415744902, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            /* Sniper */
            { ItemId.SniperRifle, new ItemsInfo("Sniper Rifle", "Sniper rifle, magazine holds 10 rounds.","inv-item-Sniper-Rifle", "Weapon", 346403307, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.HeavySniper, new ItemsInfo("Heavy Sniper", "High-caliber sniper rifle, magazine holds 6 rounds.","inv-item-Heavy-Sniper", "Weapon", 3548001216, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MarksmanRifle, new ItemsInfo("Marksman Rifle", "Marksman sniper rifle, magazine holds 8 rounds.","inv-item-Marksman-Rifle", "Weapon", 2583718658, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.HeavySniperMk2, new ItemsInfo("Heavy Sniper Mk2", "Improved high-caliber sniper rifle, magazine holds 6 rounds.","inv-item-Heavy-Sniper-Mk-II", "Weapon", 619715967, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.MarksmanRifleMk2, new ItemsInfo("Marksman Rifle Mk2", "Improved marksman sniper rifle, magazine holds 8 rounds.","inv-item-Marksman-Rifle-Mk-II", "Weapon", 2436666926, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            /* Shotguns */
            { ItemId.PumpShotgun, new ItemsInfo("Pump Shotgun", "Tactical combat pump shotgun, holds 8 shells.","inv-item-Pump-Shotgun", "Weapon", 689760839, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SawnOffShotgun, new ItemsInfo("Sawn-Off Shotgun", "Sawn-off shotgun, holds 8 shells.","inv-item-Sawed-Off-Shotgun", "Weapon", 3619125910, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.BullpupShotgun, new ItemsInfo("Bullpup Shotgun", "Pump shotgun, holds 14 shells.","inv-item-Bullpup-Shotgun", "Weapon", 2696754462, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.AssaultShotgun, new ItemsInfo("Assault Shotgun", "Assault shotgun, holds 8 shells.","inv-item-Assault-Shotgun", "Weapon", 1255410010, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Musket, new ItemsInfo("Musket", "Handheld firearm, holds 1 round.","inv-item-Musket", "Weapon", 1652015642, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.HeavyShotgun, new ItemsInfo("Heavy Shotgun", "Heavy six-shot shotgun, holds 6 shells.","inv-item-Heavy-Shotgun", "Weapon", 3085098415, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.DoubleBarrelShotgun, new ItemsInfo("Double Barrel Shotgun", "Double-barrel shotgun, holds 2 shells.","inv-item-Double-Barrel-Shotgun", "Weapon", 222483357, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.SweeperShotgun, new ItemsInfo("Sweeper Shotgun", "Compact rapid-fire shotgun, holds 10 shells.","inv-item-Sweeper-Shotgun", "Weapon", NAPI.Util.GetHashKey("w_sg_sweeper"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.PumpShotgunMk2, new ItemsInfo("Pump Shotgun Mk2", "Improved pump shotgun, holds 8 shells.","inv-item-Pump-Shotgun-Mk-II", "Weapon", 3194406291, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
     
            /* NEW WEAPONS */
            { ItemId.RayPistol, new ItemsInfo("Up-n-Atomizer", "Futuristic pistol resembling an alien gun, requires no ammunition.", "inv-item-Vintage-Pistol", "Weapon", NAPI.Util.GetHashKey("w_pi_raygun"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CeramicPistol, new ItemsInfo("Ceramic Pistol", "Ceramic pistol, magazine holds 12 rounds.", "inv-item-Combat-Pistol", "Weapon", NAPI.Util.GetHashKey("w_pi_ceramic_pistol"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.NavyRevolver, new ItemsInfo("Navy Revolver", "Army revolver, holds 6 rounds.", "inv-item-Heavy-Revolver-Mk-II", "Weapon", NAPI.Util.GetHashKey("w_pi_wep2_gun"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.RayCarbine, new ItemsInfo("Unholy Hellbringer", "Futuristic plasma rifle with an unusual appearance.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_ar_srifle"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.GrenadeLauncher, new ItemsInfo("Grenade Launcher", "Lightweight semi-automatic grenade launcher, holds up to 10 rounds.", "", "Weapon", NAPI.Util.GetHashKey("w_lr_grenadelauncher"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.RPG, new ItemsInfo("RPG", "Handheld anti-tank rocket launcher, holds 1 rocket.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_lr_rpg"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Minigun, new ItemsInfo("Minigun", "6-barrel machine gun with a very high rate of fire, holds 595 rounds.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_mg_minigun"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Firework, new ItemsInfo("Firework Launcher", "A launcher for fireworks, helps to lift the mood or celebrate a holiday.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_mg_minigun"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Railgun, new ItemsInfo("Railgun", "A heavy rail weapon with high damage, holds up to 20 rounds.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_ar_railgun"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.HomingLauncher, new ItemsInfo("Homing Launcher", "A rocket launcher with infrared homing, holds 1 rocket.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_lr_homing"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.GrenadeLauncherSmoke, new ItemsInfo("Smoke Grenade Launcher", "A light grenade launcher that fires smoke grenades instead of regular ones.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_lr_grenadelauncher"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.CompactGrenadeLauncher, new ItemsInfo("Compact Grenade Launcher", "A compact grenade launcher that holds only 1 round.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_lr_compactgl"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Widowmaker, new ItemsInfo("Widowmaker", "A futuristic plasma machine gun, holds up to 9999 rounds.", "NEEDTEXT", "Weapon", NAPI.Util.GetHashKey("w_mg_sminigun"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
 
            /* MELEE WEAPONS */
            { ItemId.Knife, new ItemsInfo("Knife", "A sharp knife that you can cut yourself with.","inv-item-Knife", "Melee Weapon", 2312523967, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Nightstick, new ItemsInfo("Nightstick", "Found a lawbreaker? Time to use the nightstick.","inv-item-Nightstick", "Melee Weapon", 2659989060, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Hammer, new ItemsInfo("Hammer", "A hammer will help you with household chores.","inv-item-Hammer", "Melee Weapon", 64104227, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Bat, new ItemsInfo("Baseball Bat", "A bat is great for playing baseball.","inv-item-Baseball-Bat", "Melee Weapon", 32653987, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Crowbar, new ItemsInfo("Crowbar", "The crowbar is one of the most popular tools.","inv-item-crowbar", "Melee Weapon", 1862268168, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.GolfClub, new ItemsInfo("Golf Club", "Will let you have a great time on the golf course.","inv-item-Golf-Club", "Melee Weapon", 3714771050, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Bottle, new ItemsInfo("Broken Bottle", "A shiv made from a bottle.","inv-item-Broken-Bottle", "Melee Weapon", 1150762982, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Dagger, new ItemsInfo("Dagger", "Very suitable for collectors of rare weapons.","inv-item-Antique-Cavalry-Dagger", "Melee Weapon", 601713565, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Hatchet, new ItemsInfo("Hatchet", "Need to chop some wood? The hatchet is an excellent helper.","inv-item-Hatchet", "Melee Weapon", 1653948529, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.KnuckleDuster, new ItemsInfo("Brass Knuckles", "An excellent helper in street fights.","inv-item-Brass-Knuckles", "Melee Weapon", 3005998612, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Machete, new ItemsInfo("Machete", "One of the most effective weapons for survival in the wild jungle. ","inv-item-Machete", "Melee Weapon", 2239480765, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Flashlight, new ItemsInfo("Flashlight", "A bright light that will help you if you get lost in a dark forest.","inv-item-Flashlight", "Melee Weapon", 2278481040, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.SwitchBlade, new ItemsInfo("Switchblade", "A folding knife that will help you solve many problems.","inv-item-Switchblade", "Melee Weapon", 3331136096, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.PoolCue, new ItemsInfo("Pool Cue", "Perfect for playing billiards.","inv-item-Pool-Cue", "Melee Weapon", 1184113278, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.Wrench, new ItemsInfo("Pipe Wrench", "The wrench is your truest helper if something breaks.","inv-item-Pipe-Wrench", "Melee Weapon", 1959553115, 1, new Vector3(0.0,0.0,-0.985), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            { ItemId.BattleAxe, new ItemsInfo("Battle Axe", "Need to chop up some enemies? The axe is an excellent helper for that.","inv-item-Battle-Axe", "Melee Weapon", 3406411762, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.MeleeWeapons) },
            /* Ammo */
            { ItemId.PistolAmmo, new ItemsInfo("Pistol Ammo", "This ammo is perfect for your pistol.","inv-item-ammo-pistol", "Ammo", NAPI.Util.GetHashKey("v_ret_gc_ammo5"), 100, new Vector3(0.0,0.0,-0.7), new Vector3(), newItemType.Ammo) },
            { ItemId.RiflesAmmo, new ItemsInfo("Rifle Ammo", "This ammo is perfect for assault rifles.","inv-item-ammo-average", "Ammo", NAPI.Util.GetHashKey("prop_ld_ammo_pack_03"), 250, new Vector3(0.0,0.0,-0.7), new Vector3(), newItemType.Ammo) },
            { ItemId.ShotgunsAmmo, new ItemsInfo("Shotgun Shells",  "Suitable for any type of shotgun.","inv-item-ammo-shotgun", "Ammo", NAPI.Util.GetHashKey("prop_ld_ammo_pack_02"), 50, new Vector3(0.0,0.0,-0.7), new Vector3(), newItemType.Ammo) },
            { ItemId.SMGAmmo, new ItemsInfo("SMG Ammo", "Perfect for your submachine gun and other small-caliber weapons.","inv-item-ammo-small", "Ammo", NAPI.Util.GetHashKey("v_ret_gc_ammo1"), 300, new Vector3(0.0,0.0,-0.7), new Vector3(), newItemType.Ammo) },
            { ItemId.SniperAmmo, new ItemsInfo("Sniper Ammo", "Suitable for all sniper rifles.","inv-item-ammo-sniper", "Ammo", NAPI.Util.GetHashKey("v_ret_gc_ammo2"), 48, new Vector3(0.0,0.0,-0.7), new Vector3(), newItemType.Ammo) },
            /* NEW */
            { ItemId.Snowball, new ItemsInfo("Snowball", "Throw it at someone!","inv-item-marijuana", "Special", 1297482736, 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Weapons) },
            { ItemId.Ball, new ItemsInfo("Ball", "For playing with your pet.","inv-item-ball", "Special", 1297482736, 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Weapons) },
    
            //
            { ItemId.cVarmod, new ItemsInfo("Weapon Tint", "With this component, you can paint a weapon, making it more visually appealing.","inv-item-Varmod", "Special", NAPI.Util.GetHashKey("prop_paint_spray01a"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cClip, new ItemsInfo("Weapon Magazine", "An improved magazine will expand the maximum ammo capacity of the weapon.","inv-item-Clips", "Special", NAPI.Util.GetHashKey("w_ar_assaultriflemk2_mag1"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cSuppressor, new ItemsInfo("Weapon Suppressor", "A suppressor, when installed, muffles the sound of shots and allows for silent kills.","inv-item-Suppressors", "Special", NAPI.Util.GetHashKey("w_at_ar_supp"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cScope, new ItemsInfo("Weapon Scope", "A scope, when installed, will allow the weapon's owner to aim more precisely at their enemies (Use the mouse wheel).","inv-item-Scopes", "Special", NAPI.Util.GetHashKey("w_at_scope_medium"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cMuzzlebrake, new ItemsInfo("Weapon Muzzle Brake", "This component reduces weapon recoil.","inv-item-Muzzle-Brakes", "Special", NAPI.Util.GetHashKey("w_at_muzzle_1"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cBarrel, new ItemsInfo("Weapon Barrel", "An improved weapon barrel increases accuracy and damage.","inv-item-Barrels", "Special", NAPI.Util.GetHashKey("w_at_sr_barrel_1"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cFlashlight, new ItemsInfo("Weapon Flashlight", "A flashlight mounted on a weapon is a great way to find your enemies in the dark (Press E to turn on).","inv-item-Flashlights", "Special", NAPI.Util.GetHashKey("w_at_ar_flsh"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cGrip, new ItemsInfo("Weapon Grip", "An improved weapon grip provides more comfortable shooting.","inv-item-Grips", "Special", NAPI.Util.GetHashKey("w_at_afgrip_2"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },
            { ItemId.cCamo, new ItemsInfo("Weapon Camo", "With a new skin, you can diversify the appearance of your weapon.","inv-item-Varmod", "Special", NAPI.Util.GetHashKey("prop_paint_spray01a"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.Modification) },

            //
            { ItemId.HalloweenCoin, new ItemsInfo("Halloween 2020 Coin", "A Halloween coin that can be exchanged for special prizes during the event.","inv-item-EventCoin", "Special", NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 999999, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Firework1, new ItemsInfo("Regular Firework", "A beautiful firework.","inv-item-Firework1", "Entertainment", NAPI.Util.GetHashKey("ind_prop_firework_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Firework2, new ItemsInfo("Star Firework", "A beautiful star-shaped firework.","inv-item-Firework2", "Entertainment", NAPI.Util.GetHashKey("ind_prop_firework_02"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Firework3, new ItemsInfo("Burst Firework", "A beautiful explosive-burst firework.","inv-item-Firework3", "Entertainment", NAPI.Util.GetHashKey("ind_prop_firework_03"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Firework4, new ItemsInfo("Fountain Firework", "A beautiful fountain-shaped firework.","inv-item-Firework4", "Entertainment", NAPI.Util.GetHashKey("ind_prop_firework_04"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.CarCoupon, new ItemsInfo("Car Coupon", "Contains a vehicle that will be delivered to your garage upon activation.","inv-item-car", "Coupons", 3125389411, 1, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.MerryChristmasCoin, new ItemsInfo("Christmas 2021 Coin", "A Christmas coin that can be exchanged for special prizes during the event.","inv-item-EventCoin", "Special", NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 999999, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Bear, new ItemsInfo("Love Bear", "Give it to someone nice!","inv-item-teddy-bear", "Special", NAPI.Util.GetHashKey("v_ilev_mr_rasberryclean"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Note, new ItemsInfo("Note", "With a note, you can leave a message, pass a secret, or other important information.","inv-item-Note", "Special", NAPI.Util.GetHashKey("p_amanda_note_01_s"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.LoveNote, new ItemsInfo("Love Note", "A valentine will help describe your warm feelings for someone.","inv-item-LoveNote", "Special", NAPI.Util.GetHashKey("p_amanda_note_01_s"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Vape, new ItemsInfo("Vape", "A puff of juicy vape always lifts the mood.","inv-item-Vape", "Special", NAPI.Util.GetHashKey("ba_prop_battle_vape_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Rose, new ItemsInfo("Rose", "A beautiful gift for a loved one!","inv-item-rose", "Special", NAPI.Util.GetHashKey("prop_single_rose"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Barbell, new ItemsInfo("Barbell", "For those who want to work on their physique.","inv-item-barbell", "Special", NAPI.Util.GetHashKey("prop_barbell_02"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Binoculars, new ItemsInfo("Binoculars", "Allows you to see objects at a long distance.","inv-item-binoculars", "Special", NAPI.Util.GetHashKey("prop_binoc_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Bong, new ItemsInfo("Bong", "A handy thing for consuming weed.","inv-item-bong", "Special", NAPI.Util.GetHashKey("prop_bong_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Umbrella, new ItemsInfo("Umbrella", "Allows you to take cover from the rain and adds style.","inv-item-umbrella", "Special", NAPI.Util.GetHashKey("p_amb_brolly_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Camera, new ItemsInfo("Camera", "Lights... Camera... Action!","inv-item-camera", "Special", NAPI.Util.GetHashKey("prop_v_cam_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Microphone, new ItemsInfo("Microphone", "The item of a true orator.","inv-item-microphone", "Special", NAPI.Util.GetHashKey("p_ing_microphonel_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Guitar, new ItemsInfo("Guitar", "It's just asking to be played... Play something!","inv-item-guitar", "Special", NAPI.Util.GetHashKey("prop_acc_guitar_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Pickaxe1, new ItemsInfo("Regular Pickaxe", "With this thing, you can work at the mine.", "inv-item-pickaxe1", "Special", NAPI.Util.GetHashKey("prop_tool_pickaxe"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Pickaxe2, new ItemsInfo("Reinforced Pickaxe", "With this thing, you can work powerfully at the mine.", "inv-item-pickaxe2", "Special", NAPI.Util.GetHashKey("prop_tool_pickaxe"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Pickaxe3, new ItemsInfo("Professional Pickaxe", "With this thing, you can work professionally at the mine.", "inv-item-pickaxe3", "Special", NAPI.Util.GetHashKey("prop_tool_pickaxe"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Coal, new ItemsInfo("Coal", "A resource from the mine.", "inv-item-coal", "Special", NAPI.Util.GetHashKey("prop_rock_5_smash2"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Iron, new ItemsInfo("Iron Ore", "A resource from the mine.", "inv-item-iron", "Special", NAPI.Util.GetHashKey("prop_rock_5_smash2"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Gold, new ItemsInfo("Gold Ore", "A resource from the mine.", "inv-item-gold", "Special", NAPI.Util.GetHashKey("prop_rock_5_smash2"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Sulfur, new ItemsInfo("Sulfur Ore", "A rare resource from the mine.", "inv-item-sulfur", "Special", NAPI.Util.GetHashKey("prop_rock_5_smash2"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Emerald, new ItemsInfo("Emerald", "An extremely rare resource from the mine.", "inv-item-emerald", "Special", NAPI.Util.GetHashKey("prop_rock_5_smash2"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Ruby, new ItemsInfo("Ruby", "A very rare resource from the mine.", "inv-item-ruby", "Special", NAPI.Util.GetHashKey("prop_rock_5_smash2"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Radio, new ItemsInfo("Walkie-Talkie", "With a walkie-talkie, you can communicate with other owners, or even eavesdrop on police or other secret frequencies.", "hud__icon-walkie-talkie", "Special", NAPI.Util.GetHashKey("prop_cs_hand_radio"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.WorkAxe, new ItemsInfo("Work Axe", "A tool for working as a lumberjack.", "inv-item-workaxe", "Special", NAPI.Util.GetHashKey("prop_ld_fireaxe"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.WoodOak, new ItemsInfo("Oak Wood", "Wood.", "inv-item-woodoak", "Special", NAPI.Util.GetHashKey("prop_fncwood_16e"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.WoodMaple, new ItemsInfo("Maple Wood", "Wood.", "inv-item-woodmaple", "Special", NAPI.Util.GetHashKey("prop_fncwood_16e"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.WoodPine, new ItemsInfo("Pine Wood", "Wood.", "inv-item-woodpine", "Special", NAPI.Util.GetHashKey("prop_fncwood_16e"), 250, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Boombox, new ItemsInfo("Boombox", "Allows you to play your own music for people around. Get the party started!", "inv-item-boombox", "Special", NAPI.Util.GetHashKey("prop_ghettoblast_01"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(0, 0, 0), newItemType.None) },
            { ItemId.Hookah, new ItemsInfo("Hookah", "A hookah for a fierce smoke! Double apple and let's fly...", "inv-item-hookah", "Special", NAPI.Util.GetHashKey("hookah_model"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Case0, new ItemsInfo(RouletteCasesData[0].Name, "A free case, can be obtained after 3 hours of play.", "inv-item-case0", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case1, new ItemsInfo(RouletteCasesData[1].Name, "A free weapon case, can be obtained after 5 hours of play.", "inv-item-case1", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case2, new ItemsInfo(RouletteCasesData[2].Name, "A free vehicle case, can be obtained after 8 hours of play.", "inv-item-case2", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case3, new ItemsInfo(RouletteCasesData[3].Name, "A standard case with standard prizes.", "inv-item-case3", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case4, new ItemsInfo(RouletteCasesData[4].Name, "A decent case for beginners.", "inv-item-case4", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case5, new ItemsInfo(RouletteCasesData[5].Name, "A juicy case, with a chance for a serious profit!", "inv-item-case5", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case6, new ItemsInfo(RouletteCasesData[6].Name, "A case with items and vehicles of higher rarity.", "inv-item-case6", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case7, new ItemsInfo(RouletteCasesData[7].Name, "A very fierce case. The grand prize is a Bugatti, the fastest car.", "inv-item-case7", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case8, new ItemsInfo(RouletteCasesData[8].Name, "A case with MEN'S clothing from the donation store. Test your luck and be stylish, or sell it and be rich. Or you could even gift it...", "inv-item-case8", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case9, new ItemsInfo(RouletteCasesData[9].Name, "A case with WOMEN'S clothing from the donation store. A great option if you want to give a gift.", "inv-item-case9", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case10, new ItemsInfo(RouletteCasesData[10].Name, "A case with cars from the Exotic DonateRoom. Donata Redbucksovna approves!", "inv-item-case10", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case11, new ItemsInfo(RouletteCasesData[11].Name, "All or nothing! Want to risk it and become the owner of the legendary RedAge sweater? Then spin the case!", "inv-item-case11", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case12, new ItemsInfo(RouletteCasesData[12].Name, "Been wanting a unique car that isn't sold anywhere? The Dodge Charger is in this case!", "inv-item-case12", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case13, new ItemsInfo(RouletteCasesData[13].Name, "Love looking down on people? A helicopter is probably perfect for that!", "inv-item-case13", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case14, new ItemsInfo(RouletteCasesData[14].Name, "A cool new case with trendy armored cars!", "inv-item-case14", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case15, new ItemsInfo(RouletteCasesData[15].Name, "A cool new case with placeable items!", "inv-item-case15", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case16, new ItemsInfo(RouletteCasesData[16].Name, "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. ", "inv-item-case16", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case17, new ItemsInfo(RouletteCasesData[17].Name, "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. ", "inv-item-case17", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case18, new ItemsInfo(RouletteCasesData[18].Name, "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. ", "inv-item-case18", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case19, new ItemsInfo(RouletteCasesData[19].Name, "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. ", "inv-item-case19", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case20, new ItemsInfo(RouletteCasesData[20].Name, "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. ", "inv-item-case20", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },
            { ItemId.Case21, new ItemsInfo(RouletteCasesData[21].Name, "An exclusive case from the Battle Pass, containing several clothing sets with an equal drop chance. ", "inv-item-case21", "Special", NAPI.Util.GetHashKey("prop_idol_case_02"), 100, new Vector3(0.0,0.0,-1.0), new Vector3(90, 0, 0), newItemType.Cases) },

            { ItemId.SummerCoin, new ItemsInfo("Jaguar Coin", "A coin that can be obtained for special prizes during the event. THIS IS NOT A DONATION CURRENCY!","inv-item-EventCoin", "Special", NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 999999, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.CandyCane, new ItemsInfo("Candy Cane", "A Christmas candy cane that can be exchanged for special prizes during the event.","inv-item-EventCoin", "Special", NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 999999, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.Qr, new ItemsInfo("QR Code", "A QR code containing information about a past illness and/or vaccination. To be presented on demand.","inv-item-QR", "Special", NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.QrFake, new ItemsInfo("QR Code", "A QR code containing information about a past illness and/or vaccination. To be presented on demand.","inv-item-QR", "Special", NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) },
            { ItemId.SimCard, new ItemsInfo("SIM Card", "A SIM card with a phone number. It can be inserted into any modern smartphone to use the cellular network: send SMS, make calls, and more.","inv-item-SimCard", "Special", NAPI.Util.GetHashKey("prop_ld_contact_card"), 1, new Vector3(0.0,0.0,-0.92), new Vector3(90, 0, 0), newItemType.None) }, /// SIMCARD NEWPHONE
            { ItemId.VehicleNumber, new ItemsInfo("Vehicle License Plate", "A vehicle license plate. You can install it on your car, sell it, or give it to someone!","inv-item-SimCard", "Special", NAPI.Util.GetHashKey("p_num_plate_02"), 1, new Vector3(0.0,0.0,-0.935), new Vector3(-90, 0, 90), newItemType.None) },
            { ItemId.Bint, new ItemsInfo("Bandage", "A bandage that restores 40% health. Can be used once every 3 minutes. ","inv-item-SimCard", "Other", 678958360, 10, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) }, //
            { ItemId.Cocaine, new ItemsInfo("White Powder", "This magical powder induces a narcotic effect.","inv-item-SimCard", "Other", NAPI.Util.GetHashKey("bkr_prop_coke_powder_02"), 10, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Rub100, new ItemsInfo("100 Rubles", "You can exchange this item for real money with the Project Lead! ","inv-item-SimCard", "Other", NAPI.Util.GetHashKey("bkr_prop_money_wrapped_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Rub200, new ItemsInfo("200 Rubles", "You can exchange this item for real money with the Project Lead! ","inv-item-SimCard", "Other", NAPI.Util.GetHashKey("bkr_prop_money_wrapped_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Rub500, new ItemsInfo("500 Rubles", "You can exchange this item for real money with the Project Lead! ","inv-item-SimCard", "Other", NAPI.Util.GetHashKey("bkr_prop_money_wrapped_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Rub1000, new ItemsInfo("1000 Rubles", "You can exchange this item for real money with the Project Lead!","inv-item-SimCard", "Other", NAPI.Util.GetHashKey("bkr_prop_money_wrapped_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            //
            { ItemId.RadioInterceptor, new ItemsInfo("Radio Interceptor", "Allows you to receive data about HeliCrashes.","hud__icon-walkie-talkie", "Other", NAPI.Util.GetHashKey("prop_cs_mini_tv"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Epinephrine, new ItemsInfo("Epinephrine","Needed for treatment, can be used once every 5 minutes.","inv-item-medical-kit", "Other",  NAPI.Util.GetHashKey("p_syringe_01_s"), 10, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.AppleCoin, new ItemsInfo("Apple Coin","A summer coin that can be obtained during the event. F for Apple Pay.","inv-item-medical-kit", "Other",  NAPI.Util.GetHashKey("ch_prop_arcade_fortune_coin_01a"), 999, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            //
            { ItemId.Fire, new ItemsInfo("Campfire", "Suitable for evening gatherings, don't get burned!","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_fire"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Matras, new ItemsInfo("Inflatable Mattress", "For beach holidays and to avoid drowning.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_lilo_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Tent, new ItemsInfo("Tent", "For camping, picnics, and a wild night.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_skid_tent_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Lezhak, new ItemsInfo("Sun Lounger", "For beach holidays, you can lie down and think about a bright future.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_patio_lounger_3"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Towel, new ItemsInfo("Rolled Towel", "For beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_towel_04"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flag, new ItemsInfo("Flag", "Russian flag, don't forget to purchase a flagpole.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_us_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Barrell, new ItemsInfo("Barrel", "Suitable for various needs.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_wooden_barrel"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Surf, new ItemsInfo("Surfboard", "For beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_surf_board_03"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Vedro, new ItemsInfo("Bucket", "Suitable for beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_buck_spade_03"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagstok, new ItemsInfo("Sprunk Flagpole", "A flagpole with a large Sprunk flag attached.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("stt_prop_flagpole_1b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Tenttwo, new ItemsInfo("Tent", "For camping, picnics, and a wild night.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_skid_tent_01b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Polotence, new ItemsInfo("Beach Towel", "For beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("p_cs_beachtowel_01_s"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Beachbag, new ItemsInfo("Beach Bag", "For beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_bag_03"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Zontik, new ItemsInfo("Umbrella", "Will protect from the sun and is suitable for beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_parasol_04"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Zontiktwo, new ItemsInfo("Umbrella", "Will protect from the sun and is suitable for beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_parasol_02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Zontikthree, new ItemsInfo("Umbrella", "Will protect from the sun and is suitable for beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_parasol_04c"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Closedzontik, new ItemsInfo("Closed Umbrella", "Won't protect from the sun, but is suitable for beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_parasol_04e"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Vball, new ItemsInfo("Volleyball", "For fun games and beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beach_volball01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Bball, new ItemsInfo("Beach Ball", "For fun games and beach holidays.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beachball_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Boomboxxx, new ItemsInfo("Boombox", "Suitable for hanging out with friends.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_boombox_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Table, new ItemsInfo("Table", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("bkr_prop_coke_table01a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Tabletwo, new ItemsInfo("Table", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_ld_farm_table02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Tablethree, new ItemsInfo("Table", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_proxy_chateau_table"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Tablefour, new ItemsInfo("Table", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_table_03b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Chair, new ItemsInfo("Chair", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("hei_prop_hei_skid_chair"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Chairtwo, new ItemsInfo("Chair", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_chair_01b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Chaierthree, new ItemsInfo("Chair", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_direct_chair_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Chaierfour, new ItemsInfo("Chair", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_table_03_chr"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Chairtable, new ItemsInfo("Table and Chair", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_picnictable_01_lod"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Korzina, new ItemsInfo("Basket", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_fruit_basket"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Light, new ItemsInfo("Lighting", "Lighting that will be useful in the dark.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_worklight_03b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Alco, new ItemsInfo("Bottle of Brandy", "An expensive bottle of brandy for fun and a good night with a girl or guy.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_bottle_brandy"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Alcotwo, new ItemsInfo("Bottle of Cognac", "An expensive bottle of cognac for fun and a good night with a girl or guy.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_bottle_cognac"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Alcothree, new ItemsInfo("Bottle of Tequila", "An expensive bottle of tequila for fun and a good night with a girl or guy.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_tequila_bottle"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Alcofour, new ItemsInfo("Bottle of Beer", "Cheap beer, suitable for hanging out with friends in nature.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_beer_am"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Cocktail, new ItemsInfo("Cocktail", "A mix of citrus fruits and alcohol, perfect for your girlfriend.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_cocktail"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Cocktailtwo, new ItemsInfo("Cocktail", "A mix of citrus fruits and alcohol, perfect for your girlfriend.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_cocktail_glass"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Fruit, new ItemsInfo("Plate of Fruit", "A delicious and fragrant plate of fruit.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("ex_mp_h_acc_fruitbowl_02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Fruittwo, new ItemsInfo("Plate of Fruit", "A delicious and fragrant plate of fruit.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_bar_fruit"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Packet, new ItemsInfo("Bag", "Suitable for a picnic.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("ng_proc_food_bag01a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Buter, new ItemsInfo("Burger", "Suitable for a picnic.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_food_bs_burg3"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Patatoes, new ItemsInfo("French Fries", "Suitable for a picnic.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_food_bs_chips"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Coffee, new ItemsInfo("Hot Coffee", "Suitable for a picnic.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_food_bs_coffee"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Podnosfood, new ItemsInfo("Food Tray", "Suitable for a picnic.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_food_bs_tray_02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Bbqtwo, new ItemsInfo("BBQ", "Suitable for a picnic or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_bbq_4_l1"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Bbq, new ItemsInfo("Large BBQ", "Suitable for a picnic or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_bbq_5"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flowerrr, new ItemsInfo("Bouquet of Flowers", "A beautiful bouquet of colorful flowers that your lady will like.","prop_snow_flower_02", "Placeable Items", NAPI.Util.GetHashKey("prop_snow_flower_02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Vaza, new ItemsInfo("Vase with Flowers", "A vase with beautiful white flowers that your lady will like.","vw_prop_flowers_vase_01a", "Placeable Items", NAPI.Util.GetHashKey("vw_prop_flowers_vase_01a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagwtokk, new ItemsInfo("Flagpole", "Suitable for picnics or general relaxation.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_flagpole_2a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.Flagau, new ItemsInfo("Flag of Australia", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_australia", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_australia"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagbr, new ItemsInfo("Flag of Brazil", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_brazil", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_brazil"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagch, new ItemsInfo("Flag of China", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_china", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_china"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagcz, new ItemsInfo("Flag of Czech Republic", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_czechrep", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_czechrep"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flageng, new ItemsInfo("Flag of England", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_england", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_england"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.Flageu, new ItemsInfo("Flag of the European Union", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_eu_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_eu_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.Flagfin, new ItemsInfo("Flag of Finland", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_finland", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_finland"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagfr, new ItemsInfo("Flag of France", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_france", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_france"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagger, new ItemsInfo("Flag of Germany", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_german_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_german_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagire, new ItemsInfo("Flag of Ireland", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_ireland", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_ireland"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagisr, new ItemsInfo("Flag of Israel", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_israel", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_israel"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagit, new ItemsInfo("Flag of Italy", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_italy", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_italy"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagjam, new ItemsInfo("Flag of Jamaica", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_jamaica", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_jamaica"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagjap, new ItemsInfo("Flag of Japan", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_japan_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_japan_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagmex, new ItemsInfo("Flag of Mexico", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_mexico_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_mexico_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagnet, new ItemsInfo("Flag of the Netherlands", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_netherlands", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_netherlands"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagnig, new ItemsInfo("Flag of Nigeria", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_nigeria", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_nigeria"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagnorw, new ItemsInfo("Flag of Norway", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_norway", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_norway"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagpol, new ItemsInfo("Flag of Poland", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_poland", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_poland"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagrus, new ItemsInfo("Flag of Russia", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_russia_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_russia_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagbel, new ItemsInfo("Flag of Belgium", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_belgium", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_belgium"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.Flagscot, new ItemsInfo("Flag of Scotland", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_scotland_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_scotland_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagscr, new ItemsInfo("Script Flag", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_script", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_script"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.Flagslov, new ItemsInfo("Flag of Slovakia", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_slovakia", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_slovakia"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagslovak, new ItemsInfo("Flag of Slovenia", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_slovenia", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_slovenia"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagsou, new ItemsInfo("Flag of South Korea", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_southkorea", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_southkorea"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagspain, new ItemsInfo("Flag of Spain", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_spain", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_spain"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagswede, new ItemsInfo("Flag of Sweden", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_sweden", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_sweden"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagswitz, new ItemsInfo("Flag of Switzerland", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_switzerland", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_switzerland"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagturk, new ItemsInfo("Flag of Turkey", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_turkey", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_turkey"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            //
            { ItemId.Flaguk, new ItemsInfo("Flag of the United Kingdom", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_uk_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_uk_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagus, new ItemsInfo("Flag of America", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_us_yt", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_us_yt"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Flagwales, new ItemsInfo("Flag of Wales", "Medium-sized flag, requires a flagpole for mounting.","apa_prop_flag_wales", "Placeable Items", NAPI.Util.GetHashKey("apa_prop_flag_wales"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            //
		            { ItemId.Konus, new ItemsInfo("Cone", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_byard_net02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Konuss, new ItemsInfo("Luminous Cone", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_air_conelight"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Otboynik1, new ItemsInfo("Barrier", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_barrier_wat_03a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Otboynik2, new ItemsInfo("Barrier", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_barrier_wat_03b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Dontcross, new ItemsInfo("Roadblock", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_barrier_work05"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Stop, new ItemsInfo("STOP Sign", "Suitable for traffic rules.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_sign_road_01a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.NetProezda, new ItemsInfo("NO ENTRY Sign", "Suitable for traffic rules.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_sign_road_03a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Zabor1, new ItemsInfo("Large Fence", "Suitable for a picnic.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("xm_prop_base_fence_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Kpp, new ItemsInfo("Checkpoint Booth", "Suitable for setting up a checkpoint.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_air_sechut_01"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Zabor2, new ItemsInfo("Small Fence", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("xm_prop_base_fence_02"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Airlight, new ItemsInfo("Night Light", "Suitable as a barrier.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_air_lights_02b"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Camera1, new ItemsInfo("CCTV Camera", "Suitable for surveillance.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_cctv_cam_05a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },
            { ItemId.Camera2, new ItemsInfo("CCTV Camera", "Suitable for surveillance.","placingprop", "Placeable Items", NAPI.Util.GetHashKey("prop_snow_cam_03a"), 1, new Vector3(0.0,0.0,-0.9), new Vector3(), newItemType.None) },

            { ItemId.TacticalRifle, new ItemsInfo("Tactical Rifle", "Advanced assault rifle, magazine holds 30 rounds.","inv-item-militaryrifle", "Weapon", 273925117, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },

            { ItemId.PrecisionRifle, new ItemsInfo("Precision Rifle", "Advanced sniper rifle, magazine holds 6 rounds.","inv-item-militaryrifle", "Weapon", 346403307, 1, new Vector3(0.0,0.0,-0.99),new Vector3(90, 0, 0), newItemType.Weapons) },

            { ItemId.CombatShotgun, new ItemsInfo("Combat Shotgun", "Advanced shotgun, holds 10 shells.","inv-item-militaryrifle", "Weapon", 689760839, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },

            { ItemId.HeavyRifle, new ItemsInfo("Heavy Rifle", "Advanced assault rifle, magazine holds 30 rounds.","inv-item-militaryrifle", "Weapon", 2379721761, 1, new Vector3(0.0,0.0,-0.99),new Vector3(90, 0, 0), newItemType.Weapons) },

            { ItemId.NeonStick, new ItemsInfo("Neon Sticks", "Red sticks that you can hold in your hands.","inv-item-Assault-Rifle", "Special", 3455618605, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.GlowStick, new ItemsInfo("Glow Sticks", "Beautiful glowing sticks that you can hold in your hands.","inv-item-Assault-Rifle", "Special", NAPI.Util.GetHashKey("ba_prop_battle_glowstick_01"), 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.Giftcoin, new ItemsInfo("Gift", "A gift box that Santa hid for you!","inv-item-Assault-Rifle", "Special", NAPI.Util.GetHashKey(""), 999, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.None) },

            { ItemId.CombatRifle, new ItemsInfo("Combat Rifle", "A rifle that mows down everything in its path.","inv-item-Carbine-Rifle", "Weapon", 2379721761, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },

            { ItemId.Glock, new ItemsInfo("Banana Glock", "Banana Glock is top-tier.","inv-item-Carbine-Rifle", "Weapon", 651271362, 1, new Vector3(0.0,0.0,-0.99), new Vector3(90, 0, 0), newItemType.Weapons) },
        };



        private static void OnSaveJsonItemsInfo()
        {
            try
            {
                File.WriteAllText(@$"json/itemsInfo.js", string.Empty);
                using (var saveCoords = new StreamWriter(@$"json/itemsInfo.js", true, Encoding.UTF8))
                {
                    int index = 0;

                    saveCoords.Write("export const ItemType = {\n");

                    foreach (var key in Enum.GetValues(typeof(newItemType)))
                    {
                        index++;

                        if (ItemsInfo.Count == index)
                            saveCoords.Write($"\t{key.ToString()}: {(int)key}\r\n");
                        else
                            saveCoords.Write($"\t{key.ToString()}: {(int)key},\r\n");
                    }
                    
                    saveCoords.Write("}\n\n");
                    index = 0;

                    saveCoords.Write("export const ItemId = {\n");

                    foreach (var key in Enum.GetValues(typeof(ItemId)))
                    {
                        index++;

                        if (ItemsInfo.Count == index)
                            saveCoords.Write($"\t{key.ToString()}: {(int)key}\r\n");
                        else
                            saveCoords.Write($"\t{key.ToString()}: {(int)key},\r\n");
                    }
                    
                    saveCoords.Write("}\n\n");
                    index = 0;
                    
                    saveCoords.Write("export const itemsInfo = {\n");
                    foreach (var itemInfo in ItemsInfo)
                    {
                        index++;

                        if (ItemsInfo.Count == index)
                            saveCoords.Write($"\t[ItemId.{itemInfo.Key.ToString()}]: {JsonConvert.SerializeObject(itemInfo.Value)}\r\n");
                        else
                            saveCoords.Write($"\t[ItemId.{itemInfo.Key.ToString()}]: {JsonConvert.SerializeObject(itemInfo.Value)},\r\n");

                        //saveData.Add(clothes.Key, data);
                    }
                    saveCoords.Write("}");
                    saveCoords.Close();
                }
            }
            catch
            {
                Log.Write($"OnSaveJsonClothes");
            }
        }
        
        
        public static ItemId[] StrongWeapons = new ItemId[20]
        {
            ItemId.DoubleAction,
            ItemId.PistolMk2,
            ItemId.SNSPistolMk2,
            ItemId.RevolverMk2,
            ItemId.SMGMk2,
            ItemId.CombatMGMk2,
            ItemId.AssaultRifleMk2,
            ItemId.CarbineRifleMk2,
            ItemId.SpecialCarbineMk2,
            ItemId.BullpupRifleMk2,
            ItemId.MilitaryRifle,
            ItemId.HeavySniperMk2,
            ItemId.MarksmanRifleMk2,
            ItemId.PumpShotgunMk2,
            ItemId.CeramicPistol,
            ItemId.NavyRevolver,
            ItemId.TacticalRifle,
            ItemId.PrecisionRifle,
            ItemId.CombatShotgun,
            ItemId.HeavyRifle
        };

        public static ItemId[] HeavyWeapons = new ItemId[13]
        {
            ItemId.RayPistol,
            ItemId.RayCarbine,
            ItemId.GrenadeLauncher,
            ItemId.RPG,
            ItemId.Minigun,
            ItemId.Firework,
            ItemId.Railgun,
            ItemId.HomingLauncher,
            ItemId.GrenadeLauncherSmoke,
            ItemId.CompactGrenadeLauncher,
            ItemId.Widowmaker,
            ItemId.Glock,
            ItemId.CombatRifle,
        };

        public static void LoadOtherItemsData(ExtPlayer player, string Name, string Id, int OtherId, int MaxSlots = 0, string selectItemId = "", bool IsArmyCar = false, bool isMyTent = false)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                string locationName = $"{Name}_{Id}";
                Name = Name.Split('_').Length > 0 ? Name.Split('_')[0] : Name;
                OtherClose(player);
                sessionData.InventoryOtherLocationName = locationName;
                if (!InventoryOtherPlayers.ContainsKey(locationName)) InventoryOtherPlayers.TryAdd(locationName, new List<ExtPlayer>());
                if (!InventoryOtherPlayers[locationName].Contains(player)) InventoryOtherPlayers[locationName].Add(player);
                if (Name != "warehouse")
                {
                    Tuple<string, int> returnData = ClientEventLoadItemsData(locationName, Name, MaxSlots);
                    Trigger.ClientEvent(player, "client.inventory.InitOtherData", OtherId, Id, returnData.Item1, returnData.Item2, selectItemId, IsArmyCar, isMyTent);
                }
                else
                {
                    Tuple<string, int> returnData = ClientEventLoadWarehouseItemsData(locationName, MaxSlots);
                    Trigger.ClientEvent(player, "client.inventory.InitOtherData", OtherId, Id, returnData.Item1, returnData.Item2, selectItemId, IsArmyCar, isMyTent);
                }
            }
            catch (Exception e)
            {
                Log.Write($"LoadOtherItemsData Exception: {e.ToString()}");
            }
        }

        public static void LoadCharItemsData(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                InitInventory(player, null);
            }
            catch (Exception e)
            {
                Log.Write($"LoadCharItemsData Exception: {e.ToString()}");
            }
        }
        public static string GetVehicleName(string data)
        {
            try
            {
                if (data != null && data.Split('_').Length >= 2 && int.TryParse(data.Split('_')[0], out int SqlId))
                {
                    var vehicleData = VehicleManager.GetVehicleToAutoId(SqlId);
                    if (vehicleData != null) 
                        return $"{vehicleData.Model.ToUpper()}_{vehicleData.Number}";
                }
            }
            catch (Exception e)
            {
                Log.Write($"GetVehicleName Exception: {e.ToString()}");
            }
            return "-_-";
        }
        public static void InitInventory(ExtPlayer player, ExtPlayer Target = null)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                var getPlayer = Target == null ? player : Target;

                var targetCharacterData = getPlayer.GetCharacterData();
                if (targetCharacterData == null)
                    return;
                
                InventoryItemData ItemBag = null;

                List<InventoryItemData> _JsonAccessoriesItemData = new List<InventoryItemData>();
                List<InventoryItemData> _JsonInventoryItemData = new List<InventoryItemData>();
                List<InventoryItemData> _JsonFastSlotsItemData = new List<InventoryItemData>();
                //List<InventoryItemData> jsonBackPackItemData = new List<InventoryItemData>();

                string locationName = $"char_{targetCharacterData.UUID}";

                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (string Location in ItemsData[locationName].Keys)
                    {
                        var sortedItemData = ItemsData[locationName][Location].Values.OrderBy(i => i.Index).ToList();
                        foreach (InventoryItemData item in sortedItemData)
                        {
                            if (item.ItemId == ItemId.Debug) continue;

                            InventoryItemData newItem = new InventoryItemData(SqlId: item.SqlId, ItemId: item.ItemId, Count: item.Count, Data: item.Data, Index: item.Index);

                            if (newItem.ItemId == ItemId.Bag) ItemBag = newItem;

                            if (newItem.ItemId == ItemId.CarKey) newItem.Data = GetVehicleName(newItem.Data);

                            if (Location == "accessories") _JsonAccessoriesItemData.Add(newItem);
                            else if (Location == "inventory") _JsonInventoryItemData.Add(newItem);
                            else if (Location == "fastSlots") _JsonFastSlotsItemData.Add(newItem);
                        }
                    }
                }
                string _ItemsData = JsonConvert.SerializeObject(new Dictionary<string, List<InventoryItemData>>
                {
                    { "accessories", _JsonAccessoriesItemData },
                    { "inventory", _JsonInventoryItemData },
                    { "fastSlots", _JsonFastSlotsItemData },
                    //{ "backpack", jsonBackPackItemData },
                });

                Trigger.ClientEvent(player, "client.inventory.InitData", _ItemsData, Target == null ? true : false);
                if (Target == null)
                {
                    isRadio(player);
                    //isBackpackItemsData(player, true);
                }
                else if (Target != null)
                {
                    if (ItemBag != null)
                    {
                        int maxSlots = 20;
                        Dictionary<string, int> PlayerBugData = ItemBag.GetData();
                        ConcurrentDictionary<int, ClothesData> ShoesData = ClothesComponents.ClothesBugsData;
                        if (ShoesData.ContainsKey(PlayerBugData["Variation"]) && ShoesData[PlayerBugData["Variation"]].MaxSlots > 0) maxSlots = ShoesData[PlayerBugData["Variation"]].MaxSlots;
                        Tuple<string, int> returnData = ClientEventLoadItemsData($"backpack_{ItemBag.SqlId}", "backpack", maxSlots);
                        Trigger.ClientEvent(player, "client.inventory.InitBackpack", maxSlots, returnData.Item1, false);
                    }
                    else Trigger.ClientEvent(player, "client.inventory.InitBackpack", 0, "[]", false);
                }
                PlayerStats(player, Target);
            }
            catch (Exception e)
            {
                Log.Write($"InitInventory Exception: {e.ToString()}");
            }
        }
        public static void isRadio(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                if (isItem(player, "inventory", ItemId.Radio) != null)
                {
                    if (sessionData.WalkieTalkieFrequency == -99) 
                        sessionData.WalkieTalkieFrequency = -1;
                }
                else sessionData.WalkieTalkieFrequency = -99;

                sessionData.IsRadioInterceptor = isItem(player, "inventory", ItemId.RadioInterceptor) != null;
            }
            catch (Exception e)
            {
                Log.Write($"isRadio Exception: {e.ToString()}");
            }
        }

        public static int isBackpackItemsData(ExtPlayer player, bool init = false)
        {
            try
            {
                if (!player.IsCharacterData()) return 0;
                InventoryItemData Bags = GetItemData(player, "accessories", 8);
                if (Bags.ItemId == ItemId.Bag)
                {
                    if (init)
                    {
                        int maxSlots = 20;
                        Dictionary<string, int> PlayerBugData = Bags.GetData();
                        ConcurrentDictionary<int, ClothesData> ShoesData = ClothesComponents.ClothesBugsData;
                        if (ShoesData.ContainsKey(PlayerBugData["Variation"]) && ShoesData[PlayerBugData["Variation"]].MaxSlots > 0) maxSlots = ShoesData[PlayerBugData["Variation"]].MaxSlots;
                        Tuple<string, int> returnData = ClientEventLoadItemsData($"backpack_{Bags.SqlId}", "backpack", maxSlots);
                        Trigger.ClientEvent(player, "client.inventory.InitBackpack", maxSlots, returnData.Item1, true);
                    }
                    return Bags.SqlId;
                }
                else if (init) Trigger.ClientEvent(player, "client.inventory.InitBackpack", 0, "[]", false);
                return 0;
            }
            catch (Exception e)
            {
                Log.Write($"isBackpackItemsData Exception: {e.ToString()}");
                return 0;
            }
        }
        public static int GetFreeSlot(string locationName, string Location)
        {
            try
            {
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location) && ItemsData[locationName][Location].Count > 0)
                {
                    var items = ItemsData[locationName][Location];
                    
                    var sortedItemData = items.Values.OrderByDescending(i => i.Index).FirstOrDefault();

                    if (sortedItemData != null)
                    {
                        for (var i = 0; i < sortedItemData.Index + 10; i++)
                        {
                            if (!items.ContainsKey(i) || items[i].ItemId == ItemId.Debug)
                                return i;
                        }
                    }
                }
                return 0;
            }
            catch (Exception e)
            {
                Log.Write($"GetWarehouseLastId Exception: {e.ToString()}");
                return 0;
            }
        }

        public static int GetItemsDataLastId(string locationName, string Location)
        {
            try
            {
                int SlotId = 0;
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location) && ItemsData[locationName][Location].Count > 0)
                {
                    var sortedItemData = ItemsData[locationName][Location].Values.OrderBy(i => i.Index).ToList();

                    foreach (InventoryItemData item in sortedItemData)
                    {
                        if (item.ItemId != ItemId.Debug)
                        {
                            if (item.Index >= SlotId) SlotId = item.Index + 1;
                        }
                    }
                }
                return SlotId;
            }
            catch (Exception e)
            {
                Log.Write($"GetWarehouseLastId Exception: {e.ToString()}");
                return 0;
            }
        }
        public static Tuple<string, int> ClientEventLoadWarehouseItemsData(string locationName, int MaxSlots)
        {
            try
            {
                var Location = "warehouse";
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location) && ItemsData[locationName][Location].Count > 0)
                {
                    var items = ItemsData[locationName][Location];
                    
                    var maxIndex = items.Values.OrderByDescending(i => i.Index).Select(i => i.Index).FirstOrDefault();
                    
                    var index = 0;
                    for (var i = 0; i < maxIndex + 10; i++)
                    {
                        if (!items.ContainsKey(i) || items[i].ItemId == ItemId.Debug)
                        {
                            var lastItem = items.LastOrDefault();

                            if (lastItem.Value != null)
                            {
                                items.TryRemove(lastItem.Key, out _);
                                
                                items[i] = new InventoryItemData(lastItem.Value.SqlId, lastItem.Value.ItemId, lastItem.Value.Count, lastItem.Value.Data, i);

                                UpdateSqlItemData(locationName, Location, i, items[i]);
                            } 
                        }
                        if (++index >= 300)
                            break;
                    }
                    
                    
                    
                    /*var sortedItemData = ItemsData[locationName][Location].Values.OrderBy(i => i.Index).ToList();
                    var index = 0;
                    foreach (var item in sortedItemData)
                    {
                        if (item.ItemId != ItemId.Debug)
                        {
                            var newItem = new InventoryItemData(SqlId: item.SqlId, ItemId: item.ItemId, Count: item.Count, Data: item.Data, Index: item.Index);
                            newItem.Price = item.Price;


                            if (newItem.Index != index)
                            {
                                var SlotId = GetFreeSlot(locationName, Location);

                                Console.WriteLine($"SlotId - ${newItem.ItemId} - ${newItem.Index} - {SlotId}");
                                newItem.Index = SlotId;

                                ItemsData[locationName][Location][SlotId] = new InventoryItemData(newItem.SqlId, newItem.ItemId, newItem.Count, newItem.Data, SlotId);

                                UpdateSqlItemData(locationName, Location, SlotId, ItemsData[locationName][Location][SlotId]);
                                
                                
                            }

                            index++;
                        }
                    }*/
                    List<InventoryItemData> _JsonInventoryItemData = new List<InventoryItemData>();
                    var sortedItemData = ItemsData[locationName][Location].Values.OrderBy(i => i.Index).ToList();
                    foreach (InventoryItemData item in sortedItemData)
                    {
                        if (item.ItemId != ItemId.Debug)
                        {
                            InventoryItemData newItem = new InventoryItemData(SqlId: item.SqlId, ItemId: item.ItemId, Count: item.Count, Data: item.Data, Index: item.Index);
                            newItem.Price = item.Price;
                            if (newItem.Index >= MaxSlots) MaxSlots = newItem.Index + 1;
                            if (newItem.ItemId == ItemId.CarKey) newItem.Data = GetVehicleName(newItem.Data);
                            _JsonInventoryItemData.Add(newItem);

                            if (_JsonInventoryItemData.Count == 300) 
                                break;
                        }
                    }
                    return new Tuple<string, int>(JsonConvert.SerializeObject(_JsonInventoryItemData), MaxSlots);
                }
                return new Tuple<string, int>(JsonConvert.SerializeObject(new List<InventoryItemData>()), MaxSlots);
            }
            catch (Exception e)
            {
                Log.Write($"ClientEventLoadWarehouseItemsData Exception: {e.ToString()}");
                return new Tuple<string, int>(JsonConvert.SerializeObject(new List<InventoryItemData>()), MaxSlots);
            }
            
        }
        public static Tuple<string, int> ClientEventLoadItemsData(string locationName, string Location, int MaxSlots)
        {
            try
            {
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location) && ItemsData[locationName][Location].Count > 0)
                {
                    var sortedItemData = ItemsData[locationName][Location].Values.OrderBy(i => i.Index).ToList();
                    int SlotId = -1;
                    foreach (InventoryItemData item in sortedItemData)
                    {
                        if (item.ItemId != ItemId.Debug)
                        {
                            InventoryItemData newItem = new InventoryItemData(SqlId: item.SqlId, ItemId: item.ItemId, Count: item.Count, Data: item.Data, Index: item.Index);
                            newItem.Price = item.Price;
                            if (newItem.Index < 0)
                            {
                                if (SlotId == -1) SlotId = GetItemsDataLastId(locationName, Location);
                                else SlotId++;
                                if (SlotId > 0 && ItemsData[locationName][Location].ContainsKey(newItem.Index))
                                {
                                    MaxSlots = SlotId + 1;

                                    ItemsData[locationName][Location].TryRemove(newItem.Index, out _);

                                    newItem.Index = SlotId;

                                    ItemsData[locationName][Location][SlotId] = new InventoryItemData(newItem.SqlId, newItem.ItemId, newItem.Count, newItem.Data, SlotId);

                                    UpdateSqlItemData(locationName, Location, SlotId, ItemsData[locationName][Location][SlotId]);
                                }
                            }
                        }
                    }
                    List<InventoryItemData> _JsonInventoryItemData = new List<InventoryItemData>();
                    sortedItemData = ItemsData[locationName][Location].Values.OrderBy(i => i.Index).ToList();
                    foreach (InventoryItemData item in sortedItemData)
                    {
                        if (item.ItemId != ItemId.Debug)
                        {
                            InventoryItemData newItem = new InventoryItemData(SqlId: item.SqlId, ItemId: item.ItemId, Count: item.Count, Data: item.Data, Index: item.Index);
                            newItem.Price = item.Price;
                            if (newItem.Index >= MaxSlots) MaxSlots = newItem.Index + 1;
                            if (newItem.ItemId == ItemId.CarKey) newItem.Data = GetVehicleName(newItem.Data);
                            _JsonInventoryItemData.Add(newItem);

                            if (_JsonInventoryItemData.Count == 250) 
                                break;
                        }
                    }
                    return new Tuple<string, int>(JsonConvert.SerializeObject(_JsonInventoryItemData), MaxSlots);
                }
                return new Tuple<string, int>(JsonConvert.SerializeObject(new List<InventoryItemData>()), MaxSlots);
            }
            catch (Exception e)
            {
                Log.Write($"ClientEventLoadItemsData Exception: {e.ToString()}");
                return new Tuple<string, int>(JsonConvert.SerializeObject(new List<InventoryItemData>()), MaxSlots);
            }
            
        }
        public static void InitItems()
        {
            OnSaveJsonItemsInfo();
            
            DateTime TestSpeedLoad = DateTime.Now;
            using MySqlCommand countSql = new MySqlCommand
            {
                CommandText = "DELETE FROM `items_data` WHERE data_id='drop' OR item_id=0"
            };
            MySQL.Query(countSql);
            using MySqlCommand cmd = new MySqlCommand()
            {
                CommandText = "SELECT * FROM `items_data`"
            };
            using DataTable result = MySQL.QueryRead(cmd);

            Dictionary<string, List<InventoryItemData>> AddWarehouseLocal = new Dictionary<string, List<InventoryItemData>>();
            if (result != null)
            {
                int SqlID;
                ItemId ItemId;
                int Count;
                string Data;
                string Location;
                int SlotId;
                string locationName;
                int count = 0;
                foreach (DataRow Row in result.Rows)
                {
                    count++;
                    locationName = Convert.ToString(Row["data_id"]);
                    SqlID = Convert.ToInt32(Row["auto_id"]);
                    ItemId = (ItemId)Convert.ToInt32(Row["item_id"]);
                    Count = Convert.ToInt32(Row["item_count"]);
                    Data = Convert.ToString(Row["item_data"]);
                    Location = Convert.ToString(Row["location"]);
                    SlotId = Convert.ToInt32(Row["slotId"]);
                    if (ItemId == ItemId.Debug)
                        continue;

                    if (!ItemsData.ContainsKey(locationName)) 
                        ItemsData.TryAdd(locationName, new ConcurrentDictionary<string, ConcurrentDictionary<int, InventoryItemData>>());

                    if (!ItemsData[locationName].ContainsKey(Location)) 
                        ItemsData[locationName].TryAdd(Location, new ConcurrentDictionary<int, InventoryItemData>());

                    if (Location == "tent")
                    {
                        if (!AddWarehouseLocal.ContainsKey(locationName)) AddWarehouseLocal.Add(locationName, new List<InventoryItemData>());
                        AddWarehouseLocal[locationName].Add(new InventoryItemData(SqlID, ItemId, Count, Data, SlotId));
                    }
                    else if (!ItemsData[locationName][Location].ContainsKey(SlotId)) 
                        ItemsData[locationName][Location].TryAdd(SlotId, new InventoryItemData(SqlID, ItemId, Count, Data, SlotId));
                    else if (InventoryLocation.ContainsKey(Location) || Location == "warehouse")
                    {
                        if (!AddWarehouseLocal.ContainsKey(locationName)) AddWarehouseLocal.Add(locationName, new List<InventoryItemData>());
                        AddWarehouseLocal[locationName].Add(new InventoryItemData(SqlID, ItemId, Count, Data, SlotId));
                    }
                
                    OnAddItem(ItemId, Data);
                }
                Log.Write($"[{DateTime.Now - TestSpeedLoad}] Inventory system loaded ({count})");
            }
            
            TestSpeedLoad = DateTime.Now;
            if (AddWarehouseLocal.Count > 0)
            {
                string Location = "warehouse";
                int count = 0;

                foreach(KeyValuePair<string, List<InventoryItemData>> WarehouseLocalData in AddWarehouseLocal)
                {
                    try
                    {
                        string locationName = WarehouseLocalData.Key;
                        locationName = $"warehouse_{locationName.Split('_')[1]}";
                        if (!ItemsData.ContainsKey(locationName))
                            ItemsData.TryAdd(locationName, new ConcurrentDictionary<string, ConcurrentDictionary<int, InventoryItemData>>());
                        if (!ItemsData[locationName].ContainsKey(Location))
                            ItemsData[locationName].TryAdd(Location, new ConcurrentDictionary<int, InventoryItemData>());

                        foreach(var Item in WarehouseLocalData.Value)
                        {
                            short SlotId = (short)GetItemsDataLastId(locationName, Location);
                            if (ItemsData[locationName][Location].ContainsKey(SlotId))
                                ItemsData[locationName][Location].TryRemove(SlotId, out _);
                            ItemsData[locationName][Location].TryAdd(SlotId, new InventoryItemData(Item.SqlId, Item.ItemId, Item.Count, Item.Data, SlotId));
                            
                            Database.Models.Items.AddItemUpdate(Item.SqlId, locationName, Item.Count, Item.Data, Location, SlotId);
                            
                            count++;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Write($"WarehouseLocalData Exception: {e.ToString()}");
                    }
                }
                Log.Write($"[{DateTime.Now - TestSpeedLoad}] Add Warehouse system loaded ({count})");
            }

        }

        static readonly IReadOnlyDictionary<string, string> InventoryLocation = new Dictionary<string, string>()
        {
            { "accessories", "char" },
            { "inventory", "char" },
            { "fastSlots", "char" },
            { "trade", "char" },
            { "with_trade", "char" }
        };

        public static string GetLocationName(ExtPlayer player, string Location)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return Location;

                var characterData = player.GetCharacterData();
                string locationName = Location;

                if (Location == "other" && sessionData.InventoryOtherLocationName != null) locationName = sessionData.InventoryOtherLocationName;
                else if (characterData == null && Location != "other" && Location != "backpack") locationName = $"char_{sessionData.SelectUUID}";
                else if (characterData != null && Location != "other" && Location != "backpack") locationName = $"char_{characterData.UUID}";
                else if (Location == "backpack")
                {
                    InventoryItemData Bags = GetItemData(player, "accessories", 8);
                    locationName = $"backpack_{Bags.SqlId}";
                }
                return locationName;
            }
            catch (Exception e)
            {
                Log.Write($"GetLocationName Exception: {e.ToString()}");
                return Location;
            }
        }
        public static void AddInventoryArray(string locationName, string Location)
        {
            try
            {
                if (!ItemsData.ContainsKey(locationName)) ItemsData.TryAdd(locationName, new ConcurrentDictionary<string, ConcurrentDictionary<int, InventoryItemData>>());
                if (!ItemsData[locationName].ContainsKey(Location)) ItemsData[locationName].TryAdd(Location, new ConcurrentDictionary<int, InventoryItemData>());
            }
            catch (Exception e)
            {
                Log.Write($"AddInventoryArray Exception: {e.ToString()}");
            }
        }

        public static void Remove(ExtPlayer player, string locationName, string Location, ItemId ItemId, int count = 1)
        {
            try
            {
                if (player != null && !player.IsCharacterData()) return;

                AddInventoryArray(locationName, Location);

                List<ItemStruct> _Items = new List<ItemStruct>();

                foreach (var item in ItemsData[locationName][Location])
                {
                    if (item.Value.ItemId == ItemId)
                    {
                        if (ItemsInfo[ItemId].Stack > 1 && (item.Value.Count - count) > 0)
                        {
                            _Items.Add(new ItemStruct(Location, item.Key, new InventoryItemData(item.Value.SqlId, item.Value.ItemId, count, item.Value.Data, item.Value.Index)));
                            count = 0;
                        }
                        else
                        {
                            if (ItemsInfo[ItemId].Stack > 1) count -= item.Value.Count;
                            else count -= 1;
                            _Items.Add(new ItemStruct(Location, item.Key, item.Value));
                        }

                        if (count == 0) break;
                    }
                }
                RemoveFix(player, locationName, _Items);
            }
            catch (Exception e)
            {
                Log.Write($"Remove Exception: {e.ToString()}");
            }
        }
        public static void RemoveIndex(ExtPlayer player, string Location, int SlotId, int count = 1)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                string locationName = GetLocationName(player, Location);
                if (locationName != null)
                {
                    InventoryItemData _Item = GetItemData(player, Location, SlotId);
                    if (_Item.ItemId != ItemId.Debug)
                    {
                        if (Location == "fastSlots" && sessionData.ActiveWeap.Index == SlotId)
                        {
                            WeaponRepository.RemoveHands(player);
                        }

                        if (ItemsInfo[_Item.ItemId].Stack > 1 && (_Item.Count - count) < 1) Remove(player, locationName, Location, _Item.ItemId, count - _Item.Count);
                        if ((_Item.Count -= count) < 1) _Item.ItemId = ItemId.Debug;
                        SetItemData(player, Location, SlotId, _Item, true);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"RemoveIndex Exception: {e.ToString()}");
            }
        }
        public static bool RemoveAllIllegal(ExtPlayer player)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return false;

                string locationName = $"char_{characterData.UUID}";
                bool removed = false;
                List<ItemStruct> _Items = new List<ItemStruct>();
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (string Location in ItemsData[locationName].Keys)
                    {
                        foreach (var itemData in ItemsData[locationName][Location])
                        {
                            if (itemData.Value.ItemId == ItemId.Material || itemData.Value.ItemId == ItemId.Drugs || itemData.Value.ItemId == ItemId.BodyArmor)
                            {
                                _Items.Add(new ItemStruct(Location, itemData.Key, itemData.Value));
                                removed = true;
                            }
                        }
                    }
                }
                RemoveFix(player, locationName, _Items);

                int BagsSqlId = isBackpackItemsData(player);
                if (BagsSqlId != 0 && ItemsData.ContainsKey($"backpack_{BagsSqlId}") && ItemsData[$"backpack_{BagsSqlId}"].ContainsKey("backpack"))
                {
                    _Items = new List<ItemStruct>();
                    foreach (var itemData in ItemsData[$"backpack_{BagsSqlId}"]["backpack"])
                    {
                        if (itemData.Value.ItemId == ItemId.Material || itemData.Value.ItemId == ItemId.Drugs || itemData.Value.ItemId == ItemId.BodyArmor)
                        {
                            _Items.Add(new ItemStruct("backpack", itemData.Key, itemData.Value));
                            removed = true;
                        }
                    }
                    RemoveFix(player, $"backpack_{BagsSqlId}", _Items);
                }
                return removed;
            }
            catch (Exception e)
            {
                Log.Write($"RemoveAllIllegal Exception: {e.ToString()}");
                return false;
            }
        }
        public static bool IsBeard(bool gender, InventoryItemData item) 
        { 
            int Variation = -1; 
            if (item.ItemId == ItemId.Mask) Variation = Convert.ToInt32(item.Data.Split('_')[0]); 
 
            var MaskData = Chars.ClothesComponents.ClothesComponentData[gender][Chars.ClothesComponent.Masks]; 
            if (Variation != -1 && MaskData.ContainsKey(Variation) && MaskData[Variation].Donate > 0) 
                return false;                 
            else if (Variation != -1 &&  
                     Variation != 125 &&  
                     Variation != 127 &&  
                     Variation != 196 &&  
                     Variation != 197 &&  
                     Variation != 204 && 
                     Variation != 182 && 
                     Variation != 183 && 
                     Variation != 190 && 
                     Variation != 210 && 
                     Variation != 209 && 
                     Variation != 224 &&  
                     Variation != 223 &&  
                     Variation != 225 &&  
                     Variation != 228 &&  
                     Variation != 229 && 
                     Variation != 230 && 
                     Variation != 235 && 
                     Variation != 236 && 
                     Variation != 237 && 
                     Variation != 238 && 
                     Variation != 239 &&  
                     Variation != 227 &&  
                     Variation != 232 &&  
                     Variation != 240 &&  
                     Variation != 241 && 
                     Variation != 242 && 
                     Variation != 233 && 
                     Variation != 231 && 
                     Variation != 215 && 
                     Variation != 216 && 
                     Variation != 217 && 
                     Variation != 243 && 
                     Variation != 244 && 
                     Variation != 212)  
                return true; 
             
            return false; 
        } 
        public static void RemoveAllWeapons(ExtPlayer player, bool ammo, bool styawki = false, bool armour = false)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                string locationName = $"char_{characterData.UUID}";
                InventoryItemData Bags = null;

                List<ItemStruct> _Items = new List<ItemStruct>();
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (string Location in ItemsData[locationName].Keys)
                    {
                        //if (Location.Key == "accessories") continue;
                        foreach (var itemData in ItemsData[locationName][Location])
                        {
                            if (itemData.Value.ItemId == ItemId.Bag) Bags = itemData.Value;
                            if (itemData.Value.ItemId == ItemId.Wrench || itemData.Value.ItemId == ItemId.Flashlight || itemData.Value.ItemId == ItemId.Ball || itemData.Value.Data == "126_0_True") continue;

                            InventoryItemData item = itemData.Value;
                            ItemsInfo ItemInfo = ItemsInfo[item.ItemId];
                            if (ItemInfo.functionType == newItemType.Weapons ||
                                ItemInfo.functionType == newItemType.MeleeWeapons ||
                                item.ItemId == ItemId.StunGun || IsBeard(characterData.Gender, item) ||
                                (ammo && ItemInfo.functionType == newItemType.Ammo) ||
                                (styawki == true && item.ItemId == ItemId.Cuffs) ||
                                (armour == true && item.ItemId == ItemId.BodyArmor))
                            {
                                _Items.Add(new ItemStruct(Location, itemData.Key, item));
                            }
                        }
                    }
                }
                RemoveFix(player, locationName, _Items);
                if (Bags != null && ItemsData.ContainsKey($"backpack_{Bags.SqlId}") && ItemsData[$"backpack_{Bags.SqlId}"].ContainsKey("backpack"))
                {
                    _Items = new List<ItemStruct>();
                    foreach (var itemData in ItemsData[$"backpack_{Bags.SqlId}"]["backpack"])
                    {
                        if (itemData.Value.ItemId == ItemId.Wrench || itemData.Value.ItemId == ItemId.Flashlight || itemData.Value.ItemId == ItemId.Ball) continue;

                        InventoryItemData item = itemData.Value;
                        ItemsInfo ItemInfo = ItemsInfo[item.ItemId];
                        if (ItemInfo.functionType == newItemType.Weapons ||
                            ItemInfo.functionType == newItemType.MeleeWeapons ||
                            item.ItemId == ItemId.StunGun || IsBeard(characterData.Gender, item) ||
                            (ammo && ItemInfo.functionType == newItemType.Ammo) ||
                            (styawki == true && item.ItemId == ItemId.Cuffs) ||
                            (armour == true && item.ItemId == ItemId.BodyArmor))
                        {
                            _Items.Add(new ItemStruct("backpack", itemData.Key, item));
                        }
                    }
                    RemoveFix(player, $"backpack_{Bags.SqlId}", _Items);
                }
                Trigger.ClientEvent(player, "removeAllWeapons");
                player.RemoveAllWeapons();
            }
            catch (Exception e)
            {
                Log.Write($"RemoveAllWeapons Exception: {e.ToString()}");
            }
        }
        public static void RemoveFix(ExtPlayer player, string locationName, List<ItemStruct> Items)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (player != null && characterData == null) return;
                bool UpdateClothes = false;
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (ItemStruct ItemStruct in Items)
                    {
                        if (!ItemsData[locationName].ContainsKey(ItemStruct.Location)) continue;
                        else if (!ItemsData[locationName][ItemStruct.Location].ContainsKey(ItemStruct.Index)) continue;

                        InventoryItemData item = ItemsData[locationName][ItemStruct.Location][ItemStruct.Index];
                        if (item.SqlId == ItemStruct.Item.SqlId)
                        {
                            if (ItemStruct.Item.Count == item.Count)
                            {
                                ItemId ItemIdDell = item.ItemId;
                                item.ItemId = ItemId.Debug;
                                UpdateSqlItemData(locationName, ItemStruct.Location, ItemStruct.Index, item, ItemIdDell);
                                if (player != null) 
                                    UpdatePlayerItemData(player, locationName, ItemStruct.Location, ItemStruct.Index, new InventoryItemData());

                                ItemsOtherUpdate(player, locationName, ItemStruct.Location, ItemStruct.Index, new InventoryItemData());
                                if (player != null && ItemStruct.Location == "accessories")
                                {
                                    AccessoriesUse(player, ItemStruct.Index);
                                    UpdateClothes = true;
                                }
                                ItemsData[locationName][ItemStruct.Location].TryRemove(ItemStruct.Index, out _);
                                if (ItemsData[locationName][ItemStruct.Location].Count < 1) 
                                    ItemsData[locationName].TryRemove(ItemStruct.Location, out _);
                            }
                            else
                            {
                                item.Count -= ItemStruct.Item.Count;
                                UpdateSqlItemData(locationName, ItemStruct.Location, ItemStruct.Index, item);
                                if (player != null) 
                                    UpdatePlayerItemData(player, locationName, ItemStruct.Location, ItemStruct.Index, item);

                                ItemsOtherUpdate(player, locationName, ItemStruct.Location, ItemStruct.Index, item);
                                if (player != null && ItemStruct.Location == "accessories")
                                {
                                    AccessoriesUse(player, ItemStruct.Index);
                                    UpdateClothes = true;
                                }
                            }
                        }
                    }
                }
                if (player != null && UpdateClothes) 
                    ClothesComponents.UpdateClothes(player);
            }
            catch (Exception e)
            {
                Log.Write($"RemoveFix Exception: {e.ToString()}");
            }
        }

        public static void RemoveAllIllegalStuff(ExtPlayer player, bool IsRemoveBag = true)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                string locationName = $"char_{characterData.UUID}";
                InventoryItemData Bags = null;

                List<ItemStruct> _Items = new List<ItemStruct>();
                foreach (string Location in ItemsData[locationName].Keys)
                {
                    foreach (var itemData in ItemsData[locationName][Location])
                    {
                        if (itemData.Value.ItemId == ItemId.Debug) continue;
                        if (itemData.Value.ItemId == ItemId.Bag) Bags = itemData.Value;
                        InventoryItemData item = itemData.Value;
                        ItemsInfo ItemInfo = ItemsInfo[item.ItemId];
                        if (Police.IllegalsItems.ContainsKey(item.ItemId) || ItemInfo.functionType == newItemType.Ammo)
                        {
                            _Items.Add(new ItemStruct(Location, itemData.Key, item));
                        }
                    }
                }
                RemoveFix(player, locationName, _Items);
                if (IsRemoveBag && Bags != null && ItemsData.ContainsKey($"backpack_{Bags.SqlId}") && ItemsData[$"backpack_{Bags.SqlId}"].ContainsKey("backpack"))
                {
                    _Items = new List<ItemStruct>();
                    foreach (var itemData in ItemsData[$"backpack_{Bags.SqlId}"]["backpack"])
                    {
                        InventoryItemData item = itemData.Value;
                        ItemsInfo ItemInfo = ItemsInfo[item.ItemId];
                        if (Police.IllegalsItems.ContainsKey(item.ItemId) || ItemInfo.functionType == newItemType.Ammo)
                        {
                            _Items.Add(new ItemStruct("backpack", itemData.Key, item));
                        }
                    }
                    RemoveFix(player, $"backpack_{Bags.SqlId}", _Items);
                }
                Trigger.ClientEvent(player, "removeAllWeapons");
                player.RemoveAllWeapons();
            }
            catch (Exception e)
            {
                Log.Write($"RemoveAllIllegalStuff Exception: {e.ToString()}");
            }
        }
        public static void RemoveAllClothes(ExtPlayer player)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                string locationName = $"char_{characterData.UUID}";
                InventoryItemData Bags = null;

                List<ItemStruct> _Items = new List<ItemStruct>();
                foreach (string Location in ItemsData[locationName].Keys)
                {
                    if (Location == "fastSlots") continue;
                    foreach (var itemData in ItemsData[locationName][Location])
                    {
                        InventoryItemData item = itemData.Value;
                        if (item.ItemId == ItemId.Bag) Bags = item;
                        ItemsInfo ItemInfo = ItemsInfo[item.ItemId];
                        if (ItemInfo.functionType == newItemType.Clothes && item.ItemId != ItemId.Bag &&
                            (item.ItemId != ItemId.Mask || IsBeard(characterData.Gender, item)))
                        {
                            _Items.Add(new ItemStruct(Location, itemData.Key, item));
                        }
                    }
                }
                RemoveFix(player, locationName, _Items);
                if (Bags != null && ItemsData.ContainsKey($"backpack_{Bags.SqlId}") && ItemsData[$"backpack_{Bags.SqlId}"].ContainsKey("backpack"))
                {
                    _Items = new List<ItemStruct>();
                    foreach (var itemData in ItemsData[$"backpack_{Bags.SqlId}"]["backpack"])
                    {
                        InventoryItemData item = itemData.Value;
                        ItemsInfo ItemInfo = ItemsInfo[item.ItemId];
                        if (ItemInfo.functionType == newItemType.Clothes &&
                            (item.ItemId != ItemId.Mask || IsBeard(characterData.Gender, item)))
                        {
                            _Items.Add(new ItemStruct("backpack", itemData.Key, item));
                        }
                    }
                    RemoveFix(player, $"backpack_{Bags.SqlId}", _Items);
                }
                Trigger.ClientEvent(player, "removeAllWeapons");
                player.RemoveAllWeapons();
            }
            catch (Exception e)
            {
                Log.Write($"RemoveAllClothes Exception: {e.ToString()}");
            }
        }
        public static void RemoveAll(string locationName)
        {
            try
            {
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (var _ItemsData in ItemsData[locationName].Values)
                    {
                        foreach (InventoryItemData itemData in _ItemsData.Values)
                        {
                            OnDellItem(itemData.ItemId, itemData.Data);
                            
                            GameLog.Items($"deletedItem({itemData.SqlId})", locationName, (int)itemData.ItemId, itemData.Count, itemData.Data);
                            Database.Models.Items.AddItemDelete(itemData.SqlId);
                        }
                    }
                    ItemsData.TryRemove(locationName, out _);
                }
                ItemsAllClose(locationName);
                if (InventoryOtherPlayers.ContainsKey(locationName))
                    InventoryOtherPlayers.TryRemove(locationName, out _);
            }
            catch (Exception e)
            {
                Log.Write($"RemoveAll Exception: {e.ToString()}");
            }
        }

        public static IReadOnlyDictionary<string, int> InventoryMaxSlots = new Dictionary<string, int>()
        {
            { "accessories", 15 },
            { "inventory", 35 },
            { "backpack", 21 },
            { "fastSlots", 5 },
            { "trade", 8 },
            { "vehicle", 25 },//76
            { "CarKey", 38 },
            { "Fraction", 300 },
            { "Organization", 300 },
            { "furniture", 25 },
            { "tent", 16 },
        };
        public const int MaxSlotsInventory = 35;
        public static int GetMaxSlots(ExtPlayer player, string Location)
        {
            if (Location == "backpack" && player.IsCharacterData())
            {

                InventoryItemData Bags = GetItemData(player, "accessories", 8);
                if (Bags.ItemId == ItemId.Bag)
                {
                    int maxSlots = 20;
                    Dictionary<string, int> PlayerBugData = Bags.GetData();
                    ConcurrentDictionary<int, ClothesData> ShoesData = ClothesComponents.ClothesBugsData;
                    if (ShoesData.ContainsKey(PlayerBugData["Variation"]) && ShoesData[PlayerBugData["Variation"]].MaxSlots > 0) maxSlots = ShoesData[PlayerBugData["Variation"]].MaxSlots;
                    return maxSlots;
                }
            }
            if (InventoryMaxSlots.ContainsKey(Location))
            {
                return InventoryMaxSlots[Location];
            }
            return MaxSlotsInventory;
        }
        public static int AddItem(ExtPlayer player, string locationName, string Location, InventoryItemData Item, int MaxSlots = MaxSlotsInventory, bool isWarehouse = false)
        {
            try
            {
                if (!player.IsCharacterData()) return -1;
                int success = -1;
                AddInventoryArray(locationName, Location);
                if (ItemsInfo[Item.ItemId].Stack > 1)
                {
                    foreach (int SlotId in ItemsData[locationName][Location].Keys)
                    {
                        InventoryItemData item = ItemsData[locationName][Location][SlotId];
                        if (item.ItemId == Item.ItemId && ItemsInfo[Item.ItemId].Stack > item.Count)
                        {
                            if (item.Price != Item.Price) continue;

                            if (ItemsInfo[Item.ItemId].Stack >= (item.Count + Item.Count))
                            {
                                item.Count += Item.Count;
                                Item.Count = 0;
                            }
                            else
                            {
                                Item.Count = (item.Count + Item.Count) - ItemsInfo[Item.ItemId].Stack;
                                item.Count = ItemsInfo[Item.ItemId].Stack;
                            }
                            ItemId ItemIdDell = Item.ItemId; 
                            Item.ItemId = ItemId.Debug; 
                            UpdateSqlItemData(locationName, Location, Item.Index, Item, ItemIdDell); 
                            UpdateSqlItemData(locationName, Location, SlotId, item); 
                            UpdatePlayerItemData(player, locationName, Location, SlotId, item); 
                            ItemsOtherUpdate(player, locationName, Location, SlotId, item); 
                            if (Item.Count == 0) return SlotId; 
                            else success = SlotId; 
                        }
                    }
                }

                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!ItemsData[locationName][Location].ContainsKey(i) || ItemsData[locationName][Location][i].ItemId == ItemId.Debug)
                    {
                        if (Location == "fastSlots" && i == 4 && Item.ItemId != ItemId.Mask) continue;
                        if (isWarehouse && isFreeSlots(player, Item.ItemId, Item.Count, send: false) != 0) continue;
                        ItemsData[locationName][Location][i] = new InventoryItemData(Item.SqlId, Item.ItemId, Item.Count, Item.Data, i);
                        ItemsData[locationName][Location][i].Price = Item.Price;

                        UpdateSqlItemData(locationName, Location, i, ItemsData[locationName][Location][i]);
                        UpdatePlayerItemData(player, locationName, Location, i, ItemsData[locationName][Location][i]);
                        ItemsOtherUpdate(player, locationName, Location, i, ItemsData[locationName][Location][i]);
                        return i;
                    }
                }
                if (isWarehouse) 
                    AddItemWarehouse(player, Item, 10000);
                return success;
            }
            catch (Exception e)
            {
                Log.Write($"AddItem Exception: {e.ToString()}");
                return -1;
            }
        }
        public static int AddNewItem(ExtPlayer player, string locationName, string Location, ItemId ItemId, int count = 1, string ItemData = "", bool stack = true, int MaxSlots = MaxSlotsInventory, bool isInfo = true, int price = 0, bool addInWarehouse = false)
        {
            try
            {
                if (player != null && !player.IsCharacterData()) return -1;
                int success = -1;
                AddInventoryArray(locationName, Location);

                if (ItemsInfo[ItemId].Stack > 1 && stack)
                {
                    foreach (int SlotId in ItemsData[locationName][Location].Keys)
                    {
                        InventoryItemData item = ItemsData[locationName][Location][SlotId];
                        if (item.ItemId == ItemId && ItemsInfo[ItemId].Stack > item.Count)
                        {
                            if (price != item.Price) continue;

                            if (ItemsInfo[ItemId].Stack >= (item.Count + count))
                            {
                                item.Count += count;
                                count = 0;
                            }
                            else
                            {
                                count = (item.Count + count) - ItemsInfo[ItemId].Stack;
                                item.Count = ItemsInfo[ItemId].Stack;
                            }
                            UpdateSqlItemData(locationName, Location, SlotId, item);
                            if (player != null) UpdatePlayerItemData(player, locationName, Location, SlotId, item, isInfo);
                            else if (ItemId == ItemId.Bag) UpdatePlayerItemData(player, locationName, "backpack", SlotId, item, isInfo);
                            ItemsOtherUpdate(player, locationName, Location, SlotId, item);
                            if (count == 0) return SlotId;
                            else success = SlotId;
                        }
                    }
                }
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!ItemsData[locationName][Location].ContainsKey(i) || ItemsData[locationName][Location][i].ItemId == ItemId.Debug)
                    {
                        if (count > ItemsInfo[ItemId].Stack)
                        {
                            AddSqlItem(player, locationName, Location, ItemId, i, ItemsInfo[ItemId].Stack, ItemData, price);
                            count -= ItemsInfo[ItemId].Stack;
                        }
                        else
                        {
                            AddSqlItem(player, locationName, Location, ItemId, i, count, ItemData, price);
                            count = 0;
                        }
                        if (count == 0) return i;
                        else success = i;
                    }
                }

                if (success == -1 && addInWarehouse)
                {
                    AddNewItemWarehouse(player, ItemId, count, ItemData);
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventoryThenSclad), 10000);
                }
                
                return success;
            }
            catch (Exception e)
            {
                Log.Write($"AddNewItem Exception: {e.ToString()}");
                return -1;
            }
        }
        public static void AddItemWarehouse(ExtPlayer player, InventoryItemData Item, int MaxSlots = MaxSlotsInventory)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                string locationName = $"warehouse_{characterData.UUID}";
                string Location = "warehouse";

                AddInventoryArray(locationName, Location);
                if (ItemsInfo[Item.ItemId].Stack > 1)
                {
                    foreach (int SlotId in ItemsData[locationName][Location].Keys)
                    {
                        InventoryItemData item = ItemsData[locationName][Location][SlotId];
                        if (item.ItemId == Item.ItemId && ItemsInfo[Item.ItemId].Stack > item.Count)
                        {
                            if (ItemsInfo[Item.ItemId].Stack >= (item.Count + Item.Count))
                            {
                                item.Count += Item.Count;
                                Item.Count = 0;
                            }
                            else
                            {
                                Item.Count = (item.Count + Item.Count) - ItemsInfo[Item.ItemId].Stack;
                                item.Count = ItemsInfo[Item.ItemId].Stack;
                            }
                            ItemId ItemIdDell = Item.ItemId;
                            UpdateSqlItemData(locationName, Location, SlotId, item);
                            if (Item.Count == 0)
                            {
                                Item.ItemId = ItemId.Debug;
                                UpdateSqlItemData(locationName, Location, Item.Index, Item, ItemIdDell);
                                return;
                            }
                        }
                    }
                }

                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!ItemsData[locationName][Location].ContainsKey(i) || ItemsData[locationName][Location][i].ItemId == ItemId.Debug)
                    {
                        if (Location == "fastSlots" && i == 4 && Item.ItemId != ItemId.Mask) continue;

                        if (ItemsData[locationName][Location].ContainsKey(i))
                            ItemsData[locationName][Location].TryRemove(i, out _);

                        ItemsData[locationName][Location].TryAdd(i, new InventoryItemData(Item.SqlId, Item.ItemId, Item.Count, Item.Data, i));
                        UpdateSqlItemData(locationName, Location, i, ItemsData[locationName][Location][i]);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"AddItem Exception: {e.ToString()}");
            }
        }
        public static void AddNewItemWarehouse(ExtPlayer player, ItemId ItemId, int count = 1, string ItemData = "")
        {
            try
            {
                if (player == null)
                    return;
                
                var uuid = player.GetUUID();
                
                OnAddItem(ItemId, ItemData);
                
                string locationName = $"warehouse_{uuid}";
                string Location = "warehouse";
                
                short SlotId = (short)GetItemsDataLastId(locationName, Location);
                
                if (!ItemsData.ContainsKey(locationName)) 
                    ItemsData.TryAdd(locationName, new ConcurrentDictionary<string, ConcurrentDictionary<int, InventoryItemData>>());
                
                if (!ItemsData[locationName].ContainsKey(Location)) 
                    ItemsData[locationName].TryAdd(Location, new ConcurrentDictionary<int, InventoryItemData>());
                
                if (ItemsData[locationName][Location].ContainsKey(SlotId)) 
                    ItemsData[locationName][Location].TryRemove(SlotId, out _);
                
                ItemsData[locationName][Location].TryAdd(SlotId, new InventoryItemData(-1, ItemId, count, ItemData, SlotId));
                Players.Phone.Messages.Repository.AddSystemMessage(player, (int)DefaultNumber.Warehouse, LangFunc.GetText(LangType.En, DataName.Posilka, Chars.Repository.ItemsInfo[ItemId].Name), DateTime.Now);
                
                Trigger.SetTask(() =>
                {
                    AddNewItemWarehouseThread(uuid, ItemId, count, ItemData, SlotId);
                });
            }
            catch (Exception e)
            {
                Log.Write($"AddNewItemWarehouse Exception: {e.ToString()}");
            }
        }
        public static async void AddNewItemWarehouseThread(int uuid, ItemId ItemId, int count = 1, string ItemData = "", short SlotId = 0)
        {
            try
            {
                string locationName = $"warehouse_{uuid}";
                string Location = "warehouse";

                await using var db = new ServerBD("MainDB");//On Separate Thread

                int itemSqlID = await db.InsertWithInt32IdentityAsync(new ItemsData
                {
                    DataId = locationName,
                    ItemId = (short)ItemId,
                    ItemCount = (short)count,
                    ItemData = ItemData,
                    Location = Location,
                    SlotId = SlotId,
                });
                ItemsData[locationName][Location][SlotId].SqlId = itemSqlID;
                GameLog.Items($"newItem({itemSqlID})", locationName, (int)ItemId, count, ItemData);
            }
            catch (Exception e)
            {
                Log.Write($"AddNewItemWarehouse Exception: {e.ToString()}");
            }
        }

        public static async void AddNewItemWarehouseThread(ServerBD db, int uuid, ItemId ItemId, int count = 1, string ItemData = "", short SlotId = 0)
        {
            try
            {
                string locationName = $"warehouse_{uuid}";
                string Location = "warehouse";

                int itemSqlID = await db.InsertWithInt32IdentityAsync(new ItemsData
                {
                    DataId = locationName,
                    ItemId = (short)ItemId,
                    ItemCount = (short)count,
                    ItemData = ItemData,
                    Location = Location,
                    SlotId = SlotId,
                });
                ItemsData[locationName][Location][SlotId].SqlId = itemSqlID;
                GameLog.Items($"newItem({itemSqlID})", locationName, (int)ItemId, count, ItemData);
            }
            catch (Exception e)
            {
                Log.Write($"AddNewItemWarehouse Exception: {e.ToString()}");
            }
        }
        public static void AddSqlItem(ExtPlayer player, string locationName, string Location, ItemId ItemId, int Index, int count = 1, string ItemData = "", int price = 0)//Переделать
        {
            try
            {                
                AddInventoryArray(locationName, Location);
                OnAddItem(ItemId, ItemData);
                
                ItemsData[locationName][Location][Index] = new InventoryItemData(-1, ItemId, count, ItemData, Index);
                ItemsData[locationName][Location][Index].Price = price;

                Trigger.SetTask(() =>
                {
                    AddSqlItemThread(player, locationName, Location, ItemId, Index, count, ItemData);
                });
            }
            catch (Exception e)
            {
                Log.Write($"AddSqlItem Exception: {e.ToString()}");
            }
        }
        public static async void AddSqlItemThread(ExtPlayer player, string locationName, string Location, ItemId ItemId, int Index, int count = 1, string ItemData = "")
        {
            try
            {

                await using var db = new ServerBD("MainDB");//On Separate Thread

                int itemSqlID = await db.InsertWithInt32IdentityAsync(new ItemsData
                {
                    DataId = locationName,
                    ItemId = (short)ItemId,
                    ItemCount = (short)count,
                    ItemData = ItemData,
                    Location = Location,
                    SlotId = (short)Index,
                });
                var itemData = ItemsData[locationName][Location][Index];
                itemData.SqlId = itemSqlID;
                GameLog.Items($"newItem({itemSqlID})", locationName, (int)ItemId, count, ItemData);
                NAPI.Task.Run(() =>
                {
                    try
                    {
                        if (InventoryLocation.ContainsKey(Location) && player.IsCharacterData())
                        {
                            SetItemData(player, Location, Index, itemData, send: true, isSqlUpdate: false, isInfo: true);
                        }
                        else if (InventoryOtherPlayers.ContainsKey(locationName))
                        {
                            foreach (ExtPlayer foreachPlayer in InventoryOtherPlayers[locationName])
                            {
                                if (!foreachPlayer.IsCharacterData()) continue;
                                //else if (otherPlayer.Value == player.Value) continue;
                                UpdatePlayerItemData(foreachPlayer, locationName, Location, Index, itemData);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Write($"AddSqlItem NAPI.Task.Run Exception: {e.ToString()}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"AddSqlItem Exception: {e.ToString()}");
            }
        }

        public static ItemStruct isItem(ExtPlayer player, string Location, ItemId ItemId, string Data = null)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return null;
                string locationName = $"char_{characterData.UUID}";
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location))
                {
                    foreach (var item in ItemsData[locationName][Location])//Todo
                    {
                        if (item.Value.ItemId == ItemId && (Data == null || (Data != null && Data == item.Value.Data)))
                        {
                            return new ItemStruct(Location, item.Key, item.Value);
                        };
                    }
                }
                if (Location == "inventory" && ItemsData.ContainsKey(locationName))
                {
                    foreach(string _location in ItemsData[locationName].Keys)
                    {
                        if (_location != "accessories" && _location != "fastSlots") continue;
                        else if (_location == "accessories" && ItemsInfo[ItemId].functionType != newItemType.Clothes && ItemId != ItemId.BagWithDrill & ItemId != ItemId.BagWithMoney) continue;
                        foreach (var item in ItemsData[locationName][_location])//Todo
                        {
                            if (item.Value.ItemId == ItemId && (Data == null || (Data != null && Data == item.Value.Data)))
                            {
                                return new ItemStruct(_location, item.Key, item.Value);
                            };
                        }
                    }
                } 
                return null;
            }
            catch (Exception e)
            {
                Log.Write($"isItem Exception: {e.ToString()}");
                return null;
            }
        }
        public static ItemStruct isItem(ExtPlayer player, string Location, int sqlId)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return null;
                string locationName = $"char_{characterData.UUID}";
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location))
                {
                    foreach (var item in ItemsData[locationName][Location])//Todo
                    {
                        if (item.Value.SqlId == sqlId)
                        {
                            return new ItemStruct(Location, item.Key, item.Value);
                        };
                    }
                }
                if (Location == "inventory" && ItemsData.ContainsKey(locationName))
                {
                    foreach(string _location in ItemsData[locationName].Keys)
                    {
                        if (_location != "accessories" && _location != "fastSlots") continue;
                        //else if (_location == "accessories" && ItemsInfo[ItemId].functionType != newItemType.Clothes && ItemId != ItemId.BagWithDrill & ItemId != ItemId.BagWithMoney) continue;
                        foreach (var item in ItemsData[locationName][_location])//Todo
                        {
                            if (item.Value.SqlId == sqlId)
                            {
                                return new ItemStruct(_location, item.Key, item.Value);
                            };
                        }
                    }
                } 
                return null;
            }
            catch (Exception e)
            {
                Log.Write($"isItem Exception: {e.ToString()}");
                return null;
            }
        }
        public static int itemCount(ExtPlayer player, string Location, ItemId ItemId, string Data = null)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return 0;
                string locationName = $"char_{characterData.UUID}";
                var count = 0;
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location))
                {
                    foreach (var item in ItemsData[locationName][Location])//Todo
                    {
                        if (item.Value.ItemId == ItemId && (Data == null || (Data != null && Data == item.Value.Data)))
                            count++;
                    }
                }
                if (Location == "inventory" && ItemsData.ContainsKey(locationName))
                {
                    foreach(string _location in ItemsData[locationName].Keys)
                    {
                        if (_location != "accessories" && _location != "fastSlots") continue;
                        else if (_location == "accessories" && ItemsInfo[ItemId].functionType != newItemType.Clothes && ItemId != ItemId.BagWithDrill & ItemId != ItemId.BagWithMoney) continue;
                        foreach (var item in ItemsData[locationName][_location])//Todo
                        {
                            if (item.Value.ItemId == ItemId && (Data == null || (Data != null && Data == item.Value.Data)))
                                count++;
                        }
                    }
                } 
                return count;
            }
            catch (Exception e)
            {
                Log.Write($"isItem Exception: {e.ToString()}");
                return 0;
            }
        }
        public static ItemStruct isItemOther(string locationName, ItemId ItemId, string Data = null)
        {
            try
            {
                if (!ItemsData.ContainsKey(locationName)) return null;
                if (!ItemsData[locationName].ContainsKey("CarKey")) return null;
                foreach (var item in ItemsData[locationName]["CarKey"])//Todo
                {
                    if (item.Value.ItemId == ItemId && (Data == null || (Data != null && Data == item.Value.Data)))
                    {
                        return new ItemStruct("vehicle", item.Key, item.Value);
                    };
                }
                return null;
            }
            catch (Exception e)
            {
                Log.Write($"isItemOther Exception: {e.ToString()}");
                return null;
            }
        }

        //-1 - Если нет слотогв
        //Если 0 то есть

        public static int maxItemCount = 3;
        
        public static int isFreeSlots(ExtPlayer player, ItemId ItemId, int count = 1, bool send = true, string Location = "inventory")
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return 0;
                string locationName = $"char_{characterData.UUID}";
                AddInventoryArray(locationName, Location);

                var maxStack = ItemsInfo[ItemId].Stack;
                int rData = 0;
                if (ItemsInfo[ItemId].Stack <= 1)
                {
                    if (ItemsData[locationName][Location].Count(w => w.Value.ItemId != ItemId.Debug) >= InventoryMaxSlots[Location]) rData = -1;
                    //else if (BackpackSqlId != 0 && ItemsData[locationName][Location].Count >= InventoryMaxSlots[Location] && ItemsData[$"backpack_{BackpackSqlId}"]["backpack"].Count >= InventoryMaxSlots["backpack"]) rData = -1;
                    else if (ItemsInfo[ItemId].functionType == newItemType.Weapons && ItemId != ItemId.StunGun && ItemId != ItemId.RayPistol && WeaponRepository.WeaponsAmmoTypes.ContainsKey(ItemId))
                    {
                        var ammoType = WeaponRepository.WeaponsAmmoTypes[ItemId];
                        
                        var countItems = ItemsData[locationName].Sum(l => l.Key != "trade" ?
                                                                          l.Value.Count(l => WeaponRepository.WeaponsAmmoTypes.ContainsKey(l.Value.ItemId) && WeaponRepository.WeaponsAmmoTypes[l.Value.ItemId] == ammoType) : 0);
                        if (countItems >= maxItemCount)
                            rData = -2;
                    }
                    else if (ItemId == ItemId.BodyArmor)
                    {
                        var countItems = ItemsData[locationName].Sum(l => l.Key != "trade" ?
                                                                          l.Value.Count(l => l.Value.ItemId == ItemId) : 0);
                        if (countItems >= maxItemCount)
                            rData = -3;
                    }
                    else if (ItemsInfo[ItemId].functionType == newItemType.MeleeWeapons || ItemId == ItemId.Bag || ItemId == ItemId.KeyRing || ItemId == ItemId.StunGun || ItemId == ItemId.RayPistol)
                    {
                        var countItems = ItemsData[locationName].Sum(l => l.Key != "trade" ?
                            l.Value.Count(l => l.Value.ItemId == ItemId) : 0);

                        if (countItems > 0)
                            rData = -3;
                    }
                    else if (ItemId == ItemId.BagWithDrill || ItemId == ItemId.BagWithMoney)
                    {
                        var countItems = ItemsData[locationName].Values.Sum(l =>
                            l.Values.Count(l => (l.ItemId == ItemId.BagWithDrill || l.ItemId == ItemId.BagWithMoney)));

                        if (countItems > 0)
                            rData = -3;
                    }
                }
                else
                {
                    int allCount = 0;
                    foreach (var _Location in ItemsData[locationName])
                    {
                        if (_Location.Key == "trade") continue;
                        foreach (var itemData in _Location.Value)
                        {
                            if (itemData.Value.ItemId != ItemId) continue;
                            allCount += itemData.Value.Count == 0 ? 1 : itemData.Value.Count;
                        }
                    }

                    if (ItemsInfo[ItemId].functionType == newItemType.Ammo)
                        maxStack *= maxItemCount;
                        
                    if (allCount > 0)
                    {
                        if (maxStack == allCount)
                        {
                            if (send) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AlreadyHaveItemStack, ItemsInfo[ItemId].Name, maxStack), 3000);
                            return -1;
                        }
                        if (maxStack >= (allCount + count)) rData = 0;
                        else rData = maxStack - allCount;

                    }
                    else
                    {
                        if (count > maxStack) rData = count - maxStack;
                        else if (ItemsData[locationName][Location].Count >= MaxSlotsInventory) rData = -1;
                    }
                }
                if (send && rData == -1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);// "Недостаточно места в инвентаре"
                }
                else if (send && rData == -2)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AlreadyHaveWeapon, ItemsInfo[ItemId].Name), 3000); //"Невозможно взять {0}, потому что в инвентаре уже есть {0}"
                }
                else if (send && rData == -3)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AlreadyHaveItem, ItemsInfo[ItemId].Name), 3000); //"У Вас уже есть {0}
                }
                else if (send && rData != 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AlreadyHaveItemStack, ItemsInfo[ItemId].Name, maxStack), 3000); // Нет места для {0}, максимум можно иметь при себе - {1} шт.
                }
                /*else if(send && rData != 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Ошибка работы с предметом #{rData}, передайте администрации!", 3000);
                }*/
                return rData;
            }
            catch (Exception e)
            {
                Log.Write($"isFreeSlots Exception: {e.ToString()}");
                return 0;
            }
        }

        public static int getCountToLacationItem(string locationName, string Location, ItemId ItemId)
        {
            try
            {
                int count = 0;
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location))
                {
                    foreach (InventoryItemData itemData in ItemsData[locationName][Location].Values)//Todo
                    {
                        if (itemData.ItemId == ItemId.Debug) continue;
                        else if (itemData.ItemId != ItemId) continue;
                        count += itemData.Count < 1 ? 1 : itemData.Count;
                    }
                }
                return count;
            }
            catch (Exception e)
            {
                Log.Write($"getCountToLacationItem Exception: {e.ToString()}");
                return 0;
            }
        }

        public static int getCountItem(string locationName, ItemId ItemId, bool bagsToggled = true)
        {
            try
            {
                int count = 0;
                int BagsSqlId = -1;
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (var _itemsData in ItemsData[locationName].Values)//Todo
                    {
                        foreach (InventoryItemData itemData in _itemsData.Values)
                        {
                            if (itemData.ItemId == ItemId.Bag) BagsSqlId = itemData.SqlId;
                            if (itemData.ItemId == ItemId.Debug) continue;
                            else if (itemData.ItemId != ItemId) continue;
                            count += itemData.Count < 1 ? 1 : itemData.Count;
                        }
                    }
                }
                if (bagsToggled && BagsSqlId != -1 && ItemsData.ContainsKey($"backpack_{BagsSqlId}") && ItemsData[$"backpack_{BagsSqlId}"].ContainsKey("backpack"))
                {
                    foreach (InventoryItemData itemData in ItemsData[$"backpack_{BagsSqlId}"]["backpack"].Values)
                    {
                        if (itemData.ItemId == ItemId.Debug) continue;
                        else if (itemData.ItemId != ItemId) continue;
                        count += itemData.Count < 1 ? 1 : itemData.Count;
                    }
                }
                return count;
            }
            catch (Exception e)
            {
                Log.Write($"getCountItem Exception: {e.ToString()}");
                return 0;
            }
        }
        public static int getCountItem(string locationName, List<ItemId> ItemsId, bool bagsToggled = true)
        {
            try
            {
                int count = 0;
                int BagsSqlId = -1;
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (var _itemsData in ItemsData[locationName].Values)//Todo
                    {
                        foreach (InventoryItemData itemData in _itemsData.Values)
                        {
                            if (itemData.ItemId == ItemId.Bag) BagsSqlId = itemData.SqlId;
                            if (itemData.ItemId == ItemId.Debug) continue;
                            else if (!ItemsId.Contains(itemData.ItemId)) continue;
                            count += itemData.Count < 1 ? 1 : itemData.Count;
                        }
                    }
                }
                if (bagsToggled && BagsSqlId != -1 && ItemsData.ContainsKey($"backpack_{BagsSqlId}") && ItemsData[$"backpack_{BagsSqlId}"].ContainsKey("backpack"))
                {
                    foreach (InventoryItemData itemData in ItemsData[$"backpack_{BagsSqlId}"]["backpack"].Values)
                    {
                        if (itemData.ItemId == ItemId.Debug) continue;
                        else if (!ItemsId.Contains(itemData.ItemId)) continue;
                        count += itemData.Count < 1 ? 1 : itemData.Count;
                    }
                }
                return count;
            }
            catch (Exception e)
            {
                Log.Write($"getCountItem Exception: {e.ToString()}");
                return 0;
            }
        }

        public static int getCountItems(string locationName, ItemId itemId = ItemId.Debug)
        {
            try
            {
                int count = 0;
                int BagsSqlId = -1;
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (var _itemsData in ItemsData[locationName].Values)//Todo
                    {
                        foreach (InventoryItemData itemData in _itemsData.Values)
                        {
                            if (itemData.ItemId == ItemId.Bag) BagsSqlId = itemData.SqlId;
                            if (itemData.ItemId == ItemId.Debug) continue;
                            else if (itemId != ItemId.Debug && itemId != itemData.ItemId) continue;
                            count += itemData.Count < 1 ? 1 : itemData.Count;
                        }
                    }
                }
                if (BagsSqlId != -1 && ItemsData.ContainsKey($"backpack_{BagsSqlId}") && ItemsData[$"backpack_{BagsSqlId}"].ContainsKey("backpack"))
                {
                    foreach (InventoryItemData itemData in ItemsData[$"backpack_{BagsSqlId}"]["backpack"].Values)
                    {
                        if (itemData.ItemId == ItemId.Debug) continue;
                        else if (itemId != ItemId.Debug && itemId != itemData.ItemId) continue;
                        count += itemData.Count < 1 ? 1 : itemData.Count;
                    }
                }
                return count;
            }
            catch (Exception e)
            {
                Log.Write($"getCountItems Exception: {e.ToString()}");
                return 0;
            }
        }

        public static string getCountToStockItems(string locationName)
        {
            try
            {
                int ammoCount = 0;
                int otherCount = 0;
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (var _itemsData in ItemsData[locationName].Values)//Todo
                    {
                        foreach (InventoryItemData itemData in _itemsData.Values)
                        {
                            if (itemData.ItemId == ItemId.Debug) continue;
                            if (ItemsInfo[itemData.ItemId].functionType == newItemType.Ammo) ammoCount += itemData.Count < 1 ? 1 : itemData.Count;
                            else otherCount += 1;
                        }
                    }
                }

                return $"Weapons: {otherCount} pcs. | Ammo: {ammoCount} pcs.";
            }
            catch (Exception e)
            {
                Log.Write($"getCountToStockItems Exception: {e.ToString()}");
                return $"Weapons: 0 pcs. | Ammo: 0 pcs.";
            }
        }
        public static void UpdateSqlItemData(string locationName, string Location, int SlotId, InventoryItemData item, ItemId ItemIdDell = ItemId.Debug)
        {
            UpdateSqlItemDataThread(locationName, Location, SlotId, item, ItemIdDell);
        }
        public static void OnAddItem(ItemId itemId, string data)
        {
            if (itemId == ItemId.SimCard)
            {            
                var sim = 0;
                if (int.TryParse(data, out sim))
                    Players.Phone.Sim.Repository.Add(sim);
            }
            if (itemId == ItemId.VehicleNumber)
                VehicleManager.AddVehicleNumber(data);
        }
        
        public static void OnDellItem(ItemId itemId, string data)
        {
            int sim;
            
            if (itemId == ItemId.SimCard && int.TryParse(data, out sim)) 
                Players.Phone.Sim.Repository.Remove(sim);  
            
            if (itemId == ItemId.VehicleNumber)
                VehicleManager.RemoveVehicleNumber(data);
        }
        
        public static void UpdateSqlItemDataThread(string locationName, string Location, int SlotId, InventoryItemData item, ItemId ItemIdDell)
        {
            try
            {
                if (item.SqlId == 0) return;
                else if (item.ItemId == ItemId.Debug)
                {
                    GameLog.Items($"deletedItem({item.SqlId})", locationName, (int)ItemIdDell, item.Count, item.Data);
                    Database.Models.Items.AddItemDelete(item.SqlId);
                    
                    OnDellItem(ItemIdDell, item.Data);

                    /*using (var db = new ServerBD("MainDB"))
                    {
                        db.ItemsData
                            .Where(v => v.AutoId == item.SqlId)
                            .Delete();

                        db.Notes
                            .Where(v => v.ItemId == item.SqlId)
                            .Delete();
                    }*/
                }
                else
                {
                    if (!Database.Models.Items.IsItemUpdate(item.SqlId))
                        GameLog.Items($"updatedItem({item.SqlId})", locationName, (int)item.ItemId, item.Count, item.Data);
                    
                    Database.Models.Items.AddItemUpdate(item.SqlId, locationName, item.Count, item.Data, Location, SlotId);
                    
                    /*using MySqlCommand cmd = new MySqlCommand
                    {
                        CommandText = "CALL `UpdateItemData`(@data_id, @item_count, @item_data, @location, @slotId, @auto_id)"
                    };
                    cmd.Parameters.AddWithValue("@data_id", locationName);
                    cmd.Parameters.AddWithValue("@item_count", item.Count);
                    cmd.Parameters.AddWithValue("@item_data", item.Data);
                    cmd.Parameters.AddWithValue("@location", Location);
                    cmd.Parameters.AddWithValue("@slotId", SlotId);
                    cmd.Parameters.AddWithValue("@auto_id", item.SqlId);
                    MySQL.Query(cmd);*/

                    /*using (var db = new ServerBD("MainDB"))
                    {
                        db.ItemsData
                            .Where(v => v.AutoId == item.SqlId)
                            .Set(v => v.DataId, locationName)
                            .Set(v => v.ItemCount, Convert.ToInt16(item.Count))
                            .Set(v => v.ItemData, item.Data)
                            .Set(v => v.Location, Location)
                            .Set(v => v.SlotId, Convert.ToInt16(SlotId))
                            .Update();
                    }*/
                }
            }
            catch (Exception e)
            {
                Log.Write($"UpdateSqlItemData Exception: {e.ToString()}");
            }
        }
        public static void UpdatePlayerItemData(ExtPlayer player, string locationName, string Location, int SlotId, InventoryItemData item, bool isInfo = false)
        {
            try
            {
                if (!player.IsCharacterData()) return;

                if (Location != "backpack" && !InventoryLocation.ContainsKey(Location)) Location = "other";

                if (Location == "other" && (!InventoryOtherPlayers.ContainsKey(locationName) || !InventoryOtherPlayers[locationName].Contains(player))) return;

                InventoryItemData newItem = new InventoryItemData(SqlId: item.SqlId, ItemId: item.ItemId, Count: item.Count, Data: item.Data, Index: item.Index);
                newItem.Price = item.Price;
                if (newItem.ItemId == ItemId.CarKey) newItem.Data = GetVehicleName(newItem.Data);

                Trigger.ClientEvent(player, "client.inventory.UpdateSlot", Location, SlotId, JsonConvert.SerializeObject(newItem), isInfo);

                if (Location == "inventory") 
                    isRadio(player);
            }
            catch (Exception e)
            {
                Log.Write($"UpdatePlayerItemData Exception: {e.ToString()}");
            }
        }
        public static InventoryItemData GetItemData(ExtPlayer player, string Location, int SlotId)
        {
            try
            {
                //player.IsInstanceAlive
                string locationName = GetLocationName(player, Location);
                
                if (Location == "other") 
                    Location = locationName.Split('_')[0];
                
                if (locationName != null && ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey(Location) && ItemsData[locationName][Location].ContainsKey(SlotId))
                {
                    InventoryItemData Item = ItemsData[locationName][Location][SlotId];

                    var newItem = new InventoryItemData(Item.SqlId, Item.ItemId, Item.Count, Item.Data, Item.Index);
                    newItem.Price = Item.Price;

                    return newItem;
                }
                return new InventoryItemData();
            }
            catch (Exception e)
            {
                Log.Write($"GetItemData Exception: {e.ToString()}");
                return new InventoryItemData();
            }
        }
        public static void SetItemData(ExtPlayer player, string Location, int SlotId, InventoryItemData item, bool send = false, bool check = false, bool isSqlUpdate = true, bool isInfo = false, bool isWeaponShot = false)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                string locationName = GetLocationName(player, Location);
                if (Location == "other") Location = locationName.Split('_')[0];

                if (locationName != null)
                {
                    AddInventoryArray(locationName, Location);

                    InventoryItemData itemLog = item;
                    string textLog = "положил на склад";

                    if (item.ItemId == ItemId.Debug && ItemsData[locationName][Location].ContainsKey(SlotId))
                    {
                        textLog = "взял со склада";
                        itemLog = ItemsData[locationName][Location][SlotId];
                    }
                            
                    item.Index = SlotId;

                    if (ItemsData[locationName][Location].ContainsKey(SlotId))
                        ItemsData[locationName][Location].TryRemove(SlotId, out _);

                    if (item.ItemId != ItemId.Debug) 
                        ItemsData[locationName][Location].TryAdd(SlotId, item);
                    
                    if (isSqlUpdate) 
                        UpdateSqlItemData(locationName, Location, SlotId, item, itemLog.ItemId);

                    if (Location == "Fraction")
                    {
                        Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.TakeStock, $"{textLog} {ItemsInfo[itemLog.ItemId].Name} (x{itemLog.Count})");
                        var fracId = player.GetFractionId();
                        
                        if (fracId > (int)Fractions.Models.Fractions.None)
                            Manager.sendFractionMessage(fracId, "!{#636363}[F] " + $"{player.Name} ({player.Value}) {textLog} {ItemsInfo[itemLog.ItemId].Name} (x{itemLog.Count}).", true);
                    }
                    else if (Location == "Organization")
                        Organizations.Table.Logs.Repository.AddLogs(player, OrganizationLogsType.TakeStock, $"{textLog} {ItemsInfo[itemLog.ItemId].Name} (x{itemLog.Count})");

                    if (send)
                        UpdatePlayerItemData(player, locationName, Location, SlotId, item, isInfo: isInfo);

                    ItemsOtherUpdate(player, locationName, Location, SlotId, item);

                    if (Location == "accessories")
                    {
                        AccessoriesUse(player, SlotId);
                        ClothesComponents.UpdateClothes(player);
                    }
                    else if (Location == "fastSlots" && check)
                    {
                        WeaponRepository.RemoveHands(player);
                    }
                    else if (Location == "fastSlots" && sessionData.ActiveWeap.Index == SlotId && !isWeaponShot)
                    {
                        WeaponRepository.RemoveHands(player);
                    } 
                    else if (Location == "vehicle")
                    {
                        BattlePass.Repository.UpdateReward(player, 90);
                    }
                    else if (Location == "CarKey")
                    {
                        BattlePass.Repository.UpdateReward(player, 73);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"SetItemData Exception: {e.ToString()}");
            }
        }
        public static void ItemsOtherUpdate(ExtPlayer player, string locationName, string Location, int SlotId, InventoryItemData item)
        {
            try
            {
                if (Location == "trade" && player.IsCharacterData())
                {
                    var sessionData = player.GetSessionData();
                    if (sessionData.ItemsTrade != null)
                    {
                        ItemsTrade TradeItem = sessionData.ItemsTrade;
                        if (TradeItem.Target.IsCharacterData()) 
                            UpdatePlayerItemData(TradeItem.Target, locationName, "with_trade", SlotId, item);
                    }
                }
                else if (InventoryOtherPlayers.ContainsKey(locationName))
                {
                    foreach (ExtPlayer foreachPlayer in InventoryOtherPlayers[locationName])
                    {
                        if (!foreachPlayer.IsCharacterData()) continue;
                        else if (player != null && foreachPlayer.Value == player.Value) continue;
                        UpdatePlayerItemData(foreachPlayer, locationName, Location, SlotId, item);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"ItemsOtherUpdate Exception: {e.ToString()}");
            }
        }
        public static void ItemsMoveStack(ExtPlayer player, string selectLocation, int selectSlotId, string hoverLocation, int hoverSlotId, int count)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                InventoryItemData _sItem = GetItemData(player, selectLocation, selectSlotId);
                InventoryItemData _hItem = GetItemData(player, hoverLocation, hoverSlotId);
                ItemsInfo _hInfoItem = ItemsInfo[_hItem.ItemId];
                if (_sItem.ItemId == _hItem.ItemId || _hItem.ItemId == ItemId.Debug)
                {
                    /*if (_sItem.Count == _hInfoItem.Stack)
                    {
                        SetItemData(player, hoverLocation, hoverSlotId, _sItem, true);
                        SetItemData(player, selectLocation, selectSlotId, _hItem, true);
                    }
                    else */
                    if (_sItem.ItemId == _hItem.ItemId)
                    {
                        _hItem.Count += count;
                        _sItem.Count -= count;
                        SetItemData(player, hoverLocation, hoverSlotId, _hItem, true);
                        SetItemData(player, selectLocation, selectSlotId, _sItem, true);
                    }
                    else
                    {
                        _sItem.Count -= count;
                        SetItemData(player, selectLocation, selectSlotId, _sItem, true);

                        string locationName = GetLocationName(player, hoverLocation);
                        if (hoverLocation == "other") hoverLocation = locationName.Split('_')[0];

                        AddSqlItem(player, locationName, hoverLocation, _sItem.ItemId, hoverSlotId, count, _sItem.Data);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"ItemsMoveStack Exception: {e.ToString()}");
            }
        }
        public static void ItemsMove(ExtPlayer player, string selectLocation, int selectSlotId, string hoverLocation, int hoverSlotId)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                /*if(hoverLocation == "backpack") // Не фиксит передачу в рюкзак, которого нет, хоть сообщение и выдаёт.
                {
                    InventoryItemData _Item = GetItemData(player, "accessories", 8);
                    if (_Item.ItemId != ItemId.Bag) 
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "У Вас нет рюкзака", 1000);
                        return;
                    }
                }*/
                InventoryItemData _sItem = GetItemData(player, selectLocation, selectSlotId);
                InventoryItemData _hItem = GetItemData(player, hoverLocation, hoverSlotId);
                ItemsInfo _hInfoItem = ItemsInfo[_hItem.ItemId];
                if (_sItem.ItemId == _hItem.ItemId && _hInfoItem.Stack > 1 && _hInfoItem.Stack > _sItem.Count && _hInfoItem.Stack > _hItem.Count)
                {
                    int count = _hItem.Count < 2 ? 1 : _hItem.Count;
                    if (_hInfoItem.Stack >= (count + _sItem.Count))
                    {
                        _sItem.Count += count;
                        _hItem = new InventoryItemData(_hItem.SqlId);
                    }
                    else
                    {
                        _hItem.Count = (count + _sItem.Count) - _hInfoItem.Stack;
                        _sItem.Count = _hInfoItem.Stack;
                    }
                }
                
                int slotUpdate = 0;
                if (hoverLocation == "accessories" && hoverSlotId == 7)
                {
                    if (sessionData.ArmorHealth != -1 && player.Armor > sessionData.ArmorHealth) WeaponRepository.PlayerKickAntiCheat(player, 3, true);
                    _hItem.Data = player.Armor.ToString();
                    slotUpdate = 2;
                }
                else if (selectLocation == "accessories" && selectSlotId == 7)
                {
                    if (sessionData.ArmorHealth != -1 && player.Armor > sessionData.ArmorHealth) WeaponRepository.PlayerKickAntiCheat(player, 3, true);
                    _sItem.Data = player.Armor.ToString();
                    slotUpdate = 1;
                }

                SetItemData(player, hoverLocation, hoverSlotId, _sItem, slotUpdate == 1 ? true : false);
                SetItemData(player, selectLocation, selectSlotId, _hItem, slotUpdate == 2 ? true : false);
            }
            catch (Exception e)
            {
                Log.Write($"ItemsMove Exception: {e.ToString()}");
            }
        }
        #region Одежда

        public static void LoadAccessories(ExtPlayer player, bool gender = true, bool isUpdateClothes = true)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) 
                    return;
                
                var characterData = player.GetCharacterData();
                
                string locationName = $"";
                if (characterData == null) 
                    locationName = $"char_{sessionData.SelectUUID}";
                else
                {
                    gender = characterData.Gender;
                    locationName = $"char_{characterData.UUID}";
                }

                ClothesComponents.SetClothes(player, 10, Customization.EmtptySlots[gender][10], 0);
      
                /*var itemData = ClothesComponents.GetItemData(player, "accessories", 7);
                if (isUpdateClothes && characterData != null && itemData.ItemId == ItemId.BodyArmor)
                {
                    if (sessionData.ArmorHealth != -1 && player.Armor > sessionData.ArmorHealth) 
                        WeaponRepository.PlayerKickAntiCheat(player, 3, true);
                    
                    itemData.Data = player.Armor.ToString();
                    SetItemData(player, "accessories", 7, itemData, true);
                }*/

                //ConcurrentDictionary<int, InventoryItemData> accessories = null;

                //if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("accessories"))
                //    accessories = ItemsData[locationName]["accessories"];
                
                var clothes = false;
                var hat = false;
                for (int index = 0; index < InventoryMaxSlots["accessories"]; index++)
                {
                    if (new List<int>(){ 0, 1, 2, 3 }.Contains(index))
                    {
                        if (!hat)
                        {
                            AccessoriesUse(player, index, true, gender: gender);
                            hat = true;
                        }
                    }
                    else if (new List<int>(){ 5, 6, 9, 12}.Contains(index))
                    {
                        if (!clothes)
                        {
                            AccessoriesUse(player, index, true, gender: gender);
                            clothes = true;
                        }
                    }
                    else 
                        AccessoriesUse(player, index, true, gender: gender);
                }
                
                if (isUpdateClothes)
                    ClothesComponents.UpdateClothes(player);
            }
            catch (Exception e)
            {
                Log.Write($"LoadAccessories Exception: {e.ToString()}");
            }
        }

        public static bool ChangeAccessoriesItem(ExtPlayer player, int SlotId, string ItemData = "", bool UpdateSlot = false, ItemId DunamicId = ItemId.Debug)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return false;
                ItemId ItemId = DunamicId == ItemId.Debug ? AccessoriesInfo[SlotId] : DunamicId;
                if (!UpdateSlot)
                {
                    InventoryItemData _Item = GetItemData(player, "accessories", SlotId);
                    if (_Item.ItemId == ItemId.Debug)
                    {
                        AddSqlItem(player, $"char_{characterData.UUID}", "accessories", ItemId, SlotId, 1, ItemData);
                    }
                    else if (AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId, 1, ItemData) == -1) return false;
                }
                else
                {
                    InventoryItemData _Item = GetItemData(player, "accessories", SlotId);

                    if (_Item.ItemId != ItemId.Debug)
                    {
                        int FreeSlotId = AddItem(player, $"char_{characterData.UUID}", "inventory", _Item);
                        if (FreeSlotId == -1) return false;
                    }
                    AddSqlItem(player, $"char_{characterData.UUID}", "accessories", ItemId, SlotId, 1, ItemData);
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Write($"ChangeAccessoriesItem Exception: {e.ToString()}");
                return false;
            }
        }
        public static void AccessoriesUse(ExtPlayer player, int SlotId, bool toggled = false, bool gender = true)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                //if (!toggled && characterData != null && SlotId != 7 && SlotId != 8 && SlotId != 1 && ((sessionData.WorkData.OnDuty && Manager.FractionTypes[memberFractionData.Id] == FractionsType.Gov && memberFractionData.Id != (int)Fractions.Models.Fractions.FIB) || sessionData.WorkData.OnWork)) return;

                //if (!toggled && characterData != null && SlotId != 7 && player.Accessories != null && player.Accessories.ContainsKey(SlotId))
                //    return;

                //if (characterData != null && SlotId == 7 && player.Accessories != null && player.Accessories.ContainsKey(SlotId))
                //    Item = ClothesComponents.GetItemData(player, "accessories", 7);
                
                if (characterData != null)
                {
                    //Skin
                    if (player.GetSkin() == PedHash.FreemodeMale01 || player.GetSkin() == PedHash.FreemodeFemale01)
                    {
                        gender = player.GetSkin() == PedHash.FreemodeMale01;
                    }
                    else
                        gender = characterData.Gender;
                }

                
                //player.SetSkin((characterData.Gender) ? PedHash.FreemodeMale01 : PedHash.FreemodeFemale01);
                var Item = ClothesComponents.GetItemData(player, "accessories", SlotId);
                var ItemData = Item.GetData();
                var Variation = ItemData["Variation"] == -1 ? 0 : ItemData["Variation"];
                var Texture = ItemData["Texture"] == -1 ? 0 : ItemData["Texture"];

                switch (SlotId)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                        ClothesComponents.SetHat(player, gender);
                        break;
                    case 4://Accessories
                        if (Item.ItemId == ItemId.Debug)
                        {
                            Variation = Customization.EmtptySlots[gender][7];
                        }
                        else
                        {
                            ConcurrentDictionary<int, ClothesData> AccessoriesData = ClothesComponents.ClothesComponentData[gender][ClothesComponent.Accessories];
                            if (AccessoriesData.ContainsKey(Variation)) Variation = AccessoriesData[Variation].Variation;
                            else Variation = Customization.EmtptySlots[gender][7];
                        }
                        ClothesComponents.SetClothes(player, 7, Variation, Texture);
                        break;

                    case 5:
                    case 6:
                    case 9:
                    case 12:
                        //ApplyClothes(player);
                        ClothesComponents.SetTop(player, gender);
                        break;
                    case 7:
                        
                        
                        var bodyArmorsData = ClothesComponents.ClothesComponentData[gender][ClothesComponent.BodyArmors];
                        
                        if (bodyArmorsData.ContainsKey(Variation)) 
                            Variation = bodyArmorsData[Variation].Variation;

                        if (Item.ItemId != ItemId.Debug && Variation == 0)
                        {
                            Variation = 12;
                            Texture = 1;
                        }
                        ClothesComponents.SetClothes(player, 9, Variation, Texture);
                        
                        //
                        Item = GetItemData(player, "accessories", 7); 
                        
                        if (Item.ItemId == ItemId.Debug)
                        {
                            if (characterData != null && sessionData.ArmorHealth != -1)
                            {
                                sessionData.ArmorHealth = -1;
                                player.Armor = 0;
                                Trigger.ClientEvent(player, "client.isArmor", false);
                            }
                        }
                        else
                        {
                            if (int.TryParse(Item.Data, out int armdata) && characterData != null/* && sessionData.ArmorHealth == -1*/)
                            {
                                sessionData.ArmorHealth = armdata;
                                player.Armor = armdata;
                                Trigger.ClientEvent(player, "client.isArmor", true);

                                Main.OnAntiAnim(player);

                                if (!player.IsInVehicle)
                                    Trigger.PlayAnimation(player, "clothingshirt", "try_shirt_positive_d", 1);

                                Timers.StartOnce(1500, () =>
                                {
                                    try
                                    {
                                        if (!player.IsCharacterData()) return;

                                        if (!player.IsInVehicle) Trigger.StopAnimation(player);
                                        else Trigger.StopAnimation(player, false);

                                        Main.OffAntiAnim(player);
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Write($"UseArmor Task Exception: {e.ToString()}");
                                    }
                                }, true);
                            }
                        }
                        break;
                    case 8:
                        if (characterData != null)
                            isBackpackItemsData(player, true);
                        if (Item.ItemId == ItemId.BagWithMoney)
                        {
                            if (int.TryParse(Item.Data, out int moneyinbag))
                            {
                                if (moneyinbag < SafeMain.MaxMoneyInBag) 
                                    ClothesComponents.SetClothes(player, 5, 44, 0);
                                else 
                                    ClothesComponents.SetClothes(player, 5, 45, 0);
                            }
                            else 
                                ClothesComponents.SetClothes(player, 5, 44, 0);
                        }
                        else if (Item.ItemId == ItemId.BagWithDrill) 
                            ClothesComponents.SetClothes(player, 5, 41, 0);
                        else if (Item.ItemId == ItemId.Bag)
                        {
                            var BugsData = ClothesComponents.ClothesBugsData;
                            if (BugsData.ContainsKey(Variation)) Variation = BugsData[Variation].Variation;
                            else Variation = BugsData[108].Variation;//Customization.EmtptySlots[gender][5];108
                            
                            ClothesComponents.SetClothes(player, 5, Variation, Texture);
                        }
                        else 
                            ClothesComponents.SetClothes(player, 5, Customization.EmtptySlots[gender][5], 0);
                        break;
                    case 10:
                        if (Item.ItemId == ItemId.Debug)
                        {
                            ClothesComponents.ClearAccessory(player, 7, isBlock: false);
                        }
                        else
                        {
                            var braceletsData = ClothesComponents.ClothesComponentData[gender][ClothesComponent.Bracelets];
                            if (braceletsData.ContainsKey(Variation)) Variation = braceletsData[Variation].Variation;

                            ClothesComponents.SetAccessories(player, 7, Variation, Texture);
                        }
                        break;
                    case 11:
                        if (Item.ItemId == ItemId.Debug)
                        {
                            ClothesComponents.ClearAccessory(player, 6, isBlock: false);
                        }
                        else
                        {
                            var watchesData = ClothesComponents.ClothesComponentData[gender][ClothesComponent.Watches];
                            if (watchesData.ContainsKey(Variation)) 
                                Variation = watchesData[Variation].Variation;

                            ClothesComponents.SetAccessories(player, 6, Variation, Texture);
                        }
                        break;
                    case 13:
                        if (Item.ItemId == ItemId.Debug)
                        {
                            Variation = Customization.EmtptySlots[gender][6];
                        }
                        else
                        {
                            ConcurrentDictionary<int, ClothesData> ShoesData = ClothesComponents.ClothesComponentData[gender][ClothesComponent.Shoes];
                            if (ShoesData.ContainsKey(Variation)) Variation = ShoesData[Variation].Variation;
                            else Variation = Customization.EmtptySlots[gender][6];
                        }
                        //ClothesComponents.SetSpecialClothes(player, 6, Variation, Texture);
                        ClothesComponents.SetClothes(player, 6, Variation, Texture);
                        break;
                    case 14:
                        if (Item.ItemId == ItemId.Debug)
                        {
                            var decalsData = ClothesComponents.ClothesComponentData[gender][ClothesComponent.Decals];
                            if (decalsData.ContainsKey(Variation)) 
                                Variation = decalsData[Variation].Variation;
                        }
                        //ClothesComponents.SetSpecialClothes(player, 6, Variation, Texture);
                        ClothesComponents.SetClothes(player, 10, Variation, Texture);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Write($"AccessoriesUse Exception: {e.ToString()}");
            }
        }
        #endregion

        #region Использование
        public static void ItemsUse(ExtPlayer player, string ArrayName, int Index, bool fastSlotsToggled = true)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                InventoryItemData Item = GetItemData(player, ArrayName, Index);
                if (Item.ItemId == ItemId.Debug) return;
                ItemsInfo ItemInfo = ItemsInfo[Item.ItemId];
                string Name = ItemInfo.Name;

                if (ArrayName == "accessories")
                {
                    if (Index == 8 && (Item.ItemId == ItemId.BagWithDrill || Item.ItemId == ItemId.BagWithMoney))
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                        return;
                    }
                    if (Item.ItemId == ItemId.BodyArmor) 
                    {
                        if (player.IsInVehicle)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantSnyatBronikInVeh), 3000);
                            return;
                        }

                        if (sessionData.ArmorHealth != -1 && player.Armor > sessionData.ArmorHealth) WeaponRepository.PlayerKickAntiCheat(player, 3, true);
                        Item.Data = player.Armor.ToString();
                    }

                    if (AddItem(player, $"char_{characterData.UUID}", "inventory", Item) == -1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                        return;
                    }
                    Sounds.PlayPlayer3d(player, "inventory/clothes");
                    SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                    return;
                }
                else if (ArrayName == "fastSlots" && fastSlotsToggled)
                {
                    WeaponRepository.RemoteEvent_changeWeapon(player, Index + 1);
                    return;
                }
                else if (ArrayName != "inventory" && ArrayName != "fastSlots")
                {
                    if (AddItem(player, $"char_{characterData.UUID}", "inventory", Item) == -1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                        return;
                    }
                    SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGot, Name), 3000);
                    return;
                }

                bool gender = characterData.Gender;

                bool success = false;

                if (ItemInfo.functionType == newItemType.Clothes/* && Item.ItemId != ItemId.BodyArmor && Item.ItemId != ItemId.Mask*/)
                {                        
                    if (Item.ItemId == ItemId.BodyArmor && player.IsInVehicle)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "You can't put on body armor while in a vehicle.", 3000);
                        return;
                    }
                    if (Item.ItemId != ItemId.Bag && Item.ItemId != ItemId.BodyArmor && Item.ItemId != ItemId.Mask && gender != Item.GetGender())
                    {
                        string error_gender = (Item.GetGender()) ? LangFunc.GetText(LangType.En, DataName.MansC) : LangFunc.GetText(LangType.En, DataName.WomansC);
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ErrorGender, error_gender), 3000);
                        return;
                    }
                    foreach (KeyValuePair<int, ItemId> key in AccessoriesInfo)
                    {
                        if (key.Value == Item.ItemId)
                        {
                            InventoryItemData AccessoriesItem = GetItemData(player, "accessories", key.Key);
                            if (key.Key == 8 && (AccessoriesItem.ItemId == ItemId.BagWithDrill || AccessoriesItem.ItemId == ItemId.BagWithMoney))
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                                return;
                            }
                            if (AccessoriesItem.ItemId == ItemId.BodyArmor) AccessoriesItem.Data = player.Armor.ToString();
                            Sounds.PlayPlayer3d(player, "inventory/clothes");
                            if (AccessoriesItem.ItemId != ItemId.Debug) SetItemData(player, ArrayName, Index, AccessoriesItem, true);
                            else SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                            SetItemData(player, "accessories", key.Key, Item, true);
                            return;
                        }
                    }
                }
                else if (ItemInfo.functionType == newItemType.Weapons || ItemInfo.functionType == newItemType.MeleeWeapons)
                {
                    uint weaponId = (uint)WeaponRepository.GetHash(Item.ItemId.ToString());
                    if (WeaponComponents.WeaponsComponents.ContainsKey(weaponId))
                    {
                        if (sessionData.InventoryOtherLocationName == $"weapon_{Item.SqlId}") OtherClose(player);
                        else LoadOtherItemsData(player, "weapon", Item.SqlId.ToString(), 9, WeaponComponents.WeaponsComponents[weaponId].Count, $"{(int)Item.ItemId}_{Item.SqlId}");
                        return;
                    }
                    if (AddItem(player, $"char_{characterData.UUID}", "fastSlots", Item, MaxSlots: InventoryMaxSlots["fastSlots"]) == -1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                        return;
                    }
                    SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.FastSlotAdd), 3000);
                    return;
                }
                else if (Item.ItemId == ItemId.Beer)
                {
                    Trigger.TaskPlayAnim(player, "amb@world_human_drinking@beer@male@idle_a", "idle_c", 49, attachmentName: "beer");
                    Sounds.PlayPlayer3d(player, "inventory/drink");
                    BattlePass.Repository.UpdateReward(player, 66);

                    //Attachments.AddAttachment(player, Attachments.AttachmentsName.Beer);
                    //Trigger.PlayAnimation(player, "amb@world_human_drinking@beer@male@idle_a", "idle_c", 49);
                    NAPI.Task.Run(() =>
                    {
                        try
                        {
                            if (player.IsCharacterData())
                            {
                                if (player.IsInVehicle) sessionData.ToResetAnimPhone = true;
                                Trigger.ClientEvent(player, "startScreenEffect", "PPFilter", 150000, false);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Write($"ItemsUse Task#1 Exception: {e.ToString()}");
                        }
                    }, 3000);
                    Commands.RPChat("sme", player, LangFunc.GetText(LangType.En, DataName.Bahnuv) + (characterData.Gender ? "" : "а") + LangFunc.GetText(LangType.En, DataName.Pivka));

                    GameLog.Items($"usedItem({Item.SqlId})", $"char_{characterData.UUID}", Convert.ToInt32(Item.ItemId), 1, Item.Data);
                    success = true;
                }
                else if (ItemInfo.functionType == newItemType.Alco)
                {
                    int stage = Convert.ToInt32(Item.ItemId.ToString().Split("Drink")[1]);
                    stage = stage == -1 ? 0 : stage;

                    ResistData rdata = sessionData.ResistData;

                    if (rdata.Ban)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SoDrunk), 3000);
                        return;
                    }
                    int curStage = rdata.Stage;
                    int[] stageTimes = new int[4] { 0, 300, 420, 600 };

                    if (curStage == 0 || curStage == stage)
                    {
                        rdata.Stage = stage;
                        rdata.Time += stageTimes[stage];
                    }
                    else if (curStage < stage) rdata.Stage = stage;
                    else if (curStage > stage) rdata.Time += stageTimes[stage];

                    if (rdata.Time >= 1500) rdata.Ban = true;

                    Trigger.ClientEvent(player, "setResistStage", rdata.Stage);

                    Attachments.AddAttachment(player, ItemInfo.Model);

                    Main.OnAntiAnim(player);

                    if (!player.IsInVehicle) player.PlayAnimation("amb@world_human_drinking@beer@male@idle_a", "idle_c", 49);
                    // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "drink");
                    Sounds.PlayPlayer3d(player, "inventory/drink");

                    NAPI.Task.Run(() =>
                    {
                        try
                        {
                            var sessionData = player.GetSessionData();
                            if (sessionData == null) return;
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            else sessionData.ToResetAnimPhone = true;
                            Main.OffAntiAnim(player);
                            Trigger.ClientEvent(player, "startScreenEffect", "PPFilter", sessionData.ResistData.Time * 1000, false);
                            Attachments.RemoveAttachment(player, ItemInfo.Model);
                        }
                        catch (Exception e)
                        {
                            Log.Write($"ItemsUse Task#2 Exception: {e.ToString()}");
                        }
                    }, 3000);

                    if (sessionData.TimersData.ResistTimer != null) Timers.Stop(sessionData.TimersData.ResistTimer);
                    sessionData.TimersData.ResistTimer = Timers.Start(1000, () => AlcoFabrication.ResistTimer(player));
                    Commands.RPChat("sme", player, $"выпил" + (characterData.Gender ? "" : "а") + $" бутылку {Name}");
                    GameLog.Items($"usedItem({Item.SqlId})", $"char_{characterData.UUID}", Convert.ToInt32(Item.ItemId), 1, Item.Data);
                    success = true;
                }
                else if (ItemInfo.functionType == newItemType.Modification && Item.Data.Split("_").Length > 0)
                {

                    uint weaponHash = Convert.ToUInt32(Item.Data.Split("_")[0]);

                    Dictionary<uint, int> WeaponHashToItem = new Dictionary<uint, int>()
                    {
                        { 911657153, 109 },
                        { 100416529, 136 },
                        { 205991906, 137 },
                        { 177293209, 139 },
                        { 1785463520, 140 },
                        { 137902532, 105 },
                        { 171789620, 119 },
                        { 317205821, 148 },
                        { 324215364, 115 },
                        { 453432689, 100 },
                        { 487013001, 141 },
                        { 584646201, 108 },
                        { 727643628, 151 },
                        { 736523883, 117 },
                        { 961495388, 132 },
                        { 984333226, 146 },
                        { 1198256469, 153 },
                        { 1198879012, 110 },
                        { 1432025498, 149 },
                        { 1593441988, 101 },
                        { 1627465347, 122 },
                        { 1649403952, 131 },
                        { 2017895192, 142 },
                        { 2024373456, 124 },
                        { 2132975508, 130 },
                        { 2144741730, 121 },
                        { 2210333304, 127 },
                        { 2228681469, 135 },
                        { 2285322324, 113 },
                        { 2441047180, 152 },
                        { 2526821735, 134 },
                        { 2548703416, 111 },
                        { 2578377531, 102 },
                        { 2634544996, 120 },
                        { 2640438543, 143 },
                        { 2828843422, 145 },
                        { 2937143193, 128 },
                        { 3173288789, 123 },
                        { 3218215474, 103 },
                        { 3219281620, 112 },
                        { 3220176749, 126 },
                        { 3231910285, 129 },
                        { 3249783761, 107 },
                        { 3342088282, 138 },
                        { 3415619887, 114 },
                        { 3523564046, 104 },
                        { 3675956304, 116 },
                        { 3686625920, 125 },
                        { 3696079510, 106 },
                        { 3800352039, 144 },
                        { 4019527611, 147 },
                        { 4024951519, 118 },
                        { 4208062921, 133 },
                        { 3673305557, 391 }
                    };

                    if (!WeaponHashToItem.ContainsKey(weaponHash)) return;
                    ItemId WeaponItemId = (ItemId)WeaponHashToItem[weaponHash];
                    uint weaponId = (uint)WeaponRepository.GetHash((WeaponItemId).ToString());
                    if (WeaponItemId == ItemId.Debug || !WeaponComponents.WeaponsComponents.ContainsKey(weaponId)) return;
                    ItemStruct aItemStruct = isItem(player, "inventory", WeaponItemId);
                    if (aItemStruct == null) return;
                    if (ItemsData.ContainsKey($"weapon_{Item.SqlId}") && ItemsData[$"weapon_{Item.SqlId}"].ContainsKey("weapon"))
                    {
                        foreach (InventoryItemData itemData in ItemsData[$"weapon_{Item.SqlId}"]["weapon"].Values)
                        {
                            if (itemData.ItemId == Item.ItemId)
                            {
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AlreadyModifiedWeapon), 3000);
                                return;
                            };
                        }
                    }


                    if (AddItem(player, $"weapon_{aItemStruct.Item.SqlId}", "weapon", Item, MaxSlots: WeaponComponents.WeaponsComponents[weaponHash].Count) == -1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.KeyChainNoSpace), 3000);
                        return;
                    }
                    SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ModifWeapon), 3000);
                    return;
                }

                switch (Item.ItemId)
                {
                    
                    case ItemId.KeyRing:
                        if (sessionData.InventoryOtherLocationName == $"CarKey_{Item.SqlId}") OtherClose(player);
                        else LoadOtherItemsData(player, "CarKey", Item.SqlId.ToString(), 7, InventoryMaxSlots["CarKey"], $"{(int)Item.ItemId}_{Item.SqlId}");
                        return;
                    case ItemId.CarKey:

                        ItemStruct aItemStruct = isItem(player, "inventory", ItemId.KeyRing);
                        if (aItemStruct == null) return;

                        if (AddItem(player, $"CarKey_{aItemStruct.Item.SqlId}", "CarKey", Item, MaxSlots: InventoryMaxSlots["CarKey"]) == -1)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.KeyChainNoSpace), 3000);
                            return;
                        }
                        SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.KeyChainSucc), 3000);
                        BattlePass.Repository.UpdateReward(player, 73);
                        return;
                    case ItemId.Material:
                        ItemsClose(player, true);

                        Manager.OpenGunCraftMenu(player);
                        return;
                    case ItemId.Burger:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            if (sessionData.ResistData.Time < 600) Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                            Commands.RPChat("sme", player, $"съел" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 140);
                            UseEat(player, 1);
                            Sounds.PlayPlayer3d(player, "inventory/eat");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.Drugs:
                        if (DateTime.Now > sessionData.TimingsData.NextDrugs)
                        {
                            if (sessionData.InTanksLobby > -1) return;
                            Trigger.ClientEvent(player, "startScreenEffect", "DrugsTrevorClownsFight", 300000, false);
                            BattlePass.Repository.UpdateReward(player, 139);
                            switch (Main.rnd.Next(10))
                            {
                                case 0:
                                    Commands.RPChat("sme", player, $"smoked a blunt");
                                    break;
                                case 1:
                                    Commands.RPChat("sme", player, $"hit a bowl");
                                    break;
                                case 2:
                                    Commands.RPChat("sme", player, $"packed a bowl");
                                    break;
                                case 3:
                                    Commands.RPChat("sme", player, $"lit up a joint");
                                    break;
                                case 4:
                                    Commands.RPChat("sme", player, $"took a hit");
                                    break;
                                case 5:
                                    Commands.RPChat("sme", player, $"hit the pipe");
                                    break;
                                case 6:
                                    Commands.RPChat("sme", player, $"took a drag of some good stuff");
                                    break;
                                case 7:
                                    Commands.RPChat("sme", player, $"blasted a pinner");
                                    break;
                                case 8:
                                    Commands.RPChat("sme", player, $"took a toke");
                                    break;
                                default:
                                    Commands.RPChat("sme", player, $"sparked a joint");
                                    break;
                            }
                            sessionData.TimingsData.NextDrugs = DateTime.Now.AddMinutes(1.5);
                            UseEat(player, 2);
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DrugsTooMany), 3000);
                            return;
                        }
                        break;
                     case ItemId.Cocaine:
                        if (DateTime.Now > sessionData.TimingsData.NextDrugs)
                        {
                            if (sessionData.InTanksLobby > -1) return;
                            Trigger.ClientEvent(player, "startScreenEffect", "DrugsTrevorClownsFight", 300000, false);
                            BattlePass.Repository.UpdateReward(player, 139);
                            switch (Main.rnd.Next(10))
                            {
                                case 0:
                                    Commands.RPChat("sme", player, $"snorted a line");
                                    break;
                                case 1:
                                    Commands.RPChat("sme", player, $"did a line");
                                    break;
                                case 2:
                                    Commands.RPChat("sme", player, $"did a bump of blow");
                                    break;
                                case 3:
                                    Commands.RPChat("sme", player, $"railed a line");
                                    break;
                                case 4:
                                    Commands.RPChat("sme", player, $"took a key bump");
                                    break;
                                case 5:
                                    Commands.RPChat("sme", player, $"snorted some yayo");
                                    break;
                                case 6:
                                    Commands.RPChat("sme", player, $"sniffed some blow");
                                    break;
                                case 7:
                                    Commands.RPChat("sme", player, $"did a bump of coke");
                                    break;
                                case 8:
                                    Commands.RPChat("sme", player, $"hit a line of coke");
                                    break;
                                default:
                                    Commands.RPChat("sme", player, $"snorted some coke");
                                    break;
                            }
                            sessionData.TimingsData.NextDrugs = DateTime.Now.AddMinutes(1.5);
                            UseEat(player, 9);
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DrugsTooMany), 3000);
                            return;
                        }
                        break;
                    case ItemId.eCola:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            Commands.RPChat("sme", player, LangFunc.GetText(LangType.En, DataName.Drink) + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 138);
                            UseEat(player, 3);
                            Sounds.PlayPlayer3d(player, "inventory/drink");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.GasCan:
                        if (!player.IsInVehicle)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.MustInCar), 3000);
                            ItemsClose(player, true);
                            return;
                        }
                        var veh = (ExtVehicle) player.Vehicle;
                        var vehicleLocalData = veh.GetVehicleLocalData();
                        if (vehicleLocalData != null)
                        {
                            if (vehicleLocalData.Petrol <= -1) return;
                            int fuel = vehicleLocalData.Petrol;
                            if (fuel >= VehicleManager.VehicleTank[veh.Class])
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.FullFuel), 3000);
                                ItemsClose(player, true);
                                return;
                            }
                            fuel += 15;
                            
                            if (fuel > VehicleManager.VehicleTank[veh.Class]) 
                                fuel = VehicleManager.VehicleTank[veh.Class];
                            
                            veh.SetSharedData("PETROL", fuel);
                            vehicleLocalData.Petrol = fuel;
                            if (vehicleLocalData.Access == VehicleAccess.Garage || vehicleLocalData.Access == VehicleAccess.Personal)
                            {
                                string number = player.Vehicle.NumberPlate;
                                var vehicleData = VehicleManager.GetVehicleToNumber(number);
                                if (vehicleData != null) 
                                    vehicleData.Fuel = fuel;
                            }
                            BattlePass.Repository.UpdateReward(player, 42);
                        }
                        break;
                    case ItemId.HealthKit:
                        if (DateTime.Now > sessionData.TimingsData.NextMedKit)
                        {
                            if (player.Health >= 100)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouFullHP), 3000);
                                return;
                            }
                            player.Health = 100;
                            sessionData.TimingsData.NextMedKit = DateTime.Now.AddMinutes(5);
                            Commands.RPChat("sme", player, LangFunc.GetText(LangType.En, DataName.used) + (characterData.Gender ? "" : "а") + LangFunc.GetText(LangType.En, DataName.healthkity));
                            Main.OnAntiAnim(player);
                            Sounds.PlayPlayer3d(player, "inventory/medkit");
                            Trigger.ClientEvent(player, "blockMove", true);
                            if (!player.IsInVehicle)
                            {
                                player.PlayAnimation("amb@code_human_wander_texting_fat@female@enter", "enter", 49);
                                //, attachmentName: "HealthKit"
                            }
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "healthkit");
                            NAPI.Task.Run(() =>
                            {
                                try
                                {
                                    if (!player.IsCharacterData()) return;
                                    if (!player.IsInVehicle) Trigger.StopAnimation(player);
                                    else sessionData.ToResetAnimPhone = true;
                                    Main.OffAntiAnim(player);
                                    Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                                    Trigger.ClientEvent(player, "blockMove", false);
                                }
                                catch (Exception e)
                                {
                                    Log.Write($"ItemsUse Task#3 Exception: {e.ToString()}");
                                }
                            }, 3000);
                        }
                        else
                        {
                            long ticks = sessionData.TimingsData.NextMedKit.Ticks - DateTime.Now.Ticks;
                            if (ticks <= 0) return;
                            DateTime g = new DateTime(ticks);
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AptekCooldown, g.Minute, g.Second), 3000);
                            return;
                        }
                        BattlePass.Repository.UpdateReward(player, 94);
                        break;
                    case ItemId.Epinephrine:
                        if (player.Health >= 100)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouFullHP), 3000);
                            return;
                        }
                        player.Health = 100;
                        sessionData.TimingsData.NextMedKit = DateTime.Now.AddMinutes(5);
                        Commands.RPChat("sme", player, LangFunc.GetText(LangType.En, DataName.used) + (characterData.Gender ? "" : "а") + LangFunc.GetText(LangType.En, DataName.healthkity));
                        Main.OnAntiAnim(player);
                        Sounds.PlayPlayer3d(player, "inventory/medkit");
                        Trigger.ClientEvent(player, "blockMove", true);
                      
                        
                        // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "healthkit");
                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                if (!player.IsCharacterData()) return;
                                if (!player.IsInVehicle) Trigger.StopAnimation(player);
                                else sessionData.ToResetAnimPhone = true;
                                Main.OffAntiAnim(player);
                                Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                                Trigger.ClientEvent(player, "blockMove", false);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"ItemsUse Task#3 Exception: {e.ToString()}");
                            }
                        }, 3000);
                       
                        BattlePass.Repository.UpdateReward(player, 94);
                        break;
                    case ItemId.Bint:
                         if (DateTime.Now > sessionData.TimingsData.NextMedKit)
                         {
                             if (player.Health >= 100)
                             {
                                 Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouFullHP), 3000);
                                 return;
                             }
                             
                             player.Health += 40;
                             sessionData.TimingsData.NextMedKit = DateTime.Now.AddSeconds(30);
                            Commands.RPChat("sme", player, LangFunc.GetText(LangType.En, DataName.used) + (characterData.Gender ? "" : "а") + LangFunc.GetText(LangType.En, DataName.bint));
                            Main.OnAntiAnim(player);
                            Sounds.PlayPlayer3d(player, "inventory/medkit");
                            Trigger.ClientEvent(player, "blockMove", true);
                            if (!player.IsInVehicle) player.PlayAnimation("amb@code_human_wander_texting@female@enter", "enter", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "healthkit");
                            NAPI.Task.Run(() =>
                            {
                                try
                                {
                                    if (!player.IsCharacterData()) return;
                                    if (!player.IsInVehicle) Trigger.StopAnimation(player);
                                    else sessionData.ToResetAnimPhone = true;
                                    Main.OffAntiAnim(player);
                                    Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                                    Trigger.ClientEvent(player, "blockMove", false);
                                }
                                catch (Exception e)
                                {
                                    Log.Write($"ItemsUse Task#3 Exception: {e.ToString()}");
                                }
                            }, 3000);
                        }
                        else
                        {
                            long ticks = sessionData.TimingsData.NextMedKit.Ticks - DateTime.Now.Ticks;
                            if (ticks <= 0) return;
                            DateTime g = new DateTime(ticks);
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.BintCooldown, g.Minute, g.Second), 3000);
                            return;
                        }
                        break;
                    case ItemId.HotDog:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            if (sessionData.ResistData.Time < 600) Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                            Commands.RPChat("sme", player, $"съел" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 137);
                            UseEat(player, 4);
                            Sounds.PlayPlayer3d(player, "inventory/eat");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.ArmyLockpick:
                        if (!player.IsInVehicle || player.Vehicle.Model != (uint)VehicleHash.Barracks && player.Vehicle.Model != (uint)VehicleHash.Brickade)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.MustBeInArmyTruck), 3000);
                            return;
                        }
                        var vehicle = (ExtVehicle) player.Vehicle;
                        if (VehicleStreaming.GetEngineState(vehicle))
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CarAlreadyOn), 3000);
                            return;
                        }
                        int lucky = new Random().Next(0, 5);
                        if (lucky == 5) Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.ErrorCarOn), 3000);
                        else
                        {
                            Sounds.PlayPlayer3d(player, "inventory/keys");
                            VehicleStreaming.SetEngineState(vehicle, true);
                            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CarOn), 3000);
                        }
                        break;
                    case ItemId.Pizza:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            if (sessionData.ResistData.Time < 600) Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                            Commands.RPChat("sme", player, $"съел" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 134);
                            UseEat(player, 5);
                            Sounds.PlayPlayer3d(player, "inventory/eat");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.Sandwich:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            if (sessionData.ResistData.Time < 600) Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                            Commands.RPChat("sme", player, $"съел" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 135);
                            UseEat(player, 6);
                            Sounds.PlayPlayer3d(player, "inventory/eat");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.Sprunk:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            if (sessionData.ResistData.Time < 600) Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                            Commands.RPChat("sme", player, $"выпил" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 136);
                            UseEat(player, 7);
                            Sounds.PlayPlayer3d(player, "inventory/drink");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.Crisps:
                        if (DateTime.Now > sessionData.TimingsData.NextEat)
                        {
                            /*if (player.Health >= 100 || sessionData.InTanksLobby > -1)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DontWantToEat), 3000);
                                return;
                            }*/
                            sessionData.TimingsData.NextEat = DateTime.Now.AddSeconds(5);
                            if (sessionData.ResistData.Time < 600) Trigger.ClientEvent(player, "stopScreenEffect", "PPFilter");
                            Commands.RPChat("sme", player, $"съел" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            BattlePass.Repository.UpdateReward(player, 61);
                            UseEat(player, 8);
                            Sounds.PlayPlayer3d(player, "inventory/eat");
                        }
                        else
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNow), 3000);
                            return;
                        }
                        break;
                    case ItemId.Present:
                        Main.CheckMyBonusCode(player, Item.Data);
                        return;
                    case ItemId.BagWithDrill:
                    case ItemId.BagWithMoney:
                        InventoryItemData AccessoriesItem = GetItemData(player, "accessories", 8);
                        if (AccessoriesItem.ItemId != ItemId.Debug) SetItemData(player, ArrayName, Index, AccessoriesItem, true);
                        else SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                        SetItemData(player, "accessories", 8, Item, true);
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SucWearing, ItemInfo.Name), 3000);
                        return;
                    case ItemId.CarCoupon:
                        int vehiclesCount = VehicleManager.GetVehiclesCarCountToPlayer(player.Name);
                        if (vehiclesCount >= GarageManager.MaxGarageCars)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.MaxcarsCoupon), 6000);
                            return;
                        }
                        var house = HouseManager.GetHouse(player, true);
                        if (house != null)
                        {
                            var garage = house.GetGarageData();
                            if (garage == null || vehiclesCount >= GarageManager.GarageTypes[garage.Type].MaxCars)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.MaxcarsCoupon), 6000);
                                return;
                            }
                        }
                        VehicleManager.Create(player, Item.Data, new Color(225, 225, 225), new Color(225, 225, 225), Text: LangFunc.GetText(LangType.En, DataName.CouponActivate, Item.Data), Logs: $"CarCoupon({Item.Data}");
                        break;
                    case ItemId.Note:
                    case ItemId.LoveNote:
                        ItemsClose(player, true);
                        LoadNote(player, Item);
                        return;
                    case ItemId.Vape:
                        int value = 0;
                        try
                        {
                            value = Convert.ToInt32(Item.Data);
                        }
                        catch
                        {
                            value = 0;
                        }
                        if (value <= 0)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VapeBroken), 3000);
                            Chars.Repository.RemoveIndex(player, ArrayName, Index);
                            return;
                        }
                        Item.Data = $"{value - 1}";
                        SetItemData(player, ArrayName, Index, Item, true);
                        UseSmoke(player);
                        return;
                    case ItemId.Hookah:
                        ItemsToput(player, ArrayName, Index);
                        BattlePass.Repository.UpdateReward(player, 67);
                        return;
                    //
                    case ItemId.Fire:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Matras:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Tent:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Lezhak:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Towel:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flag:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Barrell:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Surf:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Vedro:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagstok:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Tenttwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Polotence:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Beachbag:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Zontik:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Zontiktwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Zontikthree:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Closedzontik:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Vball:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Bball:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Boomboxxx:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Table:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Tabletwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Tablethree:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Tablefour:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Chair:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Chairtwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Chaierthree:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Chaierfour:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Chairtable:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Korzina:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Light:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Alco:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Alcotwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Alcothree:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Alcofour:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Cocktail:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Cocktailtwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Fruit:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Fruittwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Packet:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Buter:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Patatoes:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Coffee:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Podnosfood:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Bbqtwo:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Bbq:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Firework1:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Firework2:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Firework3:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Firework4:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Vaza:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagwtokk:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagau:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagbr:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagch:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagcz:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flageng:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flageu:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagfin:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagfr:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagger:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagire:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagisr:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagit:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagjam:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagjap:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagmex:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagnig:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagnorw:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagpol:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagrus:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagbel:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagscot:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagscr:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagslovak:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagslov:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagsou:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagspain:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagswede:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagswitz:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagturk:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flaguk:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagus:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flagwales:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Flowerrr:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Konus:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Konuss:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Otboynik1:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Otboynik2:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Dontcross:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Stop:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.NetProezda:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Kpp:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Zabor1:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Zabor2:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Airlight:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Camera1:
                        ItemsToput(player, ArrayName, Index);
                        return;
                    case ItemId.Camera2:
                        ItemsToput(player, ArrayName, Index);
                        return;

                    //
                    case ItemId.Bong:
                        value = 0;
                        try
                        {
                            value = Convert.ToInt32(Item.Data);
                        }
                        catch
                        {
                            value = 0;
                        }
                        if (value <= 0)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VapeBroken), 3000);
                            Chars.Repository.RemoveIndex(player, ArrayName, Index);
                            return;
                        }
                        Item.Data = $"{value - 1}";
                        SetItemData(player, ArrayName, Index, Item, true);
                        UseBong(player);
                        BattlePass.Repository.UpdateReward(player, 69);
                        return;
                    case ItemId.SimCard:
                        if (characterData.UUID == Convert.ToInt32(Item.Data)) 
                            return;
                        
                        var phoneData = player.getPhoneData();
                        if (phoneData == null || phoneData.Settings == null) 
                            return;

                        if (phoneData.Settings.SimUpdateAntiFlood > DateTime.Now)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "You can change your SIM card once every 5 minutes.", 3000);
                            return;
                        }
                        phoneData.Settings.SimUpdateAntiFlood = DateTime.Now.AddMinutes(5);
                        
                        var sim = Players.Phone.Settings.Repository.OnAddSim(player, Convert.ToInt32(Item.Data));
          
                        if (sim != -1)
                        {
                            Item.Data = sim.ToString();
                            SetItemData(player, ArrayName, Index, Item, true);
                            ItemsClose(player, true);
                            return;   
                        }
                        break;
                    default:
                        if (!success)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantDoThisNowv2), 3000);
                            return;
                        }
                        break;
                }
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouUsed, Name), 3000);
                GameLog.Items($"usedItem({Item.SqlId})", $"char_{characterData.UUID}", Convert.ToInt32(Item.ItemId), 1, Item.Data);
                RemoveIndex(player, ArrayName, Index, 1);
                ItemsClose(player, true);

                if (Item.ItemId == ItemId.SimCard)
                    Players.Phone.Sim.Repository.Add(Convert.ToInt32(Item.Data));
            }
            catch (Exception e)
            {
                Log.Write($"ItemsUse Exception: {e.ToString()}");
            }
            return;
        }
        public static void LoadNote(ExtPlayer player, InventoryItemData Item)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                Trigger.SetTask(() =>
                {
                    LoadNoteThread(player, Item);
                });
            }
            catch (Exception e)
            {
                Log.Write($"LoadNote Exception: {e.ToString()}");
            }
        }
        public static async void LoadNoteThread(ExtPlayer player, InventoryItemData Item)
        {
            try
            {
                if (!player.IsCharacterData()) return;

                await using var db = new ServerBD("MainDB");//On Separate Thread

                var note = await db.Notes
                    .Where(v => v.ItemId == Item.SqlId)
                    .FirstOrDefaultAsync();

                Dictionary<string, object> _NoteData = new Dictionary<string, object>();
                _NoteData.Add("Type", Item.ItemId == ItemId.Note ? 0 : 1);
                if (note == null)
                {
                    _NoteData.Add("ItemId", Item.SqlId);
                }
                else
                {
                    _NoteData.Add("Name", note.Name);
                    _NoteData.Add("Text", note.Text);
                }
                Trigger.ClientEvent(player, "client.note.open", JsonConvert.SerializeObject(_NoteData));
            }
            catch (Exception e)
            {
                Log.Write($"LoadNoteThread Exception: {e.ToString()}");
            }
        }
        public static void UseSmoke(ExtPlayer player, ItemId ItemId = ItemId.Vape)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var name = "";
                uint AttachmentsName = 0;
                var sound = "";
                float scale = 0.5f;

                WeaponRepository.RemoveHands(player);

                switch (ItemId)
                {
                    case ItemId.Vape:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Vape)) return;
                        AttachmentsName = Attachments.AttachmentsName.Vape;
                        name = "вейп";
                        sound = "vape/one";
                        BattlePass.Repository.UpdateReward(player, 68);
                        BattlePass.Repository.UpdateReward(player, 71);
                        break;
                    case ItemId.Hookah:
                        name = "кальян";
                        sound = "kalik/kalik";
                        scale = 0.85f;
                        BattlePass.Repository.UpdateReward(player, 67);
                        break;
                }
                
                Main.OnAntiAnim(player);
                Commands.RPChat("sme", player, $"использовал" + (characterData.Gender ? "" : "а") + $" {name}");
               
                if (AttachmentsName != 0) 
                    Attachments.AddAttachment(player, AttachmentsName);

                if (!player.IsInVehicle) Trigger.PlayAnimation(player, "mp_player_inteat@burger", "mp_player_int_eat_burger", 49, false);
                // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "vape");
                Timers.StartOnce(100, () =>
                {
                    try
                    {
                        if (!player.IsCharacterData()) return;
                        Sounds.PlayPlayer3d(player, sound, new SoundData
                        {
                            volume = 0.15
                        });
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseSmoke Task#3 Exception: {e.ToString()}");
                    }
                }, true);
                Timers.StartOnce(1400, () =>
                {
                    try
                    {
                        if (!player.IsCharacterData()) return;
                        if (!player.IsInVehicle) Trigger.PlayAnimation(player, "anim@heists@humane_labs@finale@keycards", "ped_a_enter_loop", 49, false);
                        // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "smokeloop");
                        ParticleFx.PlayFXonEntityBone(player.Position, 25.0f, player, 20279, "core", "exp_grd_bzgas_smoke", 3400, new ParticleFxData(scale: scale));
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseSmoke Task#3 Exception: {e.ToString()}");
                    }
                }, true);
                Timers.StartOnce(4400, () =>
                {
                    try
                    {
                        if (!player.IsCharacterData()) return;

                        if (AttachmentsName != 0) 
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Vape);

                        if (!player.IsInVehicle) Trigger.StopAnimation(player);
                        else Trigger.StopAnimation(player, false);

                        Main.OffAntiAnim(player);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseSmoke Task#3 Exception: {e.ToString()}");
                    }
                }, true);
            }
            catch (Exception e)
            {
                Log.Write($"UseSmoke Exception: {e.ToString()}");
            }
        }
        public static void UseBong(ExtPlayer player, float scale = 0.5f)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                else if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Bong)) return;

                int playerDrugsAmount = getCountToLacationItem($"char_{characterData.UUID}", "inventory", ItemId.Drugs);
                if (playerDrugsAmount < 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NeedNarcoToUse), 3000);
                    return;
                }

                if (DateTime.Now < sessionData.TimingsData.NextDrugs)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.BongTooMany), 3000);
                    return;
                }

                ItemsClose(player, true);

                WeaponRepository.RemoveHands(player);
                Main.OnAntiAnim(player);

                switch (Main.rnd.Next(10))
                {
                    case 0:
                        Commands.RPChat("sme", player, $"milked the bong");
                        break;
                    case 1:
                        Commands.RPChat("sme", player, $"hit the bong");
                        break;
                    case 2:
                        Commands.RPChat("sme", player, $"packed the bong");
                        break;
                    case 3:
                        Commands.RPChat("sme", player, $"sparked the bowl in the bong");
                        break;
                    case 4:
                        Commands.RPChat("sme", player, $"took a rip from the bong");
                        break;
                    case 5:
                        Commands.RPChat("sme", player, $"ripped the bong");
                        break;
                    case 6:
                        Commands.RPChat("sme", player, $"cleared the bong");
                        break;
                    case 7:
                        Commands.RPChat("sme", player, $"torched the bowl of the bong");
                        break;
                    case 8:
                        Commands.RPChat("sme", player, $"took a massive toke from the bong");
                        break;
                    default:
                        Commands.RPChat("sme", player, $"hit the bong");
                        break;
                }
                Attachments.AddAttachment(player, Attachments.AttachmentsName.Bong);
                if (!player.IsInVehicle) Trigger.PlayAnimation(player, "mp_player_inteat@burger", "mp_player_int_eat_burger", 49, false);
                // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "vape");

                player.Health = (player.Health + 50 > 100) ? 100 : player.Health + 50;
                Remove(player, $"char_{characterData.UUID}", "inventory", ItemId.Drugs, 1);
                sessionData.TimingsData.NextDrugs = DateTime.Now.AddMinutes(2.5);
                Trigger.ClientEvent(player, "startScreenEffect", "DrugsTrevorClownsFight", 300000, false);

                Timers.StartOnce(100, () =>
                {
                    try
                    {
                        if (!player.IsCharacterData()) return;
                        Sounds.PlayPlayer3d(player, "bong/bong", new SoundData
                        {
                            volume = 0.15
                        }); // Звук для бонга
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseBong Task#1 Exception: {e.ToString()}");
                    }
                }, true);

                Timers.StartOnce(1400, () =>
                {
                    try
                    {
                        if (!player.IsCharacterData()) return;
                        if (!player.IsInVehicle) Trigger.PlayAnimation(player, "amb@code_human_in_car_mp_actions@smoke@std@rps@base", "idle_c", 49, false);
                        // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "bong");
                        ParticleFx.PlayFXonEntityBone(player.Position, 25.0f, player, 20279, "core", "exp_grd_bzgas_smoke", 3400, new ParticleFxData(scale: scale));
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseBong Task#2 Exception: {e.ToString()}");
                    }
                }, true);

                Timers.StartOnce(4400, () =>
                {
                    try
                    {
                        if (!player.IsCharacterData()) return;
                        Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Bong);
                        if (!player.IsInVehicle) Trigger.StopAnimation(player);
                        else Trigger.StopAnimation(player, false);
                        Main.OffAntiAnim(player);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseBong Task#3 Exception: {e.ToString()}");
                    }
                }, true);
            }
            catch (Exception e)
            {
                Log.Write($"UseSmoke Exception: {e.ToString()}");
            }
        }
        public static int ItemsHands(ExtPlayer player, string ArrayName, int Index, InventoryItemData Item)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return 0;

                //if (player.GetSharedData<string>("ANIM_USE") == null || player.GetSharedData<string>("ANIM_USE") == "null")
                //{
                ItemId ItemId = Item.ItemId;
                ItemsInfo ItemInfo = ItemsInfo[ItemId];
                switch (ItemId)
                {
                    case ItemId.Vape:
                        int value = 0;
                        try
                        {
                            value = Convert.ToInt32(Item.Data);
                        }
                        catch
                        {
                            value = 0;
                        }
                        if (value <= 0)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.VapeBroken), 3000);
                            Chars.Repository.RemoveIndex(player, ArrayName, Index);
                            return -1;
                        }
                        Item.Data = $"{value - 1}";
                        SetItemData(player, ArrayName, Index, Item, true);
                        UseSmoke(player);
                        return 0;
                    case ItemId.Rose:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Rose))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Rose);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Rose);
                            if (!player.IsInVehicle) player.PlayAnimation("anim@heists@humane_labs@finale@keycards", "ped_b_enter_loop", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "rose");
                        }
                        break;
                    case ItemId.Barbell:
                        if (player.IsInVehicle) return 0;
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Barbell))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Barbell);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Trigger.PlayAnimation(player, "amb@world_human_muscle_free_weights@male@barbell@idle_a", "idle_a", 49, attachmentName: "barbell");
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "barbell");
                        }
                        break;
                    /*case ItemId.Bear:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Teddy))
                        {
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Teddy, true);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Teddy);
                        }
                        return;*/
                    case ItemId.Binoculars:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Binoculars))
                        {
                            Trigger.ClientEvent(player, "binoculars.stop");
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Binoculars);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            //Trigger.TaskPlayAnim(player, "oddjobs@hunter", "binoculars_outro", 50, true);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Binoculars);
                            if (!player.IsInVehicle) Trigger.PlayAnimation(player, "oddjobs@hunter", "binoculars_loop", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "binoculars");
                            Trigger.ClientEvent(player, "binoculars.start");
                        }
                        break;
                    case ItemId.Umbrella:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Umbrella))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Umbrella);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Umbrella);
                            if (!player.IsInVehicle) player.PlayAnimation("anim@heists@humane_labs@finale@keycards", "ped_b_enter_loop", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "umbrella");
                        }
                        break;
                    case ItemId.Microphone:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.News_mic))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.News_mic);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.News_mic);
                            if (!player.IsInVehicle) player.PlayAnimation("anim@heists@humane_labs@finale@keycards", "ped_b_enter_loop", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "mic");
                        }
                        break;
                    case ItemId.Guitar:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Guitar))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Guitar);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Guitar);
                            if (!player.IsInVehicle) player.PlayAnimation("amb@lo_res_idles@", "world_human_musician_guitar_lo_res_base", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "guitar");
                        }
                        break;
                    case ItemId.Camera:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.News_camera))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.News_camera);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.News_camera);
                            if (!player.IsInVehicle) player.PlayAnimation("misscarsteal4@meltdown", "_rehearsal_camera_man", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "camera");
                        }
                        break;
                    case ItemId.VehicleNumber:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.VehicleNumber))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.VehicleNumber);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.VehicleNumber);
                        }
                        break;
                    case ItemId.NeonStick:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Neonstick))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Neonstick);
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Neonstickr);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Neonstick);
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Neonstickr);
                            if (!player.IsInVehicle) player.PlayAnimation("none", "none", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "rose");
                        }
                        break;
                    case ItemId.GlowStick:
                        if (Attachments.HasAttachment(player, Attachments.AttachmentsName.Glowstick))
                        {
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Glowstick);
                            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Glowstickr);
                            if (!player.IsInVehicle) Trigger.StopAnimation(player);
                            Main.OffAntiAnim(player);
                        }
                        else
                        {
                            Commands.RPChat("sme", player, $"достал" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Glowstick);
                            Attachments.AddAttachment(player, Attachments.AttachmentsName.Glowstickr);
                            if (!player.IsInVehicle) player.PlayAnimation("none", "none", 49);
                            // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "rose");
                        }
                        break;
                    default:
                        return -1;
                    //}
                }
                return 1;
            }
            catch (Exception e)
            {
                Log.Write($"ItemsHands Task#3 Exception: {e.ToString()}");
            }
            return -1;
        }
        public static void NoteCreate(ExtPlayer player, int type, int ItemId, string nameValue, string textValue)
        {
            try
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SucSaveLetter), 3000);
                Trigger.SetTask(async () =>
                {
                    var characterData = player.GetCharacterData();
                    if (characterData == null) return;

                    await using var db = new ServerBD("MainDB");//On Separate Thread

                    await db.InsertAsync(new Notes
                    {
                        ItemId = ItemId,
                        Name = nameValue,
                        Text = textValue,
                        Type = Convert.ToBoolean(type)
                    });
                    
                    GameLog.AddInfo($"(Note) player({characterData.UUID}): {textValue}");
                });
            }
            catch (Exception e)
            {
                Log.Write($"NoteCreate Task#3 Exception: {e.ToString()}");
            }
        }
        public static void UseEat(ExtPlayer player, byte numb)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                WeaponRepository.RemoveHands(player);
                Main.OnAntiAnim(player);
                if (!player.IsInVehicle)
                {
                    sessionData.LastCoverState = player.IsInCover;
                    if (!player.IsInCover)
                    {
                        Animations.AnimationStop(player);
                        Trigger.ClientEvent(player, "blockMove", true);
                        switch (numb)
                        {
                            case 1:
                            case 4:
                            case 5:
                            case 6:
                            case 8:
                                Trigger.PlayAnimation(player, "amb@code_human_wander_eating_donut@male@idle_a", "idle_b", 48);
                                // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "eat");
                                if (numb == 1) Attachments.AddAttachment(player, Attachments.AttachmentsName.Burger);
                                else if (numb == 4) Attachments.AddAttachment(player, Attachments.AttachmentsName.HotDog);
                                else if (numb == 5) Attachments.AddAttachment(player, Attachments.AttachmentsName.Pizza);
                                else if (numb == 6) Attachments.AddAttachment(player, Attachments.AttachmentsName.Sandwich);
                                else if (numb == 8) Attachments.AddAttachment(player, Attachments.AttachmentsName.Crisps);
                                break;
                            case 2:
                                Trigger.PlayAnimation(player, "amb@code_human_wander_smoking@male@idle_a", "idle_a", 49);
                                // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "joint");
                                Attachments.AddAttachment(player, Attachments.AttachmentsName.Joint);
                                break;
                            case 9:
                                Trigger.PlayAnimation(player, "amb@code_human_wander_smoking@male@idle_a", "idle_a", 49);
                                // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "joint");
                                Attachments.AddAttachment(player, Attachments.AttachmentsName.Cocaine);
                                break;
                            case 3:
                            case 7:
                                Trigger.PlayAnimation(player, "amb@code_human_wander_drinking@male@idle_a", "idle_c", 49);
                                // Trigger.ClientEventInRange(player.Position, 250f, "PlayAnimToKey", player, false, "cola");
                                if (numb == 3) Attachments.AddAttachment(player, Attachments.AttachmentsName.eCola);
                                else if (numb == 7) Attachments.AddAttachment(player, Attachments.AttachmentsName.Sprunk);
                                break;
                            default:
                                // Not supposed to end up here. 
                                break;
                        }
                    }
                }
                NAPI.Task.Run(() =>
                {
                    try
                    {
                        var characterData = player.GetCharacterData();
                        if (characterData == null) return;
                        bool cover = player.IsInCover;
                        if (!cover)
                        {
                            switch (numb)
                            {
                                case 1:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Burger);
                                    break;
                                case 4:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.HotDog);
                                    break;
                                case 5:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Pizza);
                                    break;
                                case 6:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Sandwich);
                                    break;
                                case 8:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Crisps);
                                    break;
                                case 2:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Joint);
                                    break;
                                case 3:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.eCola);
                                    break;
                                case 7:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Sprunk);
                                    break;
                                case 9:
                                    Attachments.RemoveAttachment(player, Attachments.AttachmentsName.Cocaine);
                                    break;
                                default:
                                    // Not supposed to end up here. 
                                    break;
                            }
                        }
                        if(player.Health <= 0) return;
                        Main.OffAntiAnim(player);
                        characterData.EatTimes++;
                        if (sessionData.LastCoverState == true)
                        {
                            sessionData.LastCoverState = false;
                            if (characterData.IsAlive && !player.IsInCover)
                            {
                                player.Health = (player.Health + 5 > 100) ? 100 : player.Health + 5;
                                if (!characterData.Achievements[21])
                                {
                                    characterData.Achievements[21] = true;
                                    Notify.Send(player, NotifyType.Alert, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.Ukrytiefivehp), 5000);
                                }
                                return;
                            }
                        }
                        if (characterData.IsAlive)
                        {
                            if (!player.IsInVehicle && !cover) Trigger.StopAnimation(player);
                            switch (numb)
                            {
                                case 1:
                                case 4:
                                case 5:
                                case 6:
                                case 8:
                                    player.Health = (player.Health + 30 > 100) ? 100 : player.Health + 30;
                                    break;
                                case 2:
                                    player.Health = (player.Health + 50 > 100) ? 100 : player.Health + 50;
                                    break;
                                case 3:
                                case 7:
                                    player.Health = (player.Health + 10 > 100) ? 100 : player.Health + 10;
                                    break;
                                default:
                                    // Not supposed to end up here. 
                                    break;
                            }
                        }
                        if (!sessionData.CuffedData.Cuffed) Trigger.ClientEvent(player, "blockMove", false);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"UseEat Task Exception: {e.ToString()}");
                    }
                }, 3000);
            }
            catch (Exception e)
            {
                Log.Write($"UseEat Exception: {e.ToString()}");
            }
        }
        #endregion
        #region Выбросить
        public static void ItemsDrops(ExtPlayer player, ItemId ItemId)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                string locationName = $"char_{characterData.UUID}";

                InventoryItemData item = new InventoryItemData();
                string ArrayName = "";
                int Index = 0;
                InventoryItemData Bags = null;
                List<ItemStruct> _Items = new List<ItemStruct>();
                if (ItemsData.ContainsKey(locationName))
                {
                    foreach (var Location in ItemsData[locationName])
                    {
                        if (!ItemsData[locationName].ContainsKey(Location.Key)) continue;
                        foreach (var itemData in Location.Value)
                        {
                            if (!ItemsData[locationName][Location.Key].ContainsKey(itemData.Key)) continue;
                            InventoryItemData sItem = itemData.Value;
                            if (sItem.ItemId == ItemId.Bag) Bags = sItem;
                            if (sItem.ItemId != ItemId) continue;
                            if (item.ItemId == ItemId.Debug)
                            {
                                item = sItem;
                                ArrayName = Location.Key;
                                Index = itemData.Key;
                            }
                            else
                            {
                                item.Count += sItem.Count;
                                _Items.Add(new ItemStruct(Location.Key, itemData.Key, itemData.Value));
                            }
                        }
                    }
                    if (item.ItemId != ItemId.Debug)
                    {
                        RemoveFix(player, locationName, _Items);
                        //SetItemData(player, ArrayName, Index, item);
                        ItemsDropToIndex(player, ArrayName, Index);
                    }
                    if (Bags != null && ItemsData.ContainsKey($"backpack_{Bags.SqlId}") && ItemsData[$"backpack_{Bags.SqlId}"].ContainsKey("backpack"))
                    {
                        _Items = new List<ItemStruct>();
                        item = new InventoryItemData();
                        foreach (var itemData in ItemsData[$"backpack_{Bags.SqlId}"]["backpack"])
                        {
                            if (itemData.Value.ItemId == ItemId.Debug) continue;
                            InventoryItemData sItem = itemData.Value;
                            if (sItem.ItemId != ItemId) continue;
                            if (item.ItemId == ItemId.Debug)
                            {
                                item = sItem;
                                Index = itemData.Key;
                            }
                            else
                            {
                                item.Count += sItem.Count;
                                _Items.Add(new ItemStruct("backpack", itemData.Key, itemData.Value));
                            }
                        }
                        if (item.ItemId != ItemId.Debug)
                        {
                            RemoveFix(player, $"backpack_{Bags.SqlId}", _Items);
                            //SetItemData(player, ArrayName, Index, item);
                            //ItemsDropToIndex(player, "backpack", Index);
                            //isBackpackItemsData(player, true);

                            ItemsDrop(player, new InventoryItemData(0, item.ItemId, item.Count, item.Data));
                            if (ItemsData.ContainsKey($"backpack_{Bags.SqlId}") && ItemsData[$"backpack_{Bags.SqlId}"].ContainsKey("backpack") && ItemsData[$"backpack_{Bags.SqlId}"]["backpack"].ContainsKey(item.Index))
                            {
                                ItemId ItemIdDell = ItemsData[$"backpack_{Bags.SqlId}"]["backpack"][item.Index].ItemId;
                                ItemsData[$"backpack_{Bags.SqlId}"]["backpack"][item.Index].ItemId = ItemId.Debug;
                                UpdateSqlItemData(locationName, "backpack", item.Index, ItemsData[$"backpack_{Bags.SqlId}"]["backpack"][item.Index], ItemIdDell);
                                ItemsData[$"backpack_{Bags.SqlId}"]["backpack"].TryRemove(item.Index, out _);

                                isBackpackItemsData(player, true);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"ItemsDrops Exception: {e.ToString()}");
            }
        }

        public static void ItemsDropToIndex(ExtPlayer player, string ArrayName, int Index, bool me = false, bool check = false, float posZ = -1f)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                InventoryItemData Item = GetItemData(player, ArrayName, Index);
                if (Item.ItemId == ItemId.Debug || Item.ItemId == ItemId.GasCan) return;
                if (ArrayName == "accessories" && Index == 7) 
                {
                    if (sessionData.ArmorHealth != -1 && player.Armor > sessionData.ArmorHealth) WeaponRepository.PlayerKickAntiCheat(player, 3, true);
                    Item.Data = player.Armor.ToString();
                }
                if (!ItemsDrop(player, Item, me, posZ: posZ)) return;
                SetItemData(player, ArrayName, Index, new InventoryItemData(), true, check);
            }
            catch (Exception e)
            {
                Log.Write($"ItemsDropToIndex Exception: {e.ToString()}");
            }
        }
        public static bool ItemsDrop(ExtPlayer player, InventoryItemData Item, bool me = false, float posZ = -1f)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) 
                    return false;

                var characterData = player.GetCharacterData();
                if (characterData == null) 
                    return false;

                sessionData.TimingsData.NextDropItem = DateTime.Now.AddMilliseconds(150);
                var itemInfo = ItemsInfo[Item.ItemId];
                double xrnd = 0;
                double yrnd = 0;
                if (me)
                {
                    Random rnd = new Random();
                    xrnd = rnd.NextDouble();
                    yrnd = rnd.NextDouble();
                    Commands.RPChat("sme", player, $"выбросил" + (characterData.Gender ? "" : "а") + $" {itemInfo.Name}");
                    BattlePass.Repository.UpdateReward(player, 16);
                }
                
                Vector3 pos = player.Position;
                if (posZ != -1f) pos.Z = posZ + 1f;

                var position = pos + itemInfo.PosOffset + new Vector3(xrnd, yrnd, 0);
                var rotation = player.Rotation + itemInfo.RotOffset;

                Inventory.Drop.Repository.PutToObject(player, Item, position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z);
                return true;
            }
            catch (Exception e)
            {
                Log.Write($"ItemsDrop Exception: {e.ToString()}");
                return false;
            }
        }
        private static readonly IReadOnlyDictionary<ItemId, FireworkData> FireworkTypeData = new Dictionary<ItemId, FireworkData>()
        {
            { ItemId.Firework4, new FireworkData("scr_indep_firework_fountain", "PLACE_FIREWORK_4_CONE", 7500) },
            { ItemId.Firework3, new FireworkData("scr_indep_firework_shotburst", "PLACE_FIREWORK_3_BOX", 5000) },
            { ItemId.Firework2, new FireworkData("scr_indep_firework_starburst", "PLACE_FIREWORK_2_CYLINDER", 5500) },
            { ItemId.Firework1, new FireworkData("scr_indep_firework_trailburst", "PLACE_FIREWORK_1_ROCKET", 3200) }
        };
        private static ItemId[] ItemsToMy = new ItemId[]
        {
            ItemId.Hookah,
            ItemId.Fire, 
            ItemId.Matras, 
            ItemId.Tent, 
            ItemId.Lezhak, 
            ItemId.Towel, 
            ItemId.Flag, 
            ItemId.Barrell, 
            ItemId.Surf, 
            ItemId.Vedro, 
            ItemId.Flagstok, 
            ItemId.Tenttwo, 
            ItemId.Polotence, 
            ItemId.Beachbag, 
            ItemId.Zontik, 
            ItemId.Zontiktwo, 
            ItemId.Zontikthree, 
            ItemId.Closedzontik, 
            ItemId.Vball, 
            ItemId.Bball, 
            ItemId.Boomboxxx, 
            ItemId.Table, 
            ItemId.Tabletwo, 
            ItemId.Tablethree, 
            ItemId.Tablefour, 
            ItemId.Chair, 
            ItemId.Chairtwo, 
            ItemId.Chaierthree, 
            ItemId.Chaierfour, 
            ItemId.Chairtable, 
            ItemId.Korzina, 
            ItemId.Light, 
            ItemId.Alco, 
            ItemId.Alcotwo, 
            ItemId.Alcothree, 
            ItemId.Alcofour, 
            ItemId.Cocktail, 
            ItemId.Cocktailtwo, 
            ItemId.Fruit, 
            ItemId.Fruittwo, 
            ItemId.Packet, 
            ItemId.Buter, 
            ItemId.Patatoes, 
            ItemId.Coffee, 
            ItemId.Podnosfood, 
            ItemId.Bbqtwo, 
            ItemId.Bbq,
            ItemId.Vaza, 
            ItemId.Flagwtokk,
            ItemId.Flagau,
            ItemId.Flagbr, 
            ItemId.Flagch,
            ItemId.Flagcz, 
            ItemId.Flageng, 
            ItemId.Flageu,
            ItemId.Flagfin, 
            ItemId.Flagfr,
            ItemId.Flagger, 
            ItemId.Flagire, 
            ItemId.Flagisr, 
            ItemId.Flagit,
            ItemId.Flagjam, 
            ItemId.Flagjap, 
            ItemId.Flagmex, 
            ItemId.Flagnet, 
            ItemId.Flagnig, 
            ItemId.Flagnorw,
            ItemId.Flagpol, 
            ItemId.Flagrus, 
            ItemId.Flagbel, 
            ItemId.Flagscot,
            ItemId.Flagscr, 
            ItemId.Flagslov,
            ItemId.Flagslovak,
            ItemId.Flagsou, 
            ItemId.Flagspain,
            ItemId.Flagswede,
            ItemId.Flagswitz,
            ItemId.Flagturk,
            ItemId.Flaguk,
            ItemId.Flagus,
            ItemId.Flagwales,
            ItemId.Flowerrr,
            ItemId.Konus,
            ItemId.Konuss,
            ItemId.Otboynik1,
            ItemId.Otboynik2,
            ItemId.Dontcross,
            ItemId.Stop,
            ItemId.NetProezda,
            ItemId.Kpp,
            ItemId.Zabor1,
            ItemId.Zabor2,
            ItemId.Airlight,
            ItemId.Camera1,
            ItemId.Camera2,
        };
        
        public static void ItemsDropToEditor(ExtPlayer player, string arrayName, int index, float posX, float posY, float posZ, float rotX, float rotY, float rotZ)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) 
                    return;
                
                var sessionData = player.GetSessionData();
                if (sessionData == null) 
                    return;
                
                InventoryItemData Item = GetItemData(player, arrayName, index);
                if (Item.ItemId == ItemId.Debug || Item.ItemId == ItemId.GasCan) return;
                sessionData.TimingsData.NextDropItem = DateTime.Now.AddMilliseconds(150);
                ItemsInfo ItemInfo = ItemsInfo[Item.ItemId];
                Commands.RPChat("sme", player, $"поставил" + (characterData.Gender ? "" : "а") + $" {ItemInfo.Name}");
                //BattlePass.Repository.UpdateReward(player, 130);
                
                if (FireworkTypeData.ContainsKey (Item.ItemId))
                {
                    SetItemData(player, arrayName, index, new InventoryItemData(Item.SqlId), true);
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.fireworkstand), 7000);
                    BattlePass.Repository.UpdateReward(player, 23);
                    uint dim = UpdateData.GetPlayerDimension(player);
                    var obj = NAPI.Object.CreateObject(ItemInfo.Model, new Vector3(posX, posY, posZ), new Vector3(0, 0, rotZ), 255, dim);
                    Timers.StartOnce(5000, () =>
                    {
                        if (obj != null && obj.Exists)
                            obj.Delete();

                        ParticleFx.PlayFXonPos(new Vector3(posX, posY, posZ), 500f, posX, posY, posZ, "scr_indep_fireworks", FireworkTypeData[Item.ItemId].ParticleName, FireworkTypeData[Item.ItemId].EffectTime);
                    }, true);
                }
                else if (ItemsToMy.Contains(Item.ItemId))
                {
                    SetItemData(player, arrayName, index, new InventoryItemData(), true);

                    Inventory.Drop.Repository.PutToObject(player, Item, posX, posY, posZ, rotX, rotY, rotZ, isMy: true);
                }
                else
                {
                    if (arrayName == "accessories" && index == 7)
                    {
                        if (sessionData.ArmorHealth != -1 && player.Armor > sessionData.ArmorHealth) WeaponRepository.PlayerKickAntiCheat(player, 3, true);
                        Item.Data = player.Armor.ToString();
                    }
                    SetItemData(player, arrayName, index, new InventoryItemData(), true);
                    
                    Inventory.Drop.Repository.PutToObject(player, Item, posX, posY, posZ, rotX, rotY, rotZ);
                }
            }
            catch (Exception e)
            {
                Log.Write($"ItemsDropToEditor Exception: {e.ToString()}");
            }
        }
        #endregion


        #region Трейд

        public static void StartTrade(ExtPlayer player, ExtPlayer target)
        {
            try
            {
                if (!player.IsCharacterData() || !target.IsCharacterData()) return;
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                else if (sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath || characterData.LVL < 1)
                {
                    Notify.Send(target, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantTrade, player.Value), 3000);
                    return;
                }
                var targetSessionData = target.GetSessionData();
                if (targetSessionData == null) return; 
                var targetCharacterData = target.GetCharacterData();
                if (targetCharacterData == null) return;
                else if (targetSessionData.CuffedData.Cuffed || targetSessionData.DeathData.InDeath || targetCharacterData.LVL < 1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantTrade, player.Value), 3000);
                    return;
                }
                sessionData.ItemsTrade = new ItemsTrade(target);
                targetSessionData.ItemsTrade = new ItemsTrade(player);
                Trigger.ClientEvent(player, "client.inventory.InitTradeData", target.Name);
                Trigger.ClientEvent(target, "client.inventory.InitTradeData", player.Name);
            }
            catch (Exception e)
            {
                Log.Write($"StartTrade Exception: {e.ToString()}");
            }
        }
        public static void OtherClose(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                if (sessionData.InventoryOtherLocationName != null)
                {
                    string locationName = sessionData.InventoryOtherLocationName;

                    if (InventoryOtherPlayers.ContainsKey(locationName) && InventoryOtherPlayers[locationName].Contains(player)) 
                        InventoryOtherPlayers[locationName].Remove(player);

                    if (InventoryOtherPlayers.ContainsKey(locationName) && InventoryOtherPlayers[locationName].Count == 0)
                        InventoryOtherPlayers.TryRemove(locationName, out _);

                    sessionData.InventoryOtherLocationName = null;
                }
            }
            catch (Exception e)
            {
                Log.Write($"OtherClose Exception: {e.ToString()}");
            }
        }
        public static void ItemsClose(ExtPlayer player, bool Event = false)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return; 
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (Event) Trigger.ClientEvent(player, "client.inventory.Close");


                if (sessionData.SelectData.SelectedVeh != null)
                {
                    var vehicle = (ExtVehicle) sessionData.SelectData.SelectedVeh;
                    var vehicleLocalData = vehicle.GetVehicleLocalData();
                    if (vehicleLocalData != null) vehicleLocalData.BagInUse = false;
                }

                OtherClose(player);
                if (sessionData.ItemsTrade != null)
                {
                    ItemsTrade TradeData = sessionData.ItemsTrade;

                    ExtPlayer target = TradeData.Target;
                    var targetSessionData = target.GetSessionData();
                    if (targetSessionData == null) return; 
                    var targetCharacterData = target.GetCharacterData();
                    if (targetCharacterData == null) return;
                    sessionData.ItemsTrade = null;

                    string locationName = GetLocationName(player, "trade");

                    if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("trade"))
                    {
                        foreach (var item in ItemsData[locationName]["trade"])
                        {
                            if (item.Value.ItemId != ItemId.Debug) AddItem(player, $"char_{characterData.UUID}", "inventory", item.Value, isWarehouse: true);
                        }
                        ItemsData[locationName].TryRemove("trade", out _);
                    }

                    locationName = GetLocationName(target, "trade");

                    if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("trade"))
                    {
                        foreach (var item in ItemsData[locationName]["trade"])
                        {
                            if (item.Value.ItemId != ItemId.Debug) AddItem(target, $"char_{targetCharacterData.UUID}", "inventory", item.Value, isWarehouse: true);
                        }
                        ItemsData[locationName].TryRemove("trade", out _);
                    }

                    if (targetSessionData != null)
                    {
                        Trigger.ClientEvent(target, "client.inventory.Close");
                        targetSessionData.ItemsTrade = null;
                    }
                }
                if (sessionData.LookingStats)
                {
                    sessionData.LookingStats = false;
                    InitInventory(player);
                }
            }
            catch (Exception e)
            {
                Log.Write($"ItemsClose Exception: {e.ToString()}");
            }
        }
        public static void ItemsAllClose(string locationName)
        {
            try
            {
                if (InventoryOtherPlayers.ContainsKey(locationName))
                {
                    foreach (ExtPlayer foreachPlayer in InventoryOtherPlayers[locationName].ToList())
                    {
                        if (!foreachPlayer.IsCharacterData()) continue;
                        ItemsClose(foreachPlayer, true);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"ItemsAllClose Exception: {e.ToString()}");
            }
        }
        public static void ItemsTrade(ExtPlayer player, int status)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                var sessionData = player.GetSessionData();
                if (sessionData.ItemsTrade == null)
                {
                    ItemsClose(player, true);
                    return;
                }
                ItemsTrade TradeData = sessionData.ItemsTrade;
                ExtPlayer target = TradeData.Target;
                if (!target.IsCharacterData())
                {
                    ItemsClose(player, true);
                    return;
                }
                var targetSessionData = target.GetSessionData();
                if (targetSessionData.ItemsTrade == null)
                {
                    ItemsClose(player, true);
                    return;
                }
                if (status == 1 && targetSessionData.ItemsTrade.Status == 1)
                {
                    string locationName = GetLocationName(player, "trade");
                    string tLocationName = GetLocationName(target, "trade");

                    int myCount = ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("trade") ? ItemsData[locationName]["trade"].Count : 0;
                    int tCount = ItemsData.ContainsKey(tLocationName) && ItemsData[tLocationName].ContainsKey("trade") ? ItemsData[tLocationName]["trade"].Count : 0;
                    if (myCount < 1 && TradeData.Money < 1 &&
                        tCount < 1 && targetSessionData.ItemsTrade.Money < 1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NobodyVibral, target.Name), 3000);
                        return;
                    }

                    int myInvCount = ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("inventory") ? ItemsData[locationName]["inventory"].Count : 0;
                    int tInvCount = ItemsData.ContainsKey(tLocationName) && ItemsData[tLocationName].ContainsKey("inventory") ? ItemsData[tLocationName]["inventory"].Count : 0;
                    if (myCount > (InventoryMaxSlots["inventory"] - tInvCount))
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NetMestaTrade, target.Name), 3000);
                        return;
                    }
                    else if (tCount > (InventoryMaxSlots["inventory"] - myInvCount))
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouNetMestaTrade, target.Name), 3000);
                        return;
                    }
                }
                if (status == 2)
                {
                    string locationName = GetLocationName(target, "trade");
                    if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("trade") && ItemsData[locationName]["trade"].Count > 0)
                    {
                        foreach (var item in ItemsData[locationName]["trade"])
                        {
                            if (item.Value.ItemId != ItemId.Debug && isFreeSlots(player, item.Value.ItemId, item.Value.Count) != 0) return;
                        }
                    }
                }

                TradeData.Status = status;
                if (status == 1) Trigger.ClientEvent(player, "client.inventory.TradeUpdate", -1);
                else if (status == 2) Trigger.ClientEvent(player, "client.inventory.TradeUpdate", -2);
                if (status == 0 && targetSessionData.ItemsTrade.Status == 2) targetSessionData.ItemsTrade.Status = 1;
                Trigger.ClientEvent(target, "client.inventory.TradeUpdate", status);
                if (status == 2 && targetSessionData.ItemsTrade.Status == 2) ConfirmTrade(player);
            }
            catch (Exception e)
            {
                Log.Write($"ItemsTrade Exception: {e.ToString()}");
            }
        }
        public static void ItemsTradeMoney(ExtPlayer player, int value)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                if (sessionData.ItemsTrade == null)
                {
                    ItemsClose(player, true);
                    return;
                }
                ItemsTrade TradeData = sessionData.ItemsTrade;
                ExtPlayer target = TradeData.Target;
                if (!target.IsCharacterData())
                {
                    ItemsClose(player, true);
                    return;
                }
                var targetSessionData = target.GetSessionData();
                if (targetSessionData == null || targetSessionData.ItemsTrade == null)
                {
                    ItemsClose(player, true);
                    return;
                }
                else if (TradeData.Status != 0)
                {
                    Trigger.ClientEvent(player, "client.inventory.tradeMoney", "YourMoney", TradeData.Money);
                    return;
                }
                else if (characterData.Money < value)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoney), 3000);
                    TradeData.Money = (int)characterData.Money;
                    Trigger.ClientEvent(target, "client.inventory.tradeMoney", "WithMoney", TradeData.Money);
                    Trigger.ClientEvent(player, "client.inventory.tradeMoney", "YourMoney", TradeData.Money);
                    return;
                }
                TradeData.Money = value;
                Trigger.ClientEvent(target, "client.inventory.tradeMoney", "WithMoney", TradeData.Money);
            }
            catch (Exception e)
            {
                Log.Write($"ItemsTradeMoney Exception: {e.ToString()}");
            }
        }
        public static void ConfirmTrade(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                if (sessionData.ItemsTrade == null)
                {
                    ItemsClose(player, true);
                    return;
                }

                ItemsTrade TradeData = sessionData.ItemsTrade;
                ExtPlayer target = TradeData.Target;

                sessionData.ItemsTrade = null;
                if (!target.IsCharacterData())
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Обмен отменён", 3000);
                    ItemsClose(player, true);
                    return;
                }
                var targetSessionData = target.GetSessionData();
                var targetCharacterData = target.GetCharacterData();
                if (targetSessionData == null || targetCharacterData == null || targetSessionData.ItemsTrade == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.TradeCancelled), 3000);
                    ItemsClose(player, true);
                    return;
                }
                else if (UpdateData.CanIChange(player, TradeData.Money) != 255)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoney), 3000);
                    Notify.Send(target, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DoesntHaveMoney, player.Name), 3000);
                    ItemsClose(player, true);
                    return;
                }
                else if (UpdateData.CanIChange(target, targetSessionData.ItemsTrade.Money) != 255)
                {
                    Notify.Send(target, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoMoney), 3000);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DoesntHaveMoney, target.Name), 3000);
                    ItemsClose(target, true);
                    return;
                }
                /*else if (Main.ServerNumber != 0 && (characterData.AdminLVL >= 1 && characterData.AdminLVL <= 6))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AdminTransferRestricted), 3000);
                    return;
                }
                else if (Main.ServerNumber != 0 && (targetCharacterData.AdminLVL >= 1 && targetCharacterData.AdminLVL <= 6))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.AdminTransferRestricted), 3000);
                    return;
                }*/

                string locationName = GetLocationName(player, "trade");

                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("trade") && ItemsData[locationName]["trade"].Count > 0)
                {
                    foreach (var item in ItemsData[locationName]["trade"])
                    {
                        if (item.Value.ItemId != ItemId.Debug)
                        {
                            if (item.Value.ItemId == ItemId.LoveNote) 
                                BattlePass.Repository.UpdateReward(player, 36);
                            else if (item.Value.ItemId == ItemId.Rose) 
                                BattlePass.Repository.UpdateReward(player, 75);
                            else if (item.Value.ItemId == ItemId.Pizza) 
                                BattlePass.Repository.UpdateReward(player, 52);
                            else if (item.Value.ItemId == ItemId.HotDog) 
                                BattlePass.Repository.UpdateReward(player, 122);
                            else if (item.Value.ItemId == ItemId.Beer) 
                                BattlePass.Repository.UpdateReward(player, 37);                                                               
                            else if (item.Value.ItemId == ItemId.eCola) 
                                BattlePass.Repository.UpdateReward(player, 141);   
                            else if (item.Value.ItemId == ItemId.Sprunk) 
                                BattlePass.Repository.UpdateReward(player, 142);
                            else if (item.Value.ItemId == ItemId.Burger) 
                                BattlePass.Repository.UpdateReward(player, 143);   
                            else if (item.Value.ItemId == ItemId.Sandwich) 
                                BattlePass.Repository.UpdateReward(player, 144);        
                            else if (item.Value.ItemId == ItemId.HealthKit) 
                                BattlePass.Repository.UpdateReward(player, 145);
                            else if (item.Value.ItemId == ItemId.Note) 
                                BattlePass.Repository.UpdateReward(player, 146);
                            else if (item.Value.ItemId == ItemId.Revolver) 
                                BattlePass.Repository.UpdateReward(player, 41); 
                            else if (item.Value.ItemId == ItemId.Drugs) 
                                BattlePass.Repository.UpdateReward(player, 54);
                            else if (item.Value.ItemId == ItemId.Case0) 
                                BattlePass.Repository.UpdateReward(player, 123);
                            else if (item.Value.ItemId == ItemId.Wrench) 
                                BattlePass.Repository.UpdateReward(player, 133);  
                            
                            AddItem(target, $"char_{targetCharacterData.UUID}", "inventory", item.Value, isWarehouse: true);                                                                                                                                                                        
                        }
                    }
                    ItemsData[locationName].TryRemove("trade", out _);
                }
                if (TradeData.Money > 0)
                {
                    Wallet.Change(player, -TradeData.Money);
                    Wallet.Change(target, TradeData.Money);
                    GameLog.Money($"player({characterData.UUID})", $"player({targetCharacterData.UUID})", TradeData.Money, $"trade");
                    Commands.RPChat("sme", player, "передал" + (characterData.Gender ? "" : "а") + $" {Wallet.Format(TradeData.Money)}$ " + "{name}", target);
                    
                    // if (TradeData.Money >= 1000000)
                    //     Admin.AdminsLog(1, $"[ВНИМАНИЕ] Игрок {target.Name}({target.Value}) получил {TradeData.Money}$ единой операцией от {player.Name}({player.Value}) (ConfirmTrade-1 - Обмен)", 1, "#FF0000");
                    //
                    // if (TradeData.Money >= 10000 && targetSessionData.LastCashOperationSum == TradeData.Money)
                    // {
                    //     Admin.AdminsLog(1, $"[ВНИМАНИЕ] Игрок {target.Name}({target.Value}) два раза подряд получил по {TradeData.Money}$ от {player.Name}({player.Value}) (ConfirmTrade-1 - Обмен)", 1, "#FF0000");
                    //     targetSessionData.LastCashOperationSum = 0;
                    // }
                    // else
                    // {
                    //     targetSessionData.LastCashOperationSum = TradeData.Money;
                    // }
                }
                Notify.Send(target, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);

                locationName = GetLocationName(target, "trade");
                TradeData = targetSessionData.ItemsTrade;
                targetSessionData.ItemsTrade = null;
                if (ItemsData.ContainsKey(locationName) && ItemsData[locationName].ContainsKey("trade") && ItemsData[locationName]["trade"].Count > 0)
                {
                    foreach (var item in ItemsData[locationName]["trade"])
                    {
                        if (item.Value.ItemId != ItemId.Debug)
                        {
                            if (item.Value.ItemId == ItemId.LoveNote) 
                                BattlePass.Repository.UpdateReward(target, 36);
                            else if (item.Value.ItemId == ItemId.Rose) 
                                BattlePass.Repository.UpdateReward(target, 75);
                            else if (item.Value.ItemId == ItemId.Pizza) 
                                BattlePass.Repository.UpdateReward(target, 52);
                            else if (item.Value.ItemId == ItemId.HotDog) 
                                BattlePass.Repository.UpdateReward(target, 122);
                            else if (item.Value.ItemId == ItemId.Beer) 
                                BattlePass.Repository.UpdateReward(target, 37);                                                               
                            else if (item.Value.ItemId == ItemId.eCola) 
                                BattlePass.Repository.UpdateReward(target, 141);   
                            else if (item.Value.ItemId == ItemId.Sprunk) 
                                BattlePass.Repository.UpdateReward(target, 142);
                            else if (item.Value.ItemId == ItemId.Burger) 
                                BattlePass.Repository.UpdateReward(target, 143);   
                            else if (item.Value.ItemId == ItemId.Sandwich) 
                                BattlePass.Repository.UpdateReward(target, 144);        
                            else if (item.Value.ItemId == ItemId.HealthKit) 
                                BattlePass.Repository.UpdateReward(target, 145);
                            else if (item.Value.ItemId == ItemId.Note) 
                                BattlePass.Repository.UpdateReward(target, 146); 
                            else if (item.Value.ItemId == ItemId.Revolver) 
                                BattlePass.Repository.UpdateReward(target, 41); 
                            else if (item.Value.ItemId == ItemId.Drugs) 
                                BattlePass.Repository.UpdateReward(target, 54); 
                            else if (item.Value.ItemId == ItemId.Case0) 
                                BattlePass.Repository.UpdateReward(target, 123);
                            else if (item.Value.ItemId == ItemId.Wrench) 
                                BattlePass.Repository.UpdateReward(target, 133); 
                            
                            AddItem(player, $"char_{characterData.UUID}", "inventory", item.Value, isWarehouse: true);                                        

                        }
                    }
                    ItemsData[locationName].TryRemove("trade", out _);
                }
                if (TradeData.Money > 0)
                {
                    Wallet.Change(target, -TradeData.Money);
                    Wallet.Change(player, TradeData.Money);
                    GameLog.Money($"player({targetCharacterData.UUID})", $"player({characterData.UUID})", TradeData.Money, $"trade");
                    Commands.RPChat("sme", target, "handed over" + (targetCharacterData.Gender ? "" : "а") + $" {Wallet.Format(TradeData.Money)}$ " + "{name}", player);
                    
                    // if (TradeData.Money >= 1000000)
                    //     Admin.AdminsLog(1, $"[ВНИМАНИЕ] Игрок {player.Name}({player.Value}) получил {TradeData.Money}$ единой операцией от {target.Name}({target.Value}) (ConfirmTrade-1 - Обмен)", 1, "#FF0000");
                    //
                    // if (TradeData.Money >= 10000 && targetSessionData.LastCashOperationSum == TradeData.Money)
                    // {
                    //     Admin.AdminsLog(1, $"[ВНИМАНИЕ] Игрок {target.Name}({target.Value}) два раза подряд получил по {TradeData.Money}$ от {player.Name}({player.Value}) (ConfirmTrade-1 - Обмен)", 1, "#FF0000");
                    //     targetSessionData.LastCashOperationSum = 0;
                    // }
                    // else
                    // {
                    //     targetSessionData.LastCashOperationSum = TradeData.Money;
                    // }
                }
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.DealSuccess), 3000);
                BattlePass.Repository.UpdateReward(player, 72);
                BattlePass.Repository.UpdateReward(target, 72);
                ItemsClose(player, true);
                ItemsClose(target, true);
            }
            catch (Exception e)
            {
                Log.Write($"ConfirmTrade Exception: {e.ToString()}");
            }
        }
        #endregion

        #region Информация о игроке
        public static void PlayerStats(ExtPlayer player, ExtPlayer target = null)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                else if (target != null && !target.IsCharacterData()) return;

                ExtPlayer getPlayer = target == null ? player : target;

                var targetSessionData = getPlayer.GetSessionData();
                if (targetSessionData == null) return;
                var targetAccountData = getPlayer.GetAccountData();
                if (targetAccountData == null) return;
                var targetCharacterData = getPlayer.GetCharacterData();
                if (targetCharacterData == null) return;

                var charData = new List<object>();
                
                charData.Add(targetAccountData.Login);//0
                charData.Add(targetAccountData.VipLvl);//1
                charData.Add(targetAccountData.VipDate);//2
                charData.Add(targetCharacterData.Warns);//3
                charData.Add(targetCharacterData.Unwarn);//4
                targetCharacterData.Time = Main.GetCurrencyTime(target == null ? player : null, targetCharacterData.Time);
                charData.Add(targetCharacterData.Time.TodayTime);//5
                charData.Add(targetCharacterData.Time.MonthTime);//6
                charData.Add(targetCharacterData.Time.YearTime);//7
                charData.Add(targetCharacterData.Time.TotalTime);//8
                //Skils
                charData.Add(targetCharacterData.JobSkills);//9
                charData.Add(Main.GetPlayerJobsNextLevel(getPlayer));//10
                int[] currentLevelsInfo = { 0, 0, 0, 0, 0, 0, 0, 0 };
                for (int i = 0; i <= 7; i++)
                {
                    if (targetCharacterData.JobSkills.ContainsKey(i))
                        currentLevelsInfo[i] = Main.GetPlayerJobLevelBonus((sbyte)i, targetCharacterData.JobSkills[i]).Item1;
                }
                charData.Add(currentLevelsInfo);//11
                //
                charData.Add($"{targetCharacterData.FirstName} {targetCharacterData.LastName}");//12
                charData.Add(targetCharacterData.AdminLVL > 0);//13
                charData.Add(targetCharacterData.WeddingName.Length > 5 ? targetCharacterData.WeddingName : "Нет");//14
                charData.Add(targetCharacterData.Gender);//15
                charData.Add(targetCharacterData.LVL);//16
                charData.Add(targetCharacterData.EXP);//17
                charData.Add(targetCharacterData.Sim);//18
                charData.Add(targetCharacterData.WorkID);//19
                
                
                var targetMemberFractionData = Fractions.Manager.GetFractionMemberData(targetCharacterData.UUID);
                if (targetMemberFractionData != null)
                {
                    charData.Add(targetMemberFractionData.Id);//20
                    charData.Add(Fractions.Manager.GetFractionRankName(targetMemberFractionData.Id, targetMemberFractionData.Rank));//21
                }
                else
                {
                    charData.Add(null);//20
                    charData.Add(null);//21
                }

                var targetMemberOrganizationData = Organizations.Manager.GetOrganizationMemberData(targetCharacterData.UUID);
                if (targetMemberOrganizationData != null)
                {
                    charData.Add(targetMemberOrganizationData.Id);//22
                    charData.Add(Organizations.Manager.GetOrganizationRankName(targetMemberOrganizationData.Id, targetMemberOrganizationData.Rank));//23
                    /*var targetOrganizationData = Organizations.Manager.GetOrganizationData(targetMemberFractionData.Id);
                    if (targetOrganizationData != null)
                    {
                        charData.Add(targetOrganizationData.Name);//22
                        charData.Add(Organizations.Manager.GetOrganizationRankName(targetMemberOrganizationData.Id, targetMemberOrganizationData.Rank));//23
                    }
                    else
                    {
                        charData.Add(null);//22
                        charData.Add(null);//23
                    }*/
                }
                else
                {
                    charData.Add(null);//22
                    charData.Add(null);//23
                }


                charData.Add(targetCharacterData.UUID);//24
                charData.Add(targetCharacterData.Bank);//25
                charData.Add(Bank.GetBalance(targetCharacterData.Bank));//26
                charData.Add(targetCharacterData.Money);//27
                charData.Add(targetCharacterData.CreateDate);//28
                
                //
                
                
                var house = HouseManager.GetHouse($"{targetCharacterData.FirstName}_{targetCharacterData.LastName}", false);
                var garage = house?.GetGarageData();
                if (house != null)
                {
                    charData.Add(house.ID);//29
                    int houseBank = (int)Bank.GetBalance(house.BankID);
                    charData.Add(HouseManager.HouseTypeList[house.Type].Name);//30
                    charData.Add(house.Price == 0 ? "$0 / $0" : $"${Wallet.Format(houseBank)} / ${Wallet.Format(GetTax(house.Price, targetAccountData.VipLvl))}");//31
                    var tax = Convert.ToInt32(house.Price / 100 * 0.026);
                    charData.Add(house.Price == 0 ? "$0" : $"${Wallet.Format(tax)}");//32
                    int paid = (houseBank == 0 || tax == 0) ? 0 : Convert.ToInt32(houseBank / tax);
                    charData.Add(paid);//33
                    charData.Add(garage != null ? GarageManager.GarageTypes[garage.Type].MaxCars : 0);//34
                }
                else
                {
                    charData.Add(null);//29
                    charData.Add(null);//30
                    charData.Add(null);//31
                    charData.Add(null);//32
                    charData.Add(null);//33
                    charData.Add(null);//34
                }

                if (targetCharacterData.BizIDs.Count > 0)
                {
                    Business biz = BusinessManager.BizList[targetCharacterData.BizIDs[0]];
                    charData.Add(biz.ID);//35
                    int BizBank = (int)Bank.GetBalance(biz.BankID);
                    charData.Add(biz.SellPrice == 0 ? "$0 / $0" : $"${Wallet.Format(BizBank)} / ${Wallet.Format(Repository.GetTax(biz.SellPrice, targetAccountData.VipLvl, biz.Tax))}");//36
                    charData.Add(biz.SellPrice == 0 ? "$0" : $"${Wallet.Format(Convert.ToInt32(biz.SellPrice / 100 * biz.Tax))}");//37
                    int paid = (BizBank == 0 || biz.SellPrice == 0) ? 0 : BizBank / Convert.ToInt32(biz.SellPrice / 100 * biz.Tax);
                    charData.Add(paid);//38
                }
                else
                {
                    charData.Add(null);//35
                    charData.Add(null);//36
                    charData.Add(null);//37
                    charData.Add(null);//38
                }
                
                charData.Add(targetCharacterData.Licenses);//39
                if (targetCharacterData.WantedLVL != null)
                    charData.Add(targetCharacterData.WantedLVL.Level);//40
                else
                    charData.Add(0);//40          
                
                var statsData = JsonConvert.SerializeObject(charData);

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                
                if (sessionData.oldPlayerStats != statsData || target != null)
                {
                    if (target == null)
                    {
                        sessionData.oldPlayerStats = statsData;
                        Trigger.ClientEvent(player, "client.inventory.stats", statsData);
                    }
                    else
                    {
                        Trigger.ClientEvent(player, "client.accountStore.otherStatsData", statsData);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"PlayerStats Exception: {e.ToString()}");
            }
        }

        public static int GetTax(int Price, int VipLevel, double Tax = 0.026)
        {
            try
            {
                if (Price == 0) return 0;
                List<int> VipDays = new List<int>()
                {
                    7,7,14,21,28,28
                };

                int correctSum = Convert.ToInt32(Price / 100 * Tax * 24 * VipDays[VipLevel]);
                return correctSum >= 5 ? correctSum : 5;
            }
            catch (Exception e)
            {
                Log.Write($"GetTax Exception: {e.ToString()}");
                return 0;
            }
        }

        #endregion

        #region Информация о игроке
        public static void Event_PlayerDeath(ExtPlayer player)
        {
            try
            {
                if (!FunctionsAccess.IsWorking("dropitem")) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;
                if (characterData.DemorganTime >= 1) return;
                /*InventoryItemData PlayerBug = GetItemData(player, "accessories", 8);
                if (PlayerBug.ItemId == ItemId.Bag)
                {
                    Dictionary<string, int> PlayerBugData = PlayerBug.GetData();
                    ConcurrentDictionary<int, ClothesData> ShoesData = ClothesComponents.ClothesBugsData;
                    if (!ShoesData.ContainsKey(PlayerBugData["Variation"]) || ShoesData[PlayerBugData["Variation"]].Donate < 1) 
                        ItemsDropToIndex(player, "accessories", 8);
                }*/
                //if (!CheckAntiSave(player)) return;
                //CheckLastActive(player);
                if (!CheckLastActive(player)) return;
                CheckAntiSave(player);

            }
            catch (Exception e)
            {
                Log.Write($"Event_PlayerDeath Exception: {e.ToString()}");
            }
        }
        public static bool CheckAntiSave(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) 
                    return false;
                
                var lastWeapon = sessionData.LastActive;
                if (lastWeapon.WeaponSqlId == 0)
                {
                    WeaponRepository.OnClearTimerWeaponUpdate(player);
                    return true;
                }
                ItemStruct ItemStruct = isItem(player, "inventory", lastWeapon.WeaponSqlId);
                if (ItemStruct == null || ItemStruct.Item.ItemId == ItemId.Debug)
                {
                    WeaponRepository.OnClearTimerWeaponUpdate(player);
                    return true;
                }
                ItemsDropToIndex(player, ItemStruct.Location, ItemStruct.Index, check: true);
                Trigger.ClientEvent(player, "removeAllWeapons");
                player.RemoveAllWeapons();
                sessionData.LastActiveWeap = 0;
                WeaponRepository.OnClearTimerWeaponUpdate(player);
            }
            catch (Exception e)
            {
                Log.Write($"CheckAntiSave Exception: {e.ToString()}");
            }
            return false;
        }
        public static bool CheckLastActive(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return false;
                if (sessionData.LastActiveWeap == 0) return true;
                ItemStruct ItemStruct = isItem(player, "inventory", sessionData.LastActiveWeap);
                if (ItemStruct == null || ItemStruct.Item.ItemId == ItemId.Debug)
                {
                    sessionData.LastActiveWeap = 0;
                    return true;
                }
                ItemsDropToIndex(player, ItemStruct.Location, ItemStruct.Index);
                Trigger.ClientEvent(player, "removeAllWeapons");
                player.RemoveAllWeapons();
                sessionData.LastActiveWeap = 0;
            }
            catch (Exception e)
            {
                Log.Write($"CheckLastActive Exception: {e.ToString()}");
            }
            return false;
        }


        #endregion
        public static void ItemBuy(ExtPlayer player, string ArrayName, int Index, int Value)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            InventoryItemData Item = GetItemData(player, ArrayName, Index);

            if (Item.ItemId == ItemId.Debug) return;
            else if (Item.Price == 0) return;
            else if (Item.Count < Value) return;
            else if (isFreeSlots(player, Item.ItemId, Value) != 0) return;

            if (Value == 0) Value = 1;

            int price = Item.Price * Value;

            string locationName = null;

            if (ArrayName == "other" && sessionData.InventoryOtherLocationName != null) locationName = sessionData.InventoryOtherLocationName;

            if (locationName != null)
            {
                int index = Convert.ToInt32(locationName.Split('_')[1]);

                index = Inventory.Tent.Repository.GetUUIDToIndex(index);

                if (!Inventory.Tent.Repository.TentsData.ContainsKey(index)) return;

                var tentData = Inventory.Tent.Repository.TentsData[index];

                var target = tentData.player;
                var targetCharacterData = target.GetCharacterData();
                if (targetCharacterData == null) return;

                if (UpdateData.CanIChange(player, price, true) != 255) return;

                //GameLog.Money($"player({characterData.UUID})", $"tent({index})", price, "itemTent");

                MoneySystem.Wallet.Change(player, -price);

                MoneySystem.Wallet.Change(target, +price);

                GameLog.Money($"player({characterData.UUID})", $"player({targetCharacterData.UUID})", price, $"itemTent");

                //Notify.Send(target, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouItemBuyed, ItemsInfo[Item.ItemId].Name, price), 10000);
                
                Players.Phone.Messages.Repository.AddSystemMessage(target, (int)DefaultNumber.Tent, LangFunc.GetText(LangType.En, DataName.YouItemBuyed, ItemsInfo[Item.ItemId].Name, price), DateTime.Now);  

                BattlePass.Repository.UpdateReward(player, 48);
                
                ItemStack(player, ArrayName, Index, 2, Value, isBuy: true);
            }
        }
        public static void ItemStack(ExtPlayer player, string ArrayName, int Index, int Id, int Value, bool isBuy = false)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var Item = GetItemData(player, ArrayName, Index);

                if (Item.ItemId == ItemId.Debug) return;
                string locationName = null;
                if (ArrayName == "other" && sessionData.InventoryOtherLocationName != null) locationName = sessionData.InventoryOtherLocationName;
                else if (ArrayName != "other" && ArrayName != "backpack") locationName = $"char_{characterData.UUID}";
                else if (ArrayName == "backpack") locationName = $"backpack_{GetItemData(player, "accessories", 8).SqlId}";

                if (locationName == null) return;
                string Location = "inventory";
                if (ArrayName == "other") Location = locationName.Split('_')[0];
                else if (ArrayName == "backpack") Location = "backpack";
                else if (ArrayName == "trade") Location = "trade";

                var itemInfo = ItemsInfo[Item.ItemId];
                
                if (Item.ItemId == ItemId.Coal || Item.ItemId == ItemId.Iron || Item.ItemId == ItemId.Gold || Item.ItemId == ItemId.Sulfur || Item.ItemId == ItemId.Emerald || Item.ItemId == ItemId.Ruby)
                {
                    if (locationName == "vehicle" || Location == "vehicle")
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.CantMoveItem), 3000);
                        return;
                    }
                }

                if (Value < 1 || ((Id == 0 && Item.Count <= Value) || (Id != 0 && Item.Count < Value)))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SomethingWrong), 3000);
                    return;
                }
                if (Id == 0)
                {
                    if (AddNewItem(player, locationName, Location, Item.ItemId, Value, Item.Data, false, MaxSlots: GetMaxSlots(player, Location)) == -1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                        return;
                    }
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SuccSplit, itemInfo.Name), 3000);
                }
                else if (Id == 1)
                {
                    /*if (DateTime.Now < sessionData.TimingsData.NextDropItem)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.Drop1secOnly), 3000);
                        return;
                    }
                    else */if (player.IsInVehicle)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoDropFromCar), 3000);
                        return;
                    }
                    if (!ItemsDrop(player, new InventoryItemData(0, Item.ItemId, Value, Item.Data))) return;
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.SuccDrop, itemInfo.Name), 3000);
                }
                else
                {
                    locationName = null;

                    if (ArrayName == "other" || ArrayName == "backpack" || ArrayName == "trade") locationName = $"char_{characterData.UUID}";
                    else if (sessionData.InventoryOtherLocationName != null) locationName = sessionData.InventoryOtherLocationName;
                    else if (sessionData.InventoryOtherLocationName == null && sessionData.ItemsTrade != null) locationName = $"char_{characterData.UUID}";
                    else if (sessionData.InventoryOtherLocationName == null && isBackpackItemsData(player) != 0) locationName = $"backpack_{isBackpackItemsData(player)}";

                    if (locationName == null) return;

                    Location = null;

                    if (ArrayName == "other" || ArrayName == "backpack" || ArrayName == "trade") Location = "inventory";
                    else if (sessionData.InventoryOtherLocationName != null) Location = locationName.Split('_')[0];
                    else if (sessionData.InventoryOtherLocationName == null && sessionData.ItemsTrade != null) Location = "trade";
                    else if (sessionData.InventoryOtherLocationName == null && isBackpackItemsData(player) != 0) Location = "backpack";
                    if (Location == null) return;

                    if (Location == "tent")
                    {
                        if (sessionData.TentIndex == -1) return;
                       
                        sessionData.InventoryTentData = new InventoryTentData
                        {
                            ArrayName = ArrayName,
                            Index = Index,
                            Value = Value
                        };
                        Trigger.ClientEvent(player, "openInput", LangFunc.GetText(LangType.En, DataName.ItemSell), LangFunc.GetText(LangType.En, DataName.ItemSellInput), 8, "sell_tent");
                        return;
                    }
                    else if (Location == "inventory" && isFreeSlots(player, Item.ItemId, Value) != 0) return;

                    var itemPrice = Item.Price;
                    Item.Price = 0;

                    if (Value == Item.Count)
                    {
                        if (!InventoryMaxSlots.ContainsKey(Location) || AddItem(player, locationName, Location, Item, MaxSlots: GetMaxSlots(player, Location)) == -1)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                            return;
                        }
                        SetItemData(player, ArrayName, Index, new InventoryItemData(), true);
                        if (isBuy) EventSys.SendCoolMsg(player,"Рынок", "Покупка предмета", LangFunc.GetText(LangType.En, DataName.YouBuy, itemInfo.Name, itemPrice), "", 5000);
                            //Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouBuy, itemInfo.Name), 3000);
                        else if (ArrayName != "other" && ArrayName != "backpack" && ArrayName != "trade") Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouSuccGive, itemInfo.Name), 3000);
                        else Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetItem, itemInfo.Name), 3000);
                        return;
                    }

                    if (AddNewItem(player, locationName, Location, Item.ItemId, Value, Item.Data, MaxSlots: GetMaxSlots(player, Location)) == -1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NoSpaceInventory), 3000);
                        return;
                    }
                    if (isBuy) EventSys.SendCoolMsg(player,"Рынок", "Покупка предмета", LangFunc.GetText(LangType.En, DataName.YouBuy, itemInfo.Name, itemPrice), "", 5000);
                        //Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouBuy, itemInfo.Name), 3000);
                    else if (ArrayName != "other" && ArrayName != "backpack" && ArrayName != "trade") Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouSuccGive, itemInfo.Name), 3000);
                    else Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.YouGetItem, itemInfo.Name), 3000);
                }
                RemoveIndex(player, ArrayName, Index, Value);
            }
            catch (Exception e)
            {
                Log.Write($"ItemStack Exception: {e.ToString()}");
            }
        }

        public static void ItemsToput(ExtPlayer player, string ArrayName, int Index)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                InventoryItemData Item = GetItemData(player, ArrayName, Index);
                if (Item.ItemId == ItemId.Debug) return;
                ItemsInfo ItemInfo = ItemsInfo[Item.ItemId];
                Trigger.ClientEvent(player, "client.inventory.objecteditor", ItemInfo.Model, ArrayName, Index);
            }
            catch (Exception e)
            {
                Log.Write($"ItemsToput Exception: {e.ToString()}");
            }
        }

        #endregion



        #region Донат сапер
        [RemoteEvent("server.sapper.bet")]
        public static void SappeGame(ExtPlayer player, int amount)
        {
            try
            {
                var accountData = player.GetAccountData();
                if (accountData == null) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                if (sessionData.SappeData != -1) return;
                else if (accountData.RedBucks < amount)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.En, DataName.NetRB), 3000);
                    return;
                }
                UpdateData.RedBucks(player, -amount, msg: "Мини - игра «Сапёр»");
                sessionData.SappeData = amount;
                Trigger.ClientEvent(player, "client.sapper.game", amount);

            }
            catch (Exception e)
            {
                Log.Write($"SappeGame Exception: {e.ToString()}");
            }
        }
        private static List<float> SappeCoef = new List<float>()
        {
            1.1f,
            1.2f,
            1.4f,
            1.6f,
            2.0f,
            2.5f,
            3.0f,
        };

        [RemoteEvent("server.sapper.end")]
        public static void SappeEnd(ExtPlayer player, int type)
        {
            var accountData = player.GetAccountData();
            if (accountData == null) return;
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;
            BattlePass.Repository.UpdateReward(player, 3);
            try
            {
                if (sessionData.SappeData == -1) return;
                if (type != -2)
                {
                    if (type <= 0 || type > SappeCoef.Count) UpdateData.RedBucks(player, sessionData.SappeData, msg: LangFunc.GetText(LangType.En, DataName.SapperGame));
                    else 
                    {
                        int winrb = Convert.ToInt32(sessionData.SappeData * SappeCoef[type - 1]);
                        UpdateData.RedBucks(player, winrb, msg: LangFunc.GetText(LangType.En, DataName.SapperGameWin));
                        Players.Phone.Messages.Repository.AddSystemMessage(player, (int)DefaultNumber.RedAge, LangFunc.GetText(LangType.En, DataName.SappWin, winrb), DateTime.Now);
                    }
                }
                sessionData.SappeData = -1;
            }
            catch (Exception e)
            {
                sessionData.SappeData = -1;
                Log.Write($"SappeEnd Exception: {e.ToString()}");
            }
        }

        [ServerEvent(Event.PlayerDeath)]
        public static void OnPlayerDeath(ExtPlayer player, ExtPlayer killer, uint reason)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                else if (sessionData.SappeData == -1) return;
                SappeEnd(player, -1);
            }
            catch (Exception e)
            {
                Log.Write($"OnPlayerDeath Exception: {e.ToString()}");
            }
        }
        public static void Sappe_PlayerDisconnected(ExtPlayer player)
        {
            try
            {
                var accountData = player.GetAccountData();
                if (accountData == null) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                if (sessionData.SappeData != -1)
                {
                    UpdateData.RedBucks(player, sessionData.SappeData, msg: LangFunc.GetText(LangType.En, DataName.SapperGameGetBack));
                    sessionData.SappeData = -1;
                }
            }
            catch (Exception e)
            {
                Log.Write($"Event_PlayerDisconnected Exception: {e.ToString()}");
            }
        }
        #endregion
    }
}
