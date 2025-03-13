#ifndef _DCL_DEFAULTS_
#define _DCL_DEFAULTS_

static const float3 UP_VECTOR = float3(0, 1, 0);
static const float3 ONE_VECTOR = float3(1, 1, 1);

static const float4x4 IDENTITY_MATRIX = float4x4(1, 0, 0, 0,
                                                0, 1, 0, 0,
                                                0, 0, 1, 0,
                                                0, 0, 0, 1);
static const float4x4 EMPTY_MATRIX = float4x4(0, 0, 0, 0,
                                            0, 0, 0, 0,
                                            0, 0, 0, 0,
                                            0, 0, 0, 0);

#endif