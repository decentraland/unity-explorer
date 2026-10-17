using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace SpringBones
{
    internal static class SpringBoneSimulator
    {
        const float EPSILON = 0.0001f;

        public static void SimulateSlot(
            int slotIndex,
            int jointCount,
            SpringBoneJointConfig[] jointConfigs,
            SpringBoneParentData parentData,
            SpringBoneTransformData[] transforms,
            float3[] prevTails,
            float3[] currentTails,
            float3[] nextTails,
            float deltaTime)
        {
            int baseIndex = slotIndex * SpringBoneService.MAX_JOINTS_PER_SPRING;

            quaternion parentRotation = parentData.Rotation;
            float4x4 parentLocalToWorld = parentData.LocalToWorldMatrix;

            for (int j = 0; j < jointCount; j++)
            {
                int idx = baseIndex + j;
                var config = jointConfigs[idx];
                var head = transforms[idx];

                head = UpdateParentMatrix(head, parentRotation, parentLocalToWorld);

                float3 gravity = config.GravityDir * config.GravityPower * deltaTime;

                float3 stiffnessForce = math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis)
                                        * config.Stiffness * deltaTime;

                float3 currentTail = currentTails[idx];
                float3 prevTail = prevTails[idx];

                float3 nextTail = currentTail
                                  + (currentTail - prevTail) * (1f - config.Drag)
                                  + stiffnessForce
                                  + gravity;

                float3 headToTail = nextTail - head.Position;
                float len = math.length(headToTail);

                nextTail = len > EPSILON
                    ? head.Position + (headToTail / len) * config.Length
                    : head.Position + math.mul(math.mul(parentRotation, config.LocalRotation), config.BoneAxis) * config.Length;

                nextTails[idx] = nextTail;

                quaternion currentRot = math.mul(parentRotation, config.LocalRotation);
                float3 currentDir = math.mul(currentRot, config.BoneAxis);
                float3 targetDir = nextTail - head.Position;

                quaternion newRotation = math.mul(FromToRotation(currentDir, targetDir), currentRot);
                head = UpdateRotation(head, newRotation, parentRotation, parentLocalToWorld);
                transforms[idx] = head;

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

            if (fromLenSq < EPSILON || toLenSq < EPSILON)
                return quaternion.identity;

            float3 f = math.normalize(from);
            float3 t = math.normalize(to);
            float dot = math.dot(f, t);

            if (dot >= 1f) return quaternion.identity;

            if (dot <= -1f)
            {
                float3 axis = math.cross(f, new float3(1, 0, 0));

                if (math.lengthsq(axis) < EPSILON)
                    axis = math.cross(f, new float3(0, 1, 0));

                return quaternion.AxisAngle(math.normalize(axis), math.PI);
            }

            float angle = math.acos(dot);
            float3 rotAxis = math.normalize(math.cross(f, t));
            return quaternion.AxisAngle(rotAxis, angle);
        }
    }
}
