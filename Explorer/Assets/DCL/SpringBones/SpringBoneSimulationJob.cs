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
        [ReadOnly] public NativeArray<int> SlotCountMap;
        [ReadOnly] public NativeArray<BlittableJointConfig> JointConfigs;
        [ReadOnly] public NativeArray<BlittableParentData> ParentData;
        [ReadOnly] public NativeArray<float3> PrevTails;
        [ReadOnly] public NativeArray<float3> CurrentTails;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float3> NextTails;
        [NativeDisableParallelForRestriction] public NativeArray<BlittableTransform> Transforms;

        public float DeltaTime;

        [BurstCompile]
        public void Execute(int slot)
        {
            int jointCount = SlotCountMap[slot];
            if (jointCount == 0) return;

            int baseIndex = slot * SpringBoneService.MAX_JOINTS_PER_SPRING;

            var parentRotation = ParentData[slot].Rotation;
            var parentLocalToWorld = ParentData[slot].LocalToWorldMatrix;

            for (int i = 0; i < jointCount; i++)
            {
                int j = baseIndex + i;
                var config = JointConfigs[j];

                var head = Transforms[j];

                head = RecomputeWorldTransform(head, parentRotation, parentLocalToWorld);

                // Verlet integration
                float3 gravity = config.GravityDir * config.GravityPower * DeltaTime;

                float3 stiffnessForce = math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis)
                                      * config.Stiffness * DeltaTime;

                float3 currentTail = CurrentTails[j];
                float3 prevTail = PrevTails[j];

                float3 nextTail = currentTail
                                + (currentTail - prevTail) * (1f - config.Drag)
                                + stiffnessForce
                                + gravity;

                // Length constraint
                float3 headToTail = nextTail - head.Position;
                float length = math.length(headToTail);

                nextTail = length > 0.0001f
                    ? head.Position + (headToTail / length * config.Length)
                    : head.Position + (math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis) * config.Length);

                NextTails[j] = nextTail;

                // Update head rotation from tail direction
                quaternion currentRot = math.mul(parentRotation, config.LocalRotation);
                float3 currentDir = math.mul(currentRot, config.BoneAxis);
                float3 targetDir = nextTail - head.Position;

                quaternion newRotation = math.mul(FromToRotation(currentDir, targetDir), currentRot);
                head = ApplyRotation(head, newRotation, parentRotation, parentLocalToWorld);

                Transforms[j] = head;

                // This joint becomes parent for the next
                parentRotation = head.Rotation;
                parentLocalToWorld = head.LocalToWorldMatrix;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BlittableTransform RecomputeWorldTransform(in BlittableTransform head, quaternion parentRotation, float4x4 parentLocalToWorld)
        {
            quaternion rotation = math.mul(parentRotation, head.LocalRotation);
            float4x4 ltw = math.mul(parentLocalToWorld, float4x4.TRS(head.LocalPosition, head.LocalRotation, head.LocalScale));

            return new BlittableTransform(rotation, head.LocalPosition, head.LocalRotation, head.LocalScale, ltw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BlittableTransform ApplyRotation(in BlittableTransform head, quaternion newWorldRotation, quaternion parentRotation, float4x4 parentLocalToWorld)
        {
            quaternion localRotation = math.normalize(math.mul(math.inverse(parentRotation), newWorldRotation));
            float4x4 ltw = math.mul(parentLocalToWorld, float4x4.TRS(head.LocalPosition, localRotation, head.LocalScale));

            return new BlittableTransform(newWorldRotation, head.LocalPosition, localRotation, head.LocalScale, ltw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion FromToRotation(in float3 from, in float3 to)
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
