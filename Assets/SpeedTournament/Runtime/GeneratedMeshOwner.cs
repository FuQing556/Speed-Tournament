using UnityEngine;

namespace SpeedTournament
{
    // Only attached to meshes created for this course, never shared primitive meshes.
    // Selecting another map releases the old mesh allocations after the frame.
    public sealed class GeneratedMeshOwner : MonoBehaviour
    {
        public Mesh OwnedMesh;
        void OnDestroy(){if(OwnedMesh!=null)Destroy(OwnedMesh);}
    }
}
