using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using GTANetworkAPI;
using LinqToDB;
using Newtonsoft.Json;

namespace NeptuneEvo.World.War.Models
{
    public class WarData
    {
        public ushort Id;
        public ushort ObjectId;
        public WarType Type;
        public string MapName;
        public ushort MapId;
        public Vector3 Position; //Battle location
        public float Range; //Battle area range
        public ushort AttackingId = 0;
        public ushort ProtectingId = 0;
        public ushort AttackingCount = 0;
        public ushort ProtectingCount = 0;
        public bool IsStartWar = false;
        //
        public WarGripType GripType; //Battle type
        public sbyte Composition = 0;
        public sbyte AttackingPlayersInZone = 0; //Number of attacking players in the zone
        public sbyte ProtectingPlayersInZone = 0; //Number of defending players in the zone
        public sbyte AttackingPlayersInZoneCount = 0; //Count of attacking players in the zone
        public sbyte ProtectingPlayersInZoneCount = 0; //Count of defending players in the zone
        public sbyte WeaponsCategory = 0; //Weapon type
        public DateTime Time;
        //
        public List<int> RetiredUuId = new List<int>();
        //
        public WarStatus Status;
        public ushort Counting;
        
        public uint GetDimension() =>
            (uint) Id + War.Repository.DefaultDimension;

        public void Insert()
        {
            
            Trigger.SetTask(async () =>
            {
                try
                {
                    await using var db = new ServerBD("MainDB"); //In a separate thread

                    await db.InsertAsync(new Wars()
                    {
                        Id = Convert.ToInt16(this.Id),
                        ObjectId = Convert.ToInt16(this.ObjectId),
                        Type = Convert.ToSByte(this.Type),
                        AttackingId = Convert.ToInt16(this.AttackingId),
                        ProtectingId = Convert.ToInt16(this.ProtectingId),
                        MapName = this.MapName,
                        MapId = Convert.ToInt16(this.MapId),
                        Position = JsonConvert.SerializeObject(this.Position),
                        Range = this.Range,
                        GripType = Convert.ToSByte(this.GripType),
                        Composition = Convert.ToSByte(this.Composition),
                        WeaponsCategory = Convert.ToSByte(this.WeaponsCategory),
                        Time = this.Time,
                    });
                }
                catch (Exception e)
                {
                    Debugs.Repository.Exception(e);
                }
            });
        }  
        
        public void Update()
        {
            
            Trigger.SetTask(async () =>
            {
                try
                {
                    await using var db = new ServerBD("MainDB"); //In a separate thread
                    
                    await db.Wars
                        .Where(w => w.Id == this.Id)
                        .Set(w => w.AttackingId, Convert.ToInt16(this.AttackingId))
                        .UpdateAsync();
                }
                catch (Exception e)
                {
                    Debugs.Repository.Exception(e);
                }
            });
        } 
        
        public void Delete()
        {
            
            Trigger.SetTask(async () =>
            {
                try
                {
                    await using var db = new ServerBD("MainDB"); //In a separate thread

                    await db.Wars
                        .DeleteAsync(w => w.Id == this.Id);
                }
                catch (Exception e)
                {
                    Debugs.Repository.Exception(e);
                }
            });
        }
        
    }
}