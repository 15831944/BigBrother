﻿using LoowooTech.Land.Zhoushan.Caching;
using LoowooTech.Land.Zhoushan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoowooTech.Land.Zhoushan.Managers
{
    public class AreaManager : ManagerBase
    {
        private static readonly string AreaCacheKey = "areas";

        private void ClearCache()
        {
            Cache.Remove(AreaCacheKey);
        }

        private List<Area> GetList()
        {
            return Cache.GetOrSet(AreaCacheKey, () =>
            {
                using (var db = GetDbContext())
                {
                    var list = db.Areas.ToList();
                    foreach (var root in list.Where(e => e.ParentID == 0))
                    {
                        root.GetChildren(list);
                    }
                    return list;
                }
            });
        }

        public List<Area> GetAreas(int[] areaIds)
        {
            return GetList().Where(e => areaIds.Contains(e.ID)).ToList();
        }

        public List<Area> GetAreas(int? parentId = null)
        {
            IEnumerable<Area> query = GetList();
            if (parentId.HasValue)
            {
                query = query.Where(e => e.ParentID == parentId.Value);
            }
            return query.ToList();
        }

        public List<Area> GetAreaTree(int[] areaIds = null)
        {
            if (areaIds == null || areaIds.Length == 0)
            {
                return GetList().Where(e => e.ParentID == 0).ToList();
            }
            else
            {
                return GetList().Where(e => areaIds.Contains(e.ID)).ToList();
            }
        }

        public Area GetArea(int id)
        {
            if (id == 0) return null;
            return GetList().FirstOrDefault(e => e.ID == id);
        }

        public Area GetArea(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return GetList().FirstOrDefault(e => e.Name == name);
        }

        public void Save(Area model)
        {
            using (var db = GetDbContext())
            {
                if (model.ID > 0)
                {
                    var entity = db.Areas.FirstOrDefault(e => e.ID == model.ID);
                    if (model.ParentID == entity.ID)
                    {
                        model.ParentID = 0;
                    }
                    entity.Name = model.Name;
                }
                else
                {
                    db.Areas.Add(model);
                }
                db.SaveChanges();
                ClearCache();
            }
        }

        public void Delete(int id)
        {
            using (var db = GetDbContext())
            {
                var entity = db.Areas.FirstOrDefault(e => e.ID == id);
                db.Areas.Remove(entity);
                db.SaveChanges();
                ClearCache();
            }
        }

        internal int[] GetChildAreaIds(int parentId)
        {
            return GetAreas().Where(e => e.ParentID == parentId).Select(e => e.ID).ToArray();
        }
    }
}
