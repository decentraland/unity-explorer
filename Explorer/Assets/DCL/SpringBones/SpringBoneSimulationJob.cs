using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct SpringBoneSimulationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> SlotJointCounts;
        [ReadOnly] public NativeArray<SpringBoneJointConfig> JointConfigs;
        [ReadOnly] public NativeArray<SpringBoneParentData> ParentData;
        [ReadOnly] public NativeArray<float3> PrevTails;
        [ReadOnly] public NativeArray<float3> CurrentTails;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float3> NextTails;

        // Transforms are read AND written (chain propagation)
        [NativeDisableParallelForRestriction] public NativeArray<SpringBoneTransformData> Transforms;

        public float DeltaTime;

        public void Execute(int slotIndex)
        {
            int jointCount = SlotJointCounts[slotIndex];
            if (jointCount == 0) return;

            int baseIndex = slotIndex * SpringBoneService.MAX_JOINTS_PER_SPRING;

            // Chain root's parent comes from the main-thread sync
            var parentRotation = ParentData[slotIndex].Rotation;
            var parentLocalToWorld = ParentData[slotIndex].LocalToWorldMatrix;

            for (int j = 0; j < jointCount; j++)
            {
                int idx = baseIndex + j;
                var config = JointConfigs[idx];
                var head = Transforms[idx];

                // Recompute head world transform from parent
                head = UpdateParentMatrix(head, parentRotation, parentLocalToWorld);

                // Verlet integration
                float3 gravity = config.GravityDir * config.GravityPower * DeltaTime;

                float3 stiffnessForce = math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis)
                                      * config.Stiffness * DeltaTime;

                float3 currentTail = CurrentTails[idx];
                float3 prevTail = PrevTails[idx];

                float3 nextTail = currentTail
                                + (currentTail - prevTail) * (1f - config.Drag)
                                + stiffnessForce
                                + gravity;

                // Length constraint
                float3 headToTail = nextTail - head.Position;
                float len = math.length(headToTail);

                nextTail = len > 0.0001f
                    ? head.Position + (headToTail / len) * config.Length
                    : head.Position + math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis) * config.Length;

                NextTails[idx] = nextTail;

                // Update head rotation from tail direction
                quaternion currentRot = math.mul(parentRotation, config.LocalRotation);
                float3 currentDir = math.mul(currentRot, config.BoneAxis);
                float3 targetDir = nextTail - head.Position;

                quaternion newRotation = math.mul(FromToRotation(currentDir, targetDir), currentRot);
                head = UpdateRotation(head, newRotation, parentRotation, parentLocalToWorld);
                Transforms[idx] = head;

                // This joint becomes parent for the next
                parentRotation = head.Rotation;
                parentLocalToWorld = head.LocalToWorldMatrix;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static SpringBoneTransformData UpdateParentMatrix(SpringBoneTransformData head, quaternion parentRotation, float4x4 parentLocalToWorld)
        {
            quaternion newRotation = math.mul(parentRotation, head.LocalRotation);
            float4x4 newLocalToWorld = math.mul(parentLocalToWorld, float4x4.TRS(head.LocalPosition, head.LocalRotation, head.LocalScale));

            return new SpringBoneTransformData(
                newRotation,
                head.LocalPosition,
                head.LocalRotation,
                head.LocalScale,
                newLocalToWorld);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static SpringBoneTransformData UpdateRotation(SpringBoneTransformData head, quaternion newWorldRotation, quaternion parentRotation, float4x4 parentLocalToWorld)
        {
            quaternion newLocalRotation = math.normalize(math.mul(math.inverse(parentRotation), newWorldRotation));
            float4x4 newLocalToWorld = math.mul(parentLocalToWorld, float4x4.TRS(head.LocalPosition, newLocalRotation, head.LocalScale));

            return new SpringBoneTransformData(
                newWorldRotation,
                head.LocalPosition,
                newLocalRotation,
                head.LocalScale,
                newLocalToWorld);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static quaternion FromToRotation(in float3 from, in float3 to)
        {
            float fromLenSq = math.lengthsq(from);
            float toLenSq = math.lengthsq(to);

            if (fromLenSq < 0.0001f || toLenSq < 0.0001f)
                return quaternion.identity;

            float3 f = math.normalize(from);
            float3 t = math.normalize(to);
            float dot = math.dot(f, t);

            if (dot >= 1f) return quaternion.identity;

            if (dot <= -1f)
            {
                float3 axis = math.cross(f, new float3(1, 0, 0));

                if (math.lengthsq(axis) < 0.0001f)
                    axis = math.cross(f, new float3(0, 1, 0));

                return quaternion.AxisAngle(math.normalize(axis), math.PI);
            }

            float angle = math.acos(dot);
            float3 rotAxis = math.normalize(math.cross(f, t));
            return quaternion.AxisAngle(rotAxis, angle);
        }
    }
}
