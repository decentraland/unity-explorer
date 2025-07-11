#ifndef QUAD_TREE_HELPERS_INCLUDED
#define QUAD_TREE_HELPERS_INCLUDED

// Frustum plane structure
struct FrustumPlane
{
    float3 normal;
    float distance;
};

// Block Size Calculation
inline uint getBlockSize(uint level)
{
    return 1 << level;
}

int getBlockArea(int level)
{
    int size = getBlockSize(level);
    return size * size;
}

// Linear Index to Quadtree Coordinate
int2 linearToQuadCoord(int linearIndex, int level)
{
    int blockSize = getBlockSize(level);
    int blocksPerRow = 16 / blockSize; // How many blocks fit in one row at this level

    int blockRow = linearIndex / blocksPerRow;
    int blockCol = linearIndex % blocksPerRow;

    return int2(blockCol, blockRow);
}

// Quadtree Coordinate to Linear Index
int quadCoordToLinear(int2 coord, int level)
{
    int blockSize = getBlockSize(level);
    int blocksPerRow = 16 / blockSize;

    return coord.y * blocksPerRow + coord.x;
}

// Get the 4 child blocks of a parent block
void getChildBlocks(int parentLinearIndex, int parentLevel, out int children[4])
{
    int2 parentCoord = linearToQuadCoord(parentLinearIndex, parentLevel);

    // Each parent coordinate maps to 2x2 children
    int2 childBaseCoord = parentCoord * 2;

    children[0] = quadCoordToLinear(childBaseCoord + int2(0, 0), parentLevel + 1); // Top-left
    children[1] = quadCoordToLinear(childBaseCoord + int2(1, 0), parentLevel + 1); // Top-right
    children[2] = quadCoordToLinear(childBaseCoord + int2(0, 1), parentLevel + 1); // Bottom-left
    children[3] = quadCoordToLinear(childBaseCoord + int2(1, 1), parentLevel + 1); // Bottom-right
}

// Parent Block Calculation
int getParentBlock(int childLinearIndex, int childLevel)
{
    if (childLevel == 0)
        return -1; // Root has no parent

    int2 childCoord = linearToQuadCoord(childLinearIndex, childLevel);
    int2 parentCoord = childCoord / 2; // Integer division

    return quadCoordToLinear(parentCoord, childLevel - 1);
}

// Block Bounds in Final Array
// Get the range of final 1x1 blocks that a block at any level encompasses
int2 getBlockBounds(int linearIndex, int level)
{
    int2 coord = linearToQuadCoord(linearIndex, level);
    int blockSize = getBlockSize(level);

    int2 startPixel = coord * blockSize;
    int startLinear = startPixel.y * 16 + startPixel.x;
    int count = blockSize * blockSize;

    return int2(startLinear, startLinear + count - 1); // [start, end] inclusive
}

// Memory Layout Helper
// Calculate memory offset for storing quadtree level data
int getLevelOffset(int level)
{
    int offset = 0;
    for (int i = 0; i < level; i++)
    {
        int levelSize = getBlockSize(i);
        offset += (16 / levelSize) * (16 / levelSize); // Number of blocks at level i
    }
    return offset;
}

inline int2 ConvertGridPositionIntoWorldParcelID(uint nWidth, uint nHeight, uint dimension)
{
    // Recentre to the middle being 0,0
    return int2((int)nWidth - (dimension >> 1), (int)nHeight - (dimension >> 1));
}

// Calculate QuadTree nodes into world space plot coord
inline int2 CalculateWorldPlotFromQuadtreeNode(uint nNodeID, uint nMaxDepth = 10)
{
    uint dimension = 512;
    uint nWidth = 0;
    uint nHeight = 0;
    uint nWidthMask = 1;
    uint nHeightMask = 2;
    uint nRemainingValue = nNodeID;
    [unroll]
    for(int i = 0; i < nMaxDepth; ++i)
    {
        uint dimensionTest = dimension >> i;
        uint dimensionTest2 = dimensionTest * dimensionTest;
        uint levelQuadrant = floor(nRemainingValue / dimensionTest2);
        nWidth += (levelQuadrant & nWidthMask) * dimensionTest;
        nHeight += ((levelQuadrant & nHeightMask) >> 1) * dimensionTest;
        nRemainingValue = nNodeID % dimensionTest2;
    }
    return ConvertGridPositionIntoWorldParcelID(nWidth, nHeight, dimension);
}

inline void CalculateBounds(uint nNodeID, uint nQuadSize, uint nCurrentDepth, out uint3 boundingBoxCentre, out uint3 boundingBoxExtents)
{
    uint2 worldSpacePos = CalculateWorldPlotFromQuadtreeNode(nNodeID, nCurrentDepth);
    const uint3 boundingSizes = uint3(nQuadSize * 16, 16, nQuadSize * 16);
    boundingBoxExtents = uint3(boundingSizes * 0.5f);
    boundingBoxCentre = uint3(worldSpacePos.x, 0, worldSpacePos.y) + boundingBoxExtents;
}

// Realign coordinate space to centre the quadtree
inline float2 ConvertToQuadtreeSpace(float3 cameraPos)
{
    return (float2(cameraPos.x / 16, cameraPos.y / 16) - float2(256,256));
}

// Extract the top 8 bits
inline uint GetTop8Bits(uint value)
{
    return value >> 24;
}

// Extract the bottom 24 bits
inline uint GetBottom24Bits(uint value)
{
    return value & 0xFFFFFF;
}

inline bool FrustumTest(in FrustumPlane frustumPlanes[6], float3 boundsMin, float3 boundsMax)
{
    // Perform frustum culling test
    bool isVisible = true;

    [unroll]
    for (int i = 0; i < 6; i++)
    {
        FrustumPlane plane = frustumPlanes[i];

        // Find the positive vertex (furthest point in direction of plane normal)
        float3 positiveVertex = float3(
            plane.normal.x >= 0 ? boundsMax.x : boundsMin.x,
            plane.normal.y >= 0 ? boundsMax.y : boundsMin.y,
            plane.normal.z >= 0 ? boundsMax.z : boundsMin.z
        );

        // Test if positive vertex is outside the plane
        if (dot(plane.normal, positiveVertex) + plane.distance < 0)
        {
            isVisible = false;
            break;
        }
    }
    return isVisible;
}

inline void GenerateFrustumPlanes(inout FrustumPlane frustumPlanes[6], float4x4 viewProjMatrix)
{
    // Left plane
    float3 leftNormal = float3(
        viewProjMatrix._14 + viewProjMatrix._11,
        viewProjMatrix._24 + viewProjMatrix._21,
        viewProjMatrix._34 + viewProjMatrix._31
    );
    float leftDist = viewProjMatrix._44 + viewProjMatrix._41;
    float leftLen = length(leftNormal);
    frustumPlanes[0].normal = leftNormal / leftLen;
    frustumPlanes[0].distance = leftDist / leftLen;

    // Right plane
    float3 rightNormal = float3(
        viewProjMatrix._14 - viewProjMatrix._11,
        viewProjMatrix._24 - viewProjMatrix._21,
        viewProjMatrix._34 - viewProjMatrix._31
    );
    float rightDist = viewProjMatrix._44 - viewProjMatrix._41;
    float rightLen = length(rightNormal);
    frustumPlanes[1].normal = rightNormal / rightLen;
    frustumPlanes[1].distance = rightDist / rightLen;

    // Bottom plane
    float3 bottomNormal = float3(
        viewProjMatrix._14 + viewProjMatrix._12,
        viewProjMatrix._24 + viewProjMatrix._22,
        viewProjMatrix._34 + viewProjMatrix._32
    );
    float bottomDist = viewProjMatrix._44 + viewProjMatrix._42;
    float bottomLen = length(bottomNormal);
    frustumPlanes[2].normal = bottomNormal / bottomLen;
    frustumPlanes[2].distance = bottomDist / bottomLen;

    // Top plane
    float3 topNormal = float3(
        viewProjMatrix._14 - viewProjMatrix._12,
        viewProjMatrix._24 - viewProjMatrix._22,
        viewProjMatrix._34 - viewProjMatrix._32
    );
    float topDist = viewProjMatrix._44 - viewProjMatrix._42;
    float topLen = length(topNormal);
    frustumPlanes[3].normal = topNormal / topLen;
    frustumPlanes[3].distance = topDist / topLen;

    // Near plane (reversed Z - was far plane)
    float3 nearNormal = float3(
        viewProjMatrix._14 - viewProjMatrix._13,
        viewProjMatrix._24 - viewProjMatrix._23,
        viewProjMatrix._34 - viewProjMatrix._33
    );
    float nearDist = viewProjMatrix._44 - viewProjMatrix._43;
    float nearLen = length(nearNormal);
    frustumPlanes[4].normal = nearNormal / nearLen;
    frustumPlanes[4].distance = nearDist / nearLen;

    // Far plane (reversed Z - was near plane)
    float3 farNormal = float3(
        viewProjMatrix._13,
        viewProjMatrix._23,
        viewProjMatrix._33
    );
    float farDist = viewProjMatrix._43;
    float farLen = length(farNormal);
    frustumPlanes[5].normal = farNormal / farLen;
    frustumPlanes[5].distance = farDist / farLen;
}

// Visibility states
#define VISIBILITY_NOT_VISIBLE 0
#define VISIBILITY_PARTIALLY_VISIBLE 1
#define VISIBILITY_FULLY_VISIBLE 2

inline int GetBoundingBoxVisibility(FrustumPlane frustumPlanes[6], float3 center, float3 extents)
{
    bool fullyInside = true;

    // Test each frustum plane
    for (int i = 0; i < 6; i++)
    {
        // Calculate the "positive vertex" - furthest corner in direction of plane normal
        float3 positiveVertex = center + float3(
            frustumPlanes[i].normal.x > 0 ? extents.x : -extents.x,
            frustumPlanes[i].normal.y > 0 ? extents.y : -extents.y,
            frustumPlanes[i].normal.z > 0 ? extents.z : -extents.z
        );

        // Calculate the "negative vertex" - furthest corner opposite to plane normal
        float3 negativeVertex = center + float3(
            frustumPlanes[i].normal.x > 0 ? -extents.x : extents.x,
            frustumPlanes[i].normal.y > 0 ? -extents.y : extents.y,
            frustumPlanes[i].normal.z > 0 ? -extents.z : extents.z
        );

        // Calculate signed distances (changed from - to +)
        float positiveDistance = dot(frustumPlanes[i].normal, positiveVertex) + frustumPlanes[i].distance;
        float negativeDistance = dot(frustumPlanes[i].normal, negativeVertex) + frustumPlanes[i].distance;

        // If the positive vertex (furthest point) is behind the plane,
        // the entire box is outside this plane
        if (positiveDistance < 0)
            return VISIBILITY_NOT_VISIBLE;

        // If the negative vertex (closest point) is behind the plane,
        // the box is intersecting this plane (not fully inside)
        if (negativeDistance < 0)
            fullyInside = false;
    }

    // If we got here, at least part of the box is visible
    return fullyInside ? (int)VISIBILITY_FULLY_VISIBLE : (int)VISIBILITY_PARTIALLY_VISIBLE;
}

// Create a translation matrix from world position
inline float4x4 CreateTranslationMatrix(float3 worldPosition)
{
    return float4x4(
        1.0, 0.0, 0.0, worldPosition.x,
        0.0, 1.0, 0.0, worldPosition.y,
        0.0, 0.0, 1.0, worldPosition.z,
        0.0, 0.0, 0.0, 1.0
    );
}

inline void CalculateBoundingBox(in float4x4 objectTransformMatrix, float4x4 matCamera_MVP, float3 vBoundsCenter, float3 vBoundsExtents, inout float4 BoundingBox[8])
{
    // Calculate clip space matrix
    float4x4 to_clip_space_mat = mul(matCamera_MVP, objectTransformMatrix);

    float3 Min = vBoundsCenter - vBoundsExtents;
    float3 Max = vBoundsCenter + vBoundsExtents;

    // Transform all 8 corner points of the object bounding box to clip space
    BoundingBox[0] = mul(to_clip_space_mat, float4(Min.x, Max.y, Min.z, 1.0));
    BoundingBox[1] = mul(to_clip_space_mat, float4(Min.x, Max.y, Max.z, 1.0));
    BoundingBox[2] = mul(to_clip_space_mat, float4(Max.x, Max.y, Max.z, 1.0));
    BoundingBox[3] = mul(to_clip_space_mat, float4(Max.x, Max.y, Min.z, 1.0));
    BoundingBox[4] = mul(to_clip_space_mat, float4(Max.x, Min.y, Min.z, 1.0));
    BoundingBox[5] = mul(to_clip_space_mat, float4(Max.x, Min.y, Max.z, 1.0));
    BoundingBox[6] = mul(to_clip_space_mat, float4(Min.x, Min.y, Max.z, 1.0));
    BoundingBox[7] = mul(to_clip_space_mat, float4(Min.x, Min.y, Min.z, 1.0));
}

inline bool IsFrustumCulled(float4 BoundingBox[8])
{
    bool isCulled = false;
    // Test all 8 points with both positive and negative planes
    for (int i = 0; i < 3; i++)
    {
            // cull if outside positive plane:
        isCulled = isCulled ||
			(BoundingBox[0][i] > BoundingBox[0].w &&
			BoundingBox[1][i] > BoundingBox[1].w &&
			BoundingBox[2][i] > BoundingBox[2].w &&
			BoundingBox[3][i] > BoundingBox[3].w &&
			BoundingBox[4][i] > BoundingBox[4].w &&
			BoundingBox[5][i] > BoundingBox[5].w &&
			BoundingBox[6][i] > BoundingBox[6].w &&
			BoundingBox[7][i] > BoundingBox[7].w );

            // cull if outside negative plane:
        isCulled = isCulled ||
			(BoundingBox[0][i] < -BoundingBox[0].w &&
			BoundingBox[1][i] < -BoundingBox[1].w &&
			BoundingBox[2][i] < -BoundingBox[2].w &&
			BoundingBox[3][i] < -BoundingBox[3].w &&
			BoundingBox[4][i] < -BoundingBox[4].w &&
			BoundingBox[5][i] < -BoundingBox[5].w &&
			BoundingBox[6][i] < -BoundingBox[6].w &&
			BoundingBox[7][i] < -BoundingBox[7].w );
    }

    return isCulled;
}

#endif
