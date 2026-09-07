using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        public sealed partial class TrackModel
        {
            readonly List<GameObject> scenerySections = new();
            readonly List<Bounds> sceneryBounds = new();
            static Material sceneryMaterial;
            public int SceneryPropCount { get; private set; }
            public int SceneryLandmarkCount { get; private set; }
            public int SceneryTriangles { get; private set; }
            public int SceneryDrawMeshes { get; private set; }
            public float SceneryMinimumClearance { get; private set; } = float.MaxValue;

            // Built once on map load. No colliders, lights, animated transforms or per-prop Updates.
            // Vertex colors let different-colored details share one opaque draw per spatial chunk.
            void BuildCircuitScenery()
            {
                if (sceneryMaterial == null)
                    sceneryMaterial = new Material(Resources.Load<Shader>("Shaders/SceneryToon")) { name="CircuitScenery_Shared" };
                var road = new List<Vector4>(3000);
                for (int i=0;i<1536;i++)
                {
                    float t=i/1536f; AddRoadProbe(road,EvaluateMain(t),LaneCount*LaneSpacing*.5f);
                    if (InnerIsOpen(t)) AddRoadProbe(road,EvaluateRoute(1,t),LaneCount*LaneSpacing*.5f);
                }
                for (int b=0;b<BranchCount;b++)
                    for (int i=0;i<=96;i++) AddRoadProbe(road,EvaluateBranch(b,i/96f),GetBranchWidth(b)*LaneSpacing*.5f);

                // Both road shoulders, at the road's actual height instead of down in the infield.
                for (int sector=0;sector<8;sector++)
                {
                    var mesh=new SceneryMesh();
                    for (int n=0;n<4;n++)
                    {
                        float t=(sector*4+n+.55f)/32f;
                        Vector3 p=EvaluateMain(t), right=Vector3.Cross(Vector3.up,TangentMain(t)).normalized;
                        Quaternion q=Quaternion.LookRotation(Vector3.ProjectOnPlane(TangentMain(t),Vector3.up));
                        foreach (int side in new[]{-1,1})
                            foreach (float offset in new[]{20f,24f,29f,36f})
                            {
                                Vector3 at=p+right*(side*offset);
                                if (!ClearOfRoad(road,at,5.6f,3.5f,out float clearance)) continue;
                                BuildPod(mesh,at,q,(sector+n+(side+1)/2)%3);
                                RegisterProp(clearance); break;
                            }
                    }
                    CommitScenery(mesh,"RoadsideDistrict_"+sector,true);
                }
                for (int section=0;section<SectionCount;section++)
                {
                    var mesh=new SceneryMesh();
                    for (int n=0;n<3;n++)
                    {
                        float t=Mathf.Lerp(SectionStart(section),ReturnStations[section],.18f+n*.30f);
                        Vector3 p=EvaluateRoute(1,t), tangent=TangentMain(1,t);
                        Vector3 at=p-Vector3.Cross(Vector3.up,tangent).normalized*21f;
                        if (!ClearOfRoad(road,at,5.6f,3.5f,out float clearance)) continue;
                        BuildPod(mesh,at,Quaternion.LookRotation(Vector3.ProjectOnPlane(tangent,Vector3.up)),(n+section+1)%3);
                        RegisterProp(clearance);
                    }
                    CommitScenery(mesh,"ShortcutDistrict_"+section,true);
                }

                // Separate bounds for each landmark: never combine the entire horizon into one mesh.
                for (int i=0;i<4;i++)
                {
                    float t=.11f+i*.24f;Vector3 p=EvaluateMain(t);
                    Vector3 right=Vector3.Cross(Vector3.up,TangentMain(t)).normalized;
                    foreach (float offset in new[]{108f,132f,160f})
                    {
                        Vector3 at=p+right*offset+Vector3.up*(Definition.Elevated?61f:49f);
                        if (!ClearOfRoad(road,at,57f,8f,out float clearance)) continue;
                        Quaternion facing=Quaternion.Euler(0,-i*75,0);
                        if(i==1)facing=Quaternion.LookRotation(Vector3.ProjectOnPlane(EvaluateMain(t-.08f)-at,Vector3.up));
                        var mesh=new SceneryMesh();BuildGiant(mesh,at,facing,i);
                        CommitScenery(mesh,"HorizonLandmark_"+i,false);SceneryLandmarkCount++;
                        SceneryMinimumClearance=Mathf.Min(SceneryMinimumClearance,clearance);break;
                    }
                }
                BuildExactEntranceFrames();
            }
            static void AddRoadProbe(List<Vector4> road,Vector3 p,float width) => road.Add(new Vector4(p.x,p.y,p.z,width));
            static bool ClearOfRoad(List<Vector4> road,Vector3 p,float radius,float margin,out float clearance)
            {
                clearance=float.MaxValue;
                // Conservative XZ clearance also protects elevated roads and bridge merge envelopes.
                foreach (Vector4 sample in road)
                {
                    float dx=p.x-sample.x,dz=p.z-sample.z;
                    float gap=Mathf.Sqrt(dx*dx+dz*dz)-sample.w-radius;
                    clearance=Mathf.Min(clearance,gap);if(gap<margin)return false;
                }
                return true;
            }
            void RegisterProp(float clearance)
            {
                SceneryPropCount++;SceneryMinimumClearance=Mathf.Min(SceneryMinimumClearance,clearance);
            }
            void CommitScenery(SceneryMesh builder,string name,bool distanceCull)
            {
                if (builder.TriangleCount==0)return;
                var mesh=builder.Finish(name);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<GeneratedMeshOwner>().OwnedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=sceneryMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                SceneryTriangles+=builder.TriangleCount;SceneryDrawMeshes++;
                if(distanceCull){scenerySections.Add(go);sceneryBounds.Add(renderer.bounds);}
            }
            void UpdateSceneryVisibility(Vector3 position)
            {
                // Runs with the existing 0.15s visibility tick, not every frame.
                for (int i=0;i<scenerySections.Count;i++)
                {
                    bool visible=sceneryBounds[i].SqrDistance(position)<220f*220f;
                    if(scenerySections[i].activeSelf!=visible)scenerySections[i].SetActive(visible);
                }
            }
            void BuildExactEntranceFrames()
            {
                for (int b=0;b<BranchCount;b++)
                {
                    if(GetBranchSourceRoute(b)!=0)continue;
                    var mesh=new SceneryMesh();Vector3 p=EvaluateBranch(b,0);
                    Quaternion q=Quaternion.LookRotation(TangentBranch(b,0));float half=GetBranchWidth(b)*LaneSpacing*.5f;
                    Color gold=new Color(1f,.64f,.18f), pale=new Color(1f,.87f,.53f);
                    mesh.Box(p+q*new Vector3(0,.055f,0),new Vector3(half*2,.09f,.30f),q,gold);
                    foreach(int side in new[]{-1,1})mesh.Box(p+q*new Vector3(side*(half+.1f),1.85f,0),new Vector3(.16f,3.7f,.16f),q,gold);
                    mesh.Box(p+q*new Vector3(0,3.75f,0),new Vector3(half*2+.36f,.20f,.20f),q,pale);
                    for(int i=0;i<3;i++)
                    {
                        float t=.07f+i*.06f;Vector3 at=EvaluateBranch(b,t)+Vector3.up*.055f;
                        Quaternion facing=Quaternion.LookRotation(TangentBranch(b,t));
                        foreach(int side in new[]{-1,1})mesh.Box(at+facing*new Vector3(side*.35f,0,0),new Vector3(.15f,.08f,1.05f),facing*Quaternion.Euler(0,-side*40,0),pale);
                    }
                    CommitScenery(mesh,"ExactEntrance_"+b,false);
                }
            }

            static readonly Color PodBody=new(.22f,.32f,.49f), PodDark=new(.09f,.15f,.28f),
                PodPale=new(.66f,.78f,.87f), PodLilac=new(.53f,.46f,.75f), PodLight=new(.29f,.85f,.81f);
            static void BuildPod(SceneryMesh m,Vector3 p,Quaternion q,int style)
            {
                m.Prism(p-Vector3.up*1.5f,5.1f,4.7f,1.4f,q,PodBody,8);
                m.Prism(p-Vector3.up*4.2f,1.9f,5f,2.7f,q,PodDark,8);
                m.Prism(p-Vector3.up*.20f,4.75f,4.75f,.15f,q,PodLight,8);
                if(style==0)
                {
                    m.Prism(p,3.1f,3.1f,4.5f,q,PodPale,8);
                    m.Ellipsoid(p+Vector3.up*4.7f,new Vector3(6.4f,4.7f,6.4f),q,PodLilac,16,8);
                    m.Box(p+q*new Vector3(0,2.5f,-3.08f),new Vector3(3.9f,1.65f,.18f),q,PodDark);
                    for(int i=-1;i<=1;i++)m.Box(p+q*new Vector3(i*1.12f,2.5f,-3.2f),new Vector3(.7f,1.1f,.15f),q,PodLight);
                }
                else if(style==1)
                {
                    m.Box(p+Vector3.up*4.25f,new Vector3(3.2f,8.5f,3.2f),q,PodPale);
                    for(int i=0;i<3;i++)m.Box(p+q*new Vector3(0,2+i*2.4f,-1.68f),new Vector3(2.9f,.65f,.2f),q,PodLight);
                    m.Ring(p+Vector3.up*9.2f,2.5f,.3f,q,PodLilac,16);
                    m.Box(p+Vector3.up*9,new Vector3(.24f,3.8f,.24f),q,PodBody);
                }
                else
                {
                    m.Box(p+Vector3.up*1.4f,new Vector3(4f,2.8f,3.8f),q,PodPale);
                    m.Box(p+Vector3.up*3.8f,new Vector3(.7f,2.5f,.7f),q,PodBody);
                    Quaternion dish=q*Quaternion.Euler(27,0,0);
                    m.Prism(p+Vector3.up*4.5f,.65f,3.8f,1.5f,dish,PodLilac,10);
                    m.Prism(p+Vector3.up*4.5f+dish*Vector3.up*1.48f,3.5f,3.5f,.08f,dish,PodLight,10);
                    foreach(int side in new[]{-1,1})
                    {
                        m.Box(p+q*new Vector3(side*3.55f,2,0),new Vector3(2.1f,.2f,3),q,PodDark);
                        for(int i=0;i<3;i++)m.Box(p+q*new Vector3(side*3.55f,2.12f,-.9f+i*.9f),new Vector3(1.85f,.08f,.55f),q,PodLight);
                    }
                }
            }
            static void BuildGiant(SceneryMesh m,Vector3 p,Quaternion q,int style)
            {
                if(style==0)
                {
                    m.Ellipsoid(p,Vector3.one*65,q,PodLilac,20,12);
                    Quaternion ring=q*Quaternion.Euler(65,0,24);
                    m.Ring(p,49,2.5f,ring,PodPale,40);m.Ring(p,54,.65f,ring,PodLight,40);
                    m.Ellipsoid(p+q*new Vector3(39,24,0),Vector3.one*10,q,PodPale,12,8);
                }
                else if(style==1)
                {
                    m.Ring(p,39,5,q,PodBody,32);m.Ring(p,31,2,q,PodLight,32);
                    m.Ellipsoid(p,Vector3.one*25,q,PodLilac,16,10);
                    foreach(int side in new[]{-1,1})m.Box(p+q*new Vector3(side*44,0,0),new Vector3(15,3,14),q,PodPale);
                    m.Box(p-Vector3.up*42,new Vector3(10,22,12),q,PodBody);
                }
                else if(style==2)
                {
                    m.Ellipsoid(p,new Vector3(82,23,32),q,PodPale,20,10);
                    m.Box(p-Vector3.up*3,new Vector3(100,3,12),q,PodLilac);
                    m.Ellipsoid(p+q*new Vector3(0,3,-13),new Vector3(28,12,10),q,PodDark,16,8);
                    foreach(int side in new[]{-1,1})
                    {
                        m.Box(p+q*new Vector3(side*34,-7,0),new Vector3(10,11,22),q,PodBody);
                        m.Box(p+q*new Vector3(side*34,-7,11.2f),new Vector3(7,7,.5f),q,PodLight);
                    }
                }
                else
                {
                    m.Prism(p-Vector3.up*28,23,14,55,q,PodBody,8);
                    m.Prism(p+Vector3.up*27,14,0,28,q,PodLilac,8);
                    m.Ring(p+Vector3.up*18,41,3.5f,q*Quaternion.Euler(90,0,0),PodPale,32);
                    m.Ring(p+Vector3.up*4,35,1.2f,q*Quaternion.Euler(90,0,0),PodLight,32);
                    for(int i=0;i<4;i++)m.Box(p+q*new Vector3(0,-20+i*10,-21.8f+i*1.64f),new Vector3(13,2,.3f),q,PodLight);
                }
            }

            sealed class SceneryMesh
            {
                readonly List<Vector3> vertices=new();readonly List<Color32> colors=new();readonly List<int> triangles=new();
                public int TriangleCount=>triangles.Count/3;
                void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
                {
                    int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                    for(int i=0;i<4;i++)colors.Add(color);
                    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
                }
                void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
                {
                    int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                    for(int i=0;i<3;i++){colors.Add(color);triangles.Add(n+i);}
                }
                public void Box(Vector3 p,Vector3 size,Quaternion q,Color c)
                {
                    Vector3 x=q*Vector3.right*size.x*.5f,y=q*Vector3.up*size.y*.5f,z=q*Vector3.forward*size.z*.5f;
                    Quad(p-x-y-z,p-x+y-z,p+x+y-z,p+x-y-z,c);
                    Quad(p+x-y+z,p+x+y+z,p-x+y+z,p-x-y+z,c);
                    Quad(p+x-y-z,p+x+y-z,p+x+y+z,p+x-y+z,c);
                    Quad(p-x-y+z,p-x+y+z,p-x+y-z,p-x-y-z,c);
                    Quad(p-x+y-z,p-x+y+z,p+x+y+z,p+x+y-z,c);
                    Quad(p-x-y+z,p-x-y-z,p+x-y-z,p+x-y+z,c);
                }
                public void Prism(Vector3 p,float lower,float upper,float height,Quaternion q,Color c,int sides)
                {
                    for(int i=0;i<sides;i++)
                    {
                        float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                        Vector3 va=new(Mathf.Cos(a),0,Mathf.Sin(a)),vb=new(Mathf.Cos(b),0,Mathf.Sin(b));
                        Vector3 aa=p+q*(va*lower),bb=p+q*(vb*lower),at=p+q*(va*upper+Vector3.up*height),bt=p+q*(vb*upper+Vector3.up*height);
                        Quad(aa,at,bt,bb,c);Triangle(p,aa,bb,c);Triangle(p+q*Vector3.up*height,bt,at,c);
                    }
                }
                public void Ellipsoid(Vector3 p,Vector3 size,Quaternion q,Color c,int longitude,int latitude)
                {
                    Vector3 Point(int x,int y)
                    {
                        float a=x*Mathf.PI*2/longitude,b=-Mathf.PI*.5f+y*Mathf.PI/latitude;
                        return p+q*Vector3.Scale(new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Sin(a)*Mathf.Cos(b)),size*.5f);
                    }
                    for(int y=0;y<latitude;y++)for(int x=0;x<longitude;x++)Quad(Point(x,y),Point(x,y+1),Point(x+1,y+1),Point(x+1,y),c);
                }
                public void Ring(Vector3 p,float radius,float tube,Quaternion q,Color c,int segments)
                {
                    Vector3 Point(int i,int j)
                    {
                        float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*.5f,r=radius+Mathf.Cos(b)*tube;
                        return p+q*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*tube);
                    }
                    for(int i=0;i<segments;i++)for(int j=0;j<4;j++)Quad(Point(i,j),Point(i+1,j),Point(i+1,j+1),Point(i,j+1),c);
                }
                public Mesh Finish(string name)
                {
                    var mesh=new Mesh { name=name };
                    if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                    mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
                    mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
                }
            }
        }
    }
}
