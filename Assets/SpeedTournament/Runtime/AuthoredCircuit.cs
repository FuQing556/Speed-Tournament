using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        public sealed partial class TrackModel
        {
            public const int LaneCount = 6, RouteCount = 2;
            public const float LaneSpacing = 3.15f, LaneWidth = 3.02f;
            const int Samples = 1536;
            readonly Transform parent;
            readonly Vector3[][] points = { new Vector3[Samples + 1], new Vector3[Samples + 1] };
            readonly Vector3[][] tangents = { new Vector3[Samples + 1], new Vector3[Samples + 1] };
            readonly float[][] distances = { new float[Samples + 1], new float[Samples + 1] };
            readonly List<Branch> branches = new();
            readonly List<GameObject> sectors = new();
            readonly List<Vector3> sectorCenters = new();
            public float ApproxLength => distances[0][Samples];
            public int BranchCount => branches.Count;
            public readonly CircuitDefinition Definition;
            public float[] EntryStations => Definition.Entries;
            public float[] ReturnStations => Definition.Returns;
            public int SectionCount => EntryStations.Length;
            public float MergeSpan => Definition.MergeSpan;
            public Bounds MapBounds {get;private set;}
            sealed class Branch
            {
                public int From, To, EntryLane, ExitLane, Width;
                public float Start, End, Length;
                public Vector3[] Points = new Vector3[65];
                public Vector3[] Tangents = new Vector3[65];
            }
            public TrackModel(Transform root,int mapId=0)
            {
                Definition=CircuitCatalog.Maps[Mathf.Clamp(mapId,0,CircuitCatalog.Maps.Length-1)];
                parent = new GameObject("TRACK_"+Definition.Name).transform; parent.SetParent(root, false);
                // Arc-length resampling happens once at load. Runtime uses linear cache lookups.
                var dense = new Vector3[4097]; var cumulative = new float[dense.Length];
                for (int i=0;i<dense.Length;i++)
                {
                    dense[i]=Spline(i/4096f);
                    if(i>0) cumulative[i]=cumulative[i-1]+Vector3.Distance(dense[i-1],dense[i]);
                }
                for(int i=0;i<=Samples;i++) points[0][i]=AtDistance(dense,cumulative,cumulative[4096]*i/Samples);
                var baseline=(Vector3[])points[0].Clone();
                if(Definition.Elevated)
                for(int i=0;i<Samples;i++)
                {
                    float p=i/(float)Samples;
                    for(int s=0;s<SectionCount;s++)
                    {
                        float a=SectionStart(s),b=ReturnStations[s];if(p<a||p>b)continue;
                        float u=(p-a)/(b-a),bulge=Mathf.Sin(u*Mathf.PI);bulge*=bulge;
                        Vector3 forward=(baseline[(i+1)%Samples]-baseline[(i+Samples-1)%Samples]).normalized;
                        // Main-road dogleg绕行; upper/lower route takes the compact inside chord.
                        points[0][i]+=Vector3.Cross(Vector3.up,forward).normalized*(44f*bulge)+Vector3.up*(5f*bulge);
                    }
                }
                for(int i=0;i<Samples;i++)
                {
                    tangents[0][i]=(points[0][(i+1)%Samples]-points[0][(i+Samples-1)%Samples]).normalized;
                    if(Definition.Elevated)
                    {
                        // A scaled inner loop cannot develop offset-curve cusps on tight turns.
                        Vector3 p=baseline[i];float height=0,pct=i/(float)Samples;
                        for(int s=0;s<SectionCount;s++)
                            if(pct>=EntryStations[s]&&pct<=ReturnStations[s]+0.04f){height=Definition.Heights[s];break;}
                        points[1][i]=new Vector3(p.x*.77f,p.y+height,p.z*.77f);
                    }
                    else points[1][i]=points[0][i]-Vector3.Cross(Vector3.up,tangents[0][i]).normalized*38f;
                }
                points[1][Samples]=points[1][0]; tangents[0][Samples]=tangents[0][0];
                for(int i=0;i<Samples;i++) tangents[1][i]=(points[1][(i+1)%Samples]-points[1][(i+Samples-1)%Samples]).normalized;
                tangents[1][Samples]=tangents[1][0];
                for(int r=0;r<2;r++) for(int i=1;i<=Samples;i++) distances[r][i]=distances[r][i-1]+Vector3.Distance(points[r][i-1],points[r][i]);
                var bounds=new Bounds(points[0][0],Vector3.zero);foreach(var p in points[0])bounds.Encapsulate(p);MapBounds=bounds;
                for(int s=0;s<SectionCount;s++)
                {
                    int width=Definition.Widths[s],entry=Definition.EntryLanes[s],exit=entry==0?6-width:0;
                    AddBranch(0,1,entry,exit,EntryStations[s],EntryStations[s]+MergeSpan,width);
                    AddBranch(1,0,0,0,ReturnStations[s],ReturnStations[s]+0.04f,6);
                }
            }
            Vector3 Spline(float t)
            {
                var Anchors=Definition.Anchors;
                float p=Mathf.Repeat(t,1f)*Anchors.Length; int i=Mathf.FloorToInt(p); float u=p-i;
                Vector3 a=Anchors[(i+Anchors.Length-1)%Anchors.Length], b=Anchors[i], c=Anchors[(i+1)%Anchors.Length],d=Anchors[(i+2)%Anchors.Length];
                return 0.5f*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
            }
            static Vector3 AtDistance(Vector3[] p,float[] d,float target)
            {
                int lo=0,hi=d.Length-1;
                while(hi-lo>1){int m=(lo+hi)/2;if(d[m]<target)lo=m;else hi=m;}
                return Vector3.Lerp(p[lo],p[hi],Mathf.InverseLerp(d[lo],d[hi],target));
            }
            void AddBranch(int from,int to,int entry,int exit,float start,float end,int width)
            {
                var b=new Branch{From=from,To=to,EntryLane=entry,ExitLane=exit,Start=start,End=end,Width=width};
                Vector3 p0=MainLanePoint(from,start,LaneOffset(entry)+(width-1)*LaneSpacing*.5f),p3=MainLanePoint(to,end,LaneOffset(exit)+(width-1)*LaneSpacing*.5f);
                float handle=Vector3.Distance(p0,p3)*0.38f;
                Vector3 p1=p0+TangentMain(from,start)*handle,p2=p3-TangentMain(to,end)*handle;
                var raw=new Vector3[129];var len=new float[129];
                for(int i=0;i<=128;i++){float t=i/128f,u=1-t;raw[i]=u*u*u*p0+3*u*u*t*p1+3*u*t*t*p2+t*t*t*p3;if(i>0)len[i]=len[i-1]+Vector3.Distance(raw[i],raw[i-1]);}
                b.Length=len[128];
                for(int i=0;i<=64;i++)b.Points[i]=AtDistance(raw,len,b.Length*i/64f);
                for(int i=0;i<=64;i++)b.Tangents[i]=(b.Points[Mathf.Min(64,i+1)]-b.Points[Mathf.Max(0,i-1)]).normalized;
                b.Tangents[0]=TangentMain(from,start);b.Tangents[64]=TangentMain(to,end);
                branches.Add(b);
            }
            public static float LaneOffset(int lane)=>(lane-2.5f)*LaneSpacing;
            public float GetRouteLength(int r)=>distances[r][Samples];
            public float GetBranchStart(int i)=>branches[i].Start;
            public float GetBranchEnd(int i)=>branches[i].End;
            public int GetBranchEntranceLane(int i)=>branches[i].EntryLane;
            public int GetBranchExitLane(int i)=>branches[i].ExitLane;
            public int GetBranchWidth(int i)=>branches[i].Width;
            public bool BranchAcceptsLane(int i,int lane)=>lane>=branches[i].EntryLane&&lane<branches[i].EntryLane+branches[i].Width;
            public float BranchLateral(int i,float lateral)=>lateral-LaneOffset(branches[i].EntryLane)-(branches[i].Width-1)*LaneSpacing*.5f;
            public float SectionStart(int s)=>EntryStations[s]+MergeSpan;
            public int GetBranchSourceRoute(int i)=>branches[i].From;
            public int GetBranchTargetRoute(int i)=>branches[i].To;
            public float GetBranchLength(int i)=>branches[i].Length;
            static Vector3 Sample(Vector3[] a,float t,bool loop)
            {
                float f=(loop?Mathf.Repeat(t,1f):Mathf.Clamp01(t))*(a.Length-1);int i=Mathf.Min(a.Length-2,Mathf.FloorToInt(f));return Vector3.LerpUnclamped(a[i],a[i+1],f-i);
            }
            public Vector3 EvaluateMain(float p)=>EvaluateRoute(0,p);
            public Vector3 EvaluateRoute(int r,float p)=>Sample(points[r],p,true);
            public Vector3 TangentMain(float p)=>TangentMain(0,p);
            public Vector3 TangentMain(int r,float p)=>Sample(tangents[r],p,true).normalized;
            public Vector3 EvaluateBranch(int i,float t)=>Sample(branches[i].Points,t,false);
            public Vector3 TangentBranch(int i,float t)=>Sample(branches[i].Tangents,t,false).normalized;
            public Vector3 MainLanePoint(int r,float p,float lateral)=>EvaluateRoute(r,p)+Vector3.Cross(Vector3.up,TangentMain(r,p)).normalized*lateral;
            public Vector3 MainLanePoint(float p,float lateral)=>MainLanePoint(0,p,lateral);
            public Vector3 Position(int r,float p,float lateral)=>MainLanePoint(r,p,lateral)+Vector3.up*1.03f;
            public Vector3 Position(float p,float lateral)=>Position(0,p,lateral);
            public Vector3 BranchPosition(int i,float t,float lateral)=>EvaluateBranch(i,t)+Vector3.Cross(Vector3.up,TangentBranch(i,t)).normalized*lateral+Vector3.up*1.03f;
            public bool InnerIsOpen(float p)
            {
                p=Mathf.Repeat(p,1f);for(int i=0;i<SectionCount;i++)if(p>=SectionStart(i)&&p<=ReturnStations[i]+0.0001f)return true;return false;
            }
            public float StationDistance(int r,float p)
            {
                float f=Mathf.Repeat(p,1f)*Samples;int i=Mathf.Min(Samples-1,(int)f);return Mathf.Lerp(distances[r][i],distances[r][i+1],f-i);
            }
            public float Advance(int route,float progress,float meters)
            {
                float target=StationDistance(route,progress)+meters;int lap=Mathf.FloorToInt(progress);float length=GetRouteLength(route);
                while(target>=length){target-=length;lap++;}
                int lo=0,hi=Samples;var d=distances[route];while(hi-lo>1){int m=(lo+hi)/2;if(d[m]<target)lo=m;else hi=m;}
                return lap+(lo+Mathf.InverseLerp(d[lo],d[hi],target))/Samples;
            }
            public float AheadMeters(int route,float from,float to)=>Mathf.Repeat(StationDistance(route,to)-StationDistance(route,from),GetRouteLength(route));
            public void BuildWorld()
            {
                Material road=MakeMaterial("Road_Charcoal",new Color(0.105f,0.15f,0.23f),Color.black);
                Material alternating=MakeMaterial("Road_Slate",new Color(0.13f,0.185f,0.27f),Color.black);
                Material lines=MakeMaterial("LaneMarkings",new Color(0.43f,0.57f,0.68f),Color.black);
                Material rail=MakeMaterial("OuterRail",RaceVisuals.Cyan,Color.black), inner=MakeMaterial("ShortcutRail",RaceVisuals.Orange,Color.black);
                for(int sector=0;sector<32;sector++)
                {
                    float a=sector/32f,b=(sector+1)/32f;int s=sector;
                    var group=new GameObject("CircuitSector_"+s).transform;group.SetParent(parent,false);
                    sectors.Add(group.gameObject);sectorCenters.Add(EvaluateMain((a+b)*0.5f));
                    for(int lane=0;lane<6;lane++){int l=lane;Ribbon(group,"Lane_"+lane,t=>MainLanePoint(0,Mathf.Lerp(a,b,t),LaneOffset(l)),LaneWidth,lane%2==0?road:alternating,24);}
                    for(int boundary=1;boundary<6;boundary++){float x=LaneOffset(boundary)-LaneSpacing*0.5f;Ribbon(group,"Divider",t=>MainLanePoint(0,Mathf.Lerp(a,b,t),x)+Vector3.up*0.025f,0.065f,lines,24);}
                    foreach(float x in new[]{-9.65f,9.65f})Ribbon(group,"Guardrail",t=>MainLanePoint(0,Mathf.Lerp(a,b,t),x)+Vector3.up*0.30f,0.22f,rail,24);
                }
                for(int sector=0;sector<SectionCount;sector++)
                {
                    float a=SectionStart(sector),b=ReturnStations[sector];var group=new GameObject("InnerSection_"+sector).transform;group.SetParent(parent,false);
                    for(int lane=0;lane<6;lane++){int l=lane;Ribbon(group,"ShortcutLane_"+l,t=>MainLanePoint(1,Mathf.Lerp(a,b,t),LaneOffset(l)),LaneWidth,l%2==0?road:alternating,100);}
                    foreach(float x in new[]{-9.65f,9.65f})Ribbon(group,"ShortcutEdge",t=>MainLanePoint(1,Mathf.Lerp(a,b,t),x)+Vector3.up*0.28f,0.24f,inner,100);
                    var junctionGroup=new GameObject("Junction_"+sector).transform;junctionGroup.SetParent(parent,false);
                    for(int lane=0;lane<6;lane++)
                    {
                        var sign=new GameObject("ApproachLane_"+lane).transform;sign.SetParent(junctionGroup,false);float p=EntryStations[sector]-0.018f;
                        sign.SetPositionAndRotation(MainLanePoint(0,p,LaneOffset(lane))+Vector3.up*0.035f,Quaternion.LookRotation(TangentMain(0,p)));
                        bool entry=lane>=Definition.EntryLanes[sector]&&lane<Definition.EntryLanes[sector]+Definition.Widths[sector];
                        for(int chevron=0;chevron<4;chevron++)RaceVisuals.Chevron(sign,new Vector3(0,0,-chevron*6),1.5f,entry?inner:rail);
                        // Approach arrows only. The entrance frame is placed exactly at branch.Start.
                    }
                }
                for(int branch=0;branch<BranchCount;branch++)
                {
                    int idx=branch;var group=new GameObject("Merge_"+branch).transform;group.SetParent(parent,false);
                    int width=branches[branch].Width;
                    for(int lane=0;lane<width;lane++)
                    {
                        float offset=(lane-(width-1)*.5f)*LaneSpacing;
                        Ribbon(group,"BranchRoad",t=>BranchPosition(idx,t,offset)-Vector3.up*1.03f,LaneWidth,lane%2==0?road:alternating,64);
                    }
                    if(branches[branch].From==0)
                        foreach(float x in new[]{-width*LaneSpacing*.5f,width*LaneSpacing*.5f})Ribbon(group,"BranchEdge",t=>EvaluateBranch(idx,t)+Vector3.Cross(Vector3.up,TangentBranch(idx,t)).normalized*x+Vector3.up*0.1f,0.12f,inner,64);
                }
                foreach(Transform group in parent) CombineRoadGroup(group);
                BuildLandmarks(rail);
            }
            static void CombineRoadGroup(Transform group)
            {
                if(!group.name.StartsWith("CircuitSector_")&&!group.name.StartsWith("InnerSection_")&&!group.name.StartsWith("Merge_")&&!group.name.StartsWith("Junction_")&&group.name!="StartFinish")return;
                var filters=group.GetComponentsInChildren<MeshFilter>();
                var batches=new Dictionary<Material,List<CombineInstance>>();
                foreach(var f in filters)
                {
                    var mr=f.GetComponent<MeshRenderer>();if(mr==null)continue;
                    if(!batches.TryGetValue(mr.sharedMaterial,out var list)){list=new List<CombineInstance>();batches.Add(mr.sharedMaterial,list);}
                    list.Add(new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix});
                }
                if(filters.Length<2)return;
                foreach(var batch in batches)
                {
                    var m=new Mesh{name="Combined_"+group.name+"_"+batch.Key.name};m.CombineMeshes(batch.Value.ToArray(),true,true);m.UploadMeshData(true);
                    var go=new GameObject(m.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(group,false);go.GetComponent<MeshFilter>().sharedMesh=m;
                    go.AddComponent<GeneratedMeshOwner>().OwnedMesh=m;
                    var mr=go.GetComponent<MeshRenderer>();mr.sharedMaterial=batch.Key;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
                }
                foreach(var f in filters){f.gameObject.SetActive(false);UnityEngine.Object.Destroy(f.gameObject);}
            }
            void BuildLandmarks(Material rail)
            {
                var gate=new GameObject("StartFinish").transform;gate.SetParent(parent,false);gate.SetPositionAndRotation(EvaluateMain(0),Quaternion.LookRotation(TangentMain(0)));
                var dark=MakeMaterial("GateBody",new Color(0.15f,0.20f,0.31f),Color.black);
                foreach(float x in new[]{-11f,11f})RaceVisuals.Part(gate,"Pillar",PrimitiveType.Cube,new Vector3(x,4,0),new Vector3(1.2f,8,1.2f),dark,true);
                RaceVisuals.Part(gate,"Arch",PrimitiveType.Cube,new Vector3(0,8,0),new Vector3(23f,1.1f,1.2f),dark,true);
                RaceVisuals.Part(gate,"ArchLight",PrimitiveType.Cube,new Vector3(0,7.6f,-0.67f),new Vector3(20f,0.15f,0.1f),rail);
                var white=MakeMaterial("OffWhite",new Color(0.90f,0.96f,1f),Color.black);
                for(int i=0;i<24;i++)RaceVisuals.Part(gate,"Checker",PrimitiveType.Cube,new Vector3(-8.65f+(i%12)*1.57f,0.05f,i/12*1.1f),new Vector3(1.56f,0.035f,1.09f),((i%12)+(i/12))%2==0?white:dark);
                CombineRoadGroup(gate);
                BuildCircuitScenery();
            }
            public void UpdateVisibility(Vector3 cameraPosition)
            {
                UpdateSceneryVisibility(cameraPosition);
                for(int i=0;i<sectors.Count;i++){bool visible=(sectorCenters[i]-cameraPosition).sqrMagnitude<250f*250f;if(sectors[i].activeSelf!=visible)sectors[i].SetActive(visible);}
            }
            static void Ribbon(Transform parent,string name,Func<float,Vector3> eval,float width,Material mat,int segments)
            {
                var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
                for(int i=0;i<=segments;i++)
                {
                    float t=i/(float)segments;Vector3 p=eval(t),tangent=(eval(Mathf.Min(1,t+0.002f))-eval(Mathf.Max(0,t-0.002f))).normalized;
                    Vector3 right=Vector3.Cross(Vector3.up,tangent).normalized*width*0.5f;vertices[i*2]=p-right;vertices[i*2+1]=p+right;uv[i*2]=new Vector2(0,t);uv[i*2+1]=new Vector2(1,t);
                    if(i==segments)continue;int v=i*2,k=i*6;triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3;
                }
                var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<GeneratedMeshOwner>().OwnedMesh=mesh;
                var mr=go.GetComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
            }
            public static Material MakeMaterial(string name,Color baseColor,Color emission)=>RaceVisuals.Material(name,baseColor);
        }
    }
}
