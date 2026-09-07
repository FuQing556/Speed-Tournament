using UnityEngine;

namespace SpeedTournament
{
    // Authored station data, shared by geometry, gates, dressing, AI and map selection.
    // No runtime random geometry. Race records are scoped to the course layout.
    public sealed class CircuitDefinition
    {
        public string Name, Description, RecordKey;
        public Vector3[] Anchors;
        public float[] Entries, Returns, Heights;
        public int[] EntryLanes, Widths;
        public bool Elevated;
        public float MergeSpan => Elevated ? 0.045f : 0.035f;
    }

    public static class CircuitCatalog
    {
        public static readonly CircuitDefinition[] Maps = {
            new CircuitDefinition {
                Name="星环观测站", RecordKey="SpeedTournament.Best.ObservatoryV3",
                Description="经典赛道 · 3 圈\n主道补给 / 内线避障 · 三处单道捷径\n适合熟悉角色、练习切轨与能量循环。",
                Entries=new[]{.105f,.425f,.745f}, Returns=new[]{.265f,.585f,.885f},
                EntryLanes=new[]{0,0,0}, Widths=new[]{1,1,1}, Heights=new[]{0f,0f,0f},
                Anchors=new[]{new Vector3(0,10,-250),new Vector3(155,10,-250),new Vector3(252,13,-170),new Vector3(275,18,-25),
                    new Vector3(240,24,125),new Vector3(125,27,220),new Vector3(-30,25,245),new Vector3(-172,19,200),
                    new Vector3(-257,13,95),new Vector3(-266,10,-42),new Vector3(-218,10,-166),new Vector3(-118,10,-237)}
            },
            new CircuitDefinition {
                Name="云端立交", RecordKey="SpeedTournament.Best.SkyInterchangeV1", Elevated=true,
                Description="进阶赛道 · 3 圈\n四处分流：左 2 / 右 3 / 左 4 / 右 4 道\n上层桥、下潜线、密集补给与炸弹区；橙色箭头通往支线。",
                Entries=new[]{.080f,.300f,.530f,.760f}, Returns=new[]{.220f,.460f,.690f,.920f},
                EntryLanes=new[]{0,3,0,2}, Widths=new[]{2,3,4,4}, Heights=new[]{22f,-12f,28f,18f},
                Anchors=new[]{new Vector3(0,18,-305),new Vector3(186,18,-300),new Vector3(302,27,-205),new Vector3(330,44,-30),
                    new Vector3(288,58,150),new Vector3(150,50,264),new Vector3(-36,30,294),new Vector3(-206,17,240),
                    new Vector3(-308,24,114),new Vector3(-319,43,-50),new Vector3(-261,38,-199),new Vector3(-142,22,-284)}
            }
        };
    }
}
