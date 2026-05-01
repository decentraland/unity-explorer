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
        [ReadOnly] public NativeArray<bool> SlotActive;
        [ReadOnly] public NativeArray<SpringBoneJointConfig> JointConfigs;
        [ReadOnly] public NativeArray<SpringBoneParentData> ParentData;
        [ReadOnly] public NativeArray<SpringBoneParentData> PrevParentData;
        [ReadOnly] public NativeArray<float3> PrevTails;
        [ReadOnly] public NativeArray<float3> CurrentTails;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float3> NextTails;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<quaternion> CurrStepRotations;

        // Transforms are read AND written (chain propagation)
        [NativeDisableParallelForRestriction] public NativeArray<SpringBoneTransformData> Transforms;

        public float DeltaTime;
        public float SubstepAlpha;

        public void Execute(int slotIndex)
        {
            int jointCount = SlotJointCounts[slotIndex];
            if (jointCount == 0 || !SlotActive[slotIndex]) return;

            int baseIndex = slotIndex * SpringBoneService.MAX_JOINTS_PER_SPRING;

            // Chain root's parent: interpolate from previous frame's parent state to current
            // along the substep so a 30°/frame avatar rotation distributes across substeps
            // instead of jumping all at once and exciting the springs.
            var prevP = PrevParentData[slotIndex];
            var currP = ParentData[slotIndex];
            var parentRotation = math.slerp(prevP.Rotation, currP.Rotation, SubstepAlpha);
            float4x4 parentLocalToWorld = new float4x4(
                math.lerp(prevP.LocalToWorldMatrix.c0, currP.LocalToWorldMatrix.c0, SubstepAlpha),
                math.lerp(prevP.LocalToWorldMatrix.c1, currP.LocalToWorldMatrix.c1, SubstepAlpha),
                math.lerp(prevP.LocalToWorldMatrix.c2, currP.LocalToWorldMatrix.c2, SubstepAlpha),
                math.lerp(prevP.LocalToWorldMatrix.c3, currP.LocalToWorldMatrix.c3, SubstepAlpha));
            float scaleFactor = math.lerp(prevP.ScaleFactor, currP.ScaleFactor, SubstepAlpha);

            for (int j = 0; j < jointCount; j++)
            {
                int idx = baseIndex + j;
                var config = JointConfigs[idx];
                var head = Transforms[idx];

                float scaledLength = config.Length * scaleFactor;

                // Recompute head world transform from parent
                head = UpdateParentMatrix(head, parentRotation, parentLocalToWorld);

                // Verlet integration
                float3 gravity = config.GravityDir * config.GravityPower * DeltaTime;

                float3 stiffnessForce = math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis)
                                      * config.Stiffness * DeltaTime;

                float3 currentTail = CurrentTails[idx];
                float3 prevTail = PrevTails[idx];

                // Drag is authored as per-step retention at ~60Hz. Scale exponent by 60*dt so
                // per-second damping is framerate-invariant — at 144fps, raw (1-drag)^144 over-damps.
                float dragRetention = math.pow(math.max(1e-6f, 1f - config.Drag), 60f * DeltaTime);

                float3 nextTail = currentTail
                                + (currentTail - prevTail) * dragRetention
                                + stiffnessForce
                                + gravity;

                // Length constraint
                float3 headToTail = nextTail - head.Position;
                float len = math.length(headToTail);

                nextTail = len > 0.0001f
                    ? head.Position + (headToTail / len) * scaledLength
                    : head.Position + math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis) * scaledLength;

                NextTails[idx] = nextTail;

                // Update head rotation from tail direction
                quaternion currentRot = math.mul(parentRotation, config.LocalRotation);
                float3 currentDir = math.mul(currentRot, config.BoneAxis);
                float3 targetDir = nextTail - head.Position;

                quaternion newRotation = math.mul(FromToRotation(currentDir, targetDir), currentRot);
                head = UpdateRotation(head, newRotation, parentRotation, parentLocalToWorld);
                Transforms[idx] = head;
                CurrStepRotations[idx] = newRotation;

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
