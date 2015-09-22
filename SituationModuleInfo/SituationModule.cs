﻿using System;
using System.Collections.Generic;
using System.Linq;
using KspHelper.Behavior;
using UnityEngine;
using System.Reflection;
using System.IO;


namespace SituationModuleInfo
{
    class Item
    {
        public Item()
        {
            ExperimentTitles = new string[1] { string.Empty };
        }
        public string ModuleName { get; set; }

        public string Biome { get; set; }

        public string Situation { get; set; }

        public string[] ExperimentTitles { get; set; }
    }

    // указываем что аддон активируется 1 раз при входе в любой едитор (VAB, SPH)
    [KSPAddon(KSPAddon.Startup.EditorAny, true)]
    public class SituationModule : KspBehavior
    {
        private const string DEFAULT_EXPERIMENT_CONT_TITLE = "Science Experiment";
        // создаем эталлонный список ситуаций, который будем менять 
        private readonly string[] _etalonSituationMask = new string[6]  
        {
            "Flying High: <b><color=red>X</color></b>",
            "Flying Low: <b><color=red>X</color></b>",
            "In Space High: <b><color=red>X</color></b>",
            "In Space Low: <b><color=red>X</color></b>",
            "Landed: <b><color=red>X</color></b>",
            "Splashed: <b><color=red>X</color></b>"
        };

        private string usageMaskInt = "";

        // создаем тип лист для списка партов
        private List<AvailablePart> _partsWithScience = new List<AvailablePart>();

        private ConfigNode _config;

        private List<Item> _items = new List<Item>();

        protected override void Start()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var directoryPath = Path.GetDirectoryName(assemblyPath);
            _config = ConfigNode.Load(Path.Combine(directoryPath, "config.cfg"));
            LoadConfig();
            // переопределяем стандартный метод запускающийся при старте (добавляем свой действия)
            SituationMaskAnalys();
        }

        private void LoadConfig()  // загружаем ноду из конфига
        {
            var nodes = _config.GetNodes("ITEM");
            foreach (var node in nodes)
            {
                var item = new Item();
                item.ModuleName = node.GetValue("moduleName");
                item.Biome = node.GetValue("biome");
                item.Situation = node.GetValue("situation");
                var experimentTitles = node.GetValue("experimentTitles");
                //Debug.LogWarning(experimentTitles);
                item.ExperimentTitles = experimentTitles.Split(',').Select(x => x.Trim()).ToArray();
                foreach (var data in item.ExperimentTitles)
                {
                    //Debug.LogWarning(data);
                }
                _items.Add(item);
            }
        }

        private void SituationMaskAnalys()
        {
            //получаем в ранее созданный объект типа список перечень партов, имеющих ModuleScienceExperiment или модуль с этим родителем 
            _partsWithScience = PartLoader.LoadedPartsList.Where(p => p.partPrefab.Modules.GetModules<ModuleScienceExperiment>().Any()).ToList();
            //цикл. перебираем все элементы в полученном ранее списке

            foreach (var part in _partsWithScience)
            {
                // получаем в список все нужные модули (ModuleScienceExperiment и детки) в рамках парта
                var modules = part.partPrefab.Modules.GetModules<ModuleScienceExperiment>();

                //цикл. перебираем полученные модули. этот цикл имеет смысл если модулей больше одного 
                foreach (var moduleScienceExperiment in modules)
                {
                    //получаем каждый эксперимент
                    ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(moduleScienceExperiment.experimentID) ?? new ScienceExperiment();

                    var moduleNode = _items.FirstOrDefault(x => x.ModuleName.Equals(moduleScienceExperiment.name, StringComparison.InvariantCultureIgnoreCase));
                    if (moduleNode != null)
                    {
                        Type t = moduleScienceExperiment.GetType();
                        var field = t.GetField(moduleNode.Biome);
                        if (field != null)
                        {
                            var value = field.GetValue(moduleScienceExperiment);
                            experiment.biomeMask = (uint)value;
                        }
                        else
                        {
                            var property = t.GetProperty(moduleNode.Biome);
                            if (property != null)
                            {
                                var value = property.GetValue(moduleScienceExperiment, null);
                                experiment.biomeMask = (uint)value;
                            }
                        }

                        field = t.GetField(moduleNode.Situation);
                        if (field != null)
                        {
                            var value = field.GetValue(moduleScienceExperiment);
                            experiment.situationMask = (uint)value;
                        }
                        else
                        {
                            var property = t.GetProperty(moduleNode.Situation);
                            if (property != null)
                            {
                                var value = property.GetValue(moduleScienceExperiment, null);
                                experiment.situationMask = (uint)value;
                            }
                        }
                    }

                    // для DMagic
                    //            var testModule = moduleScienceExperiment as DMModuleScienceAnimate; //безопасное приведение типов, идем от парента к наследникам
                    //            if (testModule != null)
                    //            {
                    //                experiment.biomeMask = (uint)testModule.bioMask;
                    //                experiment.situationMask = (uint)testModule.sitMask;
                    //            }

                    //создаем новую строку которую потом будем ложить в блок инфы в ЮИ парта в ВАБ
                    var itemInfo = new string[6];
                    Array.Copy(_etalonSituationMask, itemInfo, 6);

                    if ((experiment.situationMask & (uint)ExperimentSituations.FlyingHigh) ==
                        (uint)ExperimentSituations.FlyingHigh)
                    {
                        itemInfo[0] = "Flying High: <b><color=green>V</color></b>";
                    }

                    if ((experiment.biomeMask & (uint)ExperimentSituations.FlyingHigh) ==
                    (uint)ExperimentSituations.FlyingHigh)
                    {
                        itemInfo[0] = "Flying High: <b><color=green>V</color> Biome Depending</b>";
                    }

                    if ((experiment.situationMask & (uint)ExperimentSituations.FlyingLow) ==
                        (uint)ExperimentSituations.FlyingLow)
                    {
                        itemInfo[1] = "Flying Low: <b><color=green>V</color></b>";
                    }

                    if ((experiment.biomeMask & (uint)ExperimentSituations.FlyingLow) ==
                    (uint)ExperimentSituations.FlyingLow)
                    {
                        itemInfo[1] = "Flying Low: <b><color=green>V</color> Biome Depending</b>";
                    }

                    if ((experiment.situationMask & (uint)ExperimentSituations.InSpaceHigh) ==
                        (uint)ExperimentSituations.InSpaceHigh)
                    {
                        itemInfo[2] = "Space High: <b><color=green>V</color></b>";
                    }

                    if ((experiment.biomeMask & (uint)ExperimentSituations.InSpaceHigh) ==
                    (uint)ExperimentSituations.InSpaceHigh)
                    {
                        itemInfo[2] = "Space High: <b><color=green>V</color> Biome Depending</b>";
                    }

                    if ((experiment.situationMask & (uint)ExperimentSituations.InSpaceLow) ==
                        (uint)ExperimentSituations.InSpaceLow)
                    {
                        itemInfo[3] = "Space Low: <b><color=green>V</color></b>";
                    }

                    if ((experiment.biomeMask & (uint)ExperimentSituations.InSpaceLow) ==
                    (uint)ExperimentSituations.InSpaceLow)
                    {
                        itemInfo[3] = "Space Low: <b><color=green>V</color> Biome Depending</b>";
                    }

                    if ((experiment.situationMask & (uint)ExperimentSituations.SrfLanded) ==
                        (uint)ExperimentSituations.SrfLanded)
                    {
                        itemInfo[4] = "Landed: <b><color=green>V</color></b>";
                    }

                    if ((experiment.biomeMask & (uint)ExperimentSituations.SrfLanded) ==
                    (uint)ExperimentSituations.SrfLanded)
                    {
                        itemInfo[4] = "Landed: <b><color=green>V</color> Biome Depending</b>";
                    }

                    if ((experiment.situationMask & (uint)ExperimentSituations.SrfSplashed) ==
                        (uint)ExperimentSituations.SrfSplashed)
                    {
                        itemInfo[5] = "Splashed: <b><color=green>V</color></b>";
                    }

                    if ((experiment.biomeMask & (uint)ExperimentSituations.SrfSplashed) ==
                    (uint)ExperimentSituations.SrfSplashed)
                    {
                        itemInfo[5] = "Splashed: <b><color=green>V</color> Biome Depending</b>";
                    }

                    switch (moduleScienceExperiment.usageReqMaskInternal)
                    {
                        case -1:
                            usageMaskInt = "<i><color=red>Experiment can't be used at all.</color></i>";
                            break;
                        case 0:
                            usageMaskInt = "<i><color=maroon>Experiment can always be used.</color></i>";
                            break;
                        case 1:
                            usageMaskInt = "<i><color=green>Experiment can be used if vessel is under control.</color></i>";
                            break;
                        case 2:
                            usageMaskInt = "<i><color=navy>Experiment can only be used if vessel is crewed.</color></i>";
                            break;
                        case 4:
                            usageMaskInt = "<i><color=teal>Experiment can only be used if part contains crew.</color></i>";
                            break;
                        case 8:
                            usageMaskInt = "<i><color=purple>Experiment can only be used if crew is scientist.</color></i>";
                            break;
                    }
                    // ищем в форматировании инфы парта в ВАБ блок который нам нужен
                    try
                    {
                        // получаем имеющийся блок с нужным заголовком (Science Experiment)

                        //moduleNode = moduleNode ?? new Item();

                        var infos = part.moduleInfos.Where(
                            x =>
                                x.moduleName.Equals("Science Experiment", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMModule Science Animate", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMAnomaly Scanner", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMBio Drill", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMRover Goo Mat", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMSoil Moisture", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMSolar Collector", StringComparison.InvariantCultureIgnoreCase)
                                    || x.moduleName.Equals("DMXRay Diffract", StringComparison.InvariantCultureIgnoreCase)
                                    ).ToList();
                                    //|| x.moduleName.Equals(_items.ExperimentTitles, StringComparison.InvariantCultureIgnoreCase)
                                    //).ToList();
                        //var infos = part.moduleInfos.Where(x => x.moduleName.Equals(DEFAULT_EXPERIMENT_CONT_TITLE, StringComparison.InvariantCultureIgnoreCase) 
                        //    || moduleNode.ExperimentTitles.Any(m => m.Equals(x.moduleName, StringComparison.InvariantCultureIgnoreCase))).ToList();
                        
                        if (!infos.Any()) continue;
                        // получаем имеющуюся в блоке инфу
                        // if(condition) { do } else {do2} => condition ? true : false
                        var d = infos.FirstOrDefault(x => x.info.Contains(string.IsNullOrEmpty(moduleScienceExperiment.experimentActionName) ? moduleScienceExperiment.experimentID : moduleScienceExperiment.experimentActionName));
                        // для сложного типа и строки значение по умолчанию null
                        // если ничего нет - следующая итерация цикла
                        if (d == null) continue;
                        // склеиваем  и передаем  сформированную строку в метод подстановки
                        d.info = string.Concat(d.info, "\n", GetInfo(itemInfo, usageMaskInt));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        private string GetInfo(string[] moduleInfos, string usageMaskInt)
        {
            string data = "--------------------------------\n";
            //data = string.Concat(data, $"<b><color={XKCDColors.HexFormat.Cyan}>{moduleInfos[0]}</color></b>\n\n");
            //цикл.  подставляем готовые строки заканчивая каждую переводом строки
            foreach (string moduleInfo in moduleInfos)
            {
                data = string.Concat(data, moduleInfo + "\n");
            }
            data = string.Concat(data, "\n" + usageMaskInt.ToUpper() + "\n");
            return data;
        }
    }
}