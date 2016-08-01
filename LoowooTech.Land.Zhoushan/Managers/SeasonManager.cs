﻿using LoowooTech.Land.Zhoushan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoowooTech.Land.Zhoushan.Managers
{
    public class SeasonManager:ManagerBase
    {
        public int SaveSeason(Season season)
        {

            using (var db = GetDbContext())
            {
                if (season.ID == 0)
                {
                    db.Seasons.Add(season);
                }
                else
                {
                    var entry = db.Seasons.FirstOrDefault(e => e.ID == season.ID);
                    if (entry != null)
                    {
                        db.Entry(entry).CurrentValues.SetValues(season);
                    }
                }
                db.SaveChanges();
                return season.ID;
            }
        }

        public List<Season> GetSeasons()
        {
            using (var db = GetDbContext())
            {
                return db.Seasons.Where(e=>e.Delete==false).ToList();
            }
        }
        public Season GetSeason(int id)
        {
            if (id == 0) return null;
            using (var db = GetDbContext())
            {
                return db.Seasons.FirstOrDefault(e => e.ID == id);
            }
        }

        public bool Delete(int id)
        {
            if (id == 0) return false;
            using (var db = GetDbContext())
            {
                var entry = db.Seasons.FirstOrDefault(e => e.ID == id);
                if (entry == null || entry.Delete)
                {
                    return false;
                }
                entry.Delete = true;
                db.SaveChanges();
                return true;
            }
        }

    }
}