using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Chars;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Localization;

namespace NeptuneEvo.World.PayDayBonus
{
    public class Repository : Script
    {
        
        private static int MinTime = 30;

        private static List<ExtPlayer> AntiAfkPlayers = new List<ExtPlayer>();
        
        public static void AddBonus(ExtPlayer player)
        {
            if (!FunctionsAccess.IsWorking("PayDayBonus"))
                return;
            
            if (DateTime.Now.Hour >= 0 && DateTime.Now.Hour < 12)
                return;
            
            var sessionData = player.GetSessionData();
            if (sessionData == null) 
                return;
            
            var characterData = player.GetCharacterData();
            if (characterData == null) 
                return;

            var afkData = sessionData.AfkData;
            
            if (afkData.IsAfk)
            {
                var inAFK = DateTime.Now - afkData.Time;
                afkData.PayDayMinute += inAFK.Minutes + 1;   
            }
            
            if (characterData.LastHourMin > MinTime && MinTime > afkData.PayDayMinute)
                AntiAfkPlayers.Add(player);
            
            afkData.PayDayMinute = 0;
        }
        
        public static void Bonus()
        {
            var players = AntiAfkPlayers.ToList();
            
            AntiAfkPlayers.Clear();

            if (players.Count > 0)
            {
                var rand = new Random();
                var winnersPlayer = new List<ExtPlayer>();

                for (int i = 0; i < Main.ServerSettings.NumberWinners; i++)
                {
                    
                    var index = rand.Next(0, players.Count - 1);
                    var winPlayer = players[index];
                    winnersPlayer.Add(winPlayer);
                    players.Remove(winPlayer);
                    
                    if (players.Count == 0)
                        break;
                }
                
                if (winnersPlayer.Count == 0)
                    return;
                
                var winersName = "";
                foreach (var foreachPlayer in winnersPlayer)
                {
                    var foreachSessionData = foreachPlayer.GetSessionData();
                    if (foreachSessionData == null) 
                        return;
                
                    UpdateData.RedBucks(foreachPlayer, Main.DonateSettings.HappyHoursRB, msg: LangFunc.GetText(LangType.En, DataName.HappyHours));
                    Trigger.ClientEvent(foreachPlayer, "hud.info", LangFunc.GetText(LangType.En, DataName.LuckOnYourSide), LangFunc.GetText(LangType.En, DataName.CongratsOnWinning, Main.DonateSettings.HappyHoursRB), LangFunc.GetText(LangType.En, DataName.HappyHours), "https://cloud.redage.net/cloud/img/time.png");

                    winersName += $" {foreachSessionData.Name}";
                }

                NAPI.Chat.SendChatMessageToAll($"~o~[HAPPY HOURS] A draw for {Main.DonateSettings.HappyHoursRB} RedBucks is held every hour, exclusively for active players.");
                NAPI.Chat.SendChatMessageToAll($"~o~[HAPPY HOURS] This hour's winners are: {winersName}. They each receive {Main.DonateSettings.HappyHoursRB}, congratulations!");
            }
        }

        /*[Command("testbonus")]
        public static void CMD_AllMedias(ExtPlayer player)
        {
            foreach (ExtPlayer foreachPlayer in Character.Repository.GetPlayers())
            {
                if (!foreachPlayer.IsCharacterData()) continue;
                
                World.PayDayBonus.Repository.AddBonus(foreachPlayer);
            }

            Bonus();
        }*/
    }
}