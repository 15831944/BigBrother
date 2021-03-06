﻿using LoowooTech.Land.Zhoushan.Caching;
using LoowooTech.Land.Zhoushan.Common;
using LoowooTech.Land.Zhoushan.Models;
using NPOI.HSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Caching;

namespace LoowooTech.Land.Zhoushan.Managers
{
    public class TemplateManager : ManagerBase
    {
        /// <summary>
        /// 把Template的Field中的Value数据写入数据库
        /// </summary>
        public void WriteExcelDataToDb(int year, Quarter quarter, int areaId, Template template)
        {
            using (var db = GetDbContext())
            {
                foreach (var field in template.Fields)
                {
                    if (!field.HasPrameter(FieldType.Value)) continue;
                    var parameter = field.GetNodeValueParameter(year, quarter, new[] { areaId });
                    if (parameter == null)
                    {
                        continue;
                    }
                    var entity = Core.FormManager.GetNodeValues(parameter).FirstOrDefault();
                    if (entity == null)
                    {
                        entity = new NodeValue();
                        field.SetEntity(entity, year, quarter, areaId);
                        db.NodeValues.Add(entity);
                    }
                    else
                    {
                        field.SetEntity(entity, year, quarter, areaId);
                        Core.FormManager.SaveNodeValue(entity);
                    }
                    db.SaveChanges();
                }
            }
        }

        /// <summary>
        /// 导出
        /// </summary>
        public List<ExcelCell> WriteDbDataToExcel(Form form, int year, Quarter quarter, int[] areaIds, Template template)
        {
            using (var db = GetDbContext())
            {
                var result = new List<ExcelCell>();
                var areas = Core.AreaManager.GetAreas();
                foreach (var field in template.Fields)
                {
                    if (field.Parameters.Count > 0)
                    {
                        if (field.HasPrameter(FieldType.Hidden))
                        {
                            field.Value = string.Empty;
                            result.Add(field.Cell);
                            continue;
                        }

                        var firstParameter = field.Parameters[0];
                        switch (firstParameter.Type)
                        {
                            case FieldType.Unit:
                                field.Value = "";
                                break;
                            case FieldType.Form:
                                form = form ?? Core.FormManager.GetForm(firstParameter.Value);
                                field.Value = form.Name;
                                break;
                            case FieldType.Area:
                                var area = Core.AreaManager.GetArea(firstParameter.Value);
                                field.Value = area == null ? "未知区域" : area.Name;
                                break;
                            case FieldType.Node:
                                var node = Core.FormManager.GetNode(firstParameter.Value);
                                field.Value = node == null ? "未知分类" : node.Name;
                                break;
                            case FieldType.Quarter:
                            case FieldType.Quarters:
                                //如果模板没有指定季度，则按照传参
                                if (firstParameter.Value == 0)
                                {
                                    field.Value = quarter.GetDescription();
                                }
                                else
                                {
                                    field.Value = ((Quarter)firstParameter.Value).GetDescription();
                                }
                                break;
                            case FieldType.Type:
                                {
                                    var valueType = Core.FormManager.GetNodeValueType(firstParameter.Value);
                                    field.Value = valueType == null ? "未知类型" : valueType.Name;
                                }
                                break;
                            case FieldType.Year:
                                field.Value = firstParameter.Value == 0 ? year : firstParameter.Value;
                                break;
                            case FieldType.LastYear:
                                field.Value = year - 1;
                                break;
                            case FieldType.Period:
                                field.Value = ((Period)firstParameter.Value).GetDescription();
                                break;
                            case FieldType.Value:
                            case FieldType.RateValue:
                                double value = 0;
                                List<NodeValue> values = null;
                                var parameter = field.GetNodeValueParameter(year, quarter, areaIds);
                                if (parameter == null)
                                {
                                    //没有权限导出该区域的数据
                                    field.Value = 0;
                                    break;
                                }
                                if (firstParameter.Type == FieldType.Value)
                                {
                                    values = Core.FormManager.GetNodeValues(parameter);
                                    value = values.Select(e => e.Value).DefaultIfEmpty(0).Sum();
                                }
                                else if (firstParameter.Type == FieldType.RateValue)
                                {
                                    //这里写死了是同比
                                    parameter.RateType = RateType.YearOnYear;
                                    values = Core.FormManager.GetNodeValues(parameter);
                                    var sumVal = values.Select(e => e.Value).DefaultIfEmpty(0).Sum();
                                    var comVal = values.Select(e => e.CompareValue).DefaultIfEmpty(0).Sum();

                                    value = MathHelper.GetRateValue(sumVal, comVal);
                                }
                                double ratio = 1;
                                if (field.HasPrameter(FieldType.Unit))
                                {
                                    var typeId = values.Count == 0 ? 1 : values.FirstOrDefault().TypeID;
                                    var index = field.Parameters.First(e => e.Type == FieldType.Unit).Value;
                                    var valueType = Core.FormManager.GetNodeValueType(typeId);
                                    ratio = 1.0 / (index == 0 ? 1 : (int)Math.Pow(valueType.Ratio, index));
                                }
                                if (firstParameter.Type == FieldType.Value)
                                {
                                    field.Value = double.Parse((value * ratio).ToString("f4"));
                                }
                                else
                                {
                                    field.Value = double.Parse((value * ratio).ToString("f2"));
                                }

                                break;
                        }
                    }
                    result.Add(field.Cell);
                }

                template.WriteData(result);
                return result;
            }
        }
    }

    internal static class FieldExtension
    {
        public static NodeValueParameter GetNodeValueParameter(this Field field, int year, Quarter quarter, int[] areaIds)
        {
            var result = new NodeValueParameter { GetArea = false, GetNode = true, GetValueType = false };
            foreach (var parameter in field.Parameters)
            {
                switch (parameter.Type)
                {
                    case FieldType.Area:
                        //如果当先用户限制了areaIds，但是并不包含模板中的该区域，则跳过此数据
                        if (areaIds != null && !areaIds.Contains(parameter.Value))
                        {
                            result = null;
                            return result;
                        }
                        else
                        {
                            result.AreaID = parameter.Value;
                        }
                        break;
                    case FieldType.Node:
                        result.NodeID = parameter.Value;
                        break;
                    case FieldType.Quarter:
                        if (parameter.Value > 0)
                        {
                            result.Quarter = (Quarter)parameter.Value;
                        }
                        else
                        {
                            result.Quarter = quarter;
                        }
                        break;
                    case FieldType.Quarters:
                        result.Quarter = quarter;
                        break;
                    case FieldType.Type:
                    case FieldType.Value:
                        result.TypeID = parameter.Value;
                        break;
                    case FieldType.Year:
                        if (parameter.Value > 0)
                        {
                            result.Year = parameter.Value;
                        }
                        else
                        {
                            result.Year = year;
                        }
                        break;
                    case FieldType.LastYear:
                        result.Year = year - 1;
                        break;
                    case FieldType.Rate:
                        result.RateType = (RateType)parameter.Value;
                        break;
                    case FieldType.Period:
                        result.Period = (Period)parameter.Value;
                        break;
                }
            }
            if (result.Year == 0 && (result.Years == null || result.Years.Length == 0))
            {
                result.Year = year;
            }
            if (result.AreaID == 0 && areaIds != null)
            {
                result.AreaIds = areaIds;
            }
            if (result.Quarter == 0)
            {
                result.Quarter = quarter;
            }
            return result;
        }

        public static void SetEntity(this Field field, NodeValue entity, int year, Quarter quarter, int areaId)
        {
            foreach (var parameter in field.Parameters)
            {
                switch (parameter.Type)
                {
                    case FieldType.Area:
                        entity.AreaID = parameter.Value;
                        break;
                    case FieldType.Node:
                        entity.NodeID = parameter.Value;
                        break;
                    case FieldType.Quarter:
                        entity.Quarter = (Quarter)parameter.Value;
                        break;
                    case FieldType.Type:
                    case FieldType.Value:
                        entity.TypeID = parameter.Value;
                        break;
                    case FieldType.Year:
                        entity.Year = parameter.Value;
                        break;
                }
            }

            if (entity.AreaID == 0) entity.AreaID = areaId;
            if (entity.Year == 0) entity.Year = year;
            if (entity.Quarter == 0) entity.Quarter = quarter;

            //如果导入的时候指定了单位的索引编号（亩,公顷，如果有{Unit=1}，则指定的导入的单位是公顷），则值需要乘以type.Ratio 即1000）
            var ratio = 1;

            if (field.HasPrameter(FieldType.Unit))
            {
                var index = field.Parameters.First(e => e.Type == FieldType.Unit).Value;
                var valueType = ManagerCore.Instance.FormManager.GetNodeValueType(entity.TypeID);
                ratio = (int)Math.Pow(valueType.Ratio, index);
            }

            double val = 0;
            if (field.Value != null)
            {
                if (!double.TryParse(field.Value.ToString(), out val))
                {

                }
            }
            entity.Value = val * ratio;
        }

    }
}
