using System;
using System.Collections.Generic;
using UnityEngine;

namespace Qingxi
{
    public readonly struct ClueRecord
    {
        public readonly int Area;
        public readonly bool IsKey;
        public readonly string Text;

        public ClueRecord(int area, bool isKey, string text)
        {
            Area = area;
            IsKey = isKey;
            Text = text;
        }
    }

    public sealed class AreaDefinition
    {
        public string Name { get; }
        public string ShortName { get; }
        public int DeepCost { get; }
        public string SurfaceClue { get; }
        public string DeepClue { get; }
        public Color Color { get; }

        public AreaDefinition(
            string name,
            string shortName,
            int deepCost,
            string surfaceClue,
            string deepClue,
            Color color)
        {
            Name = name;
            ShortName = shortName;
            DeepCost = deepCost;
            SurfaceClue = surfaceClue;
            DeepClue = deepClue;
            Color = color;
        }
    }

    public sealed class SuspectDefinition
    {
        public string Label { get; }

        public SuspectDefinition(string label)
        {
            Label = label;
        }
    }

    public sealed class CaseDefinition
    {
        public string ChapterName { get; }
        public string PlaceName { get; }
        public string MapCaption { get; }
        public string IntroBody { get; }
        public string SuccessText { get; }
        public string FailText { get; }
        public string ClueHint { get; }
        public int TruthIndex { get; }
        public AreaDefinition[] Areas { get; }
        public SuspectDefinition[] Suspects { get; }

        public CaseDefinition(
            string chapterName,
            string placeName,
            string mapCaption,
            string introBody,
            string successText,
            string failText,
            string clueHint,
            int truthIndex,
            AreaDefinition[] areas,
            SuspectDefinition[] suspects)
        {
            ChapterName = chapterName;
            PlaceName = placeName;
            MapCaption = mapCaption;
            IntroBody = introBody;
            SuccessText = successText;
            FailText = failText;
            ClueHint = clueHint;
            TruthIndex = truthIndex;
            Areas = areas;
            Suspects = suspects;
        }
    }

    public static class QingxiCases
    {
        public const int StartingPoints = 3;

        public static readonly CaseDefinition[] All =
        {
            RiverCase(),
            ReservoirCase(),
            CreekCase(),
        };

        public static int Count
        {
            get { return All.Length; }
        }

        public static CaseDefinition Get(int index)
        {
            return All[index];
        }

        static AreaDefinition Area(
            string name,
            string shortName,
            string surfaceClue,
            string deepClue,
            Color color)
        {
            return new AreaDefinition(name, shortName, 1, surfaceClue, deepClue, color);
        }

        static CaseDefinition RiverCase()
        {
            return new CaseDefinition(
                "河流浊水",
                "清溪小镇",
                "清溪小镇  ·  河水自北向南",
                "清溪小镇河流水质恶化，你是环保专员，携带 3 点调查权限，找出污染主因。",
                "你准确锁定了主要污染源，后续治理工作顺利推进，清溪逐步恢复了清澈。",
                "误判了主要污染源，治理效果不佳。建议重点核查上游排污相关的线索。",
                "河流深查显示污染物以有机废水为主，塑料占比很低；鱼塘死亡源于上游来水中的毒素；养殖场有夜间偷排记录，废水处理设施已停运多月；回收站防渗层完好；居民证实深夜臭味来自养殖场方向。主要污染源是养殖场违规排污。",
                0,
                new[]
                {
                    Area("河流区域", "河流",
                        "河水浑浊，水面混杂着塑料碎屑和不明油渍",
                        "水质检测显示污染物以有机废水为主，塑料占比很低",
                        new Color(0.455f, 0.690f, 0.745f)),
                    Area("养殖场区域", "养殖场",
                        "养殖规模很大，排污口直通下游河流",
                        "查到夜间偷排记录，废水处理设施已停运多月",
                        new Color(0.780f, 0.655f, 0.420f)),
                    Area("鱼塘区域", "鱼塘",
                        "鱼群出现死亡个体，养殖密度看起来偏高",
                        "鱼体检测显示中毒特征，毒素源头来自上游来水",
                        new Color(0.478f, 0.710f, 0.580f)),
                    Area("回收站区域", "回收站",
                        "岸边堆着不少废弃塑料，看起来有渗漏风险",
                        "防渗层完好无损，近期没有大量塑料渗漏的痕迹",
                        new Color(0.620f, 0.655f, 0.635f)),
                    Area("居民社区", "居民社区",
                        "居民都抱怨最近河水味道大",
                        "多位居民证实，深夜养殖场方向飘来的臭味最明显",
                        new Color(0.800f, 0.580f, 0.490f)),
                },
                new[]
                {
                    new SuspectDefinition("养殖场违规排污"),
                    new SuspectDefinition("回收站塑料渗漏"),
                    new SuspectDefinition("鱼塘养殖污染"),
                });
        }

        static CaseDefinition ReservoirCase()
        {
            return new CaseDefinition(
                "水库藻华",
                "清溪水库",
                "清溪水库  ·  西侧支流汇入库区",
                "清溪水库是小镇饮用水源，近日水面泛绿。你是环保专员，携带 3 点调查权限，找出藻华主因。",
                "你准确锁定了主要污染源，溢流被截住，水库藻华开始消退。",
                "误判了主要污染源，藻华仍在扩散。建议重点核查西侧支流的高浓度来水。",
                "库区深查是氮磷引发的蓝藻，油类占比很低；网箱今年投喂量很小；餐馆排污已进市政管网；取水社区的异味从西侧支流涨水那天开始。化肥厂事故池在雨后溢流，高浓度氮磷废水进入支流。主要污染源是化肥厂雨后溢流。",
                1,
                new[]
                {
                    Area("库区水面", "库区",
                        "水面漂着大片绿色浮膜，还夹着少量油光",
                        "检测为蓝藻暴发，营养盐以氮磷为主，油类含量极低",
                        new Color(0.420f, 0.710f, 0.670f)),
                    Area("网箱养殖", "网箱",
                        "库湾停着成片网箱，饲料袋堆在岸边",
                        "今年投喂量远低于往年，氮磷贡献很小，不是这次藻华的主因",
                        new Color(0.520f, 0.700f, 0.500f)),
                    Area("化肥厂", "化肥厂",
                        "西侧支流边的化肥厂气味刺鼻，厂墙外有湿漉漉的水痕",
                        "雨后事故池溢流，高浓度氮磷废水汇入西侧支流",
                        new Color(0.800f, 0.720f, 0.400f)),
                    Area("湖滨餐馆", "餐馆",
                        "餐馆排污管伸向库湾，洗碗水味道很重",
                        "排污已接入市政管网，近周抽查没有餐厨废水特征",
                        new Color(0.820f, 0.580f, 0.520f)),
                    Area("取水社区", "取水口",
                        "居民说自来水发绿，煮开后仍有土腥味",
                        "多户确认异味从西侧支流涨水那天开始，和化肥厂方向的来水一致",
                        new Color(0.640f, 0.620f, 0.760f)),
                },
                new[]
                {
                    new SuspectDefinition("库湾网箱养殖"),
                    new SuspectDefinition("化肥厂雨后溢流"),
                    new SuspectDefinition("湖滨餐馆直排"),
                });
        }

        static CaseDefinition CreekCase()
        {
            return new CaseDefinition(
                "山溪赤浊",
                "清溪山溪",
                "清溪山溪  ·  留意东侧山腰的来水",
                "清溪上游山溪突然变得赤黄，下游灌溉用水不能再用。你是环保专员，携带 3 点调查权限，找出赤浊主因。",
                "你准确锁定了主要污染源，酸性涌水被拦住，山溪和灌溉渠逐渐变清。",
                "误判了主要污染源，赤浊仍在下泄。建议重点核查东侧山腰的锈色来水。",
                "山溪深查显示铁锰和酸度严重超标，是酸性矿水，泡沫不是主因；农家乐排水量很小；采石场沉淀池出水清澈；灌区农户看见赤浊从东侧矿硐方向冲下。矿硐封堵墙雨后坍塌，积水涌入山溪。主要污染源是废弃矿硐酸性涌水。",
                2,
                new[]
                {
                    Area("山溪河道", "山溪",
                        "溪水赤黄，石头上挂着锈色，水面还有白色泡沫",
                        "铁锰和酸度严重超标，属于酸性矿水，泡沫解释不了整条溪的赤浊",
                        new Color(0.820f, 0.520f, 0.400f)),
                    Area("溪边农家乐", "农家乐",
                        "新开的农家乐把洗涤泡沫排进溪沟",
                        "排水量很小，主要是表面活性剂，对不上整条溪的酸度和铁锰",
                        new Color(0.790f, 0.670f, 0.450f)),
                    Area("采石场", "采石场",
                        "山坡采石场扬尘很大，泥浆沟直通溪沟",
                        "泥浆已导入沉淀池，出水清澈，不是这次赤浊的主因",
                        new Color(0.660f, 0.680f, 0.640f)),
                    Area("废弃矿硐", "矿硐",
                        "东侧山腰有处封闭多年的矿硐，硐口下方岩层被染成锈红色",
                        "封堵墙在雨后坍塌，硐内积水涌入山溪",
                        new Color(0.720f, 0.450f, 0.400f)),
                    Area("灌区农户", "灌区",
                        "农户说稻田进水后禾苗发黄，怀疑有人往渠里投了东西",
                        "多户看见赤浊从东侧矿硐方向冲下来，灌溉渠同一天变色",
                        new Color(0.520f, 0.700f, 0.480f)),
                },
                new[]
                {
                    new SuspectDefinition("溪边农家乐洗涤剂"),
                    new SuspectDefinition("采石场泥浆直排"),
                    new SuspectDefinition("废弃矿硐酸性涌水"),
                });
        }
    }

    public sealed class AreaProgress
    {
        public bool SurfaceDone;
        public bool DeepDone;
    }

    public sealed class InvestigationSession
    {
        readonly List<ClueRecord> clues = new List<ClueRecord>();
        CaseDefinition current;
        AreaProgress[] progress = new AreaProgress[0];

        public int Points { get; private set; }
        public IReadOnlyList<ClueRecord> Clues => clues;
        public CaseDefinition Case => current;

        public InvestigationSession(CaseDefinition definition)
        {
            Bind(definition);
        }

        public void Bind(CaseDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            current = definition;
            Points = QingxiCases.StartingPoints;
            clues.Clear();
            progress = new AreaProgress[definition.Areas.Length];
            for (int i = 0; i < progress.Length; i++)
                progress[i] = new AreaProgress();
        }

        public AreaProgress GetProgress(int area)
        {
            return progress[area];
        }

        public bool TrySurface(int area, out string error)
        {
            error = null;
            AreaProgress state = progress[area];
            if (state.SurfaceDone)
            {
                error = "已经观察过这里";
                return false;
            }

            state.SurfaceDone = true;
            clues.Add(new ClueRecord(area, false, current.Areas[area].SurfaceClue));
            return true;
        }

        public bool TryDeep(int area, out string error)
        {
            error = null;
            AreaProgress state = progress[area];
            if (state.DeepDone)
            {
                error = "已经深入核查过这里";
                return false;
            }

            AreaDefinition def = current.Areas[area];
            if (Points < def.DeepCost)
            {
                error = "调查点数不足";
                return false;
            }

            Points -= def.DeepCost;
            state.DeepDone = true;
            clues.Add(new ClueRecord(area, true, def.DeepClue));
            return true;
        }

        public bool IsCorrect(int suspect)
        {
            return suspect == current.TruthIndex;
        }
    }
}
